using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Internal batch processor to construct, wire, and save the Level 01 minimap directly into SCN_Level01_Beach.unity.
/// Does NOT expose any user-facing MenuItem. Executed via Unity batchmode.
/// </summary>
public static class ApplyMinimapBatch
{
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";

    public static void Execute()
    {
        Debug.Log("[ApplyMinimapBatch] Opening scene: " + ScenePath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[ApplyMinimapBatch] Failed to open scene: " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        GameObject hudCanvasObj = GameObject.Find("HUDCanvas");
        if (hudCanvasObj == null)
        {
            Debug.LogError("[ApplyMinimapBatch] HUDCanvas not found in scene!");
            EditorApplication.Exit(1);
            return;
        }

        Transform hudCanvas = hudCanvasObj.transform;

        // Ensure exactly one Minimap object exists
        Transform existingMinimap = hudCanvas.Find("Minimap");
        if (existingMinimap != null)
        {
            Debug.Log("[ApplyMinimapBatch] Removing existing Minimap to recreate clean structure.");
            GameObject.DestroyImmediate(existingMinimap.gameObject);
        }

        // World references
        GameObject achillesObj = GameObject.Find("Player_Achilles");
        GameObject cyclopsObj = GameObject.Find("Enemy_CyclopsNormal");
        GameObject boundsObj = GameObject.Find("LevelBounds");
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = GameObject.Find("Main Camera");
            if (camObj != null) mainCam = camObj.GetComponent<Camera>();
        }

        if (achillesObj == null || cyclopsObj == null || boundsObj == null || mainCam == null)
        {
            Debug.LogError($"[ApplyMinimapBatch] Missing world objects: Achilles={achillesObj}, Cyclops={cyclopsObj}, Bounds={boundsObj}, Cam={mainCam}");
            EditorApplication.Exit(1);
            return;
        }

        Collider2D boundsCollider = boundsObj.GetComponent<Collider2D>();
        if (boundsCollider == null)
        {
            Debug.LogError("[ApplyMinimapBatch] LevelBounds has no Collider2D!");
            EditorApplication.Exit(1);
            return;
        }

        // 1. Root Minimap GameObject
        GameObject minimapObj = new GameObject("Minimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Level01Minimap));
        minimapObj.transform.SetParent(hudCanvas, false);

        RectTransform minimapRect = minimapObj.GetComponent<RectTransform>();
        minimapRect.anchorMin = new Vector2(1f, 1f);
        minimapRect.anchorMax = new Vector2(1f, 1f);
        minimapRect.pivot = new Vector2(1f, 1f);
        minimapRect.anchoredPosition = new Vector2(-40f, -125f);
        minimapRect.sizeDelta = new Vector2(200f, 105f);

        Image bgImage = minimapObj.GetComponent<Image>();
        bgImage.color = new Color(0.06f, 0.09f, 0.14f, 0.82f);
        bgImage.raycastTarget = false;

        Outline outline = minimapObj.GetComponent<Outline>();
        outline.effectColor = new Color(0.65f, 0.55f, 0.35f, 0.70f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 2. MapContainer (masked inner area for markers & viewport)
        GameObject containerObj = new GameObject("MapContainer", typeof(RectTransform), typeof(RectMask2D));
        containerObj.transform.SetParent(minimapObj.transform, false);

        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.offsetMin = new Vector2(3f, 3f);
        containerRect.offsetMax = new Vector2(-3f, -3f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);

        // Subtle horizon/sea guide
        GameObject groundLineObj = new GameObject("GroundGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        groundLineObj.transform.SetParent(containerObj.transform, false);
        RectTransform groundLineRect = groundLineObj.GetComponent<RectTransform>();
        groundLineRect.anchorMin = new Vector2(0f, 0.22f);
        groundLineRect.anchorMax = new Vector2(1f, 0.22f);
        groundLineRect.pivot = new Vector2(0.5f, 0.5f);
        groundLineRect.anchoredPosition = Vector2.zero;
        groundLineRect.sizeDelta = new Vector2(0f, 1f);
        Image groundLineImage = groundLineObj.GetComponent<Image>();
        groundLineImage.color = new Color(0.4f, 0.45f, 0.5f, 0.25f);
        groundLineImage.raycastTarget = false;

        // 3. Camera Viewport
        GameObject viewportObj = new GameObject("CameraViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        viewportObj.transform.SetParent(containerObj.transform, false);

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.sizeDelta = new Vector2(50f, 30f);

        Image viewportImage = viewportObj.GetComponent<Image>();
        viewportImage.color = new Color(1f, 0.95f, 0.4f, 0.14f);
        viewportImage.raycastTarget = false;

        // 4 Border lines for camera viewport
        CreateViewportBorder(viewportObj.transform, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1.5f));
        CreateViewportBorder(viewportObj.transform, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1.5f));
        CreateViewportBorder(viewportObj.transform, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f));
        CreateViewportBorder(viewportObj.transform, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1.5f, 0f));

        // 4. Achilles Marker
        GameObject achillesMarkerObj = new GameObject("AchillesMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        achillesMarkerObj.transform.SetParent(containerObj.transform, false);

        RectTransform achillesMarkerRect = achillesMarkerObj.GetComponent<RectTransform>();
        achillesMarkerRect.anchorMin = new Vector2(0.5f, 0.5f);
        achillesMarkerRect.anchorMax = new Vector2(0.5f, 0.5f);
        achillesMarkerRect.pivot = new Vector2(0.5f, 0.5f);
        achillesMarkerRect.sizeDelta = new Vector2(8f, 8f);

        Image achillesImage = achillesMarkerObj.GetComponent<Image>();
        achillesImage.color = new Color(0.2f, 0.85f, 1f, 1f); // Azure/Cyan
        achillesImage.raycastTarget = false;

        // 5. Cyclops Marker
        GameObject cyclopsMarkerObj = new GameObject("CyclopsMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cyclopsMarkerObj.transform.SetParent(containerObj.transform, false);

        RectTransform cyclopsMarkerRect = cyclopsMarkerObj.GetComponent<RectTransform>();
        cyclopsMarkerRect.anchorMin = new Vector2(0.5f, 0.5f);
        cyclopsMarkerRect.anchorMax = new Vector2(0.5f, 0.5f);
        cyclopsMarkerRect.pivot = new Vector2(0.5f, 0.5f);
        cyclopsMarkerRect.sizeDelta = new Vector2(10f, 10f);

        Image cyclopsImage = cyclopsMarkerObj.GetComponent<Image>();
        cyclopsImage.color = new Color(1f, 0.32f, 0.22f, 1f); // Coral/Red
        cyclopsImage.raycastTarget = false;

        // 6. Configure Level01Minimap component
        Level01Minimap minimapComponent = minimapObj.GetComponent<Level01Minimap>();
        minimapComponent.Configure(
            containerRect,
            achillesMarkerRect,
            cyclopsMarkerRect,
            viewportRect,
            achillesObj.transform,
            cyclopsObj.transform,
            mainCam,
            boundsCollider);

        minimapComponent.UpdateMinimap();

        // 7. Save Scene
        EditorUtility.SetDirty(minimapObj);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);

        if (saved)
        {
            Debug.Log("[ApplyMinimapBatch] SUCCESS: SCN_Level01_Beach.unity saved with Level01Minimap!");
        }
        else
        {
            Debug.LogError("[ApplyMinimapBatch] FAILED: Could not save SCN_Level01_Beach.unity!");
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    private static void CreateViewportBorder(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject border = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        border.transform.SetParent(parent, false);

        RectTransform rt = border.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;

        Image img = border.GetComponent<Image>();
        img.color = new Color(1f, 0.92f, 0.32f, 0.90f); // Crisp golden line
        img.raycastTarget = false;
    }
}
