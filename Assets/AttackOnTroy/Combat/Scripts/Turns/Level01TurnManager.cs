using System;
using TMPro;
using UnityEngine;

public enum Level01TurnState
{
    PlayerTurn,
    PlayerThrowing,
    ProjectileFlight,
    ResolutionDelay,
    CyclopsTurn,
    TurnTransition
}

/// <summary>
/// Minimal turn sequencer prototype for Level 01.
/// Manages player vs. enemy turn progression, input gating, HUD state labels,
/// and camera focus transitions between Achilles, the flying projectile, and the Cyclops.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01TurnManager : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private Level01TurnState currentState = Level01TurnState.PlayerTurn;

    [Header("Actor References")]
    [SerializeField] private AchillesProjectileDebugLauncher launcher;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform cyclopsTransform;
    [SerializeField] private GroundedActorMotor2D playerMotor;

    [Header("Camera & UI References")]
    [SerializeField] private Level01CameraController cameraController;
    [SerializeField] private TextMeshProUGUI turnText;

    [Header("Sequence Timers")]
    [SerializeField, Range(0.5f, 1.5f)] private float resolutionDelayDuration = 0.75f;
    [SerializeField, Range(0.5f, 3.0f)] private float cyclopsTurnDuration = 1.5f;
    [SerializeField, Range(0.1f, 1.0f)] private float turnTransitionDuration = 0.4f;

    private float stateTimer;

    public Level01TurnState CurrentState => currentState;
    public bool IsPlayerTurn => currentState == Level01TurnState.PlayerTurn;

    private void Awake()
    {
        InitializeReferences();
    }

    private void Start()
    {
        EnterState(Level01TurnState.PlayerTurn);
    }

    private void OnEnable()
    {
        SubscribeLauncherEvents();
    }

    private void OnDisable()
    {
        UnsubscribeLauncherEvents();
    }

    private void Update()
    {
        switch (currentState)
        {
            case Level01TurnState.ResolutionDelay:
                stateTimer += Time.deltaTime;
                if (stateTimer >= resolutionDelayDuration)
                {
                    EnterState(Level01TurnState.CyclopsTurn);
                }
                break;

            case Level01TurnState.CyclopsTurn:
                stateTimer += Time.deltaTime;
                if (stateTimer >= cyclopsTurnDuration)
                {
                    EnterState(Level01TurnState.TurnTransition);
                }
                break;

            case Level01TurnState.TurnTransition:
                stateTimer += Time.deltaTime;
                if (stateTimer >= turnTransitionDuration)
                {
                    EnterState(Level01TurnState.PlayerTurn);
                }
                break;
        }
    }

    public void Configure(
        AchillesProjectileDebugLauncher launcherRef,
        Transform playerRef,
        Transform cyclopsRef,
        GroundedActorMotor2D motorRef,
        Level01CameraController cameraRef,
        TextMeshProUGUI turnTextRef)
    {
        UnsubscribeLauncherEvents();
        launcher = launcherRef;
        playerTransform = playerRef;
        cyclopsTransform = cyclopsRef;
        playerMotor = motorRef;
        cameraController = cameraRef;
        turnText = turnTextRef;
        SubscribeLauncherEvents();
    }

    private void InitializeReferences()
    {
        if (launcher == null && playerTransform != null)
            launcher = playerTransform.GetComponent<AchillesProjectileDebugLauncher>();

        if (playerMotor == null && playerTransform != null)
            playerMotor = playerTransform.GetComponent<GroundedActorMotor2D>();

        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<Level01CameraController>();
    }

    private void SubscribeLauncherEvents()
    {
        if (launcher != null)
        {
            launcher.ThrowStarted -= OnThrowStarted;
            launcher.ProjectileLaunched -= OnProjectileLaunched;
            launcher.ProjectileCompleted -= OnProjectileCompleted;

            launcher.ThrowStarted += OnThrowStarted;
            launcher.ProjectileLaunched += OnProjectileLaunched;
            launcher.ProjectileCompleted += OnProjectileCompleted;
        }
    }

    private void UnsubscribeLauncherEvents()
    {
        if (launcher != null)
        {
            launcher.ThrowStarted -= OnThrowStarted;
            launcher.ProjectileLaunched -= OnProjectileLaunched;
            launcher.ProjectileCompleted -= OnProjectileCompleted;
        }
    }

    private void OnThrowStarted()
    {
        if (currentState == Level01TurnState.PlayerTurn)
        {
            EnterState(Level01TurnState.PlayerThrowing);
        }
    }

    private void OnProjectileLaunched()
    {
        if (currentState == Level01TurnState.PlayerThrowing)
        {
            EnterState(Level01TurnState.ProjectileFlight);
        }
    }

    private void OnProjectileCompleted()
    {
        if (currentState == Level01TurnState.ProjectileFlight || currentState == Level01TurnState.PlayerThrowing)
        {
            EnterState(Level01TurnState.ResolutionDelay);
        }
    }

    private void EnterState(Level01TurnState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        switch (currentState)
        {
            case Level01TurnState.PlayerTurn:
                UpdateTurnHUD("PLAYER TURN");
                if (launcher != null)
                    launcher.SetTurnInputEnabled(true);
                else if (playerMotor != null)
                    playerMotor.SetMovementEnabled(true);

                if (cameraController != null && playerTransform != null)
                    cameraController.FocusActor(playerTransform);
                break;

            case Level01TurnState.PlayerThrowing:
                // Disable further turn input so player cannot move or initiate another throw
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);
                break;

            case Level01TurnState.ProjectileFlight:
                // Projectile is in flight. Launcher and camera controller manage projectile follow.
                break;

            case Level01TurnState.ResolutionDelay:
                // Wait for existing camera impact hold (0.6s) to settle without competing camera calls
                break;

            case Level01TurnState.CyclopsTurn:
                UpdateTurnHUD("CYCLOPS TURN");
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);

                if (cameraController != null && cyclopsTransform != null)
                    cameraController.FocusActor(cyclopsTransform);
                break;

            case Level01TurnState.TurnTransition:
                // Blend camera back to Achilles ahead of next turn
                if (cameraController != null && playerTransform != null)
                    cameraController.FocusActor(playerTransform);
                break;
        }
    }

    private void UpdateTurnHUD(string label)
    {
        if (turnText != null)
        {
            turnText.text = label;
        }
    }
}
