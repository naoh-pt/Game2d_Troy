using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI component supporting held states, multi-touch tracking, release-inside vs cancel gestures,
/// and safe release on disable or exit to avoid stuck mobile inputs.
/// </summary>
[DisallowMultipleComponent]
public class TouchHoldButton : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private bool interactable = true;
    [SerializeField] private bool cancelOnPointerExit = true;

    private bool isHeld;
    private int activePointerId = -1;

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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!interactable || !IsActive())
            return;

        if (!isHeld)
        {
            isHeld = true;
            activePointerId = eventData.pointerId;
            HoldStarted?.Invoke();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld)
            return;

        if (eventData.pointerId == activePointerId || activePointerId == -1)
        {
            ReleasedInside?.Invoke();
            ReleaseHold(false);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHeld)
            return;

        if (cancelOnPointerExit && (eventData.pointerId == activePointerId || activePointerId == -1))
        {
            Cancelled?.Invoke();
            ReleaseHold(true);
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
