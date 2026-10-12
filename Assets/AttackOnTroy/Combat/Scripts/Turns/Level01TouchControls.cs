using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Touch controls controller for Level 01 on Android and touch devices.
/// Manages hold-to-move (Left/Right), hold-to-aim (Up/Down), weapon selection (Spear/Bomb),
/// hold-to-charge/release-to-throw, multi-touch coordination, and visual active/dim states.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01TouchControls : MonoBehaviour
{
    [Header("Gameplay References")]
    [SerializeField] private AchillesProjectileDebugLauncher launcher;
    [SerializeField] private PlayerMovementDebugInput playerMovement;
    [SerializeField] private GroundedActorMotor2D playerMotor;
    [SerializeField] private Level01TurnManager turnManager;

    [Header("UI Canvas Group")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0.1f, 1f)] private float activeAlpha = 1.0f;
    [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.35f;

    [Header("Movement Buttons (Bottom-Left)")]
    [SerializeField] private TouchHoldButton moveLeftButton;
    [SerializeField] private TouchHoldButton moveRightButton;

    [Header("Aim & Action Buttons (Bottom-Right)")]
    [SerializeField] private TouchHoldButton aimUpButton;
    [SerializeField] private TouchHoldButton aimDownButton;
    [SerializeField] private TouchHoldButton throwButton;

    [Header("Weapon Selection (Bottom-Right)")]
    [SerializeField] private Button weaponSpearButton;
    [SerializeField] private Button weaponBombButton;
    [SerializeField] private Image spearSelectedIndicator;
    [SerializeField] private Image bombSelectedIndicator;
    [SerializeField] private Image weaponSlot1Icon;
    [SerializeField] private Image weaponSlot2Icon;
    [SerializeField] private Sprite spearIcon;
    [SerializeField] private Sprite bombIcon;
    [SerializeField] private Sprite arrowIcon;
    [SerializeField] private Sprite oilJarIcon;

    [Header("Special / Double Shot")]
    [SerializeField] private Button specialButton;
    [SerializeField] private TMPro.TextMeshProUGUI specialButtonText;
    [SerializeField] private Image specialButtonBackground;
    [SerializeField] private Image specialArmedIndicator;
    [SerializeField] private PlayerEnergySystem energySystem;

    private bool wasPlayerTurn;
    private bool wasCanAim;

    private void Awake()
    {
        InitializeReferences();
        HookButtonEvents();
    }

    private void Start()
    {
        UpdateWeaponButtonIcons();
        UpdateVisualStates(true);
    }

    public void Configure(
        AchillesProjectileDebugLauncher launcherRef,
        PlayerMovementDebugInput movementRef,
        GroundedActorMotor2D motorRef,
        Level01TurnManager turnRef,
        CanvasGroup groupRef,
        TouchHoldButton moveLeft,
        TouchHoldButton moveRight,
        TouchHoldButton aimUp,
        TouchHoldButton aimDown,
        TouchHoldButton throwBtn,
        Button spearBtn,
        Button bombBtn,
        Image spearSelect,
        Image bombSelect)
    {
        launcher = launcherRef;
        playerMovement = movementRef;
        playerMotor = motorRef;
        turnManager = turnRef;
        canvasGroup = groupRef;
        moveLeftButton = moveLeft;
        moveRightButton = moveRight;
        aimUpButton = aimUp;
        aimDownButton = aimDown;
        throwButton = throwBtn;
        weaponSpearButton = spearBtn;
        weaponBombButton = bombBtn;
        spearSelectedIndicator = spearSelect;
        bombSelectedIndicator = bombSelect;

        HookButtonEvents();
    }

    public void RebindPlayer(
        AchillesProjectileDebugLauncher newLauncher,
        PlayerMovementDebugInput newMovement,
        GroundedActorMotor2D newMotor)
    {
        launcher = newLauncher;
        playerMovement = newMovement;
        playerMotor = newMotor;
        UpdateWeaponButtonIcons();
        UpdateVisualStates(true);
    }

    public void UpdateWeaponButtonIcons()
    {
        if (launcher == null) return;
        bool isOdysseus = launcher.ProjectilePrefab != null &&
            launcher.ProjectilePrefab.name.IndexOf("Arrow", StringComparison.OrdinalIgnoreCase) >= 0;

        if (weaponSlot1Icon != null)
        {
            if (isOdysseus && arrowIcon != null)
                weaponSlot1Icon.sprite = arrowIcon;
            else if (!isOdysseus && spearIcon != null)
                weaponSlot1Icon.sprite = spearIcon;
        }

        if (weaponSlot2Icon != null)
        {
            if (isOdysseus && oilJarIcon != null)
                weaponSlot2Icon.sprite = oilJarIcon;
            else if (!isOdysseus && bombIcon != null)
                weaponSlot2Icon.sprite = bombIcon;
        }
    }

    private void InitializeReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (turnManager == null)
            turnManager = FindAnyObjectByType<Level01TurnManager>();

        if (weaponSlot1Icon == null && weaponSpearButton != null)
        {
            var iconTr = weaponSpearButton.transform.Find("WeaponIcon");
            if (iconTr != null) weaponSlot1Icon = iconTr.GetComponent<Image>();
        }

        if (weaponSlot2Icon == null && weaponBombButton != null)
        {
            var iconTr = weaponBombButton.transform.Find("WeaponIcon");
            if (iconTr != null) weaponSlot2Icon = iconTr.GetComponent<Image>();
        }

        if (launcher == null || !launcher.gameObject.activeInHierarchy)
            launcher = FindAnyObjectByType<AchillesProjectileDebugLauncher>();

        if (launcher != null)
        {
            if (playerMovement == null || !playerMovement.gameObject.activeInHierarchy)
                playerMovement = launcher.GetComponent<PlayerMovementDebugInput>();
            if (playerMotor == null || !playerMotor.gameObject.activeInHierarchy)
                playerMotor = launcher.GetComponent<GroundedActorMotor2D>();
        }
        else
        {
            var achilles = GameObject.Find("Player_Achilles");
            if (achilles != null && achilles.activeInHierarchy)
            {
                launcher = achilles.GetComponent<AchillesProjectileDebugLauncher>();
                playerMovement = achilles.GetComponent<PlayerMovementDebugInput>();
                playerMotor = achilles.GetComponent<GroundedActorMotor2D>();
            }
        }

        if (energySystem == null)
            energySystem = FindAnyObjectByType<PlayerEnergySystem>();
    }

    public void SetEnergySystem(PlayerEnergySystem system)
    {
        energySystem = system;
        UpdateVisualStates(true);
    }

    private void HookButtonEvents()
    {
        UnhookButtonEvents();

        if (moveLeftButton != null)
        {
            moveLeftButton.HoldStarted += OnMoveLeftStarted;
            moveLeftButton.HoldEnded += OnMoveLeftEnded;
        }

        if (moveRightButton != null)
        {
            moveRightButton.HoldStarted += OnMoveRightStarted;
            moveRightButton.HoldEnded += OnMoveRightEnded;
        }

        if (aimUpButton != null)
        {
            aimUpButton.HoldStarted += OnAimUpStarted;
            aimUpButton.HoldEnded += OnAimUpEnded;
        }

        if (aimDownButton != null)
        {
            aimDownButton.HoldStarted += OnAimDownStarted;
            aimDownButton.HoldEnded += OnAimDownEnded;
        }

        if (throwButton != null)
        {
            throwButton.HoldStarted += OnThrowHoldStarted;
            throwButton.ReleasedInside += OnThrowReleasedInside;
            throwButton.Cancelled += OnThrowCancelled;
        }

        if (weaponSpearButton != null)
        {
            weaponSpearButton.onClick.AddListener(SelectSpear);
        }

        if (weaponBombButton != null)
        {
            weaponBombButton.onClick.AddListener(SelectBomb);
        }

        if (specialButton != null)
        {
            specialButton.onClick.AddListener(OnSpecialButtonClicked);
        }
    }

    private void UnhookButtonEvents()
    {
        if (moveLeftButton != null)
        {
            moveLeftButton.HoldStarted -= OnMoveLeftStarted;
            moveLeftButton.HoldEnded -= OnMoveLeftEnded;
        }

        if (moveRightButton != null)
        {
            moveRightButton.HoldStarted -= OnMoveRightStarted;
            moveRightButton.HoldEnded -= OnMoveRightEnded;
        }

        if (aimUpButton != null)
        {
            aimUpButton.HoldStarted -= OnAimUpStarted;
            aimUpButton.HoldEnded -= OnAimUpEnded;
        }

        if (aimDownButton != null)
        {
            aimDownButton.HoldStarted -= OnAimDownStarted;
            aimDownButton.HoldEnded -= OnAimDownEnded;
        }

        if (throwButton != null)
        {
            throwButton.HoldStarted -= OnThrowHoldStarted;
            throwButton.ReleasedInside -= OnThrowReleasedInside;
            throwButton.Cancelled -= OnThrowCancelled;
        }

        if (weaponSpearButton != null)
        {
            weaponSpearButton.onClick.RemoveListener(SelectSpear);
        }

        if (weaponBombButton != null)
        {
            weaponBombButton.onClick.RemoveListener(SelectBomb);
        }

        if (specialButton != null)
        {
            specialButton.onClick.RemoveListener(OnSpecialButtonClicked);
        }
    }

    private void OnDestroy()
    {
        UnhookButtonEvents();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            ClearAllHeldInputs();
            return;
        }

        UpdateVisualStates(false);
    }

    private void UpdateVisualStates(bool force)
    {
        bool isPlayerTurn = turnManager != null ? turnManager.IsPlayerTurn : true;
        bool isBattleOver = turnManager != null && turnManager.IsBattleOver;
        bool canAim = launcher != null && launcher.CanAim;
        bool controlsActive = isPlayerTurn && !isBattleOver;

        if (controlsActive != wasPlayerTurn || canAim != wasCanAim || force)
        {
            wasPlayerTurn = controlsActive;
            wasCanAim = canAim;

            if (canvasGroup != null)
            {
                bool fullyInteractive = controlsActive && canAim;
                canvasGroup.alpha = fullyInteractive ? activeAlpha : disabledAlpha;
                canvasGroup.interactable = controlsActive;
                canvasGroup.blocksRaycasts = controlsActive;
            }

            if (!controlsActive || !canAim)
            {
                ClearAllHeldInputs();
            }
        }

        // Weapon Selection Visual Feedback
        if (launcher != null)
        {
            bool isSpear = launcher.SelectedWeapon == AchillesWeapon.Spear;
            bool isBomb = launcher.SelectedWeapon == AchillesWeapon.ClubBomb;

            if (spearSelectedIndicator != null && spearSelectedIndicator.gameObject.activeSelf != isSpear)
                spearSelectedIndicator.gameObject.SetActive(isSpear);

            if (bombSelectedIndicator != null && bombSelectedIndicator.gameObject.activeSelf != isBomb)
                bombSelectedIndicator.gameObject.SetActive(isBomb);

            bool canSelectWeapon = controlsActive && launcher.CanSelectWeapon;
            if (weaponSpearButton != null && weaponSpearButton.interactable != canSelectWeapon)
                weaponSpearButton.interactable = canSelectWeapon;

            bool canSelectBomb = canSelectWeapon && launcher.ClubBombPrefab != null;
            if (weaponBombButton != null && weaponBombButton.interactable != canSelectBomb)
                weaponBombButton.interactable = canSelectBomb;
        }

        // Special Button Visual Feedback
        if (specialButton != null && energySystem != null)
        {
            bool canUseSpecial = controlsActive && canAim && (energySystem.CanActivateSpecial || energySystem.IsSpecialArmed);
            if (specialButton.interactable != canUseSpecial)
                specialButton.interactable = canUseSpecial;

            if (energySystem.IsSpecialArmed)
            {
                if (specialButtonText != null)
                    specialButtonText.text = "CANCEL";
                if (specialButtonBackground != null)
                    specialButtonBackground.color = new Color(1.0f, 0.75f, 0.1f, 1.0f); // Bright Gold/Amber
                if (specialArmedIndicator != null && !specialArmedIndicator.gameObject.activeSelf)
                    specialArmedIndicator.gameObject.SetActive(true);
            }
            else if (energySystem.IsEnergyFull)
            {
                if (specialButtonText != null)
                    specialButtonText.text = "SPECIAL";
                if (specialButtonBackground != null)
                    specialButtonBackground.color = new Color(0.15f, 0.85f, 1.0f, 1.0f); // Bright Cyan
                if (specialArmedIndicator != null && specialArmedIndicator.gameObject.activeSelf)
                    specialArmedIndicator.gameObject.SetActive(false);
            }
            else
            {
                if (specialButtonText != null)
                    specialButtonText.text = $"SP\n{Mathf.RoundToInt(energySystem.CurrentEnergy)}%";
                if (specialButtonBackground != null)
                    specialButtonBackground.color = new Color(0.2f, 0.25f, 0.32f, 0.6f); // Dimmed slate
                if (specialArmedIndicator != null && specialArmedIndicator.gameObject.activeSelf)
                    specialArmedIndicator.gameObject.SetActive(false);
            }
        }
    }

    private void OnSpecialButtonClicked()
    {
        if (energySystem != null)
        {
            energySystem.ToggleSpecial();
        }
    }

    private void OnMoveLeftStarted() => SetMoveInput(-1f);
    private void OnMoveLeftEnded() => SetMoveInput(0f);
    private void OnMoveRightStarted() => SetMoveInput(1f);
    private void OnMoveRightEnded() => SetMoveInput(0f);

    private void SetMoveInput(float value)
    {
        if (playerMovement != null)
            playerMovement.SetExternalMoveInput(value);
        else if (playerMotor != null)
            playerMotor.SetMoveInput(value);
    }

    private void OnAimUpStarted()
    {
        if (launcher != null)
        {
            launcher.NudgeAimAngle(1f);
            launcher.SetAimInput(1f);
        }
    }

    private void OnAimUpEnded() => SetAimInput(0f);

    private void OnAimDownStarted()
    {
        if (launcher != null)
        {
            launcher.NudgeAimAngle(-1f);
            launcher.SetAimInput(-1f);
        }
    }

    private void OnAimDownEnded() => SetAimInput(0f);

    private void SetAimInput(float value)
    {
        if (launcher != null)
            launcher.SetAimInput(value);
    }

    private void OnThrowHoldStarted()
    {
        if (launcher != null && launcher.CanAim)
            launcher.StartCharging();
    }

    private void OnThrowReleasedInside()
    {
        if (launcher != null && launcher.IsCharging)
            launcher.ReleaseChargeAndThrow();
    }

    private void OnThrowCancelled()
    {
        if (launcher != null && launcher.IsCharging)
            launcher.CancelCharging();
    }

    private void SelectSpear()
    {
        if (launcher != null && launcher.CanSelectWeapon)
            launcher.TrySelectWeapon(AchillesWeapon.Spear);
    }

    private void SelectBomb()
    {
        if (launcher != null && launcher.CanSelectWeapon)
            launcher.TrySelectWeapon(AchillesWeapon.ClubBomb);
    }

    public void ClearAllHeldInputs()
    {
        if (moveLeftButton != null && moveLeftButton.IsHeld)
            moveLeftButton.ReleaseHold(true);

        if (moveRightButton != null && moveRightButton.IsHeld)
            moveRightButton.ReleaseHold(true);

        if (aimUpButton != null && aimUpButton.IsHeld)
            aimUpButton.ReleaseHold(true);

        if (aimDownButton != null && aimDownButton.IsHeld)
            aimDownButton.ReleaseHold(true);

        if (throwButton != null && throwButton.IsHeld)
            throwButton.ReleaseHold(true);

        SetMoveInput(0f);
        SetAimInput(0f);

        if (launcher != null && launcher.IsCharging)
            launcher.CancelCharging();
    }

    private void OnDisable()
    {
        ClearAllHeldInputs();
    }
}
