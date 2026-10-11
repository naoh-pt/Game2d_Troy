using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public enum Level01TurnState
{
    PlayerTurn = 0,
    PlayerThrowing = 1,
    ProjectileFlight = 2,
    CyclopsPreparation = 3,
    CyclopsTurn = 4,
    TurnTransition = 5,
    BattleOver = 6,
    Dialogue = 7
}

/// <summary>
/// Turn sequencer for Level 01.
/// Manages player vs. enemy turn progression, input gating, HUD state labels,
/// camera focus transitions, turn pacing pauses, and health/battle result (WIN/LOSE) resolution.
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
    [SerializeField] private GroundedActorMotor2D cyclopsMotor;
    [SerializeField] private CyclopsNormalTurnController cyclopsController;
    [SerializeField] private ActorDamageReceiver2D playerDamageReceiver;
    [SerializeField] private ActorDamageReceiver2D cyclopsDamageReceiver;

    [Header("Camera & UI References")]
    [SerializeField] private Level01CameraController cameraController;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private Level01BattleUI battleUI;

    [Header("Dialogue System")]
    [SerializeField] private LevelDialogueData dialogueData;
    [SerializeField] private DialoguePanelUI dialogueUI;

    [Header("Sequence Timers")]
    [Tooltip("Configurable pause after Achilles' projectile finishes resolving before Cyclops begins action. Focuses Cyclops and shows CYCLOPS TURN.")]
    [FormerlySerializedAs("resolutionDelayDuration")]
    [SerializeField, Range(0.5f, 3.0f)] private float cyclopsTurnDelay = 1.5f;
    [SerializeField, Range(0.5f, 15.0f)] private float cyclopsTurnDuration = 10.0f;
    [SerializeField, Range(0.1f, 1.0f)] private float turnTransitionDuration = 0.4f;
    [Tooltip("Configurable pause after lethal hit before showing WIN/LOSE result UI, allowing the death animation to play.")]
    [SerializeField, Range(0.5f, 3.0f)] private float battleResultDelay = 1.0f;

    private float stateTimer;
    private bool isBattleOver;
    private bool hasPlayedOpeningDialogue;
    private int enemyTurnCount;
    private readonly System.Collections.Generic.HashSet<string> playedEnemyDialogueIds = new System.Collections.Generic.HashSet<string>();

    public Level01TurnState CurrentState => currentState;
    public bool IsPlayerTurn => currentState == Level01TurnState.PlayerTurn && !isBattleOver;
    public bool IsBattleOver => isBattleOver;
    public LevelDialogueData DialogueData => dialogueData;
    public DialoguePanelUI DialogueUI => dialogueUI;
    public float CyclopsTurnDelay
    {
        get => cyclopsTurnDelay;
        set => cyclopsTurnDelay = Mathf.Max(0.1f, value);
    }
    public float BattleResultDelay
    {
        get => battleResultDelay;
        set => battleResultDelay = Mathf.Max(0.1f, value);
    }

    public event Action<bool> BattleEnded;

    private void Awake()
    {
        InitializeReferences();
    }

    private void Start()
    {
        InitializeHealthDisplay();

        if (dialogueUI == null)
            dialogueUI = FindAnyObjectByType<DialoguePanelUI>(FindObjectsInactive.Include);

        Debug.Log($"[Level01TurnManager] Start: dialogueData={(dialogueData != null ? dialogueData.name : "null")}, dialogueUI={(dialogueUI != null ? dialogueUI.name : "null")}");

        if (dialogueData != null && dialogueUI != null && !hasPlayedOpeningDialogue)
        {
            string heroName = BattleSessionData.SelectedHero;
            DialogueLine[] openingLines = dialogueData.GetOpeningLines(heroName);
            if (openingLines != null && openingLines.Length > 0)
            {
                hasPlayedOpeningDialogue = true;
                Debug.Log($"[Level01TurnManager] Starting opening dialogue with {openingLines.Length} lines for hero: {heroName}");
                EnterState(Level01TurnState.Dialogue);
                dialogueUI.ShowSequence(openingLines, OnOpeningDialogueCompleted, OnDialogueLineDisplayed);
                return;
            }
        }

        EnterState(Level01TurnState.PlayerTurn);
    }

    private void OnOpeningDialogueCompleted()
    {
        Debug.Log("[Level01TurnManager] Opening dialogue completed. Transitioning to PlayerTurn.");
        if (isBattleOver)
            return;

        if (cameraController != null && playerTransform != null)
            cameraController.FocusActor(playerTransform);

        EnterState(Level01TurnState.PlayerTurn);
    }

    private void OnDialogueLineDisplayed(DialogueLine line, int lineIndex)
    {
        if (cameraController == null)
            return;

        if (IsEnemySpeaker(line.speakerName))
        {
            if (cyclopsTransform != null)
                cameraController.FocusActor(cyclopsTransform);
        }
        else
        {
            if (playerTransform != null)
                cameraController.FocusActor(playerTransform);
        }
    }

    private bool IsEnemySpeaker(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName))
            return false;

        string enemyId = cyclopsController != null ? cyclopsController.EnemyId : "CyclopsNormal";
        return speakerName.IndexOf("Cyclops", StringComparison.OrdinalIgnoreCase) >= 0
            || speakerName.IndexOf(enemyId, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnEnable()
    {
        SubscribeLauncherEvents();
        SubscribeCyclopsEvents();
        SubscribeDamageEvents();
    }

    private void OnDisable()
    {
        UnsubscribeLauncherEvents();
        UnsubscribeCyclopsEvents();
        UnsubscribeDamageEvents();
    }

    private void Update()
    {
        if (isBattleOver)
            return;

        switch (currentState)
        {
            case Level01TurnState.CyclopsPreparation:
                stateTimer += Time.deltaTime;
                if (stateTimer >= cyclopsTurnDelay)
                {
                    TryTriggerEnemyDialogueOrStartTurn();
                }
                break;

            case Level01TurnState.CyclopsTurn:
                stateTimer += Time.deltaTime;
                // Master timeout ensures the turn never deadlocks if an external event is missed
                float maxTimeout = Mathf.Max(cyclopsTurnDuration, 12f);
                if (stateTimer >= maxTimeout)
                {
                    Debug.LogWarning("[Level01TurnManager] Cyclops turn timeout reached. Finishing Cyclops turn.");
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

    private void TryTriggerEnemyDialogueOrStartTurn()
    {
        if (isBattleOver)
            return;

        string enemyId = cyclopsController != null ? cyclopsController.EnemyId : "CyclopsNormal";
        DialogueLine[] preAttackLines = dialogueData != null ? dialogueData.GetEnemyTurnTauntLines(enemyId, enemyTurnCount) : null;
        enemyTurnCount++;

        if (preAttackLines != null && preAttackLines.Length > 0 && dialogueUI != null)
        {
            EnterState(Level01TurnState.Dialogue);
            dialogueUI.ShowSequence(preAttackLines, () =>
            {
                if (!isBattleOver)
                {
                    EnterState(Level01TurnState.CyclopsTurn);
                }
            }, OnDialogueLineDisplayed);
            return;
        }

        EnterState(Level01TurnState.CyclopsTurn);
    }

    public void Configure(
        AchillesProjectileDebugLauncher launcherRef,
        Transform playerRef,
        Transform cyclopsRef,
        GroundedActorMotor2D motorRef,
        Level01CameraController cameraRef,
        TextMeshProUGUI turnTextRef,
        CyclopsNormalTurnController cyclopsControllerRef = null,
        Level01BattleUI battleUIRef = null,
        ActorDamageReceiver2D playerDamageRef = null,
        ActorDamageReceiver2D cyclopsDamageRef = null,
        GroundedActorMotor2D cyclopsMotorRef = null,
        float? resultDelay = null,
        LevelDialogueData dialogueDataRef = null,
        DialoguePanelUI dialogueUIRef = null)
    {
        UnsubscribeLauncherEvents();
        UnsubscribeCyclopsEvents();
        UnsubscribeDamageEvents();

        launcher = launcherRef;
        playerTransform = playerRef;
        cyclopsTransform = cyclopsRef;
        playerMotor = motorRef;
        cameraController = cameraRef;
        turnText = turnTextRef;

        if (cyclopsControllerRef != null)
            cyclopsController = cyclopsControllerRef;
        else if (cyclopsTransform != null)
            cyclopsController = cyclopsTransform.GetComponent<CyclopsNormalTurnController>();

        if (battleUIRef != null)
            battleUI = battleUIRef;

        if (playerDamageRef != null)
            playerDamageReceiver = playerDamageRef;
        else if (playerTransform != null)
            playerDamageReceiver = playerTransform.GetComponent<ActorDamageReceiver2D>();

        if (cyclopsDamageRef != null)
            cyclopsDamageReceiver = cyclopsDamageRef;
        else if (cyclopsTransform != null)
            cyclopsDamageReceiver = cyclopsTransform.GetComponent<ActorDamageReceiver2D>();

        if (cyclopsMotorRef != null)
            cyclopsMotor = cyclopsMotorRef;
        else if (cyclopsTransform != null)
            cyclopsMotor = cyclopsTransform.GetComponent<GroundedActorMotor2D>();

        if (dialogueDataRef != null)
            dialogueData = dialogueDataRef;

        if (dialogueUIRef != null)
            dialogueUI = dialogueUIRef;

        if (resultDelay.HasValue)
            battleResultDelay = Mathf.Max(0.1f, resultDelay.Value);

        enemyTurnCount = 0;
        hasPlayedOpeningDialogue = false;

        SubscribeLauncherEvents();
        SubscribeCyclopsEvents();
        SubscribeDamageEvents();
    }

    public void SetPlayerReferences(
        Transform newPlayerTransform,
        GroundedActorMotor2D newPlayerMotor,
        AchillesProjectileDebugLauncher newLauncher,
        ActorDamageReceiver2D newPlayerDamage)
    {
        UnsubscribeLauncherEvents();
        if (playerDamageReceiver != null)
            playerDamageReceiver.HealthChanged -= OnPlayerHealthChanged;

        playerTransform = newPlayerTransform;
        playerMotor = newPlayerMotor;
        launcher = newLauncher;
        playerDamageReceiver = newPlayerDamage;

        SubscribeLauncherEvents();
        if (playerDamageReceiver != null)
        {
            playerDamageReceiver.HealthChanged -= OnPlayerHealthChanged;
            playerDamageReceiver.HealthChanged += OnPlayerHealthChanged;
        }

        if (battleUI != null && playerDamageReceiver != null)
        {
            battleUI.UpdatePlayerHealth(playerDamageReceiver.CurrentHealth, playerDamageReceiver.MaximumHealth);
        }
    }

    private void InitializeReferences()
    {
        if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
        {
            var activeLauncher = FindAnyObjectByType<AchillesProjectileDebugLauncher>();
            if (activeLauncher != null)
            {
                playerTransform = activeLauncher.transform;
                launcher = activeLauncher;
            }
        }

        if (launcher == null && playerTransform != null)
            launcher = playerTransform.GetComponent<AchillesProjectileDebugLauncher>();

        if (playerMotor == null && playerTransform != null)
            playerMotor = playerTransform.GetComponent<GroundedActorMotor2D>();

        if (playerDamageReceiver == null && playerTransform != null)
            playerDamageReceiver = playerTransform.GetComponent<ActorDamageReceiver2D>();

        if (cyclopsController == null && cyclopsTransform != null)
            cyclopsController = cyclopsTransform.GetComponent<CyclopsNormalTurnController>();

        if (cyclopsMotor == null && cyclopsTransform != null)
            cyclopsMotor = cyclopsTransform.GetComponent<GroundedActorMotor2D>();

        if (cyclopsDamageReceiver == null && cyclopsTransform != null)
            cyclopsDamageReceiver = cyclopsTransform.GetComponent<ActorDamageReceiver2D>();

        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<Level01CameraController>();

        if (battleUI == null)
            battleUI = FindAnyObjectByType<Level01BattleUI>();

        if (dialogueUI == null)
            dialogueUI = FindAnyObjectByType<DialoguePanelUI>(FindObjectsInactive.Include);
    }

    private void InitializeHealthDisplay()
    {
        if (battleUI == null)
            return;

        if (playerDamageReceiver != null)
            battleUI.UpdatePlayerHealth(playerDamageReceiver.CurrentHealth, playerDamageReceiver.MaximumHealth);

        if (cyclopsDamageReceiver != null)
            battleUI.UpdateCyclopsHealth(cyclopsDamageReceiver.CurrentHealth, cyclopsDamageReceiver.MaximumHealth);
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

    private void SubscribeCyclopsEvents()
    {
        if (cyclopsController != null)
        {
            cyclopsController.TurnCompleted -= OnCyclopsTurnCompleted;
            cyclopsController.TurnCompleted += OnCyclopsTurnCompleted;
        }
    }

    private void UnsubscribeCyclopsEvents()
    {
        if (cyclopsController != null)
        {
            cyclopsController.TurnCompleted -= OnCyclopsTurnCompleted;
        }
    }

    private void SubscribeDamageEvents()
    {
        if (playerDamageReceiver != null)
        {
            playerDamageReceiver.HealthChanged -= OnPlayerHealthChanged;
            playerDamageReceiver.HealthChanged += OnPlayerHealthChanged;
        }
        if (cyclopsDamageReceiver != null)
        {
            cyclopsDamageReceiver.HealthChanged -= OnCyclopsHealthChanged;
            cyclopsDamageReceiver.HealthChanged += OnCyclopsHealthChanged;
        }
    }

    private void UnsubscribeDamageEvents()
    {
        if (playerDamageReceiver != null)
            playerDamageReceiver.HealthChanged -= OnPlayerHealthChanged;
        if (cyclopsDamageReceiver != null)
            cyclopsDamageReceiver.HealthChanged -= OnCyclopsHealthChanged;
    }

    private void OnPlayerHealthChanged(float current, float max)
    {
        if (battleUI != null)
            battleUI.UpdatePlayerHealth(current, max);

        if (current <= 0f && !isBattleOver)
        {
            EndBattle(false);
        }
    }

    private void OnCyclopsHealthChanged(float current, float max)
    {
        if (battleUI != null)
            battleUI.UpdateCyclopsHealth(current, max);

        if (current <= 0f && !isBattleOver)
        {
            EndBattle(true);
        }
    }

    /// <summary>
    /// Immediately concludes the battle on a lethal hit, taking strict priority
    /// over pending turn timers and projectile flight/callbacks.
    /// Locks movement, aiming, charging, weapon selection, and further attacks.
    /// </summary>
    public void EndBattle(bool isWin)
    {
        if (isBattleOver)
            return;

        isBattleOver = true;
        currentState = Level01TurnState.BattleOver;

        // Lock Achilles: aiming, charging, weapon selection, horizontal motor
        if (launcher != null)
            launcher.SetTurnInputEnabled(false);
        if (playerMotor != null)
        {
            playerMotor.SetMovementEnabled(false);
            playerMotor.SetMoveInput(0f);
        }

        // Lock Cyclops: disable turn controller and horizontal motor
        if (cyclopsController != null)
            cyclopsController.enabled = false;
        if (cyclopsMotor != null)
        {
            cyclopsMotor.SetMovementEnabled(false);
            cyclopsMotor.SetMoveInput(0f);
        }

        // Disable further collision damage on the defeated actor
        if (isWin && cyclopsDamageReceiver != null)
        {
            cyclopsDamageReceiver.enabled = false;
        }
        else if (!isWin && playerDamageReceiver != null)
        {
            playerDamageReceiver.enabled = false;
        }

        // Focus camera on the defeated actor so death animation is clearly visible
        if (cameraController != null)
        {
            if (isWin && cyclopsTransform != null)
                cameraController.FocusActor(cyclopsTransform);
            else if (!isWin && playerTransform != null)
                cameraController.FocusActor(playerTransform);
        }

        // Ensure death animation trigger is set on the defeated actor
        if (isWin && cyclopsDamageReceiver != null)
            cyclopsDamageReceiver.TriggerDeathAnimation();
        else if (!isWin && playerDamageReceiver != null)
            playerDamageReceiver.TriggerDeathAnimation();

        UpdateTurnHUD(isWin ? "VICTORY" : "DEFEAT");

        // Delayed display of result panel so death animation can be seen
        StartCoroutine(ShowResultDelayedRoutine(isWin, battleResultDelay));
        Debug.Log($"[Level01TurnManager] Battle ending with result: {(isWin ? "WIN" : "LOSE")} (Result delay: {battleResultDelay:F1}s)");
    }

    private System.Collections.IEnumerator ShowResultDelayedRoutine(bool isWin, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (isWin)
        {
            string heroName = BattleSessionData.SelectedHero;
            DialogueLine[] victoryLines = dialogueData != null ? dialogueData.GetVictoryLines(heroName) : null;
            if (victoryLines != null && victoryLines.Length > 0 && dialogueUI != null)
            {
                bool victoryDone = false;
                dialogueUI.ShowSequence(victoryLines, () =>
                {
                    victoryDone = true;
                }, OnDialogueLineDisplayed);

                while (!victoryDone)
                {
                    yield return null;
                }
            }
        }

        if (battleUI != null)
            battleUI.ShowResult(isWin);

        BattleEnded?.Invoke(isWin);
        Debug.Log($"[Level01TurnManager] Battle result UI shown: {(isWin ? "WIN" : "LOSE")}");
    }

    private void OnThrowStarted()
    {
        if (isBattleOver)
            return;

        if (currentState == Level01TurnState.PlayerTurn)
        {
            EnterState(Level01TurnState.PlayerThrowing);
        }
    }

    private void OnProjectileLaunched()
    {
        if (isBattleOver)
            return;

        if (currentState == Level01TurnState.PlayerThrowing)
        {
            EnterState(Level01TurnState.ProjectileFlight);
        }
    }

    private void OnProjectileCompleted()
    {
        if (isBattleOver)
            return;

        // After Achilles projectile resolves (actor hit, terrain hit, timeout, or bounds),
        // enter the Cyclops preparation pause. A lethal hit already bypassed this path.
        if (currentState == Level01TurnState.ProjectileFlight || currentState == Level01TurnState.PlayerThrowing)
        {
            EnterState(Level01TurnState.CyclopsPreparation);
        }
    }

    private void OnCyclopsTurnCompleted()
    {
        if (isBattleOver)
            return;

        if (currentState == Level01TurnState.CyclopsTurn)
        {
            EnterState(Level01TurnState.TurnTransition);
        }
    }

    private void EnterState(Level01TurnState newState)
    {
        if (isBattleOver && newState != Level01TurnState.BattleOver)
            return;

        currentState = newState;
        stateTimer = 0f;

        switch (currentState)
        {
            case Level01TurnState.Dialogue:
                // Disable turn input while dialogue is active
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);
                if (playerMotor != null)
                {
                    playerMotor.SetMovementEnabled(false);
                    playerMotor.SetMoveInput(0f);
                }
                break;

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
                // Disable turn input while throwing
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);
                break;

            case Level01TurnState.ProjectileFlight:
                break;

            case Level01TurnState.CyclopsPreparation:
                // Configurable pause (1.5s): display CYCLOPS TURN and focus camera on Cyclops.
                // Cyclops does NOT act until this pause completes.
                UpdateTurnHUD("CYCLOPS TURN");
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);

                if (cameraController != null && cyclopsTransform != null)
                    cameraController.FocusActor(cyclopsTransform);
                break;

            case Level01TurnState.CyclopsTurn:
                UpdateTurnHUD("CYCLOPS TURN");
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);

                // Start Cyclops action only after preparation pause has elapsed
                if (cyclopsController != null)
                    cyclopsController.BeginTurn();
                break;

            case Level01TurnState.TurnTransition:
                // Blend camera back to Achilles ahead of next player turn
                if (cameraController != null && playerTransform != null)
                    cameraController.FocusActor(playerTransform);
                break;

            case Level01TurnState.BattleOver:
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
