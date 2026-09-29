using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configures the existing Level 01 Beach background objects for parallax.</summary>
public static class ConfigureLevel01Parallax
{
    private const string RequiredSceneName = "SCN_Level01_Beach";
    private static readonly LayerConfiguration[] Layers =
    {
        new LayerConfiguration("BG_Beach_Sky", 0.01f),
        new LayerConfiguration("BG_Beach_Far", 0.08f),
        new LayerConfiguration("BG_Beach_Mid", 0.16f)
    };

    [MenuItem("Attack on Troy/Setup/Configure Level 01 Parallax")]
    private static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != RequiredSceneName)
        {
            EditorUtility.DisplayDialog(
                "Level 01 Parallax",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        List<string> problems = new List<string>();
        GameObject cameraObject = FindUniqueObject(scene, "Main Camera", problems);
        Camera sceneCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        if (sceneCamera == null)
            problems.Add("Main Camera does not have a Camera component.");

        GameObject[] layerObjects = new GameObject[Layers.Length];
        SpriteRenderer[] renderers = new SpriteRenderer[Layers.Length];
        Level01ParallaxLayer[] existingComponents = new Level01ParallaxLayer[Layers.Length];
        for (int i = 0; i < Layers.Length; i++)
        {
            layerObjects[i] = FindUniqueObject(scene, Layers[i].ObjectName, problems);
            if (layerObjects[i] == null)
                continue;

            renderers[i] = layerObjects[i].GetComponent<SpriteRenderer>();
            if (renderers[i] == null)
                problems.Add(Layers[i].ObjectName + " does not have a SpriteRenderer.");

            Level01ParallaxLayer[] components = layerObjects[i].GetComponents<Level01ParallaxLayer>();
            if (components.Length > 1)
                problems.Add(Layers[i].ObjectName + " has multiple Level01ParallaxLayer components.");
            else if (components.Length == 1)
                existingComponents[i] = components[0];
        }

        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog("Level 01 Parallax", string.Join("\n", problems), "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Parallax");
        int addedCount = 0;
        int configuredCount = 0;
        for (int i = 0; i < Layers.Length; i++)
        {
            Level01ParallaxLayer component = existingComponents[i];
            if (component == null)
            {
                component = Undo.AddComponent<Level01ParallaxLayer>(layerObjects[i]);
                addedCount++;
            }

            Undo.RecordObject(component, "Configure Level 01 Parallax Layer");
            component.Configure(sceneCamera, renderers[i], Layers[i].Factor);
            EditorUtility.SetDirty(component);
            configuredCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.DisplayDialog(
            "Level 01 Parallax",
            "Configured " + configuredCount + " background layers (" + addedCount + " components added).\n"
                + "Inspect the scene and press Ctrl+S to save when ready.",
            "OK");
    }

    private static GameObject FindUniqueObject(Scene scene, string objectName, List<string> problems)
    {
        GameObject match = null;
        int matchCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                GameObject candidate = transforms[j].gameObject;
                if (candidate.name != objectName)
                    continue;

                match = candidate;
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            problems.Add("Could not find " + objectName + " in the active scene.");
            return null;
        }

        if (matchCount > 1)
        {
            problems.Add("Found " + matchCount + " objects named " + objectName + "; resolve the duplicate names first.");
            return null;
        }

        return match;
    }

    private struct LayerConfiguration
    {
        public readonly string ObjectName;
        public readonly float Factor;

        public LayerConfiguration(string objectName, float factor)
        {
            ObjectName = objectName;
            Factor = factor;
        }
    }
}
