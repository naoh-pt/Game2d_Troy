using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configures reusable Rigidbody2D movement on all four character prefab assets.</summary>
public static class ConfigureCharacterPhysics
{
    private const string MenuPath = "Attack on Troy/Setup/Configure Character Physics";
    private const string Level01Path = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const float GravityScale = 3f;
    private const float DefaultRecoveryY = -12f;

    private static readonly CharacterConfiguration[] Characters =
    {
        new CharacterConfiguration(
            "Assets/AttackOnTroy/Characters/Achilles/Prefabs/PF_Achilles.prefab",
            true, 3f, 18f, 24f),
        new CharacterConfiguration(
            "Assets/AttackOnTroy/Characters/Odysseus/Prefabs/PF_Odysseus.prefab",
            true, 3f, 18f, 24f),
        new CharacterConfiguration(
            "Assets/AttackOnTroy/Enemies/CyclopsNormal/Prefabs/PF_CyclopsNormal.prefab",
            false, 2f, 12f, 18f),
        new CharacterConfiguration(
            "Assets/AttackOnTroy/Enemies/CyclopsHeavy/Prefabs/PF_CyclopsHeavy.prefab",
            false, 2f, 12f, 18f)
    };

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer < 0)
        {
            EditorUtility.DisplayDialog(
                "Configure Character Physics",
                "The required Terrain physics layer does not exist. No prefab or scene changes were made.",
                "OK");
            return;
        }

        var problems = new List<string>();
        for (int i = 0; i < Characters.Length; i++)
            ValidatePrefab(Characters[i].Path, problems);
        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Configure Character Physics",
                string.Join("\n", problems) + "\n\nNo changes were made.",
                "OK");
            return;
        }

        float recoveryY = ResolveLevel01RecoveryThreshold();
        LayerMask terrainMask = 1 << terrainLayer;
        var summaries = new List<string>(Characters.Length);
        for (int i = 0; i < Characters.Length; i++)
            summaries.Add(ConfigurePrefab(Characters[i], terrainMask, recoveryY));

        AssetDatabase.Refresh();

        Scene activeScene = SceneManager.GetActiveScene();
        int connectedInstanceCount = CountConnectedInstances(activeScene);
        if (activeScene.IsValid() &&
            string.Equals(activeScene.path, Level01Path, StringComparison.OrdinalIgnoreCase) &&
            connectedInstanceCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        EditorUtility.DisplayDialog(
            "Configure Character Physics",
            string.Join("\n", summaries) + "\n\n" +
            "Terrain layer mask: Terrain (layer " + terrainLayer + ").\n" +
            "Development fall-recovery Y: " + recoveryY.ToString("0.###") + ".\n" +
            "Connected prefab instances found in the active scene: " + connectedInstanceCount + ".\n\n" +
            "Prefab assets were saved. The active scene was not saved; inspect it and save manually when ready.",
            "OK");
    }

    private static void ValidatePrefab(string path, List<string> problems)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            problems.Add("Missing prefab: " + path);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<Rigidbody2D>() == null)
                problems.Add(path + " has no root Rigidbody2D.");
            Collider2D[] rootColliders = root.GetComponents<Collider2D>();
            if (rootColliders.Length != 1)
                problems.Add(path + " must have exactly one root Collider2D; found " + rootColliders.Length + ".");

            Transform visual = root.transform.Find("Visual");
            if (visual == null)
                problems.Add(path + " has no direct child named Visual.");
            else
            {
                if (visual.GetComponent<SpriteRenderer>() == null)
                    problems.Add(path + " Visual has no SpriteRenderer.");
                Animator animator = visual.GetComponent<Animator>();
                if (animator == null)
                    problems.Add(path + " Visual has no Animator.");
                else if (!HasFloatParameter(animator, "Speed"))
                    problems.Add(path + " Animator Controller has no Speed float parameter.");
            }

            if (root.GetComponents<GroundedActorMotor2D>().Length > 1)
                problems.Add(path + " has duplicate GroundedActorMotor2D components.");
            if (root.GetComponents<ActorFallRecovery2D>().Length > 1)
                problems.Add(path + " has duplicate ActorFallRecovery2D components.");
            if (root.GetComponents<PlayerMovementDebugInput>().Length > 1)
                problems.Add(path + " has duplicate PlayerMovementDebugInput components.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string ConfigurePrefab(CharacterConfiguration configuration, LayerMask terrainMask, float recoveryY)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(configuration.Path);
        try
        {
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            Collider2D bodyCollider = root.GetComponent<Collider2D>();
            Transform visual = root.transform.Find("Visual");
            SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
            Animator animator = visual.GetComponent<Animator>();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = GravityScale;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.StartAwake;
            bodyCollider.isTrigger = false;

            GroundedActorMotor2D motor = root.GetComponent<GroundedActorMotor2D>();
            bool addedMotor = motor == null;
            if (addedMotor)
                motor = root.AddComponent<GroundedActorMotor2D>();
            motor.Configure(
                body,
                bodyCollider,
                visualRenderer,
                animator,
                terrainMask,
                configuration.MaximumSpeed,
                configuration.Acceleration,
                configuration.Deceleration,
                addedMotor);

            ActorFallRecovery2D recovery = root.GetComponent<ActorFallRecovery2D>();
            bool addedRecovery = recovery == null;
            if (addedRecovery)
                recovery = root.AddComponent<ActorFallRecovery2D>();
            recovery.Configure(body, recoveryY, addedRecovery);

            bool addedDebugInput = false;
            if (configuration.IsPlayer)
            {
                PlayerMovementDebugInput debugInput = root.GetComponent<PlayerMovementDebugInput>();
                addedDebugInput = debugInput == null;
                if (addedDebugInput)
                    debugInput = root.AddComponent<PlayerMovementDebugInput>();
                debugInput.Configure(motor);
                EditorUtility.SetDirty(debugInput);
            }

            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(bodyCollider);
            EditorUtility.SetDirty(motor);
            EditorUtility.SetDirty(recovery);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, configuration.Path);

            return root.name + ": motor " + (addedMotor ? "added" : "updated") +
                ", recovery " + (addedRecovery ? "added" : "updated") +
                (configuration.IsPlayer ? ", debug input " + (addedDebugInput ? "added" : "updated") : ", no AI/debug input");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static float ResolveLevel01RecoveryThreshold()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, Level01Path, StringComparison.OrdinalIgnoreCase))
            return DefaultRecoveryY;

        GameObject levelBounds = FindUniqueSceneObject(scene, "LevelBounds");
        Collider2D boundsCollider = levelBounds != null ? levelBounds.GetComponent<Collider2D>() : null;
        return boundsCollider != null ? boundsCollider.bounds.min.y - 2f : DefaultRecoveryY;
    }

    private static int CountConnectedInstances(Scene scene)
    {
        if (!scene.IsValid())
            return 0;

        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                GameObject candidate = transforms[transformIndex].gameObject;
                if (PrefabUtility.GetNearestPrefabInstanceRoot(candidate) != candidate)
                    continue;
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
                if (IsConfiguredPrefabPath(prefabPath))
                    count++;
            }
        }
        return count;
    }

    private static GameObject FindUniqueSceneObject(Scene scene, string objectName)
    {
        GameObject match = null;
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                if (!string.Equals(transforms[transformIndex].name, objectName, StringComparison.Ordinal))
                    continue;
                match = transforms[transformIndex].gameObject;
                count++;
            }
        }
        return count == 1 ? match : null;
    }

    private static bool IsConfiguredPrefabPath(string path)
    {
        for (int i = 0; i < Characters.Length; i++)
        {
            if (string.Equals(Characters[i].Path, path, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasFloatParameter(Animator animator, string parameterName)
    {
        if (animator.runtimeAnimatorController == null)
            return false;
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == AnimatorControllerParameterType.Float)
                return true;
        }
        return false;
    }

    private readonly struct CharacterConfiguration
    {
        public readonly string Path;
        public readonly bool IsPlayer;
        public readonly float MaximumSpeed;
        public readonly float Acceleration;
        public readonly float Deceleration;

        public CharacterConfiguration(
            string path,
            bool isPlayer,
            float maximumSpeed,
            float acceleration,
            float deceleration)
        {
            Path = path;
            IsPlayer = isPlayer;
            MaximumSpeed = maximumSpeed;
            Acceleration = acceleration;
            Deceleration = deceleration;
        }
    }
}
