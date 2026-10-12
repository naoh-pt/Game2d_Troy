using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable horizontal physics motor for actors standing on Terrain-layer colliders.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class GroundedActorMotor2D : MonoBehaviour
{
    private const int ContactCapacity = 16;
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");

    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float maximumSpeed = 3f;
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [SerializeField, Min(0f)] private float deceleration = 24f;
    [SerializeField] private bool movementEnabled = true;
    [Tooltip("True if source sprite artwork natively faces left when un-flipped (e.g. Cyclops). False if it natively faces right (e.g. Achilles).")]
    [SerializeField] private bool spriteFacesLeftByDefault;

    [Header("Locomotion Animation (Actual Displacement)")]
    [SerializeField, Min(0.001f)] private float runEnterSpeed = 0.15f;
    [SerializeField, Min(0f)] private float runExitSpeed = 0.05f;
    [Tooltip("Seconds of near-zero actual travel before returning to Idle. Does not delay entering Run.")]
    [SerializeField, Min(0f)] private float idleGraceDuration = 0.08f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask terrainLayers = 1 << 6;
    [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.2f;
    [SerializeField, Range(0f, 89f)] private float maximumWalkableSlopeAngle = 60f;
    [SerializeField, Range(0.01f, 1f)] private float minimumGroundNormalY = 0.5f;
    [Tooltip("Maximum height difference treated as a contour stair step rather than a wall. Set above the terrain's 4-pixel sample height.")]
    [SerializeField, Min(0f)] private float maximumStepHeight = 0.35f;
    [Tooltip("Horizontal distance used to sample the terrain profile ahead of the actor.")]
    [SerializeField, Min(0.05f)] private float slopeProbeDistance = 0.45f;
    [Tooltip("Counteracts gravity along walkable slopes and removes tangent velocity when stationary.")]
    [SerializeField] private bool idleAntiSlide = true;

    [Header("Debug Gizmos")]
    [SerializeField] private bool showGroundDebugGizmos;
    [SerializeField, Min(0.1f)] private float debugVectorLength = 0.8f;

    [Header("Optional Movement Diagnostics")]
    [SerializeField] private bool movementDiagnosticsEnabled;
    [SerializeField, Min(0.1f)] private float diagnosticInterval = 0.5f;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[ContactCapacity];
    private readonly RaycastHit2D[] stepSurfaceHits = new RaycastHit2D[ContactCapacity];
    private readonly List<RaycastHit2D> profileHits = new List<RaycastHit2D>(ContactCapacity);
    private readonly ContactPoint2D[] contactPoints = new ContactPoint2D[ContactCapacity];
    private ContactFilter2D terrainFilter;
    private float moveInput;
    private int facingDirection = 1;
    private bool hasSpeedParameter;
    private bool initialized;
    private Vector2 groundPoint;
    private Vector2 groundNormal = Vector2.up;
    private Vector2 groundTangent = Vector2.right;
    private Vector2 debugBlockStart;
    private Vector2 debugBlockEnd;
    private bool hasGroundPoint;
    private bool hasBlockCast;
    private bool forwardBlocked;
    private bool hasGroundContact;
    private bool stepAssist;
    private bool profileWalkable;
    private float profileRise;
    private float stepRise;
    private float acceptedHitFraction = -1f;
    private Vector2 acceptedHitNormal;
    private Collider2D acceptedHitCollider;
    private Vector2 requestedVelocity;
    private Vector2 previousPosition;
    private bool positionRecorded;
    private float nextDiagnosticTime;
    private int terrainContactCount;
    private float minimumContactSeparation;
    private float maximumContactFriction;
    private bool locomotionRunning;
    private float stationaryDuration;
    private float measuredHorizontalSpeed;
    private float animationSpeed;

    public bool IsGrounded { get; private set; }
    public int FacingDirection => facingDirection;
    public bool SpriteFacesLeftByDefault
    {
        get => spriteFacesLeftByDefault;
        set => spriteFacesLeftByDefault = value;
    }
    public float EffectiveStepHeight
    {
        get
        {
            if (bodyCollider != null)
            {
                float halfHeight = bodyCollider.bounds.size.y * 0.5f;
                return Mathf.Max(maximumStepHeight, halfHeight);
            }
            return maximumStepHeight;
        }
    }

    private void Awake() => Initialize();
    private void OnEnable() => Initialize();

    private void OnDisable()
    {
        moveInput = 0f;
        positionRecorded = false;
        locomotionRunning = false;
        stationaryDuration = 0f;
        measuredHorizontalSpeed = 0f;
        animationSpeed = 0f;
        stepRise = 0f;
        if (animator != null && hasSpeedParameter)
            animator.SetFloat(SpeedParameter, 0f);
    }

    private void OnValidate()
    {
        maximumSpeed = Mathf.Max(0f, maximumSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        runEnterSpeed = Mathf.Max(0.001f, runEnterSpeed);
        runExitSpeed = Mathf.Clamp(runExitSpeed, 0f, runEnterSpeed * 0.9f);
        idleGraceDuration = Mathf.Max(0f, idleGraceDuration);
        groundCheckDistance = Mathf.Max(0.001f, groundCheckDistance);
        maximumWalkableSlopeAngle = Mathf.Clamp(maximumWalkableSlopeAngle, 0f, 89f);
        minimumGroundNormalY = Mathf.Clamp(minimumGroundNormalY, 0.01f, 1f);
        maximumStepHeight = Mathf.Max(0f, maximumStepHeight);
        slopeProbeDistance = Mathf.Max(0.05f, slopeProbeDistance);
        debugVectorLength = Mathf.Max(0.1f, debugVectorLength);
        diagnosticInterval = Mathf.Max(0.1f, diagnosticInterval);
    }

    public void SetMoveInput(float input)
    {
        moveInput = movementEnabled ? Mathf.Clamp(input, -1f, 1f) : 0f;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
            moveInput = 0f;
    }

    /// <summary>Repair-menu tuning; retains speed, acceleration, visuals and collider geometry.</summary>
    public void ConfigureCollisionMovement(LayerMask terrainMask)
    {
        terrainLayers = terrainMask;
        groundCheckDistance = 0.2f;
        maximumStepHeight = 0.35f;
        slopeProbeDistance = 0.45f;
        idleAntiSlide = true;
        initialized = false;
    }

    /// <summary>Editor setup entry point; preserves all collider and visual transforms.</summary>
    public void Configure(
        Rigidbody2D bodyReference,
        Collider2D colliderReference,
        SpriteRenderer visualReference,
        Animator animatorReference,
        LayerMask terrainMask,
        float speed,
        float accelerationRate,
        float decelerationRate,
        bool applyMovementDefaults)
    {
        body = bodyReference;
        bodyCollider = colliderReference;
        visualRenderer = visualReference;
        animator = animatorReference;
        terrainLayers = terrainMask;
        if (applyMovementDefaults)
        {
            maximumSpeed = Mathf.Max(0f, speed);
            acceleration = Mathf.Max(0f, accelerationRate);
            deceleration = Mathf.Max(0f, decelerationRate);
        }
        initialized = false;
    }

    private void FixedUpdate()
    {
        if (!initialized)
            Initialize();
        if (body == null || bodyCollider == null)
            return;

        Vector2 observedVelocity = body.linearVelocity;
        Vector2 displacementVelocity = positionRecorded ? (body.position - previousPosition) / Time.fixedDeltaTime : Vector2.zero;
        // Sample completed physics travel BEFORE assigning this tick's requested velocity.
        // Spawn/fall-recovery teleports must not look like locomotion.
        bool discontinuity = positionRecorded && (body.position - previousPosition).sqrMagnitude >
            Mathf.Pow(Mathf.Max(0.5f, maximumSpeed * Time.fixedDeltaTime * 4f), 2f);
        float input = movementEnabled ? moveInput : 0f;
        UpdateLocomotionAnimation(discontinuity ? Vector2.zero : displacementVelocity, discontinuity, input);
        previousPosition = body.position;
        positionRecorded = true;
        acceptedHitFraction = -1f;
        acceptedHitNormal = Vector2.zero;
        acceptedHitCollider = null;
        UpdateGroundedState();
        int direction = input > 0.001f ? 1 : input < -0.001f ? -1 : 0;
        stepAssist = false;
        stepRise = 0f;
        profileRise = 0f;
        profileWalkable = direction != 0 && IsGrounded && TryBuildGroundProfile(direction);
        hasBlockCast = false;
        forwardBlocked = direction != 0 && IsBlockedBySteepTerrain(direction);
        requestedVelocity = new Vector2(0f, observedVelocity.y);

        if (direction != 0)
        {
            SetFacing(direction);
            if (!forwardBlocked)
            {
                Vector2 tangent = IsGrounded ? GetWalkableTangent(direction) : Vector2.right * direction;
                requestedVelocity.x = tangent.x * Mathf.Abs(input) * maximumSpeed;
                body.linearVelocityX = Mathf.MoveTowards(observedVelocity.x, requestedVelocity.x, acceleration * Time.fixedDeltaTime);
                if (IsGrounded && (hasGroundContact || observedVelocity.y >= -0.5f) && (tangent.y > 0.001f || stepAssist))
                {
                    float climbSpeed = Mathf.Max(0f, tangent.y * Mathf.Abs(input) * maximumSpeed);
                    if (stepAssist)
                    {
                        float effectiveRise = Mathf.Max(profileRise, stepRise);
                        climbSpeed = Mathf.Max(climbSpeed, (Mathf.Max(0f, effectiveRise) + 0.02f) * Mathf.Abs(input) * maximumSpeed / slopeProbeDistance);
                    }
                    climbSpeed = Mathf.Max(climbSpeed, -groundNormal.x * body.linearVelocityX / groundNormal.y);
                    requestedVelocity.y = Mathf.Max(observedVelocity.y, climbSpeed);
                    // Never command a downward tangent into a tread. Downhill travel and falling use gravity.
                    body.linearVelocityY = Mathf.Max(observedVelocity.y, climbSpeed);
                }
            }
            else
            {
                body.linearVelocityX = Mathf.MoveTowards(
                    body.linearVelocityX,
                    0f,
                    deceleration * Time.fixedDeltaTime);
            }
        }
        else
        {
            if (IsGrounded && idleAntiSlide)
            {
                // When grounded and movement input is zero, remove velocity along the ground tangent.
                Vector2 tangentVelocity = body.linearVelocity - Vector2.Dot(body.linearVelocity, groundNormal) * groundNormal;
                body.linearVelocity -= tangentVelocity;

                // Counteract only the component of gravity that would slide the actor along the accepted walkable ground.
                Vector2 gravity = Physics2D.gravity * body.gravityScale;
                Vector2 gravityNormal = Vector2.Dot(gravity, groundNormal) * groundNormal;
                Vector2 gravityTangent = gravity - gravityNormal;
                body.AddForce(-gravityTangent * body.mass, ForceMode2D.Force);

                requestedVelocity = Vector2.zero;
            }
            else
            {
                // Airborne/no-input horizontal control never changes gravity-driven vertical velocity.
                body.linearVelocityX = Mathf.MoveTowards(
                    body.linearVelocityX,
                    0f,
                    deceleration * Time.fixedDeltaTime);
            }
        }

        LogMovementDiagnostics(input, observedVelocity, displacementVelocity);
    }

    private void UpdateLocomotionAnimation(Vector2 completedTravelVelocity, bool discontinuity, float input)
    {
        measuredHorizontalSpeed = Mathf.Abs(completedTravelVelocity.x);
        bool hasInput = Mathf.Abs(input) > 0.001f;

        if (discontinuity || !hasInput)
        {
            locomotionRunning = false;
            stationaryDuration = 0f;
        }
        else if (!locomotionRunning)
        {
            if (measuredHorizontalSpeed >= runEnterSpeed)
            {
                locomotionRunning = true;
                stationaryDuration = 0f;
            }
        }
        else if (measuredHorizontalSpeed <= runExitSpeed)
        {
            stationaryDuration += Time.fixedDeltaTime;
            if (stationaryDuration >= idleGraceDuration)
                locomotionRunning = false;
        }
        else
        {
            stationaryDuration = 0f;
        }

        // A latched Run emits a nonzero measured-speed floor during its short exit grace.
        // Idle emits exactly zero, preventing controller transitions on solver noise.
        animationSpeed = (locomotionRunning && hasInput) ? Mathf.Max(measuredHorizontalSpeed, runEnterSpeed) : 0f;
        if (animator != null && hasSpeedParameter)
            animator.SetFloat(SpeedParameter, animationSpeed);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        body = body != null ? body : GetComponent<Rigidbody2D>();
        bodyCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        terrainFilter = new ContactFilter2D();
        terrainFilter.SetLayerMask(terrainLayers);
        terrainFilter.useTriggers = false;
        facingDirection = spriteFacesLeftByDefault
            ? (visualRenderer != null && visualRenderer.flipX ? 1 : -1)
            : (visualRenderer != null && visualRenderer.flipX ? -1 : 1);
        hasSpeedParameter = AnimatorHasSpeedParameter(animator);
        initialized = body != null && bodyCollider != null;
    }

    private void UpdateGroundedState()
    {
        IsGrounded = false;
        hasGroundContact = false;
        hasGroundPoint = false;
        groundPoint = body.position;
        groundNormal = Vector2.up;
        float requiredNormalY = RequiredGroundNormalY;

        int contactCount = bodyCollider.GetContacts(terrainFilter, contactPoints);
        terrainContactCount = contactCount;
        minimumContactSeparation = 0f;
        maximumContactFriction = 0f;
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contactPoints[i];
            minimumContactSeparation = Mathf.Min(minimumContactSeparation, contact.separation);
            maximumContactFriction = Mathf.Max(maximumContactFriction, contact.friction);
            Vector2 normal = contact.normal;
            if (normal.y < requiredNormalY)
                continue;
            if (!hasGroundPoint || contact.point.y > groundPoint.y)
            {
                groundPoint = contact.point;
                groundNormal = normal;
                hasGroundPoint = true;
                acceptedHitNormal = normal;
                acceptedHitCollider = contact.collider == bodyCollider ? contact.otherCollider : contact.collider;
            }
            IsGrounded = true;
            hasGroundContact = true;
        }

        if (IsGrounded)
            return;

        int hitCount = bodyCollider.Cast(Vector2.down, terrainFilter, castHits, groundCheckDistance, true);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (!IsUsableCastHit(hit) || hit.fraction <= 0.0001f || hit.distance <= 0.0001f || hit.normal.y < requiredNormalY)
                continue;
            if (!hasGroundPoint || hit.point.y > groundPoint.y)
            {
                groundPoint = hit.point;
                groundNormal = hit.normal;
                hasGroundPoint = true;
                RecordHit(hit);
            }
            IsGrounded = true;
        }
    }

    private Vector2 GetWalkableTangent(int direction)
    {
        Vector2 fallback = new Vector2(groundNormal.y, -groundNormal.x);
        if (fallback.x < 0f)
            fallback = -fallback;
        fallback.Normalize();

        if (!profileWalkable)
        {
            groundTangent = fallback;
            return direction > 0 ? groundTangent : -groundTangent;
        }

        // Equal-height casts of the same capsule measure its clearance envelope. Contact
        // points may move sideways on a slope, so their Y coordinates cannot define this slope.
        float slope = profileRise / (direction * slopeProbeDistance);
        groundTangent = new Vector2(1f, slope).normalized;
        return direction > 0 ? groundTangent : -groundTangent;
    }

    private bool IsBlockedBySteepTerrain(int direction)
    {
        Vector2 castDirection = direction > 0 ? Vector2.right : Vector2.left;
        float distance = 0.02f + Mathf.Max(maximumSpeed, Mathf.Abs(body.linearVelocityX)) * Time.fixedDeltaTime;
        int hitCount = bodyCollider.Cast(castDirection, terrainFilter, castHits, distance, true);
        hasBlockCast = true;
        debugBlockStart = bodyCollider.bounds.center;
        debugBlockEnd = debugBlockStart + castDirection * distance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (!IsUsableCastHit(hit) || hit.fraction <= 0.0001f || hit.distance <= 0.0001f)
                continue;
            bool opposesMovement = hit.normal.x * direction < -0.05f;
            if (!opposesMovement || hit.normal.y >= RequiredGroundNormalY)
                continue;

            RecordHit(hit);
            if (CanStepPast(direction, hit.point, distance, out float detectedStepHeight))
            {
                stepAssist = true;
                stepRise = Mathf.Max(stepRise, detectedStepHeight);
                continue;
            }

            debugBlockEnd = hit.point;
            return true;
        }

        // A touching riser often produces only a zero-distance cast, which is correctly
        // rejected above. Use real contact data to classify that riser instead.
        int contacts = bodyCollider.GetContacts(terrainFilter, contactPoints);
        for (int i = 0; i < contacts; i++)
        {
            ContactPoint2D contact = contactPoints[i];
            if (contact.normal.x * direction >= -0.05f || contact.normal.y >= RequiredGroundNormalY)
                continue;
            acceptedHitNormal = contact.normal;
            acceptedHitFraction = -1f;
            acceptedHitCollider = contact.collider == bodyCollider ? contact.otherCollider : contact.collider;
            if (CanStepPast(direction, contact.point, distance, out float detectedStepHeight))
            {
                stepAssist = true;
                stepRise = Mathf.Max(stepRise, detectedStepHeight);
            }
            else
            {
                debugBlockEnd = contact.point;
                return true;
            }
        }
        return false;
    }

    private bool CanStepPast(int direction, Vector2 obstaclePoint, float distance, out float stepHeight)
    {
        stepHeight = 0f;
        if (!IsGrounded)
            return false;

        float stepLimit = EffectiveStepHeight;

        // Step height limit check: obstacle contact point must not exceed maximum step allowance
        float obstacleRelativeY = obstaclePoint.y - groundPoint.y;
        if (obstacleRelativeY > stepLimit + 0.02f)
            return false;

        // Verify raised clearance for the capsule at maximum step height.
        // A wall continuing upward or a low ceiling will block the raised capsule.
        profileHits.Clear();
        float raisedOffset = stepLimit + 0.02f;
        bodyCollider.Cast(
            (Vector2)bodyCollider.transform.position + Vector2.up * raisedOffset,
            bodyCollider.transform.eulerAngles.z,
            Vector2.right * direction,
            terrainFilter,
            profileHits,
            distance,
            true);

        for (int i = 0; i < profileHits.Count; i++)
        {
            RaycastHit2D hit = profileHits[i];
            if (!IsUsableCastHit(hit))
                continue;
            if (hit.distance <= 0.0001f || (hit.normal.x * direction < -0.05f && hit.normal.y < RequiredGroundNormalY))
                return false;
        }

        // A step is traversable only when a valid walkable supporting surface exists
        // on top or immediately ahead, with normal within the maximum walkable slope.
        // This prevents climbing vertical walls or stepping over sheer drops / unsupportive obstacles.
        if (!TryFindWalkableStepSurface(direction, obstaclePoint, out stepHeight))
            return false;

        return stepHeight <= stepLimit + 0.02f;
    }

    private bool TryFindWalkableStepSurface(int direction, Vector2 obstaclePoint, out float stepHeight)
    {
        stepHeight = 0f;
        float stepLimit = EffectiveStepHeight;
        float probeStartY = groundPoint.y + stepLimit + 0.05f;
        float probeDistance = stepLimit + 0.15f;
        float minWalkableY = RequiredGroundNormalY;

        // Sample on top of the step and immediately ahead
        float[] sampleOffsets = { 0f, 0.05f, 0.12f, 0.20f };
        bool foundWalkable = false;

        for (int s = 0; s < sampleOffsets.Length; s++)
        {
            float probeX = obstaclePoint.x + direction * sampleOffsets[s];
            Vector2 rayOrigin = new Vector2(probeX, probeStartY);
            int hitCount = Physics2D.Raycast(rayOrigin, Vector2.down, terrainFilter, stepSurfaceHits, probeDistance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = stepSurfaceHits[i];
                if (!IsUsableCastHit(hit) || hit.fraction <= 0.0001f || hit.distance <= 0.0001f)
                    continue;

                // Normal must be within accepted walkable slope
                if (hit.normal.y < minWalkableY)
                    continue;

                float surfaceHeight = hit.point.y - groundPoint.y;
                if (surfaceHeight > stepLimit + 0.02f || surfaceHeight < -0.05f)
                    continue;

                foundWalkable = true;
                stepHeight = Mathf.Max(stepHeight, Mathf.Max(0f, surfaceHeight));
                break; // Topmost valid surface at this horizontal sample
            }
        }

        return foundWalkable;
    }

    private bool TryBuildGroundProfile(int direction)
    {
        float stepLimit = EffectiveStepHeight;
        float overlapDepth = hasGroundPoint ? Mathf.Max(0f, groundPoint.y - bodyCollider.bounds.min.y) : 0f;
        float lift = overlapDepth + stepLimit + slopeProbeDistance * Mathf.Tan(maximumWalkableSlopeAngle * Mathf.Deg2Rad) + 0.05f;
        float distance = lift + stepLimit + groundCheckDistance;
        if (!TryGetSupportDistance(Vector2.up * lift, distance, out float currentDistance) ||
            !TryGetSupportDistance(Vector2.up * lift + Vector2.right * (direction * slopeProbeDistance), distance, out float aheadDistance))
            return false;
        profileRise = currentDistance - aheadDistance;
        float limit = Mathf.Tan(Mathf.Acos(RequiredGroundNormalY)) * slopeProbeDistance;
        return Mathf.Abs(profileRise) <= limit;
    }

    private bool TryGetSupportDistance(Vector2 offset, float distance, out float supportDistance)
    {
        supportDistance = float.PositiveInfinity;
        profileHits.Clear();
        bodyCollider.Cast(
            (Vector2)bodyCollider.transform.position + offset,
            bodyCollider.transform.eulerAngles.z,
            Vector2.down,
            terrainFilter,
            profileHits,
            distance,
            true);

        bool found = false;
        for (int i = 0; i < profileHits.Count; i++)
        {
            RaycastHit2D hit = profileHits[i];
            if (!IsUsableCastHit(hit) || hit.fraction <= 0.0001f || hit.distance <= 0.0001f || hit.normal.y < RequiredGroundNormalY)
                continue;
            if (!found || hit.distance < supportDistance)
            {
                supportDistance = hit.distance;
                found = true;
            }
        }
        return found;
    }

    private void RecordHit(RaycastHit2D hit)
    {
        acceptedHitNormal = hit.normal;
        acceptedHitFraction = hit.fraction;
        acceptedHitCollider = hit.collider;
    }

    private void LogMovementDiagnostics(float input, Vector2 observedVelocity, Vector2 displacementVelocity)
    {
        if (!movementDiagnosticsEnabled || Time.fixedTime < nextDiagnosticTime)
            return;
        nextDiagnosticTime = Time.fixedTime + Mathf.Max(0.1f, diagnosticInterval);
        Debug.Log($"[GroundedActorMotor2D] {name} input={input:F2} position={body.position:F3} " +
            $"requested={requestedVelocity:F3} assigned={body.linearVelocity:F3} observedAfterPreviousPhysics={observedVelocity:F3} " +
            $"displacementVelocity={displacementVelocity:F3} grounded={IsGrounded} blocking={forwardBlocked} stepAssist={stepAssist} " +
            $"measuredHorizontalSpeed={measuredHorizontalSpeed:F3} animationSpeed={animationSpeed:F3} running={locomotionRunning} idleTimer={stationaryDuration:F3} " +
            $"profileWalkable={profileWalkable} profileRise={profileRise:F3} hitNormal={acceptedHitNormal:F3} hitFraction={acceptedHitFraction:F4} " +
            $"contacts={terrainContactCount} minSeparation={minimumContactSeparation:F4} effectiveFriction={maximumContactFriction:F3} " +
            $"hitCollider={(acceptedHitCollider != null ? acceptedHitCollider.name : "none")}", this);
    }

    private bool IsUsableCastHit(RaycastHit2D hit)
    {
        return hit.collider != null && hit.collider != bodyCollider && hit.collider.attachedRigidbody != body && !hit.collider.isTrigger &&
               (terrainLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
    }

    public void SetFacing(int direction)
    {
        facingDirection = direction >= 0 ? 1 : -1;
        if (visualRenderer != null)
            visualRenderer.flipX = spriteFacesLeftByDefault ? (facingDirection > 0) : (facingDirection < 0);
    }

    private float RequiredGroundNormalY => Mathf.Max(
        minimumGroundNormalY,
        Mathf.Cos(maximumWalkableSlopeAngle * Mathf.Deg2Rad));

    private void OnDrawGizmosSelected()
    {
        if (!showGroundDebugGizmos)
            return;
        Vector3 origin = body != null ? (Vector3)body.position : transform.position;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, 0.08f);
        if (hasGroundPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(groundPoint, groundPoint + groundNormal * debugVectorLength);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(groundPoint, groundPoint + groundTangent * debugVectorLength);
        }
        if (hasBlockCast)
        {
            Gizmos.color = forwardBlocked ? Color.magenta : Color.gray;
            Gizmos.DrawLine(debugBlockStart, debugBlockEnd);
        }
    }

    private static bool AnimatorHasSpeedParameter(Animator targetAnimator)
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
            return false;
        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == SpeedParameter && parameters[i].type == AnimatorControllerParameterType.Float)
                return true;
        }
        return false;
    }
}
