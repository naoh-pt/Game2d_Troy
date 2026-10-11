using UnityEngine;

/// <summary>
/// Manages hero selection in Level 01 Beach.
/// At scene startup, activates the selected hero (Achilles vs. Odysseus)
/// based on BattleSessionData.SelectedHero, deactivates the other hero,
/// and rebinds all Level 01 systems (TurnManager, CameraController,
/// Cyclops AI, TouchControls, Minimap, and Battle UI).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class Level01HeroManager : MonoBehaviour
{
    [Header("Hero Roots")]
    [SerializeField] private GameObject achillesRoot;
    [SerializeField] private GameObject odysseusRoot;

    [Header("Scene Systems to Rebind")]
    [SerializeField] private Level01TurnManager turnManager;
    [SerializeField] private Level01CameraController cameraController;
    [SerializeField] private CyclopsNormalTurnController cyclopsController;
    [SerializeField] private Level01TouchControls touchControls;
    [SerializeField] private Level01Minimap minimap;
    [SerializeField] private Level01BattleUI battleUI;
    [SerializeField] private ProjectileAimHUD aimHUD;
    [SerializeField] private AimGuideRenderer aimGuide;
    [SerializeField] private PlayerEnergySystem energySystem;

    public GameObject AchillesRoot => achillesRoot;
    public GameObject OdysseusRoot => odysseusRoot;

    private void Awake()
    {
        InitializeHero();
    }

    public void InitializeHero()
    {
        bool isOdysseus = BattleSessionData.IsOdysseus;

        // Auto-resolve hero roots if unassigned
        if (achillesRoot == null)
            achillesRoot = GameObject.Find("Player_Achilles");

        if (odysseusRoot == null)
        {
            var tr = transform.Find("Player_Odysseus");
            odysseusRoot = tr != null ? tr.gameObject : GameObject.Find("Player_Odysseus");
        }

        // Auto-resolve systems if unassigned
        if (turnManager == null) turnManager = FindAnyObjectByType<Level01TurnManager>();
        if (cameraController == null && Camera.main != null) cameraController = Camera.main.GetComponent<Level01CameraController>();
        if (cyclopsController == null) cyclopsController = FindAnyObjectByType<CyclopsNormalTurnController>();
        if (touchControls == null) touchControls = FindAnyObjectByType<Level01TouchControls>();
        if (minimap == null) minimap = FindAnyObjectByType<Level01Minimap>();
        if (battleUI == null) battleUI = FindAnyObjectByType<Level01BattleUI>();
        if (aimHUD == null) aimHUD = FindAnyObjectByType<ProjectileAimHUD>();
        if (aimGuide == null) aimGuide = FindAnyObjectByType<AimGuideRenderer>();

        // Only the selected character should be active in the battle
        if (achillesRoot != null)
            achillesRoot.SetActive(!isOdysseus);

        if (odysseusRoot != null)
            odysseusRoot.SetActive(isOdysseus);

        GameObject activeHero = isOdysseus ? odysseusRoot : achillesRoot;
        if (activeHero == null)
        {
            Debug.LogError("[Level01HeroManager] Active hero GameObject is null!");
            return;
        }

        Transform heroTransform = activeHero.transform;
        GroundedActorMotor2D motor = activeHero.GetComponent<GroundedActorMotor2D>();
        PlayerMovementDebugInput input = activeHero.GetComponent<PlayerMovementDebugInput>();
        AchillesProjectileDebugLauncher launcher = activeHero.GetComponent<AchillesProjectileDebugLauncher>();
        ActorDamageReceiver2D damage = activeHero.GetComponent<ActorDamageReceiver2D>();

        if (turnManager != null)
        {
            turnManager.SetPlayerReferences(heroTransform, motor, launcher, damage);
        }

        if (cameraController != null)
        {
            Transform enemyTr = cyclopsController != null ? cyclopsController.transform : null;
            cameraController.ConfigureTargets(heroTransform, enemyTr);
            cameraController.FocusActor(heroTransform, immediate: true);
        }

        if (cyclopsController != null)
        {
            cyclopsController.SetTarget(heroTransform, launcher);
        }

        if (touchControls != null)
        {
            touchControls.RebindPlayer(launcher, input, motor);
        }

        if (minimap != null)
        {
            minimap.SetPlayerTransform(heroTransform);
        }

        if (aimHUD != null)
        {
            aimHUD.SetLauncher(launcher);
        }

        if (aimGuide != null && launcher != null)
        {
            aimGuide.Configure(launcher, launcher.SpawnPoint);
        }

        if (battleUI != null)
        {
            battleUI.SetPlayerName(isOdysseus ? "ODYSSEUS" : "ACHILLES");
            if (damage != null)
            {
                battleUI.UpdatePlayerHealth(damage.CurrentHealth, damage.MaximumHealth);
            }
        }

        if (energySystem == null)
            energySystem = FindAnyObjectByType<PlayerEnergySystem>();

        if (energySystem != null)
        {
            energySystem.SetPlayer(damage, launcher);
            energySystem.ResetEnergy();
        }

        Debug.Log($"[Level01HeroManager] Selected hero: {(isOdysseus ? "Odysseus" : "Achilles")} initialized successfully.");
    }
}
