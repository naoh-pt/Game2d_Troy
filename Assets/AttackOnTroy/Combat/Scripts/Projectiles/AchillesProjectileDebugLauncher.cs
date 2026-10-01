using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Development launcher component attached to Player_Achilles.
/// Provides angle adjustment, power charging, wind randomization, spawn offset mirroring,
/// animation-synchronized spear release during the Throw animation, and camera follow switching.
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
    [SerializeField] private Animator animator;

    [Header("Animation Integration")]
    [SerializeField] private string throwStateName = "Attack";
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField, Range(0.1f, 0.9f)] private float releaseNormalizedTime = 0.45f;
    [SerializeField, Min(0.1f)] private float failSafeTimeoutDuration = 1.5f;

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

    // Throw sequence state
    private bool isThrowInProgress;
    private bool throwStateEntered;
    private bool projectileSpawnedThisThrow;
    private float throwTimeoutTimer;
    private float storedAngle;
    private float storedSpeed;
    private float storedWind;
    private int storedFacing;

    private int throwStateHash;
    private int attackTriggerHash;

    public float CurrentAngle => currentAngle;
    public bool IsCharging => isCharging;
    public float ChargeFraction => Mathf.Clamp01(chargeTimer / chargeDuration);
    public Projectile2D ActiveProjectile => activeProjectile;
    public bool IsThrowInProgress => isThrowInProgress;

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
        releaseNormalizedTime = Mathf.Clamp(releaseNormalizedTime, 0.1f, 0.9f);
        failSafeTimeoutDuration = Mathf.Max(0.1f, failSafeTimeoutDuration);
        throwStateHash = Animator.StringToHash(throwStateName);
        attackTriggerHash = Animator.StringToHash(attackTriggerName);
    }

    public void Configure(
        GameObject prefabReference,
        Transform spawnPointReference,
        GroundedActorMotor2D motorReference,
        Collider2D colliderReference,
        Level01CameraController cameraReference,
        DestructibleTerrain2D terrainReference,
        Collider2D boundsReference,
        Animator animatorReference = null,
        string throwState = "Attack",
        string attackTrigger = "Attack",
        float releaseNormalized = 0.45f)
    {
        projectilePrefab = prefabReference;
        spawnPoint = spawnPointReference;
        motor = motorReference;
        actorCollider = colliderReference;
        cameraController = cameraReference;
        terrainTarget = terrainReference;
        levelBoundsCollider = boundsReference;
        if (animatorReference != null)
            animator = animatorReference;
        throwStateName = throwState;
        attackTriggerName = attackTrigger;
        releaseNormalizedTime = releaseNormalized;
        throwStateHash = Animator.StringToHash(throwStateName);
        attackTriggerHash = Animator.StringToHash(attackTriggerName);
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
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        throwStateHash = Animator.StringToHash(throwStateName);
        attackTriggerHash = Animator.StringToHash(attackTriggerName);
    }

    private void Update()
    {
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif

        if (!debugLauncherEnabled)
            return;

        // Process active throw sequence if one is in progress
        if (isThrowInProgress)
        {
            UpdateThrowSequence();
            return; // Ignore aiming and charging input while throwing
        }

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
        // Only one active projectile allowed at a time, and cannot charge/throw while already throwing
        if (activeProjectile != null || isThrowInProgress)
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
                RequestThrow(speed);
            }
        }
    }

    private void RequestThrow(float speed)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[AchillesProjectileDebugLauncher] Cannot throw: Projectile prefab is not assigned.");
            return;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[AchillesProjectileDebugLauncher] Missing Animator or runtimeAnimatorController reference. Character movement restored.");
            if (motor != null)
                motor.SetMovementEnabled(true);
            return;
        }

        if (!animator.isActiveAndEnabled)
        {
            Debug.LogError("[AchillesProjectileDebugLauncher] Animator is not active or enabled. Character movement restored.");
            if (motor != null)
                motor.SetMovementEnabled(true);
            return;
        }

        // Store shot parameters and lock facing direction
        storedAngle = currentAngle;
        storedSpeed = speed;
        storedWind = UnityEngine.Random.Range(minWind, maxWind);
        storedFacing = motor != null ? motor.FacingDirection : 1;
        if (storedFacing == 0)
            storedFacing = 1;

        // Lock movement on motor (does not freeze Rigidbody2D or disable gravity)
        if (motor != null)
            motor.SetMovementEnabled(false);

        // Trigger Animator Attack state
        animator.SetTrigger(attackTriggerHash);

        // Initialize throw sequence tracking
        isThrowInProgress = true;
        throwStateEntered = false;
        projectileSpawnedThisThrow = false;
        throwTimeoutTimer = failSafeTimeoutDuration;
    }

    private void UpdateThrowSequence()
    {
        // Fail-safe timeout check
        throwTimeoutTimer -= Time.deltaTime;
        if (throwTimeoutTimer <= 0f)
        {
            Debug.LogWarning("[AchillesProjectileDebugLauncher] Throw sequence timed out. Restoring character movement.");
            CompleteThrow();
            return;
        }

        if (animator == null)
        {
            Debug.LogError("[AchillesProjectileDebugLauncher] Animator reference lost during throw. Restoring character movement.");
            CompleteThrow();
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool inThrowState = stateInfo.shortNameHash == throwStateHash;

        if (!throwStateEntered)
        {
            if (inThrowState)
            {
                throwStateEntered = true;
                // Once entered, refresh fail-safe timer with clip duration headroom
                throwTimeoutTimer = Mathf.Max(1.0f, failSafeTimeoutDuration);
            }
            return;
        }

        // We have entered the Throw state
        if (!projectileSpawnedThisThrow)
        {
            if (inThrowState)
            {
                if (stateInfo.normalizedTime >= releaseNormalizedTime)
                {
                    projectileSpawnedThisThrow = true;
                    SpawnProjectile();
                }
            }
            else
            {
                // Throw state exited before reaching release normalized time (interrupted)
                CompleteThrow();
            }
        }
        else
        {
            // Projectile has been spawned; wait for throw animation to complete and return to locomotion
            if (!inThrowState && !animator.IsInTransition(0))
            {
                CompleteThrow();
            }
        }
    }

    private void SpawnProjectile()
    {
        Vector3 spawnWorldPosition = ComputeSpawnPosition(storedFacing);

        // Direction based on elevation angle and locked facing direction
        float rad = storedAngle * Mathf.Deg2Rad;
        Vector2 launchDirection = new Vector2(storedFacing * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        Vector2 initialVelocity = launchDirection * storedSpeed;

        float initialAngleDeg = Mathf.Atan2(initialVelocity.y, initialVelocity.x) * Mathf.Rad2Deg;
        Quaternion initialRotation = Quaternion.Euler(0f, 0f, initialAngleDeg);

        GameObject projectileObject = Instantiate(projectilePrefab, spawnWorldPosition, initialRotation);
        Projectile2D projectile = projectileObject.GetComponent<Projectile2D>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<Projectile2D>();

        activeProjectile = projectile;

        // Camera follow begins only when projectile is actually created
        if (cameraController != null)
        {
            cameraController.FollowProjectile(projectile.transform);
        }

        Collider2D ownerCol = actorCollider != null ? actorCollider : GetComponent<Collider2D>();
        projectile.Launch(
            initialVelocity,
            ownerCol,
            storedWind,
            levelBoundsCollider,
            terrainTarget,
            OnProjectileCompleted);

        // Log only one concise line when firing: angle, launch speed and wind
        Debug.Log($"[Achilles Launcher] Fired - Angle: {storedAngle:F1}°, Launch Speed: {storedSpeed:F1}, Wind: {storedWind:F2}");
    }

    private void CompleteThrow()
    {
        isThrowInProgress = false;
        throwStateEntered = false;
        projectileSpawnedThisThrow = false;

        // Re-enable character movement on motor
        if (motor != null)
            motor.SetMovementEnabled(true);
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
        if (isThrowInProgress)
        {
            CompleteThrow();
        }

        isCharging = false;
        chargeTimer = 0f;
    }

    private void OnDestroy()
    {
        if (isThrowInProgress)
        {
            CompleteThrow();
        }
    }
}
