using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Development launcher component attached to Player_Achilles.
/// Provides angle adjustment, power charging, wind randomization, spawn offset mirroring,
/// and camera follow switching for projectile firing.
/// </summary>
[DisallowMultipleComponent]
public sealed class AchillesProjectileDebugLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GroundedActorMotor2D motor;
    [SerializeField] private Collider2D actorCollider;
    [SerializeField] private Level01CameraController cameraController;
    [SerializeField] private DestructibleTerrain2D terrainTarget;
    [SerializeField] private Collider2D levelBoundsCollider;

    [Header("Angle Settings")]
    [SerializeField, Range(10f, 80f)] private float currentAngle = 45f;
    [SerializeField, Min(0f)] private float minAngle = 10f;
    [SerializeField, Min(0f)] private float maxAngle = 80f;
    [SerializeField, Min(1f)] private float angleAdjustmentSpeed = 45f;

    [Header("Launch Power")]
    [SerializeField, Min(0f)] private float minLaunchSpeed = 12f;
    [SerializeField, Min(0f)] private float maxLaunchSpeed = 24f;
    [SerializeField, Min(0.1f)] private float chargeDuration = 1.5f;

    [Header("Wind")]
    [SerializeField] private float minWind = -2f;
    [SerializeField] private float maxWind = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLauncherEnabled = true;

    private float chargeTimer;
    private bool isCharging;
    private Projectile2D activeProjectile;
    private readonly Vector3 fallbackSpawnOffset = new Vector3(0.8f, 1.2f, 0f);

    public float CurrentAngle => currentAngle;
    public bool IsCharging => isCharging;
    public float ChargeFraction => Mathf.Clamp01(chargeTimer / chargeDuration);
    public Projectile2D ActiveProjectile => activeProjectile;

    private void Awake()
    {
        InitializeReferences();
    }

    private void OnValidate()
    {
        currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
        minLaunchSpeed = Mathf.Max(0f, minLaunchSpeed);
        maxLaunchSpeed = Mathf.Max(minLaunchSpeed, maxLaunchSpeed);
        chargeDuration = Mathf.Max(0.1f, chargeDuration);
    }

    public void Configure(
        GameObject prefabReference,
        Transform spawnPointReference,
        GroundedActorMotor2D motorReference,
        Collider2D colliderReference,
        Level01CameraController cameraReference,
        DestructibleTerrain2D terrainReference,
        Collider2D boundsReference)
    {
        projectilePrefab = prefabReference;
        spawnPoint = spawnPointReference;
        motor = motorReference;
        actorCollider = colliderReference;
        cameraController = cameraReference;
        terrainTarget = terrainReference;
        levelBoundsCollider = boundsReference;
    }

    private void InitializeReferences()
    {
        if (motor == null)
            motor = GetComponent<GroundedActorMotor2D>();
        if (actorCollider == null)
            actorCollider = GetComponent<Collider2D>();
        if (spawnPoint == null)
        {
            Transform child = transform.Find("ProjectileSpawnPoint");
            if (child != null)
                spawnPoint = child;
        }
    }

    private void Update()
    {
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif

        if (!debugLauncherEnabled)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        HandleAngleInput(keyboard);
        HandleChargeAndLaunchInput(keyboard);
    }

    private void HandleAngleInput(Keyboard keyboard)
    {
        if (keyboard.wKey.isPressed)
        {
            currentAngle = Mathf.Min(maxAngle, currentAngle + angleAdjustmentSpeed * Time.deltaTime);
        }
        else if (keyboard.sKey.isPressed)
        {
            currentAngle = Mathf.Max(minAngle, currentAngle - angleAdjustmentSpeed * Time.deltaTime);
        }
    }

    private void HandleChargeAndLaunchInput(Keyboard keyboard)
    {
        // Only one active projectile allowed at a time
        if (activeProjectile != null)
        {
            isCharging = false;
            chargeTimer = 0f;
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            isCharging = true;
            chargeTimer = 0f;
        }

        if (isCharging)
        {
            if (keyboard.spaceKey.isPressed)
            {
                chargeTimer += Time.deltaTime;
            }

            if (keyboard.spaceKey.wasReleasedThisFrame)
            {
                float fraction = Mathf.Clamp01(chargeTimer / chargeDuration);
                float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, fraction);
                isCharging = false;
                chargeTimer = 0f;
                Launch(speed);
            }
        }
    }

    private void Launch(float speed)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[AchillesProjectileDebugLauncher] Cannot fire: Projectile prefab is not assigned.");
            return;
        }

        int facing = motor != null ? motor.FacingDirection : 1;
        if (facing == 0)
            facing = 1;

        Vector3 spawnWorldPosition = ComputeSpawnPosition(facing);

        // Direction based on elevation angle and facing direction
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector2 launchDirection = new Vector2(facing * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        Vector2 initialVelocity = launchDirection * speed;

        float wind = UnityEngine.Random.Range(minWind, maxWind);

        float initialAngleDeg = Mathf.Atan2(initialVelocity.y, initialVelocity.x) * Mathf.Rad2Deg;
        Quaternion initialRotation = Quaternion.Euler(0f, 0f, initialAngleDeg);

        GameObject projectileObject = Instantiate(projectilePrefab, spawnWorldPosition, initialRotation);
        Projectile2D projectile = projectileObject.GetComponent<Projectile2D>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<Projectile2D>();

        activeProjectile = projectile;

        if (cameraController != null)
        {
            cameraController.FollowProjectile(projectile.transform);
        }

        Collider2D ownerCol = actorCollider != null ? actorCollider : GetComponent<Collider2D>();
        projectile.Launch(
            initialVelocity,
            ownerCol,
            wind,
            levelBoundsCollider,
            terrainTarget,
            OnProjectileCompleted);

        // Log only one concise line when firing: angle, launch speed and wind
        Debug.Log($"[Achilles Launcher] Fired - Angle: {currentAngle:F1}°, Launch Speed: {speed:F1}, Wind: {wind:F2}");
    }

    private Vector3 ComputeSpawnPosition(int facing)
    {
        Vector3 baseOffset = spawnPoint != null ? spawnPoint.localPosition : fallbackSpawnOffset;
        // Mirror horizontal offset so spawn remains in front of upper body when flipX changes facing
        float horizontalOffset = Mathf.Abs(baseOffset.x) * (facing < 0 ? -1f : 1f);
        Vector3 mirroredLocalOffset = new Vector3(horizontalOffset, baseOffset.y, baseOffset.z);
        return transform.TransformPoint(mirroredLocalOffset);
    }

    private void OnProjectileCompleted(Projectile2D projectile)
    {
        if (activeProjectile == projectile)
        {
            activeProjectile = null;
        }

        if (cameraController != null)
        {
            if (projectile != null && projectile.LastCompletionReason == ProjectileCompletionReason.TerrainImpact)
            {
                cameraController.NotifyProjectileEnded(transform);
            }
            else
            {
                cameraController.FocusActor(transform);
            }
        }
    }

    private void OnDisable()
    {
        isCharging = false;
        chargeTimer = 0f;
    }
}
