using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configures the Level 01 terrain prototype without saving the scene.</summary>
public static class ConfigureLevel01DestructibleTerrain
{
    private const string MenuPath = "Attack on Troy/Setup/Configure Level 01 Destructible Terrain";
    private const string RequiredScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string TerrainObjectName = "ENV_Beach_Terrain";
    private const string CameraObjectName = "Main Camera";

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, RequiredScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Destructible Terrain",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        var problems = new List<string>();
        GameObject terrainObject = FindUniqueObject(scene, TerrainObjectName, problems);
        GameObject cameraObject = FindUniqueObject(scene, CameraObjectName, problems);
        SpriteRenderer spriteRenderer = terrainObject != null ? terrainObject.GetComponent<SpriteRenderer>() : null;
        if (terrainObject != null && (spriteRenderer == null || spriteRenderer.sprite == null))
            problems.Add(TerrainObjectName + " must have a SpriteRenderer with a sprite.");

        Camera worldCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        if (cameraObject != null && worldCamera == null)
            problems.Add(CameraObjectName + " has no Camera component.");

        DestructibleTerrain2D terrain = null;
        TerrainDestructionDebugTester tester = null;
        if (terrainObject != null)
        {
            DestructibleTerrain2D[] terrainComponents = terrainObject.GetComponents<DestructibleTerrain2D>();
            TerrainDestructionDebugTester[] testerComponents = terrainObject.GetComponents<TerrainDestructionDebugTester>();
            if (terrainComponents.Length > 1)
                problems.Add(TerrainObjectName + " has duplicate DestructibleTerrain2D components.");
            else if (terrainComponents.Length == 1)
                terrain = terrainComponents[0];
            if (testerComponents.Length > 1)
                problems.Add(TerrainObjectName + " has duplicate TerrainDestructionDebugTester components.");
            else if (testerComponents.Length == 1)
                tester = testerComponents[0];
        }

        TextureImporter textureImporter = null;
        string texturePath = string.Empty;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            texturePath = AssetDatabase.GetAssetPath(spriteRenderer.sprite.texture);
            textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter == null)
                problems.Add("The terrain sprite texture does not have a TextureImporter.");
        }

        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Destructible Terrain",
                string.Join("\n", problems) + "\n\nNo changes were made.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Destructible Terrain");

        bool importerChanged = !textureImporter.isReadable;
        if (importerChanged)
        {
            Undo.RegisterCompleteObjectUndo(textureImporter, "Enable Terrain Texture Read/Write");
            textureImporter.isReadable = true;
            textureImporter.SaveAndReimport();
        }

        bool addedTerrain = terrain == null;
        bool addedTester = tester == null;
        if (addedTerrain)
            terrain = Undo.AddComponent<DestructibleTerrain2D>(terrainObject);
        if (addedTester)
            tester = Undo.AddComponent<TerrainDestructionDebugTester>(terrainObject);

        Undo.RecordObject(terrain, "Configure Destructible Terrain");
        terrain.Configure(spriteRenderer);
        Undo.RecordObject(tester, "Configure Terrain Debug Tester");
        tester.Configure(terrain, worldCamera);

        int terrainLayer = LayerMask.NameToLayer("Terrain");
        int selectedLayer = terrainLayer >= 0 ? terrainLayer : LayerMask.NameToLayer("Default");
        if (terrainObject.layer != selectedLayer)
        {
            Undo.RecordObject(terrainObject, "Set Terrain Physics Layer");
            terrainObject.layer = selectedLayer;
        }

        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(tester);
        EditorUtility.SetDirty(terrainObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        string layerMessage = terrainLayer >= 0
            ? "The terrain uses the existing Terrain physics layer."
            : "No Terrain physics layer exists, so the terrain temporarily uses Default. No layer or collision-matrix settings were created or changed.";
        EditorUtility.DisplayDialog(
            "Configure Level 01 Destructible Terrain",
            "Configured DestructibleTerrain2D" + (addedTerrain ? " (added)" : string.Empty) +
            " and TerrainDestructionDebugTester" + (addedTester ? " (added)" : string.Empty) + ".\n" +
            (importerChanged ? "Enabled Read/Write on " + texturePath + ".\n" : "Terrain texture Read/Write was already enabled.\n") +
            layerMessage + "\n\nThe source PNG and scene were not saved. Inspect the changes and save manually when ready.",
            "OK");
    }

    private static GameObject FindUniqueObject(Scene scene, string objectName, List<string> problems)
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

        if (count == 0)
            problems.Add("Could not find " + objectName + " in the active scene.");
        else if (count > 1)
            problems.Add("Found " + count + " objects named " + objectName + "; resolve duplicates first.");
        return count == 1 ? match : null;
    }
}
