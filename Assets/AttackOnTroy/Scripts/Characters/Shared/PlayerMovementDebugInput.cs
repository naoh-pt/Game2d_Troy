using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Development-only keyboard input adapter for player-prefab movement testing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GroundedActorMotor2D))]
public sealed class PlayerMovementDebugInput : MonoBehaviour
{
    [SerializeField] private GroundedActorMotor2D motor;
    [SerializeField] private bool debugInputEnabled = true;

    private float externalMoveInput;

    public void Configure(GroundedActorMotor2D motorReference)
    {
        motor = motorReference;
    }

    public void SetExternalMoveInput(float value)
    {
        externalMoveInput = Mathf.Clamp(value, -1f, 1f);
    }

    private void Awake()
    {
        motor = motor != null ? motor : GetComponent<GroundedActorMotor2D>();
    }

    private void Update()
    {
        float keyboardInput = 0f;
        Keyboard keyboard = Keyboard.current;
        if (debugInputEnabled && keyboard != null)
        {
            bool moveLeft = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            bool moveRight = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            keyboardInput = (moveRight ? 1f : 0f) - (moveLeft ? 1f : 0f);
        }

        float combinedInput = Mathf.Abs(externalMoveInput) > 0.001f ? externalMoveInput : keyboardInput;
        if (motor != null)
        {
            motor.SetMoveInput(combinedInput);
        }
    }

    private void OnDisable()
    {
        ClearInput();
    }

    public void ClearInput()
    {
        externalMoveInput = 0f;
        if (motor != null)
            motor.SetMoveInput(0f);
    }
}
