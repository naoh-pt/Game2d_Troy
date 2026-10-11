using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum Level01CameraMode
{
    Manual,
    FollowActor,
    FollowProjectile,
    Transition
}

/// <summary>Owns movement, zoom, target following, input, and bounds clamping for the Level 01 camera.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class Level01CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private Collider2D levelBounds;

    [Header("Camera Targets")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform enemy;
    [SerializeField] private Transform fallbackActor;
    [SerializeField] private Level01CameraMode initialMode = Level01CameraMode.FollowActor;
    [SerializeField] private Vector3 actorFollowOffset = new Vector3(0f, 1f, 0f);

    [Header("Target Following")]
    [SerializeField, Min(0f)] private float actorFollowSmoothTime = 0.35f;
    [SerializeField, Min(0f)] private float projectileFollowSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float targetTransitionDuration = 0.5f;
    [SerializeField, Min(0f)] private float impactHoldDuration = 0.6f;
    [SerializeField] private bool enableEditorDebugHotkeys = true;

    [Header("Input")]
    [SerializeField, Min(0.01f)] private float dragSpeed = 1f;
    [SerializeField, Min(0f)] private float wheelZoomSensitivity = 0.002f;
    [SerializeField, Min(0.01f)] private float pinchZoomSensitivity = 1f;

    [Header("Zoom")]
    [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 3f;
    [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 12f;

    [Header("Manual Smoothing")]
    [SerializeField, Min(0f)] private float movementSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float zoomSmoothTime = 0.12f;

    private Vector3 targetPosition;
    private Vector3 movementVelocity;
    private float targetOrthographicSize;
    private float zoomVelocity;
    private bool initialized;

    private Level01CameraMode currentMode = Level01CameraMode.Manual;
    private Level01CameraMode transitionDestinationMode = Level01CameraMode.FollowActor;
    private Transform currentTarget;
    private Vector3 transitionStartPosition;
    private float transitionElapsed;
    private bool holdingImpactPosition;
    private float impactHoldRemaining;
    private Transform impactFallbackActor;
    private Transform projectileTargetA;
    private Transform projectileTargetB;

    private bool mouseDragging;
    private Vector2 previousMousePosition;
    private int activeTouchCount;
    private int activeTouchId;
    private Vector2 previousTouchPosition;
    private float previousPinchDistance;

    public Level01CameraMode CurrentMode => currentMode;
    public Transform CurrentTarget => currentTarget;

    private void Awake() => InitializeTargets();

    private void Start()
    {
        if (initialMode == Level01CameraMode.FollowActor && player != null)
            FocusActor(player);
        else
            SetManualMode();
    }

    private void OnEnable() => InitializeTargets();

    private void OnDisable()
    {
        mouseDragging = false;
        activeTouchCount = 0;
        holdingImpactPosition = false;
        impactHoldRemaining = 0f;
    }

    private void OnValidate()
    {
        minimumOrthographicSize = Mathf.Max(0.1f, minimumOrthographicSize);
        maximumOrthographicSize = Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);
        dragSpeed = Mathf.Max(0.01f, dragSpeed);
        wheelZoomSensitivity = Mathf.Max(0f, wheelZoomSensitivity);
        pinchZoomSensitivity = Mathf.Max(0.01f, pinchZoomSensitivity);
        movementSmoothTime = Mathf.Max(0f, movementSmoothTime);
        zoomSmoothTime = Mathf.Max(0f, zoomSmoothTime);
        actorFollowSmoothTime = Mathf.Max(0f, actorFollowSmoothTime);
        projectileFollowSmoothTime = Mathf.Max(0f, projectileFollowSmoothTime);
        targetTransitionDuration = Mathf.Max(0f, targetTransitionDuration);
        impactHoldDuration = Mathf.Max(0f, impactHoldDuration);

        if (initialMode == Level01CameraMode.FollowProjectile || initialMode == Level01CameraMode.Transition)
            initialMode = Level01CameraMode.FollowActor;
    }

    public void ConfigureReferences(Camera cameraReference, Collider2D boundsReference)
    {
        controlledCamera = cameraReference;
        levelBounds = boundsReference;
        initialized = false;
        InitializeTargets();
    }

    /// <summary>Assigns actor references without changing camera, input, or bounds settings.</summary>
    public void ConfigureTargets(Transform playerReference, Transform enemyReference)
    {
        player = playerReference;
        enemy = enemyReference;
        fallbackActor = playerReference;
        initialMode = Level01CameraMode.FollowActor;
    }

    public void FocusPlayer(bool immediate = false) => FocusActor(player, immediate);
    public void FocusEnemy(bool immediate = false) => FocusActor(enemy, immediate);

    public void FocusActor(Transform target, bool immediate = false)
    {
        if (target == null)
        {
            SetManualMode();
            return;
        }

        BeginTargetMode(target, Level01CameraMode.FollowActor, immediate);
    }

    public void FollowProjectile(Transform projectile)
    {
        projectileTargetA = projectile;
        projectileTargetB = null;

        if (projectile == null)
        {
            ReturnToFallbackActor();
            return;
        }

        BeginTargetMode(projectile, Level01CameraMode.FollowProjectile, false);
    }

    /// <summary>
    /// Follows two simultaneous projectiles, tracking their midpoint while both exist,
    /// then seamlessly continuing to track the remaining one when one finishes.
    /// </summary>
    public void FollowProjectiles(Transform projectileA, Transform projectileB)
    {
        projectileTargetA = projectileA;
        projectileTargetB = projectileB;

        if (projectileA == null && projectileB == null)
        {
            ReturnToFallbackActor();
            return;
        }

        Transform initialTarget = projectileA != null ? projectileA : projectileB;
        BeginTargetMode(initialTarget, Level01CameraMode.FollowProjectile, false);
    }

    /// <summary>Holds the last projectile position, then blends to the override or configured fallback.</summary>
    public void NotifyProjectileEnded(Transform fallbackActorOverride = null)
    {
        bool projectileMode = currentMode == Level01CameraMode.FollowProjectile ||
            (currentMode == Level01CameraMode.Transition && transitionDestinationMode == Level01CameraMode.FollowProjectile);
        if (!projectileMode)
            return;

        targetPosition = GetTargetPosition(currentTarget, false);
        currentTarget = null;
        holdingImpactPosition = impactHoldDuration > 0f;
        impactHoldRemaining = impactHoldDuration;
        impactFallbackActor = fallbackActorOverride != null ? fallbackActorOverride : fallbackActor;
        projectileTargetA = null;
        projectileTargetB = null;
        currentMode = Level01CameraMode.FollowProjectile;
        transitionElapsed = 0f;
        movementVelocity = Vector3.zero;

        if (!holdingImpactPosition)
            CompleteImpactHold();
    }

    public void SetManualMode()
    {
        currentMode = Level01CameraMode.Manual;
        currentTarget = null;
        projectileTargetA = null;
        projectileTargetB = null;
        holdingImpactPosition = false;
        impactHoldRemaining = 0f;
        impactFallbackActor = null;
        transitionElapsed = 0f;
        targetPosition = transform.position;
        movementVelocity = Vector3.zero;
    }

    public void SetFallbackActor(Transform actor) => fallbackActor = actor;

    private void Update()
    {
        if (!EnsureCamera() || !controlledCamera.orthographic)
            return;

#if UNITY_EDITOR
        ProcessEditorDebugHotkeys();
#endif

        if (ProcessTouchInput())
            return;

        ProcessMouseInput();
    }

    private void LateUpdate()
    {
        if (!EnsureCamera() || !controlledCamera.orthographic)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        UpdateImpactHold(deltaTime);
        ValidateFollowTarget();

        float desiredSize = Mathf.Clamp(targetOrthographicSize, minimumOrthographicSize, maximumOrthographicSize);
        controlledCamera.orthographicSize = zoomSmoothTime <= 0f
            ? desiredSize
            : Mathf.SmoothDamp(controlledCamera.orthographicSize, desiredSize, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);

        if (currentMode == Level01CameraMode.Transition)
        {
            UpdateTransition(deltaTime);
            return;
        }

        if (!holdingImpactPosition)
        {
            if (currentMode == Level01CameraMode.FollowProjectile)
            {
                bool hasA = projectileTargetA != null && projectileTargetA.gameObject.activeInHierarchy;
                bool hasB = projectileTargetB != null && projectileTargetB.gameObject.activeInHierarchy;

                if (hasA && hasB)
                {
                    targetPosition = (projectileTargetA.position + projectileTargetB.position) * 0.5f;
                    targetPosition.z = transform.position.z;
                }
                else if (hasA)
                {
                    currentTarget = projectileTargetA;
                    targetPosition = projectileTargetA.position;
                    targetPosition.z = transform.position.z;
                }
                else if (hasB)
                {
                    currentTarget = projectileTargetB;
                    targetPosition = projectileTargetB.position;
                    targetPosition.z = transform.position.z;
                }
                else if (currentTarget != null)
                {
                    targetPosition = currentTarget.position;
                    targetPosition.z = transform.position.z;
                }
            }
            else if (currentTarget != null)
            {
                targetPosition = GetTargetPosition(currentTarget, currentMode == Level01CameraMode.FollowActor);
            }
        }

        targetPosition = ClampToBounds(targetPosition, controlledCamera.orthographicSize);
        float smoothTime = GetCurrentMovementSmoothTime();
        Vector3 smoothedPosition = smoothTime <= 0f
            ? targetPosition
            : Vector3.SmoothDamp(transform.position, targetPosition, ref movementVelocity, smoothTime, Mathf.Infinity, deltaTime);
        transform.position = ClampToBounds(smoothedPosition, controlledCamera.orthographicSize);
    }

    private void BeginTargetMode(Transform target, Level01CameraMode destinationMode, bool immediate)
    {
        if (!EnsureCamera())
            return;

        currentTarget = target;
        holdingImpactPosition = false;
        impactHoldRemaining = 0f;
        impactFallbackActor = null;
        transitionDestinationMode = destinationMode;
        transitionElapsed = 0f;
        movementVelocity = Vector3.zero;

        Vector3 desiredPosition = GetTargetPosition(target, destinationMode == Level01CameraMode.FollowActor);
        if (immediate || targetTransitionDuration <= 0f)
        {
            currentMode = destinationMode;
            targetPosition = ClampToBounds(desiredPosition, controlledCamera.orthographicSize);
            transform.position = targetPosition;
            return;
        }

        currentMode = Level01CameraMode.Transition;
        transitionStartPosition = transform.position;
        targetPosition = transitionStartPosition;
    }

    private void UpdateTransition(float deltaTime)
    {
        if (currentTarget == null)
        {
            ReturnToFallbackActor();
            return;
        }

        transitionElapsed += deltaTime;
        float progress = targetTransitionDuration <= 0f ? 1f : Mathf.Clamp01(transitionElapsed / targetTransitionDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 destination = GetTargetPosition(currentTarget, transitionDestinationMode == Level01CameraMode.FollowActor);
        targetPosition = Vector3.LerpUnclamped(transitionStartPosition, destination, easedProgress);
        transform.position = ClampToBounds(targetPosition, controlledCamera.orthographicSize);

        if (progress < 1f)
            return;

        currentMode = transitionDestinationMode;
        targetPosition = ClampToBounds(destination, controlledCamera.orthographicSize);
        transform.position = targetPosition;
        movementVelocity = Vector3.zero;
    }

    private void ValidateFollowTarget()
    {
        bool needsTarget = currentMode == Level01CameraMode.Transition ||
            (currentMode == Level01CameraMode.FollowActor && !holdingImpactPosition) ||
            (currentMode == Level01CameraMode.FollowProjectile && !holdingImpactPosition);
        if (needsTarget)
        {
            if (currentMode == Level01CameraMode.FollowProjectile)
            {
                bool hasA = projectileTargetA != null && projectileTargetA.gameObject.activeInHierarchy;
                bool hasB = projectileTargetB != null && projectileTargetB.gameObject.activeInHierarchy;
                if (!hasA && !hasB && currentTarget == null)
                    ReturnToFallbackActor();
            }
            else if (currentTarget == null)
            {
                ReturnToFallbackActor();
            }
        }
    }

    private void UpdateImpactHold(float deltaTime)
    {
        if (!holdingImpactPosition)
            return;

        impactHoldRemaining -= deltaTime;
        if (impactHoldRemaining <= 0f)
            CompleteImpactHold();
    }

    private void CompleteImpactHold()
    {
        Transform returnActor = impactFallbackActor != null ? impactFallbackActor : fallbackActor;
        holdingImpactPosition = false;
        impactHoldRemaining = 0f;
        impactFallbackActor = null;

        if (returnActor != null)
            FocusActor(returnActor);
        else
            SetManualMode();
    }

    private void ReturnToFallbackActor()
    {
        if (fallbackActor != null && fallbackActor != currentTarget)
            FocusActor(fallbackActor);
        else
            SetManualMode();
    }

    private Vector3 GetTargetPosition(Transform target, bool includeActorOffset)
    {
        Vector3 position = target != null ? target.position : targetPosition;
        if (includeActorOffset)
            position += actorFollowOffset;
        position.z = transform.position.z;
        return position;
    }

    private float GetCurrentMovementSmoothTime()
    {
        if (currentMode == Level01CameraMode.FollowActor)
            return actorFollowSmoothTime;
        if (currentMode == Level01CameraMode.FollowProjectile)
            return projectileFollowSmoothTime;
        return movementSmoothTime;
    }

    private void InitializeTargets()
    {
        controlledCamera = controlledCamera != null ? controlledCamera : GetComponent<Camera>();
        if (controlledCamera == null)
            return;

        targetPosition = transform.position;
        targetOrthographicSize = controlledCamera.orthographicSize;
        initialized = true;
    }

    private bool EnsureCamera()
    {
        if (controlledCamera == null)
            controlledCamera = GetComponent<Camera>();
        if (controlledCamera == null)
            return false;
        if (!initialized)
            InitializeTargets();
        return true;
    }

    private bool ProcessTouchInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            activeTouchCount = 0;
            return false;
        }

        TouchControl firstTouch = null;
        TouchControl secondTouch = null;
        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            TouchControl touch = touchscreen.touches[i];
            if (!touch.press.isPressed)
                continue;
            if (firstTouch == null)
                firstTouch = touch;
            else
            {
                secondTouch = touch;
                break;
            }
        }

        if (firstTouch == null)
        {
            activeTouchCount = 0;
            return false;
        }

        int firstId = firstTouch.touchId.ReadValue();
        if (PointerIsOverUi(firstId) || (secondTouch != null && PointerIsOverUi(secondTouch.touchId.ReadValue())))
        {
            CancelMotionWhileOverUi();
            return true;
        }

        Vector2 firstPosition = firstTouch.position.ReadValue();
        if (secondTouch == null)
        {
            if (activeTouchCount != 1 || activeTouchId != firstId)
            {
                activeTouchCount = 1;
                activeTouchId = firstId;
                previousTouchPosition = firstPosition;
                return true;
            }

            Vector2 screenDelta = firstPosition - previousTouchPosition;
            if (CanManuallyPan())
                ApplyScreenDrag(screenDelta);
            previousTouchPosition = firstPosition;
            return true;
        }

        Vector2 secondPosition = secondTouch.position.ReadValue();
        float pinchDistance = Vector2.Distance(firstPosition, secondPosition);
        if (activeTouchCount != 2)
        {
            activeTouchCount = 2;
            previousPinchDistance = pinchDistance;
            return true;
        }

        if (pinchDistance > 0.01f && previousPinchDistance > 0.01f)
        {
            float ratio = previousPinchDistance / pinchDistance;
            targetOrthographicSize = Mathf.Clamp(
                targetOrthographicSize * Mathf.Pow(ratio, pinchZoomSensitivity),
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        previousPinchDistance = pinchDistance;
        return true;
    }

    private void ProcessMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            mouseDragging = false;
            return;
        }

        if (PointerIsOverUi())
        {
            mouseDragging = false;
            CancelMotionWhileOverUi();
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            mouseDragging = true;
            previousMousePosition = pointerPosition;
        }
        else if (mouse.leftButton.isPressed && mouseDragging)
        {
            Vector2 screenDelta = pointerPosition - previousMousePosition;
            if (CanManuallyPan())
                ApplyScreenDrag(screenDelta);
            previousMousePosition = pointerPosition;
        }
        else if (!mouse.leftButton.isPressed)
            mouseDragging = false;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetOrthographicSize = Mathf.Clamp(
                targetOrthographicSize * Mathf.Exp(-scroll * wheelZoomSensitivity),
                minimumOrthographicSize,
                maximumOrthographicSize);
        }
    }

    private bool CanManuallyPan()
    {
        return currentMode != Level01CameraMode.FollowProjectile &&
            !(currentMode == Level01CameraMode.Transition && transitionDestinationMode == Level01CameraMode.FollowProjectile);
    }

    private void ApplyScreenDrag(Vector2 screenDelta)
    {
        if (controlledCamera == null || Screen.height <= 0 || screenDelta.sqrMagnitude <= 0f)
            return;
        if (currentMode != Level01CameraMode.Manual)
            SetManualMode();

        float worldUnitsPerPixel = (2f * controlledCamera.orthographicSize) / Screen.height;
        targetPosition.x -= screenDelta.x * worldUnitsPerPixel * dragSpeed;
        targetPosition.y -= screenDelta.y * worldUnitsPerPixel * dragSpeed;
        targetPosition = ClampToBounds(targetPosition, controlledCamera.orthographicSize);
    }

#if UNITY_EDITOR
    private void ProcessEditorDebugHotkeys()
    {
        if (!enableEditorDebugHotkeys)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard.f1Key.wasPressedThisFrame)
            FocusPlayer();
        else if (keyboard.f2Key.wasPressedThisFrame)
            FocusEnemy();
        else if (keyboard.f3Key.wasPressedThisFrame)
            SetManualMode();
    }
#endif

    private bool PointerIsOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    private bool PointerIsOverUi(int pointerId)
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject(pointerId);
    }

    private void CancelMotionWhileOverUi()
    {
        if (currentMode == Level01CameraMode.Manual)
        {
            targetPosition = transform.position;
            movementVelocity = Vector3.zero;
        }

        if (controlledCamera != null)
        {
            targetOrthographicSize = controlledCamera.orthographicSize;
            zoomVelocity = 0f;
        }
        activeTouchCount = 0;
    }

    private Vector3 ClampToBounds(Vector3 position, float orthographicSize)
    {
        if (levelBounds == null || controlledCamera == null)
            return position;

        Bounds bounds = levelBounds.bounds;
        float halfHeight = Mathf.Max(0f, orthographicSize);
        float halfWidth = halfHeight * controlledCamera.aspect;
        position.x = ClampAxis(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth, bounds.center.x);
        position.y = ClampAxis(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight, bounds.center.y);
        return position;
    }

    private static float ClampAxis(float value, float minimum, float maximum, float center)
    {
        return minimum > maximum ? center : Mathf.Clamp(value, minimum, maximum);
    }
}
