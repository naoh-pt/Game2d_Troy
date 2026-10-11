using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controls the Cyclops Normal enemy turn in Level 01:
/// stands in place, faces Achilles to the left, triggers the Throw animation, and launches
/// exactly one stone axe toward Achilles. After axe flight completes (impact or timeout),
/// refocuses camera on Cyclops, walks a short configurable distance to reachable ground,
/// and finishes the turn reliably, returning camera to Achilles.
/// </summary>
[DisallowMultipleComponent]
public sealed class CyclopsNormalTurnController : MonoBehaviour
{
    private enum TurnPhase
    {
        Idle,
        Throwing,
        AxeInFlight,
        PostHitPause,
        CameraFocusPause,
        Repositioning,
        PostMoveSettle,
        Completed
    }

    [Header("Enemy Identity")]
    [Tooltip("Unique ID for dialogue matching (e.g. CyclopsNormal).")]
    [SerializeField] private string enemyId = "CyclopsNormal";

    [Header("Aim & Launch AI")]
    [Tooltip("Probability [0..1] of aiming directly at Achilles vs intentionally missing.")]
    [SerializeField, Range(0f, 1f)] private float accuracy = 0.70f;
    [SerializeField, Range(15f, 80f)] private float minThrowAngle = 25f;
    [SerializeField, Range(15f, 85f)] private float maxThrowAngle = 70f;
    [SerializeField, Range(20f, 75f)] private float preferredAngle = 48f;
    [SerializeField, Min(1f)] private float minLaunchSpeed = 3f;
    [SerializeField, Min(1f)] private float maxLaunchSpeed = 28f;
    [SerializeField, Min(0f)] private float targetVerticalOffset = 0.8f;
    [SerializeField, Min(0f)] private float minMissDistance = 1.2f;
    [SerializeField, Min(0f)] private float maxMissDistance = 3.5f;
    [SerializeField, Range(0f, 5f)] private float angleJitterDegrees = 1.5f;
    [SerializeField, Range(0f, 0.15f)] private float speedJitterFraction = 0.03f;
    [SerializeField, Range(3, 20)] private int candidateTrajectorySamples = 10;

    [Header("Aim & Launch (Runtime / Legacy)")]
    [SerializeField, Range(10f, 80f)] private float throwAngle = 45f;
    [SerializeField, Min(1f)] private float launchSpeed = 19f;
    [SerializeField] private Vector3 fallbackSpawnOffset = new Vector3(1.2f, 2.0f, 0f);
    [SerializeField] private Transform throwSpawnPoint;

    [Header("Animation Integration")]
    [SerializeField] private string throwTriggerName = "Throw";
    [SerializeField] private string throwStateName = "Throw";
    [SerializeField, Range(0.1f, 0.9f)] private float releaseNormalizedTime = 0.5f;
    [SerializeField, Min(0.5f)] private float throwTimeoutDuration = 2.5f;
    [SerializeField, Range(0.1f, 2f)] private float walkAnimationSpeedMultiplier = 0.7f;

    [Header("Hit Feedback & Camera Pacing")]
    [Tooltip("Pause duration focusing on the target after axe completes so hit/hurt reaction and HP change can be seen.")]
    [SerializeField, Min(0f)] private float postHitHoldDuration = 1.2f;
    [Tooltip("Delay after camera refocuses on Cyclops before Cyclops starts walking, ensuring camera arrives before movement begins.")]
    [SerializeField, Min(0f)] private float cameraFocusDelayBeforeMove = 0.5f;
    [Tooltip("Brief pause after Cyclops finishes walking before ending turn, showing the final stance.")]
    [SerializeField, Min(0f)] private float postMoveSettleDuration = 0.3f;

    [Header("Post-Throw Repositioning")]
    [FormerlySerializedAs("moveDistance")]
    [SerializeField, Min(0f)] private float repositionDistance = 1.0f;
    [SerializeField, Range(0.1f, 1f)] private float moveSpeedMultiplier = 0.8f;
    [FormerlySerializedAs("moveTimeoutDuration")]
    [SerializeField, Min(0.5f)] private float repositionTimeoutDuration = 2.5f;
    [SerializeField] private bool stepTowardsTarget = true;
    [SerializeField] private LayerMask terrainLayers = 1 << 6;

    [Header("References")]
    [SerializeField] private GroundedActorMotor2D motor;
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D actorCollider;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private GameObject axeProjectilePrefab;
    [SerializeField] private Level01CameraController cameraController;
    [SerializeField] private Collider2D levelBoundsCollider;
    [SerializeField] private AchillesProjectileDebugLauncher playerLauncher;
    [SerializeField] private Level01TurnManager turnManager;

    private static readonly int WalkSpeedParamHash = Animator.StringToHash("WalkSpeed");

    private TurnPhase currentPhase = TurnPhase.Idle;
    private int throwTriggerHash;
    private int throwStateHash;
    private float throwTimer;
    private float repositionTimer;
    private float postHitTimer;
    private float cameraFocusTimer;
    private float postMoveTimer;
    private float startPosX;
    private float targetMoveDistance;
    private int currentMoveDir;
    private bool isAxeSpawned;
    private bool turnCompletedFired;
    private Projectile2D activeProjectile;

    public event Action TurnStarted;
    public event Action ThrowStarted;
    public event Action ProjectileLaunched;
    public event Action TurnCompleted;

    public bool IsTurnActive => currentPhase != TurnPhase.Idle && currentPhase != TurnPhase.Completed;
    public int FacingDirection => motor != null ? motor.FacingDirection : (transform.localScale.x < 0 ? -1 : 1);
    public string EnemyId => !string.IsNullOrEmpty(enemyId) ? enemyId : "CyclopsNormal";

    public float RepositionDistance
    {
        get => repositionDistance;
        set => repositionDistance = Mathf.Max(0f, value);
    }

    public float WalkAnimationSpeedMultiplier
    {
        get => walkAnimationSpeedMultiplier;
        set => walkAnimationSpeedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
    }

    public float PostHitHoldDuration
    {
        get => postHitHoldDuration;
        set => postHitHoldDuration = Mathf.Max(0f, value);
    }

    public float CameraFocusDelayBeforeMove
    {
        get => cameraFocusDelayBeforeMove;
        set => cameraFocusDelayBeforeMove = Mathf.Max(0f, value);
    }

    public float PostMoveSettleDuration
    {
        get => postMoveSettleDuration;
        set => postMoveSettleDuration = Mathf.Max(0f, value);
    }

    public float Accuracy
    {
        get => accuracy;
        set => accuracy = Mathf.Clamp01(value);
    }

    public float MinThrowAngle
    {
        get => minThrowAngle;
        set => minThrowAngle = Mathf.Clamp(value, 15f, 80f);
    }

    public float MaxThrowAngle
    {
        get => maxThrowAngle;
        set => maxThrowAngle = Mathf.Clamp(value, minThrowAngle, 85f);
    }

    public float PreferredAngle
    {
        get => preferredAngle;
        set => preferredAngle = Mathf.Clamp(value, minThrowAngle, maxThrowAngle);
    }

    public float MinLaunchSpeed
    {
        get => minLaunchSpeed;
        set => minLaunchSpeed = Mathf.Max(1f, value);
    }

    public float MaxLaunchSpeed
    {
        get => maxLaunchSpeed;
        set => maxLaunchSpeed = Mathf.Max(minLaunchSpeed, value);
    }

    private void Awake()
    {
        InitializeReferences();
    }

    private void OnValidate()
    {
        repositionDistance = Mathf.Max(0f, repositionDistance);
        repositionTimeoutDuration = Mathf.Max(0.5f, repositionTimeoutDuration);
        walkAnimationSpeedMultiplier = Mathf.Clamp(walkAnimationSpeedMultiplier, 0.1f, 2f);
        postHitHoldDuration = Mathf.Max(0f, postHitHoldDuration);
        cameraFocusDelayBeforeMove = Mathf.Max(0f, cameraFocusDelayBeforeMove);
        postMoveSettleDuration = Mathf.Max(0f, postMoveSettleDuration);
        throwAngle = Mathf.Clamp(throwAngle, 10f, 80f);
        launchSpeed = Mathf.Max(1f, launchSpeed);
        releaseNormalizedTime = Mathf.Clamp(releaseNormalizedTime, 0.1f, 0.9f);
        throwTimeoutDuration = Mathf.Max(0.5f, throwTimeoutDuration);
        throwTriggerHash = Animator.StringToHash(throwTriggerName);
        throwStateHash = Animator.StringToHash(throwStateName);

        accuracy = Mathf.Clamp01(accuracy);
        minThrowAngle = Mathf.Clamp(minThrowAngle, 15f, 80f);
        maxThrowAngle = Mathf.Clamp(maxThrowAngle, minThrowAngle, 85f);
        preferredAngle = Mathf.Clamp(preferredAngle, minThrowAngle, maxThrowAngle);
        minLaunchSpeed = Mathf.Max(1f, minLaunchSpeed);
        maxLaunchSpeed = Mathf.Max(minLaunchSpeed, maxLaunchSpeed);
        minMissDistance = Mathf.Max(0f, minMissDistance);
        maxMissDistance = Mathf.Max(minMissDistance, maxMissDistance);
        candidateTrajectorySamples = Mathf.Clamp(candidateTrajectorySamples, 3, 20);
    }

    public void Configure(
        GroundedActorMotor2D motorRef,
        Animator animatorRef,
        Collider2D colliderRef,
        Transform targetRef,
        GameObject axePrefab,
        Level01CameraController cameraRef,
        Collider2D boundsRef,
        AchillesProjectileDebugLauncher launcherRef = null,
        Transform spawnPointRef = null,
        Level01TurnManager turnManagerRef = null)
    {
        motor = motorRef;
        animator = animatorRef;
        actorCollider = colliderRef;
        targetTransform = targetRef;
        axeProjectilePrefab = axePrefab;
        cameraController = cameraRef;
        levelBoundsCollider = boundsRef;
        playerLauncher = launcherRef;
        throwSpawnPoint = spawnPointRef;
        if (turnManagerRef != null)
            turnManager = turnManagerRef;
        InitializeReferences();
    }

    public void SetTarget(Transform target, AchillesProjectileDebugLauncher launcher = null)
    {
        targetTransform = target;
        playerLauncher = launcher != null ? launcher : (target != null ? target.GetComponent<AchillesProjectileDebugLauncher>() : null);
    }

    private void InitializeReferences()
    {
        if (motor == null)
            motor = GetComponent<GroundedActorMotor2D>();
        if (motor != null)
            motor.SpriteFacesLeftByDefault = true;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (actorCollider == null)
            actorCollider = GetComponent<Collider2D>();
        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<Level01CameraController>();
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            var activeLauncher = FindAnyObjectByType<AchillesProjectileDebugLauncher>();
            if (activeLauncher != null)
                targetTransform = activeLauncher.transform;
            else
            {
                GameObject playerObj = GameObject.Find("Player_Achilles");
                if (playerObj != null)
                    targetTransform = playerObj.transform;
            }
        }
        if ((playerLauncher == null || !playerLauncher.gameObject.activeInHierarchy) && targetTransform != null)
        {
            playerLauncher = targetTransform.GetComponent<AchillesProjectileDebugLauncher>();
        }
        if (throwSpawnPoint == null)
        {
            Transform child = transform.Find("ProjectileSpawnPoint");
            if (child != null)
                throwSpawnPoint = child;
        }
        if (turnManager == null)
        {
            turnManager = FindAnyObjectByType<Level01TurnManager>();
        }

        throwTriggerHash = Animator.StringToHash(throwTriggerName);
        throwStateHash = Animator.StringToHash(throwStateName);
        ApplyWalkAnimationSpeed();
    }

    private void ApplyWalkAnimationSpeed()
    {
        if (animator == null)
            return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.nameHash == WalkSpeedParamHash)
            {
                animator.SetFloat(WalkSpeedParamHash, walkAnimationSpeedMultiplier);
                break;
            }
        }
    }

    /// <summary>
    /// Begins the Cyclops turn sequence: stands in place, faces Achilles to the left,
    /// triggers the Throw animation, and launches exactly one stone axe toward Achilles.
    /// Does NOT move before throwing.
    /// </summary>
    public void BeginTurn()
    {
        if (IsTurnActive)
            return;

        InitializeReferences();
        turnCompletedFired = false;
        isAxeSpawned = false;
        activeProjectile = null;
        throwTimer = 0f;
        repositionTimer = 0f;
        postHitTimer = 0f;
        cameraFocusTimer = 0f;
        postMoveTimer = 0f;
        targetMoveDistance = repositionDistance;

        TurnStarted?.Invoke();

        if (targetTransform == null)
        {
            FailSafeComplete("Target transform (Achilles) is missing.");
            return;
        }

        if (axeProjectilePrefab == null)
        {
            FailSafeComplete("Axe projectile prefab is not assigned.");
            return;
        }

        // Stand in place: halt motor movement
        if (motor != null)
        {
            motor.SetMoveInput(0f);
            motor.SetMovementEnabled(true);
        }

        // Face Achilles to the LEFT
        if (targetTransform != null && motor != null)
        {
            int facingAchilles = targetTransform.position.x < transform.position.x ? -1 : 1;
            motor.SetFacing(facingAchilles);
        }

        ApplyWalkAnimationSpeed();

        // Start Throw sequence directly without prior movement
        currentPhase = TurnPhase.Throwing;
        ThrowStarted?.Invoke();

        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.SetTrigger(throwTriggerHash);
        }
        else
        {
            // If Animator missing or disabled, spawn axe directly
            SpawnAndLaunchAxe();
        }
    }

    private void Update()
    {
        if (turnManager != null && turnManager.IsBattleOver)
        {
            if (motor != null)
                motor.SetMoveInput(0f);
            return;
        }

        switch (currentPhase)
        {
            case TurnPhase.Throwing:
                UpdateThrowingPhase();
                break;

            case TurnPhase.AxeInFlight:
                // If active projectile was unexpectedly destroyed without callback
                if (activeProjectile == null && !turnCompletedFired)
                {
                    OnAxeCompleted(null);
                }
                break;

            case TurnPhase.PostHitPause:
                UpdatePostHitPause();
                break;

            case TurnPhase.CameraFocusPause:
                UpdateCameraFocusPause();
                break;

            case TurnPhase.Repositioning:
                UpdateRepositioningPhase();
                break;

            case TurnPhase.PostMoveSettle:
                UpdatePostMoveSettle();
                break;
        }
    }

    private void UpdateThrowingPhase()
    {
        throwTimer += Time.deltaTime;

        if (!isAxeSpawned && animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool inThrowState = stateInfo.shortNameHash == throwStateHash || stateInfo.IsName(throwStateName);

            if (inThrowState && stateInfo.normalizedTime >= releaseNormalizedTime)
            {
                isAxeSpawned = true;
                SpawnAndLaunchAxe();
            }
        }

        // Fail-safe timeout check
        if (throwTimer >= throwTimeoutDuration)
        {
            if (!isAxeSpawned)
            {
                isAxeSpawned = true;
                SpawnAndLaunchAxe();
            }
            else if (activeProjectile == null)
            {
                FailSafeComplete("Throw sequence timed out without active projectile.");
            }
        }
    }

    private struct TrajectoryCandidate
    {
        public float angle;
        public float speed;
        public Vector2 velocity;
        public float flightTime;
        public bool isClear;
        public float clearDistance;
    }

    private void SpawnAndLaunchAxe()
    {
        if (axeProjectilePrefab == null)
        {
            FailSafeComplete("Cannot spawn axe: Prefab is null.");
            return;
        }

        int facing = FacingDirection;
        Vector3 spawnWorldPos = ComputeSpawnPosition(facing);
        float wind = playerLauncher != null ? playerLauncher.CurrentWind : 0f;

        Vector2 initialVelocity = CalculateAimVelocity(spawnWorldPos, facing, wind, out bool isAccurateShot);

        GameObject axeObject = Instantiate(axeProjectilePrefab, spawnWorldPos, Quaternion.identity);
        Projectile2D projectile = axeObject.GetComponent<Projectile2D>();

        if (projectile == null)
        {
            FailSafeComplete("Spawned axe object has no Projectile2D component.");
            return;
        }

        activeProjectile = projectile;
        currentPhase = TurnPhase.AxeInFlight;

        // Camera follow begins immediately when axe is created
        if (cameraController != null)
        {
            cameraController.FollowProjectile(projectile.transform);
        }

        ProjectileLaunched?.Invoke();

        Collider2D ownerCol = actorCollider != null ? actorCollider : GetComponent<Collider2D>();

        projectile.Launch(
            initialVelocity,
            ownerCol,
            wind,
            levelBoundsCollider,
            null,
            OnAxeCompleted);

        Debug.Log($"[Cyclops Normal] Threw Stone Axe - Angle: {throwAngle:F1}°, Speed: {launchSpeed:F1}, Wind: {wind:F2}, AimAccurate: {isAccurateShot}");
    }

    private Vector2 CalculateAimVelocity(Vector3 spawnWorldPos, int facing, float wind, out bool isAccurateShot)
    {
        isAccurateShot = true;
        if (targetTransform == null)
        {
            float rad = throwAngle * Mathf.Deg2Rad;
            return new Vector2(facing * Mathf.Cos(rad), Mathf.Sin(rad)).normalized * launchSpeed;
        }

        // Determine target position with vertical offset (e.g. Achilles' torso)
        Vector3 targetPos = targetTransform.position + Vector3.up * targetVerticalOffset;

        // Configurable aiming error: determine whether this shot is an intentional miss
        isAccurateShot = UnityEngine.Random.value <= accuracy;
        if (!isAccurateShot)
        {
            float missSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float missDist = UnityEngine.Random.Range(minMissDistance, maxMissDistance);
            targetPos.x += missSign * missDist;

            if (levelBoundsCollider != null)
            {
                Bounds bounds = levelBoundsCollider.bounds;
                targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x + 1f, bounds.max.x - 1f);
            }
        }

        float deltaX = targetPos.x - spawnWorldPos.x;
        float deltaY = targetPos.y - spawnWorldPos.y;
        float absDeltaX = Mathf.Abs(deltaX);

        // Direction towards target (aligned with facing direction)
        int dirToTarget = deltaX < 0 ? -1 : 1;

        float gravityY = Physics2D.gravity.y; // Standard -9.81 m/s^2

        List<TrajectoryCandidate> candidates = new List<TrajectoryCandidate>(candidateTrajectorySamples);

        int sampleCount = Mathf.Max(3, candidateTrajectorySamples);
        float angleStep = (maxThrowAngle - minThrowAngle) / (sampleCount - 1);

        for (int i = 0; i < sampleCount; i++)
        {
            float candAngle = minThrowAngle + i * angleStep;
            float candRad = candAngle * Mathf.Deg2Rad;
            float tanTheta = Mathf.Tan(candRad);

            // Kinematic denominator: g - w * dirToTarget * tan(theta)
            float denom = gravityY - wind * dirToTarget * tanTheta;
            if (denom >= -0.1f)
            {
                // Extreme headwind blowing projectile backwards
                continue;
            }

            // Numerator: 2 * (deltaY - |deltaX| * tan(theta))
            float num = 2f * (deltaY - absDeltaX * tanTheta);
            float t2 = num / denom;

            if (t2 <= 0.05f)
            {
                continue;
            }

            float flightTime = Mathf.Sqrt(t2);
            if (flightTime < 0.2f || flightTime > 6.0f)
            {
                continue;
            }

            float v0x = (deltaX - 0.5f * wind * t2) / flightTime;
            float v0y = (deltaY - 0.5f * gravityY * t2) / flightTime;
            float candSpeed = Mathf.Sqrt(v0x * v0x + v0y * v0y);

            if (candSpeed < minLaunchSpeed || candSpeed > maxLaunchSpeed)
            {
                continue;
            }

            Vector2 candVel = new Vector2(v0x, v0y);
            bool isClear = CheckTrajectoryClearance(spawnWorldPos, candVel, wind, flightTime, out float clearDist);

            candidates.Add(new TrajectoryCandidate
            {
                angle = candAngle,
                speed = candSpeed,
                velocity = candVel,
                flightTime = flightTime,
                isClear = isClear,
                clearDistance = clearDist
            });
        }

        Vector2 selectedVel;

        // Filter clear candidates
        List<TrajectoryCandidate> clearCandidates = candidates.FindAll(c => c.isClear);

        if (clearCandidates.Count > 0)
        {
            // Prefer candidates closest to preferredAngle (typically ~45-50°)
            clearCandidates.Sort((a, b) =>
                Mathf.Abs(a.angle - preferredAngle).CompareTo(Mathf.Abs(b.angle - preferredAngle)));

            // Take the best candidate (or small chance to pick runner-up if angles are close)
            TrajectoryCandidate chosen = clearCandidates[0];
            if (clearCandidates.Count > 1 &&
                Mathf.Abs(clearCandidates[1].angle - preferredAngle) - Mathf.Abs(clearCandidates[0].angle - preferredAngle) < 8f &&
                UnityEngine.Random.value < 0.35f)
            {
                chosen = clearCandidates[1];
            }

            selectedVel = chosen.velocity;
        }
        else if (candidates.Count > 0)
        {
            // If terrain blocks all candidates, prefer the one with the greatest clearance distance
            candidates.Sort((a, b) => b.clearDistance.CompareTo(a.clearDistance));
            selectedVel = candidates[0].velocity;
        }
        else
        {
            // Safe fallback lofted shot if no candidate met strict speed criteria
            float fallbackAngle = preferredAngle;
            float fallbackRad = fallbackAngle * Mathf.Deg2Rad;
            float fallbackSpeed = Mathf.Clamp(launchSpeed, minLaunchSpeed, maxLaunchSpeed);
            selectedVel = new Vector2(dirToTarget * Mathf.Cos(fallbackRad) * fallbackSpeed, Mathf.Sin(fallbackRad) * fallbackSpeed);
        }

        // Apply slight organic jitter to angle and speed
        if (angleJitterDegrees > 0f || speedJitterFraction > 0f)
        {
            float curAngle = Mathf.Atan2(selectedVel.y, Mathf.Abs(selectedVel.x)) * Mathf.Rad2Deg;
            float curSpeed = selectedVel.magnitude;

            float jitteredAngle = Mathf.Clamp(curAngle + UnityEngine.Random.Range(-angleJitterDegrees, angleJitterDegrees), minThrowAngle, maxThrowAngle);
            float jitteredSpeed = Mathf.Clamp(curSpeed * (1f + UnityEngine.Random.Range(-speedJitterFraction, speedJitterFraction)), minLaunchSpeed, maxLaunchSpeed);

            float rad = jitteredAngle * Mathf.Deg2Rad;
            selectedVel = new Vector2(dirToTarget * Mathf.Cos(rad) * jitteredSpeed, Mathf.Sin(rad) * jitteredSpeed);
        }

        // Update inspector/runtime tracking
        throwAngle = Mathf.Atan2(selectedVel.y, Mathf.Abs(selectedVel.x)) * Mathf.Rad2Deg;
        launchSpeed = selectedVel.magnitude;

        return selectedVel;
    }

    private bool CheckTrajectoryClearance(Vector2 origin, Vector2 velocity, float wind, float flightTime, out float clearDistance)
    {
        clearDistance = float.MaxValue;
        const int segments = 10;
        float timeStep = (flightTime * 0.95f) / segments;
        Vector2 prevPoint = origin;
        float gravityY = Physics2D.gravity.y;

        for (int i = 1; i <= segments; i++)
        {
            float t = i * timeStep;
            Vector2 currentPoint = origin + new Vector2(
                velocity.x * t + 0.5f * wind * t * t,
                velocity.y * t + 0.5f * gravityY * t * t
            );

            RaycastHit2D hit = Physics2D.Linecast(prevPoint, currentPoint, terrainLayers);
            if (hit && hit.collider != null)
            {
                clearDistance = Vector2.Distance(origin, hit.point);
                return false;
            }

            prevPoint = currentPoint;
        }

        return true;
    }

    private void OnAxeCompleted(Projectile2D projectile)
    {
        if (turnCompletedFired)
            return;

        activeProjectile = null;

        if (turnManager != null && turnManager.IsBattleOver)
        {
            currentPhase = TurnPhase.Completed;
            return;
        }

        // Keep camera focused on target (Achilles) so the player's hurt animation and HP reduction are clearly seen
        if (cameraController != null && targetTransform != null)
        {
            cameraController.FocusActor(targetTransform);
        }

        if (postHitHoldDuration > 0f)
        {
            postHitTimer = 0f;
            currentPhase = TurnPhase.PostHitPause;
        }
        else
        {
            TriggerRefocusOnCyclops();
        }
    }

    private void UpdatePostHitPause()
    {
        postHitTimer += Time.deltaTime;

        if (turnManager != null && turnManager.IsBattleOver)
        {
            currentPhase = TurnPhase.Completed;
            return;
        }

        if (postHitTimer >= postHitHoldDuration)
        {
            TriggerRefocusOnCyclops();
        }
    }

    private void TriggerRefocusOnCyclops()
    {
        if (turnManager != null && turnManager.IsBattleOver)
        {
            currentPhase = TurnPhase.Completed;
            return;
        }

        // Refocus camera on Cyclops
        if (cameraController != null)
        {
            cameraController.FocusActor(transform);
        }

        if (cameraFocusDelayBeforeMove > 0f)
        {
            cameraFocusTimer = 0f;
            currentPhase = TurnPhase.CameraFocusPause;
        }
        else
        {
            BeginRepositioning();
        }
    }

    private void UpdateCameraFocusPause()
    {
        cameraFocusTimer += Time.deltaTime;

        if (turnManager != null && turnManager.IsBattleOver)
        {
            currentPhase = TurnPhase.Completed;
            return;
        }

        if (cameraFocusTimer >= cameraFocusDelayBeforeMove)
        {
            BeginRepositioning();
        }
    }

    private void BeginRepositioning()
    {
        if (turnCompletedFired)
            return;

        startPosX = transform.position.x;
        int chosenDir = 0;
        targetMoveDistance = repositionDistance;

        if (repositionDistance > 0.05f)
        {
            // Determine direction toward target (Achilles is to the left: -1)
            int preferredDir = stepTowardsTarget
                ? (targetTransform != null && targetTransform.position.x < startPosX ? -1 : 1)
                : (targetTransform != null && targetTransform.position.x < startPosX ? 1 : -1);

            // Attempt full reposition distance towards preferred direction
            if (IsGroundReachable(startPosX, preferredDir, repositionDistance))
            {
                chosenDir = preferredDir;
                targetMoveDistance = repositionDistance;
            }
            // If full distance blocked, attempt a shorter half-step towards preferred direction
            else if (repositionDistance > 0.4f && IsGroundReachable(startPosX, preferredDir, repositionDistance * 0.5f))
            {
                chosenDir = preferredDir;
                targetMoveDistance = repositionDistance * 0.5f;
            }
            // If blocked ahead towards Achilles, stay in place. Do NOT reverse direction to backtrack!
        }

        if (chosenDir == 0)
        {
            Debug.Log("[Cyclops Normal] Destination blocked ahead or no safe ground. Staying at current position.");
            if (postMoveSettleDuration > 0f)
            {
                postMoveTimer = 0f;
                currentPhase = TurnPhase.PostMoveSettle;
            }
            else
            {
                CompleteTurn();
            }
            return;
        }

        currentMoveDir = chosenDir;
        repositionTimer = 0f;
        currentPhase = TurnPhase.Repositioning;

        if (motor != null)
        {
            motor.SetMovementEnabled(true);
            motor.SetFacing(currentMoveDir);
            motor.SetMoveInput(currentMoveDir * moveSpeedMultiplier);
        }
    }

    private void UpdateRepositioningPhase()
    {
        repositionTimer += Time.deltaTime;
        float travelled = Mathf.Abs(transform.position.x - startPosX);

        bool reachedDistance = travelled >= targetMoveDistance;
        bool timedOut = repositionTimer >= repositionTimeoutDuration;
        bool edgeDetected = !HasGroundAhead(currentMoveDir);

        if (reachedDistance || timedOut || edgeDetected)
        {
            if (motor != null)
            {
                motor.SetMoveInput(0f);
            }

            if (postMoveSettleDuration > 0f)
            {
                postMoveTimer = 0f;
                currentPhase = TurnPhase.PostMoveSettle;
            }
            else
            {
                CompleteTurn();
            }
        }
        else if (motor != null)
        {
            motor.SetMoveInput(currentMoveDir * moveSpeedMultiplier);
        }
    }

    private void UpdatePostMoveSettle()
    {
        postMoveTimer += Time.deltaTime;

        if (postMoveTimer >= postMoveSettleDuration)
        {
            CompleteTurn();
        }
    }

    private void CompleteTurn()
    {
        if (turnCompletedFired)
            return;

        turnCompletedFired = true;
        currentPhase = TurnPhase.Completed;

        if (motor != null)
        {
            motor.SetMoveInput(0f);
            if (targetTransform != null)
            {
                int facingAchilles = targetTransform.position.x < transform.position.x ? -1 : 1;
                motor.SetFacing(facingAchilles);
            }
        }

        // Update fall recovery spawn pose to current reached position so it never teleports back
        var fallRecovery = GetComponent<ActorFallRecovery2D>();
        if (fallRecovery != null)
        {
            fallRecovery.RecordSpawnPose();
        }

        // Return camera to Achilles
        if (cameraController != null && targetTransform != null)
        {
            cameraController.FocusActor(targetTransform);
        }

        // Refresh wind for player's upcoming turn
        if (playerLauncher != null)
        {
            playerLauncher.GenerateNextWind();
        }

        TurnCompleted?.Invoke();
    }

    private void FailSafeComplete(string reason)
    {
        Debug.LogWarning($"[CyclopsNormalTurnController] {reason} Finishing turn safely.");
        CompleteTurn();
    }

    private Vector3 ComputeSpawnPosition(int facing)
    {
        Vector3 baseOffset = throwSpawnPoint != null ? throwSpawnPoint.localPosition : fallbackSpawnOffset;
        float horizontalOffset = Mathf.Abs(baseOffset.x) * (facing < 0 ? -1f : 1f);
        Vector3 mirroredLocalOffset = new Vector3(horizontalOffset, baseOffset.y, baseOffset.z);
        return transform.TransformPoint(mirroredLocalOffset);
    }

    private bool IsGroundReachable(float startX, int direction, float distance)
    {
        int samples = Mathf.Max(2, Mathf.CeilToInt(distance / 0.5f));
        float step = distance / samples;
        float currentY = transform.position.y;

        for (int i = 1; i <= samples; i++)
        {
            float checkX = startX + direction * (step * i);
            Vector2 origin = new Vector2(checkX, currentY + 1.5f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 4.5f, terrainLayers);
            if (!hit || hit.collider == null || hit.normal.y < 0.5f)
            {
                return false;
            }

            // Reject cliffs or walls higher than step height
            if (Mathf.Abs(hit.point.y - currentY) > 1.8f)
            {
                return false;
            }
        }
        return true;
    }

    private bool HasGroundAhead(int direction)
    {
        Vector2 origin = new Vector2(transform.position.x + direction * 0.4f, transform.position.y + 1.5f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 3.5f, terrainLayers);
        return hit && hit.collider != null && hit.normal.y >= 0.5f;
    }

    private void OnDisable()
    {
        if (IsTurnActive && !turnCompletedFired)
        {
            FailSafeComplete("Turn interrupted by component disable.");
        }
    }

    private void OnDestroy()
    {
        if (IsTurnActive && !turnCompletedFired)
        {
            FailSafeComplete("Turn interrupted by component destroy.");
        }
    }
}
