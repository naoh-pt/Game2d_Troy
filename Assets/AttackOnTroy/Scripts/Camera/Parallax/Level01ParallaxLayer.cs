using UnityEngine;
using UnityEngine.Serialization;

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

    [FormerlySerializedAs("parallaxFactor")]
    [SerializeField, Range(0f, 1f)] private float parallaxFactorX = 0.08f;
    [SerializeField, Range(0f, 1f)] private float parallaxFactorY = 1.0f;
    [SerializeField] private bool constrainVertical = false;

    private bool anchorCaptured;
    private Vector3 anchorTransformPosition;
    private Vector3 anchorCameraPosition;
    private Vector3 anchorBoundsCenter;
    private Vector3 anchorBoundsExtents;
    private float allowedLeft;
    private float allowedRight;
    private float allowedBottom;
    private float allowedTop;

    public float ParallaxFactorX => parallaxFactorX;
    public float ParallaxFactorY => parallaxFactorY;

    /// <summary>Used by the idempotent Editor setup command.</summary>
    public void Configure(Camera cameraReference, SpriteRenderer rendererReference, float factorX, float factorY = 1f)
    {
        targetCamera = cameraReference;
        targetRenderer = rendererReference;
        parallaxFactorX = Mathf.Clamp01(factorX);
        parallaxFactorY = Mathf.Clamp01(factorY);
        anchorCaptured = false;
    }

    public void Configure(Camera cameraReference, SpriteRenderer rendererReference, float factor)
    {
        Configure(cameraReference, rendererReference, factor, 1f);
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
        Vector3 desiredCenter = anchorBoundsCenter;
        desiredCenter.x += cameraDelta.x * parallaxFactorX;
        desiredCenter.y += cameraDelta.y * parallaxFactorY;

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

        if (constrainVertical)
        {
            desiredCenter.y = ConstrainAxis(
                desiredCenter.y,
                cameraPosition.y - halfHeight,
                cameraPosition.y + halfHeight,
                anchorBoundsExtents.y,
                allowedBottom,
                allowedTop);
        }
        else
        {
            // Safety guard: ensure the top edge of the background layer doesn't dip below
            // the camera viewport top edge if the sprite height is sufficient to cover it.
            float viewportTop = cameraPosition.y + halfHeight;
            float spriteTop = desiredCenter.y + anchorBoundsExtents.y;
            if (spriteTop < viewportTop && (2f * anchorBoundsExtents.y >= 2f * halfHeight))
            {
                desiredCenter.y = viewportTop - anchorBoundsExtents.y;
            }
        }

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
