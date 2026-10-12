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

    [Header("Secondary Enemy References (Level 03+)")]
    [SerializeField] private Transform cyclops2Transform;
    [SerializeField] private GroundedActorMotor2D cyclops2Motor;
    [SerializeField] private CyclopsNormalTurnController cyclops2Controller;
    [SerializeField] private ActorDamageReceiver2D cyclops2DamageReceiver;

    [Header("Camera & UI References")]
    [SerializeField] private Level01CameraController cameraController;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private Level01BattleUI battleUI;
    [SerializeField] private PlayerEnergySystem energySystem;

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
    private int enemyTurnCycleIndex = 0; // Alternates between 0 (Enemy 1) and 1 (Enemy 2)
    private CyclopsNormalTurnController currentActingEnemyController;
    private Transform currentActingEnemyTransform;
    private readonly System.Collections.Generic.HashSet<string> playedEnemyDialogueIds = new System.Collections.Generic.HashSet<string>();

    // Hit streak limiter (shared across Normal & Heavy)
    private static int consecutiveEnemyHits = 0;
    private bool playerDamagedDuringCurrentEnemyTurn = false;
    private bool hasUsedPlayerSelfHeal = false;

    public static int ConsecutiveEnemyHits => consecutiveEnemyHits;
    public static bool MustEnemyMissNext => consecutiveEnemyHits >= 2;
    public bool HasUsedPlayerSelfHeal => hasUsedPlayerSelfHeal;

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

    public bool IsEnemy1Alive => cyclopsDamageReceiver != null && !cyclopsDamageReceiver.IsDead;
    public bool IsEnemy2Alive => cyclops2DamageReceiver != null && !cyclops2DamageReceiver.IsDead;
    public bool AreAllEnemiesDead
    {
        get
        {
            if (cyclopsDamageReceiver != null && !cyclopsDamageReceiver.IsDead) return false;
            if (cyclops2DamageReceiver != null && !cyclops2DamageReceiver.IsDead) return false;
            return true;
        }
    }

    public event Action<bool> BattleEnded;

    private void Awake()
    {
        consecutiveEnemyHits = 0;
        playerDamagedDuringCurrentEnemyTurn = false;
        hasUsedPlayerSelfHeal = false;
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
            Transform targetEnemyTr = GetEnemyTransformForSpeaker(line.speakerName);
            if (targetEnemyTr != null)
                cameraController.FocusActor(targetEnemyTr);
            else if (cyclopsTransform != null)
                cameraController.FocusActor(cyclopsTransform);
        }
        else
        {
            if (playerTransform != null)
                cameraController.FocusActor(playerTransform);
        }
    }

    private Transform GetEnemyTransformForSpeaker(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName)) return cyclopsTransform;
        if (cyclops2Controller != null && cyclops2Transform != null)
        {
            if (speakerName.IndexOf(cyclops2Controller.EnemyId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                speakerName.IndexOf("Heavy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cyclops2Transform;
            }
        }
        if (currentActingEnemyTransform != null)
            return currentActingEnemyTransform;
        return cyclopsTransform != null ? cyclopsTransform : cyclops2Transform;
    }

    private bool IsEnemySpeaker(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName))
            return false;

        string enemy1Id = cyclopsController != null ? cyclopsController.EnemyId : "CyclopsNormal";
        string enemy2Id = cyclops2Controller != null ? cyclops2Controller.EnemyId : "CyclopsHeavy";

        return speakerName.IndexOf("Cyclops", StringComparison.OrdinalIgnoreCase) >= 0
            || speakerName.IndexOf(enemy1Id, StringComparison.OrdinalIgnoreCase) >= 0
            || speakerName.IndexOf(enemy2Id, StringComparison.OrdinalIgnoreCase) >= 0;
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
        if (isBattleOver || Time.timeScale <= 0f)
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

    private void SelectActiveEnemyForNextTurn()
    {
        bool enemy1Alive = IsEnemy1Alive;
        bool enemy2Alive = IsEnemy2Alive;

        if (enemy1Alive && enemy2Alive)
        {
            // Both alive: alternate between 0 and 1
            if (enemyTurnCycleIndex % 2 == 0)
            {
                currentActingEnemyController = cyclopsController;
                currentActingEnemyTransform = cyclopsTransform;
            }
            else
            {
                currentActingEnemyController = cyclops2Controller != null ? cyclops2Controller : cyclopsController;
                currentActingEnemyTransform = cyclops2Transform != null ? cyclops2Transform : cyclopsTransform;
            }
            enemyTurnCycleIndex++;
        }
        else if (enemy1Alive)
        {
            currentActingEnemyController = cyclopsController;
            currentActingEnemyTransform = cyclopsTransform;
        }
        else if (enemy2Alive)
        {
            currentActingEnemyController = cyclops2Controller;
            currentActingEnemyTransform = cyclops2Transform;
        }
        else
        {
            currentActingEnemyController = null;
            currentActingEnemyTransform = null;
        }
    }

    private void TryTriggerEnemyDialogueOrStartTurn()
    {
        if (isBattleOver)
            return;

        if (currentActingEnemyController == null)
            SelectActiveEnemyForNextTurn();

        if (currentActingEnemyController == null)
        {
            // All enemies dead or missing
            if (AreAllEnemiesDead)
            {
                EndBattle(true);
            }
            else
            {
                EnterState(Level01TurnState.TurnTransition);
            }
            return;
        }

        string enemyId = currentActingEnemyController.EnemyId;
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

        if (cyclops2DamageReceiver != null)
            battleUI.UpdateCyclops2Health(cyclops2DamageReceiver.CurrentHealth, cyclops2DamageReceiver.MaximumHealth);
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
        if (cyclops2Controller != null)
        {
            cyclops2Controller.TurnCompleted -= OnCyclopsTurnCompleted;
            cyclops2Controller.TurnCompleted += OnCyclopsTurnCompleted;
        }
    }

    private void UnsubscribeCyclopsEvents()
    {
        if (cyclopsController != null)
        {
            cyclopsController.TurnCompleted -= OnCyclopsTurnCompleted;
        }
        if (cyclops2Controller != null)
        {
            cyclops2Controller.TurnCompleted -= OnCyclopsTurnCompleted;
        }
    }

    private void SubscribeDamageEvents()
    {
        if (playerDamageReceiver != null)
        {
            playerDamageReceiver.HealthChanged -= OnPlayerHealthChanged;
            playerDamageReceiver.HealthChanged += OnPlayerHealthChanged;

            playerDamageReceiver.DamageApplied -= OnPlayerDamageAppliedForTurnStreak;
            playerDamageReceiver.DamageApplied += OnPlayerDamageAppliedForTurnStreak;
        }
        if (cyclopsDamageReceiver != null)
        {
            cyclopsDamageReceiver.HealthChanged -= OnCyclopsHealthChanged;
            cyclopsDamageReceiver.HealthChanged += OnCyclopsHealthChanged;
        }
        if (cyclops2DamageReceiver != null)
        {
            cyclops2DamageReceiver.HealthChanged -= OnCyclops2HealthChanged;
            cyclops2DamageReceiver.HealthChanged += OnCyclops2HealthChanged;
        }
    }

    private void UnsubscribeDamageEvents()
    {
        if (playerDamageReceiver != null)
        {
            playerDamageReceiver.HealthChanged -= OnPlayerHealthChanged;
            playerDamageReceiver.DamageApplied -= OnPlayerDamageAppliedForTurnStreak;
        }
        if (cyclopsDamageReceiver != null)
            cyclopsDamageReceiver.HealthChanged -= OnCyclopsHealthChanged;
        if (cyclops2DamageReceiver != null)
            cyclops2DamageReceiver.HealthChanged -= OnCyclops2HealthChanged;
    }

    private void OnPlayerDamageAppliedForTurnStreak(float actualHpLost, Vector2 hitPoint, GameObject source)
    {
        if (actualHpLost <= 0f) return;

        // Check if damage originated from an enemy axe during enemy preparation/turn
        if (currentState == Level01TurnState.CyclopsTurn || currentState == Level01TurnState.CyclopsPreparation)
        {
            Projectile2D proj = source != null ? source.GetComponent<Projectile2D>() : null;
            if (proj != null && proj.ImpactType == ProjectileImpactType.StoneAxe)
            {
                playerDamagedDuringCurrentEnemyTurn = true;
            }
            else if (source != null && (source == cyclopsTransform?.gameObject || source == cyclops2Transform?.gameObject || source.GetComponentInParent<CyclopsNormalTurnController>() != null))
            {
                playerDamagedDuringCurrentEnemyTurn = true;
            }
        }
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

        if (current <= 0f)
        {
            if (cyclopsController != null)
                cyclopsController.enabled = false;
            if (cyclopsMotor != null)
            {
                cyclopsMotor.SetMovementEnabled(false);
                cyclopsMotor.SetMoveInput(0f);
            }
            if (cyclopsDamageReceiver != null)
            {
                cyclopsDamageReceiver.enabled = false;
                cyclopsDamageReceiver.TriggerDeathAnimation();
            }

            if (AreAllEnemiesDead && !isBattleOver)
            {
                EndBattle(true);
            }
        }
    }

    private void OnCyclops2HealthChanged(float current, float max)
    {
        if (battleUI != null)
            battleUI.UpdateCyclops2Health(current, max);

        if (current <= 0f)
        {
            if (cyclops2Controller != null)
                cyclops2Controller.enabled = false;
            if (cyclops2Motor != null)
            {
                cyclops2Motor.SetMovementEnabled(false);
                cyclops2Motor.SetMoveInput(0f);
            }
            if (cyclops2DamageReceiver != null)
            {
                cyclops2DamageReceiver.enabled = false;
                cyclops2DamageReceiver.TriggerDeathAnimation();
            }

            if (AreAllEnemiesDead && !isBattleOver)
            {
                EndBattle(true);
            }
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
        if (energySystem != null && energySystem.IsSpecialArmed)
            energySystem.CancelSpecial();
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

        if (cyclops2Controller != null)
            cyclops2Controller.enabled = false;
        if (cyclops2Motor != null)
        {
            cyclops2Motor.SetMovementEnabled(false);
            cyclops2Motor.SetMoveInput(0f);
        }

        // Disable further collision damage on the defeated actor
        if (isWin)
        {
            if (cyclopsDamageReceiver != null)
                cyclopsDamageReceiver.enabled = false;
            if (cyclops2DamageReceiver != null)
                cyclops2DamageReceiver.enabled = false;
        }
        else if (!isWin && playerDamageReceiver != null)
        {
            playerDamageReceiver.enabled = false;
        }

        // Focus camera on the defeated actor so death animation is clearly visible
        if (cameraController != null)
        {
            if (isWin)
            {
                Transform targetToFocus = currentActingEnemyTransform != null ? currentActingEnemyTransform : (cyclopsTransform != null ? cyclopsTransform : cyclops2Transform);
                if (targetToFocus != null)
                    cameraController.FocusActor(targetToFocus);
            }
            else if (!isWin && playerTransform != null)
            {
                cameraController.FocusActor(playerTransform);
            }
        }

        // Ensure death animation trigger is set on the defeated actor
        if (isWin)
        {
            if (cyclopsDamageReceiver != null && cyclopsDamageReceiver.IsDead)
                cyclopsDamageReceiver.TriggerDeathAnimation();
            if (cyclops2DamageReceiver != null && cyclops2DamageReceiver.IsDead)
                cyclops2DamageReceiver.TriggerDeathAnimation();
        }
        else if (!isWin && playerDamageReceiver != null)
        {
            playerDamageReceiver.TriggerDeathAnimation();
        }

        UpdateTurnHUD(isWin ? "VICTORY" : "DEFEAT");

        // Delayed display of result panel so death animation can be seen
        if (isWin)
        {
            int currentLevel = GetCurrentLevelIndex();
            BattleSessionData.RecordLevelVictory(currentLevel);
        }

        StartCoroutine(ShowResultDelayedRoutine(isWin, battleResultDelay));
        Debug.Log($"[Level01TurnManager] Battle ending with result: {(isWin ? "WIN" : "LOSE")} (Result delay: {battleResultDelay:F1}s)");
    }

    private int GetCurrentLevelIndex()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.Contains("Level03") || sceneName.Contains("TroyWall"))
            return 3;
        if (sceneName.Contains("Level02") || sceneName.Contains("Scamander"))
            return 2;
        return 1;
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

        // After player projectile resolves, choose next active enemy and enter preparation pause.
        SelectActiveEnemyForNextTurn();

        if (currentActingEnemyController == null)
        {
            if (AreAllEnemiesDead)
            {
                EndBattle(true);
                return;
            }
        }

        if (currentState == Level01TurnState.ProjectileFlight || currentState == Level01TurnState.PlayerThrowing)
        {
            EnterState(Level01TurnState.CyclopsPreparation);
        }
    }

    private void OnCyclopsTurnCompleted()
    {
        if (isBattleOver)
            return;

        if (playerDamagedDuringCurrentEnemyTurn)
        {
            consecutiveEnemyHits++;
            Debug.Log($"[Level01TurnManager] Player was damaged by enemy this turn. Consecutive enemy hits: {consecutiveEnemyHits}");
        }
        else
        {
            consecutiveEnemyHits = 0;
            Debug.Log("[Level01TurnManager] Enemy missed player. Reset consecutive enemy hits to 0.");
        }
        playerDamagedDuringCurrentEnemyTurn = false;

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
                CheckAndApplyPlayerSelfHeal();
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
                // Configurable pause: display CYCLOPS TURN and focus camera on active enemy.
                playerDamagedDuringCurrentEnemyTurn = false;
                UpdateTurnHUD("CYCLOPS TURN");
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);

                Transform prepFocus = currentActingEnemyTransform != null ? currentActingEnemyTransform : (cyclopsTransform != null ? cyclopsTransform : cyclops2Transform);
                if (cameraController != null && prepFocus != null)
                    cameraController.FocusActor(prepFocus);
                break;

            case Level01TurnState.CyclopsTurn:
                playerDamagedDuringCurrentEnemyTurn = false;
                UpdateTurnHUD("CYCLOPS TURN");
                if (launcher != null)
                    launcher.SetTurnInputEnabled(false);

                // Start active Cyclops action only after preparation pause has elapsed
                if (currentActingEnemyController != null && !currentActingEnemyController.GetComponent<ActorDamageReceiver2D>().IsDead)
                {
                    currentActingEnemyController.BeginTurn();
                }
                else if (cyclopsController != null && !cyclopsController.GetComponent<ActorDamageReceiver2D>().IsDead)
                {
                    cyclopsController.BeginTurn();
                }
                else if (cyclops2Controller != null && !cyclops2Controller.GetComponent<ActorDamageReceiver2D>().IsDead)
                {
                    cyclops2Controller.BeginTurn();
                }
                else
                {
                    EnterState(Level01TurnState.TurnTransition);
                }
                break;

            case Level01TurnState.TurnTransition:
                // Clear acting enemy references so next cycle cleanly picks the next enemy
                currentActingEnemyController = null;
                currentActingEnemyTransform = null;
                // Blend camera back to Achilles ahead of next player turn
                if (cameraController != null && playerTransform != null)
                    cameraController.FocusActor(playerTransform);
                break;

            case Level01TurnState.BattleOver:
                break;
        }
    }

    private void CheckAndApplyPlayerSelfHeal()
    {
        if (hasUsedPlayerSelfHeal || playerDamageReceiver == null || playerDamageReceiver.IsDead)
            return;

        float maxHp = playerDamageReceiver.MaximumHealth;
        float currentHp = playerDamageReceiver.CurrentHealth;

        if (currentHp <= maxHp * 0.5f)
        {
            hasUsedPlayerSelfHeal = true;
            bool isOdysseus = BattleSessionData.IsOdysseus;
            float healAmount = isOdysseus ? 25f : 30f;
            playerDamageReceiver.Heal(healAmount);
            Debug.Log($"[Level01TurnManager] Player self-heal activated! Healed {healAmount:F0} HP. Current HP: {playerDamageReceiver.CurrentHealth:F1}/{maxHp:F1}");
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
