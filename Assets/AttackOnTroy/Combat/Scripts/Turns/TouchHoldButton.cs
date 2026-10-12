using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI component supporting held states, multi-touch tracking, tap detection,
/// and safe release on disable or exit to avoid stuck mobile inputs.
/// </summary>
[DisallowMultipleComponent]
public class TouchHoldButton : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private bool interactable = true;
    [SerializeField] private bool cancelOnPointerExit = false;
    [SerializeField] private float cancelDragDistanceThreshold = 150f;

    private bool isHeld;
    private int activePointerId = -1;
    private float pointerDownTime;
    private Vector2 pointerDownPos;

    public bool IsHeld => isHeld;
    public bool Interactable
    {
        get => interactable;
        set
        {
            interactable = value;
            if (!interactable && isHeld)
                ReleaseHold(true);
        }
    }

    public event Action HoldStarted;
    public event Action HoldEnded;
    public event Action ReleasedInside;
    public event Action Cancelled;
    public event Action Tapped;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!interactable || !IsActive())
            return;

        if (!isHeld)
        {
            isHeld = true;
            activePointerId = eventData.pointerId;
            pointerDownTime = Time.unscaledTime;
            pointerDownPos = eventData.position;
            HoldStarted?.Invoke();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld)
            return;

        if (eventData.pointerId == activePointerId || activePointerId == -1)
        {
            bool isTap = (Time.unscaledTime - pointerDownTime) < 0.35f &&
                         (eventData.position - pointerDownPos).sqrMagnitude < 2500f; // 50px tolerance

            if (isTap)
            {
                Tapped?.Invoke();
            }

            ReleasedInside?.Invoke();
            ReleaseHold(false);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHeld)
            return;

        // On mobile touchscreens, minor thumb wobble/jitter must not cancel the hold gesture.
        // Only cancel if cancelOnPointerExit is explicitly enabled AND the finger dragged far beyond the threshold.
        if (cancelOnPointerExit && (eventData.pointerId == activePointerId || activePointerId == -1))
        {
            float dragDistance = Vector2.Distance(eventData.position, pointerDownPos);
            if (dragDistance > cancelDragDistanceThreshold)
            {
                Cancelled?.Invoke();
                ReleaseHold(true);
            }
        }
    }

    public void ReleaseHold(bool isCancelled = false)
    {
        if (isHeld)
        {
            isHeld = false;
            activePointerId = -1;
            if (isCancelled)
                Cancelled?.Invoke();
            HoldEnded?.Invoke();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ReleaseHold(true);
    }
}
