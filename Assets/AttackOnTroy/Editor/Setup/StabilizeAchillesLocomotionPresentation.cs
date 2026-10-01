using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Achilles-only, asset-based presentation repair. Never saves or edits the scene.</summary>
public sealed class StabilizeAchillesLocomotionPresentation : EditorWindow
{
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string ArtPath = "Assets/AttackOnTroy/Art/Characters/Achilles/";
    private static readonly string[] ClipNames = { "Idle", "Run", "Throw", "Hurt", "Death" };
    [SerializeField] private Vector3 baselineScale = Vector3.one;
    [SerializeField] private Vector3 baselinePosition = new Vector3(-0.1f, 0.7f, 0f);
    [SerializeField] private float runScaleX = 201f / 198.5f;
    [SerializeField] private float runScaleY = 1.45f;

    [MenuItem("Attack on Troy/Setup/Stabilize Achilles Locomotion Presentation")]
    private static void Open()
    {
        GetWindow<StabilizeAchillesLocomotionPresentation>("Achilles Locomotion");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Edit Mode only. Apply updates Achilles' five shared clips and its controller, not the scene. " +
            "Baseline is ABSOLUTE on the Animator's Visual child; other Achilles instances using these clips will use this baseline too. " +
            "Keep X/Y scales positive; facing remains SpriteRenderer.flipX.", MessageType.Info);
        baselineScale = EditorGUILayout.Vector3Field("Baseline Visual scale", baselineScale);
        baselinePosition = EditorGUILayout.Vector3Field("Baseline Visual position", baselinePosition);
        runScaleX = EditorGUILayout.FloatField("Run X multiplier", runScaleX);
        runScaleY = EditorGUILayout.FloatField("Run Y multiplier", runScaleY);
        if (GUILayout.Button("Read baseline from Level 01 Visual (not animation preview)"))
        {
            try
            {
                Transform visual = ValidateSceneVisual();
                baselineScale = visual.localScale;
                baselinePosition = visual.localPosition;
            }
            catch (Exception exception) { Debug.LogError(exception.Message); }
        }
        if (GUILayout.Button("Apply Achilles presentation repair"))
        {
            try { Apply(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        EditorGUILayout.HelpBox("Default Run Y = 1.45 (conservative vs full 1.575 height match). " +
            "Feet offsets are recomputed from every sprite's actual pivot and alpha lower bound whenever you Apply. " +
            "Undo is supported. Animation preview must be stopped before applying.", MessageType.None);
    }

    private static Transform ValidateSceneVisual()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode() ||
            !scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Open active Level 01 in Edit Mode and stop Animation preview first. No assets changed.");
        Transform actor = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Actors") continue;
            if (actor != null) throw new InvalidOperationException("Duplicate Actors hierarchy.");
            actor = root.transform.Find("Player_Achilles");
        }
        Transform visual = actor != null ? actor.Find("Visual") : null;
        Animator animator = visual != null ? visual.GetComponent<Animator>() : null;
        if (animator == null || visual.GetComponent<SpriteRenderer>() == null ||
            AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) != ArtPath + "AnimatorControllers/AC_Achilles.controller")
            throw new InvalidOperationException("Expected Achilles Animator and SpriteRenderer directly on Actors/Player_Achilles/Visual.");
        return visual;
    }

    private void Apply()
    {
        ValidateSceneVisual();
        if (!Finite(baselineScale) || !Finite(baselinePosition) || baselineScale.x <= 0f ||
            baselineScale.y <= 0f || baselineScale.z <= 0f || !float.IsFinite(runScaleX) ||
            !float.IsFinite(runScaleY) || runScaleX <= 0f || runScaleY <= 0f)
            throw new InvalidOperationException("Baseline scale and Run multipliers must be finite and positive.");

        var clips = new AnimationClip[ClipNames.Length];
        var frames = new ObjectReferenceKeyframe[ClipNames.Length][];
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtPath + "Animations/ANIM_Achilles_" + ClipNames[i] + ".anim");
            if (clips[i] == null || clips[i].legacy) throw new InvalidOperationException("Missing/non-Mecanim Achilles clip: " + ClipNames[i]);
            frames[i] = GetSpriteFrames(clips[i]);
        }
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtPath + "AnimatorControllers/AC_Achilles.controller");
        if (controller == null || controller.layers.Length != 1) throw new InvalidOperationException("Expected single-layer Achilles controller.");
        bool speedExists = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            speedExists |= parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float;
        if (!speedExists) throw new InvalidOperationException("Expected Speed float parameter.");
        AnimatorState idle = GetState(controller, "Idle"), run = GetState(controller, "Run");
        if (idle.motion != clips[0] || run.motion != clips[1]) throw new InvalidOperationException("Unexpected Idle/Run motion bindings.");
        AnimatorStateTransition enter = GetTransition(idle, run), exit = GetTransition(run, idle);

        // Precompute all alpha measurements/curves before mutating any asset.
        float[] lowerBounds = new float[frames[0].Length];
        AnimationCurve runY = new AnimationCurve();
        using (var pixels = new SourcePixels())
        {
            for (int i = 0; i < lowerBounds.Length; i++) lowerBounds[i] = pixels.LowerBound((Sprite)frames[0][i].value);
            Array.Sort(lowerBounds);
            float idleLower = (lowerBounds[(lowerBounds.Length - 1) / 2] + lowerBounds[lowerBounds.Length / 2]) * 0.5f;
            foreach (ObjectReferenceKeyframe frame in frames[1])
            {
                float lower = pixels.LowerBound((Sprite)frame.value);
                float y = baselinePosition.y + baselineScale.y * (idleLower - runScaleY * lower);
                runY.AddKey(new Keyframe(frame.time, y));
            }
        }
        if (runY.keys[runY.length - 1].time < clips[1].length)
            runY.AddKey(new Keyframe(clips[1].length, runY.keys[runY.length - 1].value));
        for (int i = 0; i < runY.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(runY, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(runY, i, AnimationUtility.TangentMode.Constant);
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Stabilize Achilles Locomotion Presentation");
        try
        {
            foreach (AnimationClip clip in clips) Undo.RegisterCompleteObjectUndo(clip, "Achilles Visual curves");
            Undo.RegisterCompleteObjectUndo(enter, "Achilles Run threshold");
            Undo.RegisterCompleteObjectUndo(exit, "Achilles Idle threshold");
            for (int i = 0; i < clips.Length; i++)
            {
                Vector3 scale = baselineScale;
                if (i == 1) { scale.x *= runScaleX; scale.y *= runScaleY; }
                SetConstant(clips[i], "m_LocalScale.x", scale.x);
                SetConstant(clips[i], "m_LocalScale.y", scale.y);
                SetConstant(clips[i], "m_LocalScale.z", scale.z);
                SetConstant(clips[i], "m_LocalPosition.x", baselinePosition.x);
                SetConstant(clips[i], "m_LocalPosition.z", baselinePosition.z);
                SetCurve(clips[i], "m_LocalPosition.y", i == 1 ? runY : AnimationCurve.Constant(0f, clips[i].length, baselinePosition.y));
                EditorUtility.SetDirty(clips[i]);
            }
            // Keep state speed (including authored Idle 0.4), blend duration and trigger transitions.
            enter.conditions = new[] { new AnimatorCondition { parameter = "Speed", mode = AnimatorConditionMode.Greater, threshold = 0.1f } };
            exit.conditions = new[] { new AnimatorCondition { parameter = "Speed", mode = AnimatorConditionMode.Less, threshold = 0.05f } };
            EditorUtility.SetDirty(enter);
            EditorUtility.SetDirty(exit);
            EditorUtility.SetDirty(controller);
            foreach (AnimationClip clip in clips) AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.SaveAssetIfDirty(controller);
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Achilles presentation updated. Run multipliers=({runScaleX:F6}, {runScaleY:F3}); " +
                $"baselineScale={baselineScale}, baselinePosition={baselinePosition}. Sprite keys, PPU, prefab and scene unchanged.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    private static bool Finite(Vector3 vector) => float.IsFinite(vector.x) && float.IsFinite(vector.y) && float.IsFinite(vector.z);
    private static void SetConstant(AnimationClip clip, string property, float value) => SetCurve(clip, property, AnimationCurve.Constant(0f, clip.length, value));
    private static void SetCurve(AnimationClip clip, string property, AnimationCurve curve) =>
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);

    private static ObjectReferenceKeyframe[] GetSpriteFrames(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
            if (binding.path != "") throw new InvalidOperationException("Expected Visual-local sprite binding: " + clip.name);
            ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (frames.Length == 0) break;
            foreach (ObjectReferenceKeyframe frame in frames)
                if (!(frame.value is Sprite)) throw new InvalidOperationException("Missing sprite in " + clip.name);
            return frames;
        }
        throw new InvalidOperationException("Missing sprite curve: " + clip.name);
    }

    private static AnimatorState GetState(AnimatorController controller, string name)
    {
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            if (child.state.name == name) return child.state;
        throw new InvalidOperationException("Missing Achilles state: " + name);
    }

    private static AnimatorStateTransition GetTransition(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition result = null;
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState != destination) continue;
            if (result != null || transition.conditions.Length != 1 || transition.conditions[0].parameter != "Speed")
                throw new InvalidOperationException("Unexpected/duplicate locomotion transition. Inspect before applying.");
            result = transition;
        }
        return result != null ? result : throw new InvalidOperationException("Missing " + source.name + " -> " + destination.name);
    }

    private sealed class SourcePixels : IDisposable
    {
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Color32[]> pixels = new Dictionary<string, Color32[]>();
        public float LowerBound(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!textures.TryGetValue(path, out Texture2D texture))
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                textures.Add(path, texture);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                    throw new InvalidOperationException("Cannot read sprite PNG: " + path);
                pixels.Add(path, texture.GetPixels32());
            }
            Rect rect = sprite.rect;
            if (sprite.packed || rect.xMax > texture.width || rect.yMax > texture.height)
                throw new InvalidOperationException("Packed/resized sprite unsupported; inspect import settings: " + sprite.name);
            Color32[] data = pixels[path];
            for (int y = Mathf.RoundToInt(rect.yMin); y < Mathf.RoundToInt(rect.yMax); y++)
                for (int x = Mathf.RoundToInt(rect.xMin); x < Mathf.RoundToInt(rect.xMax); x++)
                    if (data[y * texture.width + x].a >= 128)
                        return (y - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit;
            throw new InvalidOperationException("No opaque pixels in " + sprite.name);
        }
        public void Dispose()
        {
            foreach (Texture2D texture in textures.Values) DestroyImmediate(texture);
        }
    }
}
