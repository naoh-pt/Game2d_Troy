using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Development-only keyboard input adapter for player-prefab movement testing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GroundedActorMotor2D))]
public sealed class PlayerMovementDebugInput : MonoBehaviour
{
    [SerializeField] private GroundedActorMotor2D motor;
    [SerializeField] private bool debugInputEnabled = true;

    public void Configure(GroundedActorMotor2D motorReference)
    {
        motor = motorReference;
    }

    private void Awake()
    {
        motor = motor != null ? motor : GetComponent<GroundedActorMotor2D>();
    }

    private void Update()
    {
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
        {
            ClearInput();
            return;
        }
#endif
        if (!debugInputEnabled || motor == null)
        {
            ClearInput();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            motor.SetMoveInput(0f);
            return;
        }

        bool moveLeft = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool moveRight = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        motor.SetMoveInput((moveRight ? 1f : 0f) - (moveLeft ? 1f : 0f));
    }

    private void OnDisable()
    {
        ClearInput();
    }

    private void ClearInput()
    {
        if (motor != null)
            motor.SetMoveInput(0f);
    }
}
