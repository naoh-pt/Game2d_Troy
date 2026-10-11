using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact HUD minimap for Level 01 Beach:
/// Displays a scaled overview of the level bounds, dynamic actor markers for Achilles and Cyclops Normal,
/// and a camera viewport rectangle reflecting camera position, panning, and orthographic zoom in real time.
/// Updates smoothly during actor movement, camera pan, and zoom without requiring any new art assets.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01Minimap : MonoBehaviour
{
    [Header("UI Rect References")]
    [Tooltip("The inner container RectTransform of the minimap where coordinates and markers are mapped.")]
    [SerializeField] private RectTransform mapContainer;
    [Tooltip("Marker RectTransform representing Achilles.")]
    [SerializeField] private RectTransform achillesMarker;
    [Tooltip("Marker RectTransform representing Cyclops Normal.")]
    [SerializeField] private RectTransform cyclopsMarker;
    [Tooltip("Rectangle RectTransform indicating the current camera viewport in world space.")]
    [SerializeField] private RectTransform cameraViewportRect;

    [Header("World References")]
    [Tooltip("Transform of Player Achilles.")]
    [SerializeField] private Transform achillesTransform;
    [Tooltip("Transform of Enemy Cyclops Normal.")]
    [SerializeField] private Transform cyclopsTransform;
    [Tooltip("Main camera following the actors and projectiles.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Collider representing LevelBounds.")]
    [SerializeField] private Collider2D levelBounds;

    [Header("Settings & Fallbacks")]
    [Tooltip("Fallback bounds min if levelBounds collider is missing.")]
    [SerializeField] private Vector2 fallbackBoundsMin = new Vector2(-33.7f, -8.4f);
    [Tooltip("Fallback bounds max if levelBounds collider is missing.")]
    [SerializeField] private Vector2 fallbackBoundsMax = new Vector2(34.1f, 27.7f);
    [Tooltip("Keep actor markers inside the map boundaries.")]
    [SerializeField] private bool clampMarkersToMap = true;
    [Tooltip("Padding in pixels inside the map for actor markers.")]
    [SerializeField] private float markerPadding = 3f;

    public RectTransform MapContainer => mapContainer;
    public RectTransform AchillesMarker => achillesMarker;
    public RectTransform CyclopsMarker => cyclopsMarker;
    public RectTransform CameraViewportRect => cameraViewportRect;
    public Transform AchillesTransform => achillesTransform;
    public Transform CyclopsTransform => cyclopsTransform;
    public Camera TargetCamera => targetCamera;
    public Collider2D LevelBounds => levelBounds;

    private void Awake()
    {
        AutoFindMissingReferences();
    }

    private void Start()
    {
        AutoFindMissingReferences();
        UpdateMinimap();
    }

    private void OnEnable()
    {
        AutoFindMissingReferences();
        UpdateMinimap();
    }

    private void LateUpdate()
    {
        UpdateMinimap();
    }

    public void Configure(
        RectTransform container,
        RectTransform achilles,
        RectTransform cyclops,
        RectTransform viewport,
        Transform achillesActor,
        Transform cyclopsActor,
        Camera cam,
        Collider2D bounds)
    {
        mapContainer = container;
        achillesMarker = achilles;
        cyclopsMarker = cyclops;
        cameraViewportRect = viewport;
        achillesTransform = achillesActor;
        cyclopsTransform = cyclopsActor;
        targetCamera = cam;
        levelBounds = bounds;
    }

    public void SetPlayerTransform(Transform playerTr)
    {
        achillesTransform = playerTr;
    }

    public void AutoFindMissingReferences()
    {
        if (mapContainer == null)
        {
            Transform container = transform.Find("MapContainer");
            mapContainer = container != null ? container.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        }

        if (achillesMarker == null && mapContainer != null)
        {
            Transform m = mapContainer.Find("AchillesMarker");
            if (m != null) achillesMarker = m.GetComponent<RectTransform>();
        }

        if (cyclopsMarker == null && mapContainer != null)
        {
            Transform m = mapContainer.Find("CyclopsMarker");
            if (m != null) cyclopsMarker = m.GetComponent<RectTransform>();
        }

        if (cameraViewportRect == null && mapContainer != null)
        {
            Transform vp = mapContainer.Find("CameraViewport");
            if (vp != null) cameraViewportRect = vp.GetComponent<RectTransform>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                var camObj = GameObject.Find("Main Camera");
                if (camObj != null) targetCamera = camObj.GetComponent<Camera>();
            }
        }

        if (achillesTransform == null || !achillesTransform.gameObject.activeInHierarchy)
        {
            var activeLauncher = FindAnyObjectByType<AchillesProjectileDebugLauncher>();
            if (activeLauncher != null)
                achillesTransform = activeLauncher.transform;
            else
            {
                GameObject p = GameObject.Find("Player_Achilles");
                if (p != null) achillesTransform = p.transform;
            }
        }

        if (cyclopsTransform == null)
        {
            GameObject c = GameObject.Find("Enemy_CyclopsNormal");
            if (c != null) cyclopsTransform = c.transform;
        }

        if (levelBounds == null)
        {
            GameObject b = GameObject.Find("LevelBounds");
            if (b != null) levelBounds = b.GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// Updates actor markers and camera viewport box in real time according to world coordinates.
    /// </summary>
    public void UpdateMinimap()
    {
        if (mapContainer == null)
            return;

        // 1. Retrieve world bounds from collider or fallback
        Bounds bounds;
        if (levelBounds != null)
        {
            bounds = levelBounds.bounds;
        }
        else
        {
            Vector3 center = (fallbackBoundsMin + fallbackBoundsMax) * 0.5f;
            Vector3 size = new Vector3(
                Mathf.Max(1f, fallbackBoundsMax.x - fallbackBoundsMin.x),
                Mathf.Max(1f, fallbackBoundsMax.y - fallbackBoundsMin.y),
                1f);
            bounds = new Bounds(center, size);
        }

        float worldMinX = bounds.min.x;
        float worldMaxX = bounds.max.x;
        float worldMinY = bounds.min.y;
        float worldMaxY = bounds.max.y;
        float worldWidth = Mathf.Max(0.001f, worldMaxX - worldMinX);
        float worldHeight = Mathf.Max(0.001f, worldMaxY - worldMinY);

        Rect mapRect = mapContainer.rect;

        // 2. Update Achilles Marker
        if (achillesMarker != null && achillesTransform != null)
        {
            Vector2 markerPos = WorldToMapPosition(
                achillesTransform.position,
                worldMinX, worldMinY, worldWidth, worldHeight,
                mapRect, clampMarkersToMap, markerPadding);
            achillesMarker.anchoredPosition = markerPos;
            if (!achillesMarker.gameObject.activeSelf)
                achillesMarker.gameObject.SetActive(true);
        }

        // 3. Update Cyclops Marker
        if (cyclopsMarker != null && cyclopsTransform != null)
        {
            Vector2 markerPos = WorldToMapPosition(
                cyclopsTransform.position,
                worldMinX, worldMinY, worldWidth, worldHeight,
                mapRect, clampMarkersToMap, markerPadding);
            cyclopsMarker.anchoredPosition = markerPos;
            if (!cyclopsMarker.gameObject.activeSelf)
                cyclopsMarker.gameObject.SetActive(true);
        }

        // 4. Update Camera Viewport Rect
        if (cameraViewportRect != null && targetCamera != null && targetCamera.orthographic)
        {
            UpdateCameraViewport(
                worldMinX, worldMinY, worldWidth, worldHeight,
                mapRect);
        }
    }

    private Vector2 WorldToMapPosition(
        Vector3 worldPos,
        float worldMinX, float worldMinY, float worldWidth, float worldHeight,
        Rect mapRect, bool clamp, float padding)
    {
        float normX = (worldPos.x - worldMinX) / worldWidth;
        float normY = (worldPos.y - worldMinY) / worldHeight;

        if (clamp)
        {
            normX = Mathf.Clamp01(normX);
            normY = Mathf.Clamp01(normY);
        }

        float posX = mapRect.xMin + normX * mapRect.width;
        float posY = mapRect.yMin + normY * mapRect.height;

        if (clamp && padding > 0f)
        {
            posX = Mathf.Clamp(posX, mapRect.xMin + padding, mapRect.xMax - padding);
            posY = Mathf.Clamp(posY, mapRect.yMin + padding, mapRect.yMax - padding);
        }

        return new Vector2(posX, posY);
    }

    private void UpdateCameraViewport(
        float worldMinX, float worldMinY, float worldWidth, float worldHeight,
        Rect mapRect)
    {
        Vector3 camPos = targetCamera.transform.position;
        float orthoSize = targetCamera.orthographicSize;
        float aspect = targetCamera.aspect;

        float camHalfHeight = Mathf.Max(0.1f, orthoSize);
        float camHalfWidth = camHalfHeight * aspect;

        float camMinX = camPos.x - camHalfWidth;
        float camMaxX = camPos.x + camHalfWidth;
        float camMinY = camPos.y - camHalfHeight;
        float camMaxY = camPos.y + camHalfHeight;

        // Normalized viewport boundaries within level bounds
        float normMinX = (camMinX - worldMinX) / worldWidth;
        float normMaxX = (camMaxX - worldMinX) / worldWidth;
        float normMinY = (camMinY - worldMinY) / worldHeight;
        float normMaxY = (camMaxY - worldMinY) / worldHeight;

        // Scaled size on the minimap
        float vpWidth = Mathf.Max(4f, (normMaxX - normMinX) * mapRect.width);
        float vpHeight = Mathf.Max(4f, (normMaxY - normMinY) * mapRect.height);

        // Center position relative to mapContainer pivot
        float normCenterX = (normMinX + normMaxX) * 0.5f;
        float normCenterY = (normMinY + normMaxY) * 0.5f;

        float posX = mapRect.xMin + normCenterX * mapRect.width;
        float posY = mapRect.yMin + normCenterY * mapRect.height;

        cameraViewportRect.sizeDelta = new Vector2(vpWidth, vpHeight);
        cameraViewportRect.anchoredPosition = new Vector2(posX, posY);
        if (!cameraViewportRect.gameObject.activeSelf)
            cameraViewportRect.gameObject.SetActive(true);
    }
}
