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
    [SerializeField, Range(1.5f, 5f)] private float guideLength = 3.2f;
    [SerializeField, Range(0.01f, 0.3f)] private float startWidth = 0.08f;
    [SerializeField, Range(0.01f, 0.3f)] private float endWidth = 0.04f;
    [SerializeField] private Color guideColor = new Color(1f, 0.85f, 0.2f, 0.95f);

    private readonly Vector3[] linePositions = new Vector3[2];

    private void Awake()
    {
        InitializeComponents();
        EnsureActiveLauncher();
    }

    private void OnValidate()
    {
        guideLength = Mathf.Clamp(guideLength, 1.5f, 5f);
        startWidth = Mathf.Max(0.01f, startWidth);
        endWidth = Mathf.Max(0.01f, endWidth);
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.startColor = guideColor;
            lineRenderer.endColor = guideColor;
            lineRenderer.sortingLayerName = "Projectile";
            lineRenderer.sortingOrder = 10;
        }
    }

    public void Configure(AchillesProjectileDebugLauncher launcherRef, Transform spawnPointRef)
    {
        launcher = launcherRef;
        spawnPoint = spawnPointRef;
        InitializeComponents();
        if (lineRenderer != null)
            lineRenderer.enabled = true;
    }

    private void EnsureActiveLauncher()
    {
        if (launcher != null && launcher.gameObject.activeInHierarchy)
            return;

        var launchers = FindObjectsByType<AchillesProjectileDebugLauncher>(FindObjectsSortMode.None);
        for (int i = 0; i < launchers.Length; i++)
        {
            if (launchers[i] != null && launchers[i].gameObject.activeInHierarchy)
            {
                launcher = launchers[i];
                spawnPoint = launcher.SpawnPoint;
                break;
            }
        }
    }

    private void InitializeComponents()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.startColor = guideColor;
            lineRenderer.endColor = guideColor;
            lineRenderer.sortingLayerName = "Projectile";
            lineRenderer.sortingOrder = 10;

            if (lineRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    lineRenderer.sharedMaterial = new Material(shader);
            }
        }
    }

    private void LateUpdate()
    {
        if (launcher == null || !launcher.gameObject.activeInHierarchy)
        {
            EnsureActiveLauncher();
            if (launcher == null) return;
        }

        if (lineRenderer == null)
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

        Projectile2D selectedProj = launcher.SelectedProjectilePrefab;
        bool isOilJar = selectedProj != null && selectedProj.ImpactType == ProjectileImpactType.OilJar;

        if (isOilJar)
        {
            const int arcPoints = 20;
            if (lineRenderer.positionCount != arcPoints)
                lineRenderer.positionCount = arcPoints;

            Vector2 v0 = (Vector2)direction * launcher.CurrentLaunchSpeed;
            float wind = launcher.CurrentWind;
            float gravity = Physics2D.gravity.y;
            float dt = 0.04f;

            for (int i = 0; i < arcPoints; i++)
            {
                float t = i * dt;
                float px = origin.x + v0.x * t + 0.5f * wind * t * t;
                float py = origin.y + v0.y * t + 0.5f * gravity * t * t;
                lineRenderer.SetPosition(i, new Vector3(px, py, 0f));
            }
        }
        else
        {
            if (lineRenderer.positionCount != 2)
                lineRenderer.positionCount = 2;

            linePositions[0] = origin;
            linePositions[1] = origin + direction * guideLength;
            lineRenderer.SetPositions(linePositions);
        }
    }

    private void OnDisable()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }
}
