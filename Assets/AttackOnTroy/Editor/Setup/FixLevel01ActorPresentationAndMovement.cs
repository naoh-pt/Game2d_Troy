using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repairs Level 01 foreground render order while leaving scene and prefab authoring to the user.</summary>
public static class FixLevel01ActorPresentationAndMovement
{
    private const string MenuPath = "Attack on Troy/Setup/Fix Level 01 Actor Presentation and Movement";
    private const string Level01Path = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string TagManagerPath = "ProjectSettings/TagManager.asset";
    private const string ActorLayerName = "Actors";
    private const string ForegroundLayerName = "Foreground";

    [MenuItem(MenuPath)]
    private static void Fix()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, Level01Path, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Fix Level 01 Actor Presentation and Movement",
                "Open SCN_Level01_Beach and make it the active scene before running this command. No changes were made.", "OK");
            return;
        }

        SpriteRenderer foregroundRenderer = FindUniqueForegroundRenderer(scene);
        if (foregroundRenderer == null)
        {
            EditorUtility.DisplayDialog("Fix Level 01 Actor Presentation and Movement",
                "Expected exactly one Level/World/FG_Beach_Props SpriteRenderer. No changes were made.", "OK");
            return;
        }

        UnityEngine.Object[] tagManagerObjects = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
        if (tagManagerObjects == null || tagManagerObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Fix Level 01 Actor Presentation and Movement",
                "Could not load ProjectSettings/TagManager.asset. No changes were made.", "OK");
            return;
        }

        UnityEngine.Object tagManager = tagManagerObjects[0];
        SerializedObject settings = new SerializedObject(tagManager);
        SerializedProperty layers = settings.FindProperty("m_SortingLayers");
        int actorsIndex = FindSortingLayer(layers, ActorLayerName);
        int foregroundIndex = FindSortingLayer(layers, ForegroundLayerName);
        int uiIndex = FindSortingLayer(layers, "UI");
        if (actorsIndex < 0 || foregroundIndex < 0 || uiIndex < 0)
        {
            EditorUtility.DisplayDialog("Fix Level 01 Actor Presentation and Movement",
                "The required Actors, Foreground, or UI sorting layer is missing. No changes were made.", "OK");
            return;
        }

        bool layerOrderChanged = actorsIndex > foregroundIndex || uiIndex < foregroundIndex;
        if (layerOrderChanged)
        {
            Undo.RecordObject(tagManager, "Order Level 01 Foreground Sorting");
            // Move the existing stable-ID layer entries; do not recreate sorting layers.
            actorsIndex = FindSortingLayer(layers, ActorLayerName);
            foregroundIndex = FindSortingLayer(layers, ForegroundLayerName);
            if (actorsIndex > foregroundIndex)
                layers.MoveArrayElement(actorsIndex, foregroundIndex);

            uiIndex = FindSortingLayer(layers, "UI");
            int lastIndex = layers.arraySize - 1;
            if (uiIndex != lastIndex)
                layers.MoveArrayElement(uiIndex, lastIndex);
            settings.ApplyModifiedProperties();
            EditorUtility.SetDirty(tagManager);
            AssetDatabase.SaveAssetIfDirty(tagManager);
        }

        bool rendererChanged = !string.Equals(foregroundRenderer.sortingLayerName, ForegroundLayerName, StringComparison.Ordinal);
        if (rendererChanged)
        {
            Undo.RecordObject(foregroundRenderer, "Assign Beach Foreground Sorting Layer");
            foregroundRenderer.sortingLayerName = ForegroundLayerName;
            EditorUtility.SetDirty(foregroundRenderer);
        }
        EditorSceneManager.MarkSceneDirty(scene);

        EditorUtility.DisplayDialog("Fix Level 01 Actor Presentation and Movement",
            "Foreground order is now backgrounds/world/props-behind-actors, Actors, Foreground, UI. " +
            "FG_Beach_Props uses the existing Foreground layer. The shared GroundedActorMotor2D source includes the movement repair. " +
            "No Run PPU or character size changes were applied. The scene was not saved; review and save it manually.", "OK");
    }

    private static SpriteRenderer FindUniqueForegroundRenderer(Scene scene)
    {
        SpriteRenderer result = null;
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                Transform candidate = transforms[j];
                if (!string.Equals(candidate.name, "FG_Beach_Props", StringComparison.Ordinal))
                    continue;
                result = candidate.GetComponent<SpriteRenderer>();
                if (result != null)
                    count++;
            }
        }
        return count == 1 ? result : null;
    }

    private static int FindSortingLayer(SerializedProperty layers, string layerName)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty entry = layers.GetArrayElementAtIndex(i);
            SerializedProperty name = entry.FindPropertyRelative("name");
            if (name != null && string.Equals(name.stringValue, layerName, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }
}
