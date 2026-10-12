using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AchillesWeapon { Spear, ClubBomb }

/// <summary>
/// Development launcher component attached to Player_Achilles.
/// Provides angle adjustment, power charging, prepared wind lifecycle, spawn offset mirroring,
/// animation-synchronized selected-weapon release, presentation API, and camera follow switching.
/// </summary>
[DisallowMultipleComponent]
public sealed class AchillesProjectileDebugLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject clubBombPrefab;
    [SerializeField] private Level01TurnManager turnManager;
    [SerializeField] private AchillesWeapon selectedWeapon = AchillesWeapon.Spear;
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
    [SerializeField, Min(0.5f)] private float minimumFallbackTimeout = 1.5f;
    [SerializeField, Min(0.1f)] private float timeoutSafetyMargin = 0.5f;

    [Header("Angle Settings")]
    [SerializeField, Range(0f, 85f)] private float currentAngle = 45f;
    [SerializeField, Min(0f)] private float minAngle = 0f;
    [SerializeField, Min(0f)] private float maxAngle = 85f;
    [SerializeField, Min(1f)] private float angleAdjustmentSpeed = 45f;

    [Header("Launch Power")]
    [SerializeField, Min(0f)] private float minLaunchSpeed = 12f;
    [SerializeField, Min(0f)] private float maxLaunchSpeed = 24f;
    [SerializeField, Min(0.1f)] private float chargeDuration = 1.25f;

    [Header("Wind")]
    [SerializeField] private float minWind = -0.8f;
    [SerializeField] private float maxWind = 0.8f;

    [Header("Double-Shot Ability")]
    [SerializeField, Range(1f, 15f)] private float doubleShotSpreadAngle = 3.0f;
    [SerializeField] private PlayerEnergySystem energySystem;

    [Header("Level 03 Wall Carve")]
    [SerializeField] private bool enableOilJarWallCarve = false;

    [Header("Debug")]
    [SerializeField] private bool debugLauncherEnabled = true;

    private float chargeTimer;
    private bool isCharging;
    private Projectile2D activeProjectile;
    private readonly System.Collections.Generic.List<Projectile2D> activeProjectiles = new System.Collections.Generic.List<Projectile2D>();
    private bool isDoubleShotArmed;
    private bool storedDoubleShot;
    private readonly Vector3 fallbackSpawnOffset = new Vector3(0.8f, 1.2f, 0f);

    // Prepared wind lifecycle
    private float preparedWind;

    // Throw sequence state
    private bool isThrowInProgress;
    private bool throwStateEntered;
    private bool projectileSpawnedThisThrow;
    private float throwTimeoutTimer;
    private float storedAngle;
    private float storedSpeed;
    private float storedWind;
    private int storedFacing;
    private GameObject storedProjectilePrefab;

    private int throwStateHash;
    private int attackTriggerHash;

    // Turn system integration events
    public event Action ThrowStarted;
    public event Action ProjectileLaunched;
    public event Action ProjectileCompleted;

    private bool turnInputEnabled = true;
    private bool projectileCompletedFiredThisShot;

    #region Presentation API
    public float CurrentAngle => currentAngle;
    public float CurrentCharge01 => isCharging && chargeDuration > 0f
        ? Mathf.Lerp(0.25f, 1.0f, Mathf.Clamp01(chargeTimer / chargeDuration))
        : 0f;
    public float CurrentLaunchSpeed
    {
        get
        {
            GetWeaponSpeedRange(out float minSpeed, out float maxSpeed);
            float t = isCharging && chargeDuration > 0f ? Mathf.Clamp01(chargeTimer / chargeDuration) : 0f;
            return Mathf.Lerp(minSpeed, maxSpeed, t);
        }
    }
    public float CurrentWind => preparedWind;
    public int FacingDirection => motor != null ? motor.FacingDirection : 1;
    public bool IsCharging => isCharging;
    public bool IsThrowing => isThrowInProgress;
    public bool HasActiveProjectile => activeProjectiles.Count > 0 || activeProjectile != null;
    public bool CanAim => turnInputEnabled && !isThrowInProgress && activeProjectiles.Count == 0 && activeProjectile == null;
    public bool IsDoubleShotArmed => isDoubleShotArmed;
    public float DoubleShotSpreadAngle => doubleShotSpreadAngle;
    public void SetDoubleShotArmed(bool armed) => isDoubleShotArmed = armed;
    public void SetEnergySystem(PlayerEnergySystem system) => energySystem = system;
    public bool TurnInputEnabled => turnInputEnabled;
    public AchillesWeapon SelectedWeapon => selectedWeapon;
    public GameObject ProjectilePrefab => projectilePrefab;
    public GameObject ClubBombPrefab => clubBombPrefab;
    public Transform SpawnPoint => spawnPoint;
    public Projectile2D SelectedProjectilePrefab
    {
        get
        {
            GameObject prefab = selectedWeapon == AchillesWeapon.ClubBomb ? clubBombPrefab : projectilePrefab;
            return prefab != null ? prefab.GetComponent<Projectile2D>() : null;
        }
    }
    public string SelectedWeaponName
    {
        get
        {
            if (selectedWeapon == AchillesWeapon.ClubBomb)
            {
                if (clubBombPrefab != null && (clubBombPrefab.name.IndexOf("Jar", StringComparison.OrdinalIgnoreCase) >= 0 || clubBombPrefab.name.IndexOf("Oil", StringComparison.OrdinalIgnoreCase) >= 0))
                    return "OIL JAR";
                return "CLUB BOMB";
            }
            if (projectilePrefab != null && projectilePrefab.name.IndexOf("Arrow", StringComparison.OrdinalIgnoreCase) >= 0)
                return "ARROW";
            return "SPEAR";
        }
    }
    public bool CanSelectWeapon => CanAim && turnManager != null && turnManager.IsPlayerTurn;
    #endregion

    /// <summary>Narrow setup API: does not touch aiming, wind, throw timing or presentation.</summary>
    public void ConfigureWeapons(GameObject spear, GameObject bomb, Level01TurnManager turns)
    {
        projectilePrefab = spear;
        clubBombPrefab = bomb;
        turnManager = turns;
    }

    public bool TrySelectWeapon(AchillesWeapon weapon)
    {
        if (!CanSelectWeapon || (weapon != AchillesWeapon.Spear && weapon != AchillesWeapon.ClubBomb)) return false;
        GameObject prefab = weapon == AchillesWeapon.ClubBomb ? clubBombPrefab : projectilePrefab;
        Projectile2D projectile = prefab != null ? prefab.GetComponent<Projectile2D>() : null;
        if (projectile == null) return false;
        if (weapon == AchillesWeapon.ClubBomb)
        {
            if (projectile.ImpactType != ProjectileImpactType.ClubBomb && projectile.ImpactType != ProjectileImpactType.OilJar)
                return false;
        }
        else
        {
            if (projectile.ImpactType != ProjectileImpactType.Spear && projectile.ImpactType != ProjectileImpactType.Arrow)
                return false;
        }
        selectedWeapon = weapon;
        return true;
    }

    private float externalAimInput;

    public void SetAimInput(float direction)
    {
        externalAimInput = Mathf.Clamp(direction, -1f, 1f);
    }

    public void StartCharging()
    {
        if (!CanAim || activeProjectile != null || isThrowInProgress)
            return;
        isCharging = true;
        chargeTimer = 0f;
    }

    public void GetWeaponSpeedRange(out float minSpeed, out float maxSpeed)
    {
        Projectile2D prefab = SelectedProjectilePrefab;
        if (prefab != null)
        {
            switch (prefab.ImpactType)
            {
                case ProjectileImpactType.Spear:
                    minSpeed = 16f;
                    maxSpeed = 32f;
                    return;
                case ProjectileImpactType.ClubBomb:
                    minSpeed = 12f;
                    maxSpeed = 28f;
                    return;
                case ProjectileImpactType.Arrow:
                    minSpeed = 32f;
                    maxSpeed = 50f;
                    return;
                case ProjectileImpactType.OilJar:
                    minSpeed = 12f;
                    maxSpeed = 27f;
                    return;
            }
        }
        minSpeed = minLaunchSpeed;
        maxSpeed = maxLaunchSpeed;
    }

    public void ReleaseChargeAndThrow()
    {
        if (!isCharging)
            return;
        float t = chargeDuration > 0f ? Mathf.Clamp01(chargeTimer / chargeDuration) : 0f;
        GetWeaponSpeedRange(out float minSpeed, out float maxSpeed);
        float speed = Mathf.Lerp(minSpeed, maxSpeed, t);
        isCharging = false;
        chargeTimer = 0f;
        RequestThrow(speed);
    }

    public void CancelCharging()
    {
        isCharging = false;
        chargeTimer = 0f;
    }

    public void SetTurnInputEnabled(bool enabled)
    {
        turnInputEnabled = enabled;

        if (!enabled)
        {
            isCharging = false;
            chargeTimer = 0f;
            externalAimInput = 0f;
        }

        if (motor != null)
        {
            motor.SetMovementEnabled(enabled);
        }
    }

    private void Awake()
    {
        InitializeReferences();
        GenerateNextWind();
    }

    private void OnValidate()
    {
        currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
        minLaunchSpeed = Mathf.Max(0f, minLaunchSpeed);
        maxLaunchSpeed = Mathf.Max(minLaunchSpeed, maxLaunchSpeed);
        chargeDuration = Mathf.Max(0.1f, chargeDuration);
        releaseNormalizedTime = Mathf.Clamp(releaseNormalizedTime, 0.1f, 0.9f);
        minimumFallbackTimeout = Mathf.Max(0.5f, minimumFallbackTimeout);
        timeoutSafetyMargin = Mathf.Max(0.1f, timeoutSafetyMargin);
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

        if (float.IsNaN(preparedWind) || preparedWind == 0f)
            GenerateNextWind();
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
        if (turnManager == null)
            turnManager = FindAnyObjectByType<Level01TurnManager>();
        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<Level01CameraController>();

        throwStateHash = Animator.StringToHash(throwStateName);
        attackTriggerHash = Animator.StringToHash(attackTriggerName);
    }

    public void GenerateNextWind()
    {
        UpdateWindBoundsForCurrentScene();
        preparedWind = UnityEngine.Random.Range(minWind, maxWind);
    }

    private void UpdateWindBoundsForCurrentScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.IndexOf("Level03", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            minWind = -2.0f;
            maxWind = 2.0f;
        }
        else if (sceneName.IndexOf("Level02", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            minWind = -1.5f;
            maxWind = 1.5f;
        }
        else
        {
            minWind = -0.8f;
            maxWind = 0.8f;
        }
    }

    public Vector3 GetSpawnPosition()
    {
        int facing = FacingDirection;
        return ComputeSpawnPosition(facing);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // Resolve an already-started animation even if debug input is subsequently disabled.
        if (isThrowInProgress)
        {
            UpdateThrowSequence();
            return;
        }
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif

        if (!debugLauncherEnabled)
            return;

        if (!turnInputEnabled || activeProjectile != null)
            return;

        // Apply external aim input (touch controls)
        if (CanAim && Mathf.Abs(externalAimInput) > 0.001f)
        {
            currentAngle = Mathf.Clamp(currentAngle + externalAimInput * angleAdjustmentSpeed * Time.deltaTime, minAngle, maxAngle);
        }

        // Advance charging timer while charging (driven by keyboard space or touch throw button)
        if (isCharging)
        {
            chargeTimer = Mathf.Min(chargeTimer + Time.deltaTime, chargeDuration);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !debugLauncherEnabled)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            TrySelectWeapon(AchillesWeapon.Spear);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            TrySelectWeapon(AchillesWeapon.ClubBomb);

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
        if (activeProjectile != null || isThrowInProgress)
        {
            isCharging = false;
            chargeTimer = 0f;
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            StartCharging();
        }

        if (isCharging && keyboard.spaceKey.wasReleasedThisFrame)
        {
            ReleaseChargeAndThrow();
        }
    }

    private void RequestThrow(float speed)
    {
        if (Time.timeScale <= 0f || !CanAim) return;
        GameObject chosenPrefab = selectedWeapon == AchillesWeapon.ClubBomb ? clubBombPrefab : projectilePrefab;
        if (chosenPrefab == null || chosenPrefab.GetComponent<Projectile2D>() == null)
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
        storedProjectilePrefab = chosenPrefab;
        // Prepared wind is locked in for this throw
        storedWind = preparedWind;
        storedFacing = motor != null ? motor.FacingDirection : 1;
        if (storedFacing == 0)
            storedFacing = 1;

        // Double-shot special attack state
        storedDoubleShot = isDoubleShotArmed;
        if (storedDoubleShot)
        {
            isDoubleShotArmed = false;
            if (energySystem != null)
                energySystem.ConsumeSpecialOnLaunch();
        }

        // Lock movement on motor (does not freeze Rigidbody2D or disable gravity)
        if (motor != null)
            motor.SetMovementEnabled(false);

        // Trigger Animator Attack state
        animator.SetTrigger(attackTriggerHash);

        // Initialize throw sequence tracking with initial fallback timeout
        isThrowInProgress = true;
        throwStateEntered = false;
        projectileSpawnedThisThrow = false;
        projectileCompletedFiredThisShot = false;
        throwTimeoutTimer = minimumFallbackTimeout;

        ThrowStarted?.Invoke();
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
                // Derive safe timeout from active Throw state/clip duration and Animator playback speed
                float clipLength = stateInfo.length > 0f ? stateInfo.length : 0.5f;
                float effectiveSpeed = Mathf.Max(0.01f, Mathf.Abs(animator.speed * stateInfo.speed));
                float stateDuration = clipLength / effectiveSpeed;
                throwTimeoutTimer = Mathf.Max(minimumFallbackTimeout, stateDuration * 2.0f + timeoutSafetyMargin);
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
        if (storedDoubleShot)
        {
            SpawnDoubleProjectiles();
        }
        else
        {
            SpawnSingleProjectile();
        }
    }

    private void SpawnSingleProjectile()
    {
        Vector3 spawnWorldPosition = ComputeSpawnPosition(storedFacing);

        // Direction based on elevation angle and locked facing direction
        float rad = storedAngle * Mathf.Deg2Rad;
        Vector2 launchDirection = new Vector2(storedFacing * Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        Vector2 initialVelocity = launchDirection * storedSpeed;

        float initialAngleDeg = Mathf.Atan2(initialVelocity.y, initialVelocity.x) * Mathf.Rad2Deg;
        Projectile2D prefabProjectile = storedProjectilePrefab.GetComponent<Projectile2D>();
        Quaternion initialRotation = (prefabProjectile.ImpactType == ProjectileImpactType.ClubBomb || prefabProjectile.ImpactType == ProjectileImpactType.OilJar)
            ? Quaternion.identity : Quaternion.Euler(0f, 0f, initialAngleDeg);

        GameObject projectileObject = Instantiate(storedProjectilePrefab, spawnWorldPosition, initialRotation);
        Projectile2D projectile = projectileObject.GetComponent<Projectile2D>();

        activeProjectiles.Clear();
        activeProjectiles.Add(projectile);
        activeProjectile = projectile;

        // Camera follow begins only when projectile is actually created
        if (cameraController != null)
        {
            cameraController.FollowProjectile(projectile.transform);
        }

        // Fire ProjectileLaunched event
        ProjectileLaunched?.Invoke();

        Collider2D ownerCol = actorCollider != null ? actorCollider : GetComponent<Collider2D>();
        if (enableOilJarWallCarve && projectile.ImpactType == ProjectileImpactType.OilJar)
        {
            projectile.OilJarWallCarveEnabled = true;
        }

        projectile.Launch(
            initialVelocity,
            ownerCol,
            storedWind,
            levelBoundsCollider,
            terrainTarget,
            OnProjectileCompleted);

        // Log only one concise line when firing: angle, launch speed and wind
        Debug.Log($"[Achilles Launcher] Fired {projectile.ImpactType} - Angle: {storedAngle:F1}°, Launch Speed: {storedSpeed:F1}, Wind: {storedWind:F2}");
    }

    private void SpawnDoubleProjectiles()
    {
        Vector3 spawnWorldPosition = ComputeSpawnPosition(storedFacing);
        Projectile2D prefabProjectile = storedProjectilePrefab.GetComponent<Projectile2D>();
        bool isBallisticRot = (prefabProjectile.ImpactType == ProjectileImpactType.ClubBomb || prefabProjectile.ImpactType == ProjectileImpactType.OilJar);

        // Symmetric angles: storedAngle - spread and storedAngle + spread
        float angle1 = Mathf.Clamp(storedAngle - doubleShotSpreadAngle, 0f, 85f);
        float angle2 = Mathf.Clamp(storedAngle + doubleShotSpreadAngle, 0f, 85f);

        float rad1 = angle1 * Mathf.Deg2Rad;
        Vector2 launchDir1 = new Vector2(storedFacing * Mathf.Cos(rad1), Mathf.Sin(rad1)).normalized;
        Vector2 initialVelocity1 = launchDir1 * storedSpeed;

        float rad2 = angle2 * Mathf.Deg2Rad;
        Vector2 launchDir2 = new Vector2(storedFacing * Mathf.Cos(rad2), Mathf.Sin(rad2)).normalized;
        Vector2 initialVelocity2 = launchDir2 * storedSpeed;

        float angleDeg1 = Mathf.Atan2(initialVelocity1.y, initialVelocity1.x) * Mathf.Rad2Deg;
        float angleDeg2 = Mathf.Atan2(initialVelocity2.y, initialVelocity2.x) * Mathf.Rad2Deg;

        Quaternion rot1 = isBallisticRot ? Quaternion.identity : Quaternion.Euler(0f, 0f, angleDeg1);
        Quaternion rot2 = isBallisticRot ? Quaternion.identity : Quaternion.Euler(0f, 0f, angleDeg2);

        GameObject obj1 = Instantiate(storedProjectilePrefab, spawnWorldPosition, rot1);
        GameObject obj2 = Instantiate(storedProjectilePrefab, spawnWorldPosition, rot2);

        Projectile2D p1 = obj1.GetComponent<Projectile2D>();
        Projectile2D p2 = obj2.GetComponent<Projectile2D>();

        // Ensure the two projectiles don't collide with each other
        Collider2D col1 = obj1.GetComponent<Collider2D>();
        Collider2D col2 = obj2.GetComponent<Collider2D>();
        if (col1 != null && col2 != null)
        {
            Physics2D.IgnoreCollision(col1, col2, true);
        }

        activeProjectiles.Clear();
        activeProjectiles.Add(p1);
        activeProjectiles.Add(p2);
        activeProjectile = p1;

        if (cameraController != null)
        {
            cameraController.FollowProjectiles(p1.transform, p2.transform);
        }

        ProjectileLaunched?.Invoke();

        Collider2D ownerCol = actorCollider != null ? actorCollider : GetComponent<Collider2D>();
        if (enableOilJarWallCarve && p1.ImpactType == ProjectileImpactType.OilJar)
        {
            p1.OilJarWallCarveEnabled = true;
            p2.OilJarWallCarveEnabled = true;
        }

        p1.Launch(
            initialVelocity1,
            ownerCol,
            storedWind,
            levelBoundsCollider,
            terrainTarget,
            OnProjectileCompleted);

        p2.Launch(
            initialVelocity2,
            ownerCol,
            storedWind,
            levelBoundsCollider,
            terrainTarget,
            OnProjectileCompleted);

        Debug.Log($"[Achilles Launcher] DOUBLE-SHOT Fired 2x {prefabProjectile.ImpactType} - Angles: ({angle1:F1}°, {angle2:F1}°), Speed: {storedSpeed:F1}, Wind: {storedWind:F2}");
    }

    private void CompleteThrow()
    {
        isThrowInProgress = false;
        throwStateEntered = false;

        // If the throw sequence aborted or timed out before spawning, ensure completion is signaled
        if (!projectileSpawnedThisThrow && !projectileCompletedFiredThisShot)
        {
            projectileCompletedFiredThisShot = true;
            ProjectileCompleted?.Invoke();
        }

        projectileSpawnedThisThrow = false;

        // Re-enable character movement on motor only if turn input is enabled
        if (motor != null && turnInputEnabled)
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
        if (this == null) return;

        activeProjectiles.Remove(projectile);
        if (activeProjectile == projectile)
            activeProjectile = activeProjectiles.Count > 0 ? activeProjectiles[0] : null;

        // If battle ended (e.g. lethal hit on Cyclops), stop immediately!
        if (turnManager != null && turnManager.IsBattleOver)
        {
            return;
        }

        // If another projectile is still flying, do NOT complete the turn!
        if (activeProjectiles.Count > 0)
        {
            if (cameraController != null && activeProjectiles[0] != null)
            {
                cameraController.FollowProjectile(activeProjectiles[0].transform);
            }
            return;
        }

        if (projectileCompletedFiredThisShot) return;
        projectileCompletedFiredThisShot = true;

        // Generate the NEXT wind value only after ALL projectiles have completed
        GenerateNextWind();

        try
        {
            ProjectileCompleted?.Invoke();
        }
        finally
        {
            // When turnManager is active, Level01TurnManager controls camera transitions between turns
            // (e.g. focusing Cyclops during preparation/turn, or focusing defeated actor on battle end).
            // Only fall back to focusing Achilles if running in standalone debug mode without a turn manager.
            if (turnManager == null && cameraController != null)
            {
                if (projectile != null && projectile.LastCompletionReason == ProjectileCompletionReason.TerrainImpact)
                    cameraController.NotifyProjectileEnded(transform);
                else
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
        externalAimInput = 0f;
    }

    private void OnDestroy()
    {
        if (isThrowInProgress)
        {
            CompleteThrow();
        }
    }
}
