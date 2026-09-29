using UnityEngine;

/// <summary>
/// Applies camera-relative parallax while keeping a sprite within its original
/// viewport coverage. Assign references in the Editor; no runtime searches occur.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class Level01ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField, Range(0f, 1f)] private float parallaxFactor = 0.08f;

    private bool anchorCaptured;
    private Vector3 anchorTransformPosition;
    private Vector3 anchorCameraPosition;
    private Vector3 anchorBoundsCenter;
    private Vector3 anchorBoundsExtents;
    private float allowedLeft;
    private float allowedRight;
    private float allowedBottom;
    private float allowedTop;

    /// <summary>Used by the idempotent Editor setup command.</summary>
    public void Configure(Camera cameraReference, SpriteRenderer rendererReference, float factor)
    {
        targetCamera = cameraReference;
        targetRenderer = rendererReference;
        parallaxFactor = Mathf.Clamp01(factor);
        anchorCaptured = false;
    }

    private void Awake()
    {
        CaptureAnchor();
    }

    private void OnEnable()
    {
        CaptureAnchor();
    }

    private void LateUpdate()
    {
        if (!anchorCaptured && !CaptureAnchor())
            return;

        if (targetCamera == null || targetRenderer == null || !targetCamera.orthographic)
            return;

        Vector3 cameraDelta = targetCamera.transform.position - anchorCameraPosition;
        Vector3 desiredCenter = anchorBoundsCenter + cameraDelta * parallaxFactor;
        Vector3 cameraPosition = targetCamera.transform.position;
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        desiredCenter.x = ConstrainAxis(
            desiredCenter.x,
            cameraPosition.x - halfWidth,
            cameraPosition.x + halfWidth,
            anchorBoundsExtents.x,
            allowedLeft,
            allowedRight);
        desiredCenter.y = ConstrainAxis(
            desiredCenter.y,
            cameraPosition.y - halfHeight,
            cameraPosition.y + halfHeight,
            anchorBoundsExtents.y,
            allowedBottom,
            allowedTop);

        // Set from the immutable anchor each frame to avoid cumulative drift.
        transform.position = anchorTransformPosition + (desiredCenter - anchorBoundsCenter);
    }

    private bool CaptureAnchor()
    {
        if (targetCamera == null || targetRenderer == null || !targetCamera.orthographic)
            return false;

        anchorTransformPosition = transform.position;
        anchorCameraPosition = targetCamera.transform.position;
        Bounds rendererBounds = targetRenderer.bounds;
        anchorBoundsCenter = rendererBounds.center;
        anchorBoundsExtents = rendererBounds.extents;
        float halfWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float halfHeight = targetCamera.orthographicSize;
        allowedLeft = Mathf.Max(0f, anchorBoundsCenter.x - anchorBoundsExtents.x - (anchorCameraPosition.x - halfWidth));
        allowedRight = Mathf.Max(0f, anchorCameraPosition.x + halfWidth - (anchorBoundsCenter.x + anchorBoundsExtents.x));
        allowedBottom = Mathf.Max(0f, anchorBoundsCenter.y - anchorBoundsExtents.y - (anchorCameraPosition.y - halfHeight));
        allowedTop = Mathf.Max(0f, anchorCameraPosition.y + halfHeight - (anchorBoundsCenter.y + anchorBoundsExtents.y));
        anchorCaptured = true;
        return true;
    }

    private static float ConstrainAxis(
        float desiredCenter,
        float viewportMin,
        float viewportMax,
        float spriteExtent,
        float allowedMinExposure,
        float allowedMaxExposure)
    {
        // This interval prevents exposure from growing beyond the original framing.
        float minimumCenter = viewportMax - spriteExtent - allowedMaxExposure;
        float maximumCenter = viewportMin + spriteExtent + allowedMinExposure;
        if (minimumCenter <= maximumCenter)
            return Mathf.Clamp(desiredCenter, minimumCenter, maximumCenter);

        // If the viewport is larger than the available sprite, center the unavoidable excess.
        return (viewportMin + viewportMax) * 0.5f
            + (allowedMinExposure - allowedMaxExposure) * 0.5f;
    }
}
