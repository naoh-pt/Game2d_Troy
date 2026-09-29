using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configures the existing Level 1 camera and bounds without saving the scene.
/// </summary>
public static class ConfigureLevel01Camera
{
    private const string MenuPath = "AttackOnTroy/Setup/Configure Level 01 Camera";
    private const string SceneName = "SCN_Level01_Beach";
    private const string CameraObjectName = "Main Camera";
    private const string BoundsObjectName = "LevelBounds";
    private const string TerrainObjectName = "ENV_Beach_Terrain";

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.name, SceneName, StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Camera",
                "Make SCN_Level01_Beach the active scene before running this command. No changes were made.",
                "OK");
            return;
        }

        GameObject[] cameras = FindAllSceneObjectsByName(scene, CameraObjectName);
        GameObject[] boundsObjects = FindAllSceneObjectsByName(scene, BoundsObjectName);
        GameObject[] terrains = FindAllSceneObjectsByName(scene, TerrainObjectName);
        if (cameras.Length != 1 || boundsObjects.Length != 1)
        {
            ShowPreflightFailure(
                "Expected one Main Camera and one LevelBounds in the active scene. Found " + cameras.Length + " camera object(s) and " + boundsObjects.Length + " LevelBounds object(s). No changes were made.");
            return;
        }

        GameObject cameraObject = cameras[0];
        GameObject boundsObject = boundsObjects[0];
        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        if (cameraComponent == null)
        {
            ShowPreflightFailure("Main Camera has no Camera component. No component was added and no changes were made.");
            return;
        }

        Level01CameraController[] controllers = cameraObject.GetComponents<Level01CameraController>();
        if (controllers.Length > 1)
        {
            ShowPreflightFailure("Main Camera has duplicate Level01CameraController components. They were left unchanged.");
            return;
        }

        if (!TryResolveBoundsCollider(boundsObject, terrains, out Collider2D boundsCollider, out bool addBox, out bool sizeBox, out Bounds terrainBounds, out string boundsError))
        {
            ShowPreflightFailure(boundsError);
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Camera");

        bool addedBounds = false;
        if (addBox)
        {
            boundsCollider = Undo.AddComponent<BoxCollider2D>(boundsObject);
            addedBounds = true;
            sizeBox = true;
        }

        if (boundsCollider is BoxCollider2D box && sizeBox)
        {
            Vector2 localCenter;
            Vector2 localSize;
            GetLocalRectangle(boundsObject.transform, terrainBounds, out localCenter, out localSize);
            Undo.RecordObject(box, "Size LevelBounds from terrain");
            box.offset = localCenter;
            box.size = localSize;
            box.isTrigger = true;
            EditorUtility.SetDirty(box);
        }

        if (!cameraComponent.orthographic)
        {
            // Keep the existing transform, background color, and size while enforcing 2D projection.
            Undo.RecordObject(cameraComponent, "Set Main Camera to Orthographic");
            cameraComponent.orthographic = true;
            EditorUtility.SetDirty(cameraComponent);
        }

        Level01CameraController controller = controllers.Length == 1
            ? controllers[0]
            : Undo.AddComponent<Level01CameraController>(cameraObject);

        Undo.RecordObject(controller, "Configure Level 01 Camera Controller");
        controller.ConfigureReferences(cameraComponent, boundsCollider);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(cameraObject);
        EditorUtility.SetDirty(boundsObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        string boundsDescription = addedBounds
            ? "Added a trigger BoxCollider2D sized from ENV_Beach_Terrain."
            : sizeBox
                ? "Reused and sized the existing BoxCollider2D from ENV_Beach_Terrain."
                : "Reused the existing " + boundsCollider.GetType().Name + " without changing its shape.";
        EditorUtility.DisplayDialog(
            "Configure Level 01 Camera",
            "Camera controller configured.\n" + boundsDescription + "\n" +
            "The camera transform, background color, and orthographic size were preserved.\n\n" +
            "Inspect the scene and press Ctrl+S to save your reviewed changes.",
            "OK");
    }

    private static bool TryResolveBoundsCollider(
        GameObject boundsObject,
        GameObject[] terrainObjects,
        out Collider2D resolvedCollider,
        out bool addBox,
        out bool sizeBox,
        out Bounds terrainBounds,
        out string error)
    {
        resolvedCollider = null;
        addBox = false;
        sizeBox = false;
        terrainBounds = new Bounds();
        error = string.Empty;

        BoxCollider2D[] boxes = boundsObject.GetComponents<BoxCollider2D>();
        if (boxes.Length > 1)
        {
            error = "LevelBounds has multiple BoxCollider2D components. They were left unchanged; remove the ambiguity manually before setup.";
            return false;
        }

        if (boxes.Length == 1)
        {
            resolvedCollider = boxes[0];
            if (boxes[0].size.x > 0.01f && boxes[0].size.y > 0.01f)
                return true;

            if (!TryGetTerrainBounds(terrainObjects, out terrainBounds, out error))
                return false;
            sizeBox = true;
            return true;
        }

        Collider2D[] suitableColliders = Array.FindAll(
            boundsObject.GetComponents<Collider2D>(),
            collider => collider != null && collider.bounds.size.x > 0.01f && collider.bounds.size.y > 0.01f);
        if (suitableColliders.Length == 1)
        {
            resolvedCollider = suitableColliders[0];
            return true;
        }

        if (suitableColliders.Length > 1)
        {
            error = "LevelBounds has multiple usable Collider2D components. No collider was selected; resolve the ambiguity manually.";
            return false;
        }

        if (!TryGetTerrainBounds(terrainObjects, out terrainBounds, out error))
            return false;

        addBox = true;
        sizeBox = true;
        return true;
    }

    private static bool TryGetTerrainBounds(GameObject[] terrainObjects, out Bounds terrainBounds, out string error)
    {
        terrainBounds = new Bounds();
        error = string.Empty;
        if (terrainObjects.Length != 1)
        {
            error = "A unique ENV_Beach_Terrain object is required to size LevelBounds, but " + terrainObjects.Length + " were found. No changes were made.";
            return false;
        }

        SpriteRenderer terrainRenderer = terrainObjects[0].GetComponent<SpriteRenderer>();
        if (terrainRenderer == null || terrainRenderer.sprite == null || !terrainRenderer.enabled)
        {
            error = "ENV_Beach_Terrain has no enabled SpriteRenderer with a sprite; LevelBounds cannot be sized safely. No changes were made.";
            return false;
        }

        terrainBounds = terrainRenderer.bounds;
        if (terrainBounds.size.x <= 0.01f || terrainBounds.size.y <= 0.01f)
        {
            error = "ENV_Beach_Terrain has empty renderer bounds; LevelBounds cannot be sized safely. No changes were made.";
            return false;
        }

        return true;
    }

    private static void GetLocalRectangle(Transform boundsTransform, Bounds worldBounds, out Vector2 center, out Vector2 size)
    {
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, worldBounds.center.z);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, worldBounds.center.z);

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                Vector3 worldCorner = new Vector3(
                    x == 0 ? worldBounds.min.x : worldBounds.max.x,
                    y == 0 ? worldBounds.min.y : worldBounds.max.y,
                    worldBounds.center.z);
                Vector3 localCorner = boundsTransform.InverseTransformPoint(worldCorner);
                minimum.x = Mathf.Min(minimum.x, localCorner.x);
                minimum.y = Mathf.Min(minimum.y, localCorner.y);
                maximum.x = Mathf.Max(maximum.x, localCorner.x);
                maximum.y = Mathf.Max(maximum.y, localCorner.y);
            }
        }

        center = new Vector2((minimum.x + maximum.x) * 0.5f, (minimum.y + maximum.y) * 0.5f);
        size = new Vector2(maximum.x - minimum.x, maximum.y - minimum.y);
    }

    private static GameObject[] FindAllSceneObjectsByName(Scene scene, string objectName)
    {
        GameObject[] loadedObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        var matches = new List<GameObject>();
        foreach (GameObject candidate in loadedObjects)
        {
            if (candidate.scene == scene && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                matches.Add(candidate);
        }

        return matches.ToArray();
    }

    private static void ShowPreflightFailure(string message)
    {
        EditorUtility.DisplayDialog("Configure Level 01 Camera", message, "OK");
    }
}
