using System;
using UnityEngine;

/// <summary>
/// Renders a short 2D world-space directional line indicating the projectile aim angle and facing.
/// Dynamically updates position and orientation while aiming and hides during flight or throwing.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class AimGuideRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AchillesProjectileDebugLauncher launcher;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Guide Tuning")]
    [SerializeField, Range(1.5f, 4f)] private float guideLength = 2.5f;
    [SerializeField, Range(0.01f, 0.2f)] private float startWidth = 0.06f;
    [SerializeField, Range(0.01f, 0.2f)] private float endWidth = 0.03f;
    [SerializeField] private Color guideColor = new Color(1f, 0.85f, 0.2f, 0.85f);

    private readonly Vector3[] linePositions = new Vector3[2];

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnValidate()
    {
        guideLength = Mathf.Clamp(guideLength, 1.5f, 4f);
        startWidth = Mathf.Max(0.01f, startWidth);
        endWidth = Mathf.Max(0.01f, endWidth);
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.startColor = guideColor;
            lineRenderer.endColor = guideColor;
        }
    }

    public void Configure(AchillesProjectileDebugLauncher launcherRef, Transform spawnPointRef)
    {
        launcher = launcherRef;
        spawnPoint = spawnPointRef;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.startColor = guideColor;
            lineRenderer.endColor = guideColor;
            lineRenderer.sortingLayerName = "Projectile";
            lineRenderer.sortingOrder = 1;
        }
    }

    private void LateUpdate()
    {
        if (launcher == null || lineRenderer == null)
            return;

        bool shouldShow = launcher.CanAim && !launcher.IsThrowing && !launcher.HasActiveProjectile;
        if (!shouldShow)
        {
            if (lineRenderer.enabled)
                lineRenderer.enabled = false;
            return;
        }

        if (!lineRenderer.enabled)
            lineRenderer.enabled = true;

        int facing = launcher.FacingDirection;
        Vector3 origin = launcher.GetSpawnPosition();

        float angleRad = launcher.CurrentAngle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(facing * Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f).normalized;

        linePositions[0] = origin;
        linePositions[1] = origin + direction * guideLength;

        lineRenderer.SetPositions(linePositions);
    }

    private void OnDisable()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }
}
