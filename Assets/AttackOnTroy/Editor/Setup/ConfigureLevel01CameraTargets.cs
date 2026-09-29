using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Assigns Level 01 actor targets without saving or otherwise changing the scene.</summary>
public static class ConfigureLevel01CameraTargets
{
    private const string MenuPath = "Attack on Troy/Setup/Configure Level 01 Camera Targets";
    private const string RequiredScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, RequiredScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Camera Targets",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        var problems = new List<string>();
        GameObject cameraObject = FindUniqueObject(scene, "Main Camera", problems);
        GameObject playerObject = FindUniqueObject(scene, "Player_Achilles", problems);
        GameObject enemyObject = FindUniqueObject(scene, "Enemy_CyclopsNormal", problems);

        Camera cameraComponent = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        if (cameraObject != null && cameraComponent == null)
            problems.Add("Main Camera has no Camera component.");

        Level01CameraController controller = null;
        if (cameraObject != null)
        {
            Level01CameraController[] controllers = cameraObject.GetComponents<Level01CameraController>();
            if (controllers.Length > 1)
                problems.Add("Main Camera has duplicate Level01CameraController components.");
            else if (controllers.Length == 1)
                controller = controllers[0];
        }

        Collider2D boundsCollider = null;
        if (controller == null)
        {
            GameObject boundsObject = FindUniqueObject(scene, "LevelBounds", problems);
            if (boundsObject != null)
            {
                Collider2D[] colliders = boundsObject.GetComponents<Collider2D>();
                if (colliders.Length == 1)
                    boundsCollider = colliders[0];
                else
                    problems.Add("A missing camera controller can only be added when LevelBounds has exactly one Collider2D.");
            }
        }

        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Camera Targets",
                string.Join("\n", problems) + "\n\nNo changes were made.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Camera Targets");

        bool addedController = controller == null;
        if (addedController)
        {
            controller = Undo.AddComponent<Level01CameraController>(cameraObject);
            Undo.RecordObject(controller, "Configure Level 01 Camera References");
            controller.ConfigureReferences(cameraComponent, boundsCollider);
        }

        Undo.RecordObject(controller, "Assign Level 01 Camera Targets");
        controller.ConfigureTargets(playerObject.transform, enemyObject.transform);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "Configure Level 01 Camera Targets",
            (addedController ? "Added and configured" : "Configured") +
            " the Level01CameraController target references.\n" +
            "Achilles is the initial and fallback actor. Existing camera transform, zoom, bounds, input settings, and parallax were preserved.\n\n" +
            "Inspect the scene and press Ctrl+S to save when ready.",
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
                if (!string.Equals(transforms[j].name, objectName, StringComparison.Ordinal))
                    continue;

                match = transforms[j].gameObject;
                matchCount++;
            }
        }

        if (matchCount == 0)
            problems.Add("Could not find " + objectName + " in the active scene.");
        else if (matchCount > 1)
            problems.Add("Found " + matchCount + " objects named " + objectName + "; resolve the duplicate names first.");

        return matchCount == 1 ? match : null;
    }
}
