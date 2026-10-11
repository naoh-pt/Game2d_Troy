using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Screen HUD component managing player and enemy health bars,
/// numeric HP/SP labels, in-game Pause system, and the post-battle Victory/Defeat result panel.
/// Built using standard Unity UI Image/Button components and the Greek TMP font.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01BattleUI : MonoBehaviour
{
    [Header("Player HP (Achilles / Odysseus)")]
    [SerializeField] private Image playerHpFill;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("Player Energy (SP)")]
    [SerializeField] private Image playerEnergyFill;
    [SerializeField] private TextMeshProUGUI playerEnergyText;
    [SerializeField] private PlayerEnergySystem energySystem;

    [Header("Enemy HP (Cyclops)")]
    [SerializeField] private Image cyclopsHpFill;
    [SerializeField] private TextMeshProUGUI cyclopsHpText;
    [SerializeField] private TextMeshProUGUI cyclopsNameText;

    [Header("HUD Pause Button")]
    [SerializeField] private Button pauseButton;

    [Header("Pause Panel")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseHomeButton;

    [Header("Battle Result Panel")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultSubtitleText;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI restartButtonText;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button homeButton;

    [Header("Font Reference")]
    [SerializeField] private TMP_FontAsset fontAsset;

    private static Sprite cachedWhiteSprite;
    private bool isPaused;

    public Image PlayerHpFill => playerHpFill;
    public TextMeshProUGUI PlayerHpText => playerHpText;
    public Image CyclopsHpFill => cyclopsHpFill;
    public TextMeshProUGUI CyclopsHpText => cyclopsHpText;
    public GameObject ResultPanel => resultPanel;
    public GameObject PausePanel => pausePanel;
    public Button PauseButton => pauseButton;
    public Button NextLevelButton => nextLevelButton;
    public Button HomeButton => homeButton;
    public bool IsPaused => isPaused;
    public static bool IsGamePaused { get; private set; }

    private void Awake()
    {
        EnsureEventSystemExists();
        Time.timeScale = 1f;
        isPaused = false;
        IsGamePaused = false;

        TryAutoFindReferences();
        EnsureFillSprites();
        HookAllButtons();

        if (energySystem == null)
            energySystem = FindAnyObjectByType<PlayerEnergySystem>();
        if (energySystem != null)
            SetEnergySystem(energySystem);

        if (resultPanel != null)
            resultPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
        {
            TogglePause();
        }
    }

    private void TryAutoFindReferences()
    {
        Transform pPanel = transform.Find("PlayerHealthPanel");
        if (pPanel != null)
        {
            if (playerHpFill == null)
            {
                Transform fillTr = pPanel.Find("BarBackground/BarFill");
                if (fillTr != null) playerHpFill = fillTr.GetComponent<Image>();
            }
            if (playerHpText == null)
            {
                Transform hpTr = pPanel.Find("BarBackground/HpText");
                if (hpTr != null) playerHpText = hpTr.GetComponent<TextMeshProUGUI>();
            }
            if (playerNameText == null)
            {
                Transform nameTr = pPanel.Find("NameText");
                if (nameTr != null) playerNameText = nameTr.GetComponent<TextMeshProUGUI>();
            }
        }

        Transform cPanel = transform.Find("CyclopsHealthPanel");
        if (cPanel != null)
        {
            if (cyclopsHpFill == null)
            {
                Transform fillTr = cPanel.Find("BarBackground/BarFill");
                if (fillTr != null) cyclopsHpFill = fillTr.GetComponent<Image>();
            }
            if (cyclopsHpText == null)
            {
                Transform hpTr = cPanel.Find("BarBackground/HpText");
                if (hpTr != null) cyclopsHpText = hpTr.GetComponent<TextMeshProUGUI>();
            }
            if (cyclopsNameText == null)
            {
                Transform nameTr = cPanel.Find("NameText");
                if (nameTr != null) cyclopsNameText = nameTr.GetComponent<TextMeshProUGUI>();
            }
        }

        if (pauseButton == null)
        {
            Transform pbTr = transform.Find("ButtonPause");
            if (pbTr == null && transform.parent != null) pbTr = transform.parent.Find("ButtonPause");
            if (pbTr != null) pauseButton = pbTr.GetComponent<Button>();
        }

        if (pausePanel == null)
        {
            Transform ppTr = transform.Find("PausePanel");
            if (ppTr == null && transform.parent != null) ppTr = transform.parent.Find("PausePanel");
            if (ppTr != null) pausePanel = ppTr.gameObject;
        }

        if (pausePanel != null)
        {
            Transform dialog = pausePanel.transform.Find("DialogFrame");
            Transform targetRoot = dialog != null ? dialog : pausePanel.transform;
            if (resumeButton == null)
            {
                Transform btn = targetRoot.Find("ResumeButton");
                if (btn != null) resumeButton = btn.GetComponent<Button>();
            }
            if (pauseRestartButton == null)
            {
                Transform btn = targetRoot.Find("RestartButton");
                if (btn != null) pauseRestartButton = btn.GetComponent<Button>();
            }
            if (pauseHomeButton == null)
            {
                Transform btn = targetRoot.Find("HomeButton");
                if (btn != null) pauseHomeButton = btn.GetComponent<Button>();
            }
        }

        Transform rPanel = transform.Find("ResultPanel");
        if (rPanel != null)
        {
            if (resultPanel == null) resultPanel = rPanel.gameObject;
            if (resultTitleText == null)
            {
                Transform titleTr = rPanel.Find("TitleText");
                if (titleTr != null) resultTitleText = titleTr.GetComponent<TextMeshProUGUI>();
            }
            if (resultSubtitleText == null)
            {
                Transform subTr = rPanel.Find("SubtitleText");
                if (subTr != null) resultSubtitleText = subTr.GetComponent<TextMeshProUGUI>();
            }
            if (restartButton == null)
            {
                Transform btnTr = rPanel.Find("RestartButton");
                if (btnTr != null) restartButton = btnTr.GetComponent<Button>();
            }
            if (homeButton == null)
            {
                Transform btnTr = rPanel.Find("HomeButton");
                if (btnTr != null) homeButton = btnTr.GetComponent<Button>();
            }
            if (nextLevelButton == null)
            {
                Transform btnTr = rPanel.Find("NextLevelButton");
                if (btnTr != null) nextLevelButton = btnTr.GetComponent<Button>();
            }
        }
    }

    private void Start()
    {
        EnsureFillSprites();
    }

    public void Configure(
        Image playerFill,
        TextMeshProUGUI playerHp,
        TextMeshProUGUI playerName,
        Image cyclopsFill,
        TextMeshProUGUI cyclopsHp,
        TextMeshProUGUI cyclopsName,
        GameObject resultPanelObj,
        TextMeshProUGUI resultTitle,
        TextMeshProUGUI resultSubtitle,
        Button restartBtn,
        TextMeshProUGUI restartBtnText = null,
        TMP_FontAsset font = null)
    {
        playerHpFill = playerFill;
        playerHpText = playerHp;
        playerNameText = playerName;
        cyclopsHpFill = cyclopsFill;
        cyclopsHpText = cyclopsHp;
        cyclopsNameText = cyclopsName;
        resultPanel = resultPanelObj;
        resultTitleText = resultTitle;
        resultSubtitleText = resultSubtitle;
        restartButton = restartBtn;
        restartButtonText = restartBtnText;
        if (font != null)
            fontAsset = font;

        EnsureFillSprites();
        HookAllButtons();

        if (resultPanel != null)
            resultPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    public void UpdatePlayerHealth(float current, float max)
    {
        EnsureFillSprites();
        float clampedMax = Mathf.Max(1f, max);
        float clampedCurrent = Mathf.Clamp(current, 0f, clampedMax);

        if (playerHpFill != null)
        {
            playerHpFill.fillAmount = clampedCurrent / clampedMax;
        }

        if (playerHpText != null)
        {
            playerHpText.text = $"{Mathf.CeilToInt(clampedCurrent)} / {Mathf.CeilToInt(clampedMax)}";
        }
    }

    public void UpdateCyclopsHealth(float current, float max)
    {
        EnsureFillSprites();
        float clampedMax = Mathf.Max(1f, max);
        float clampedCurrent = Mathf.Clamp(current, 0f, clampedMax);

        if (cyclopsHpFill != null)
        {
            cyclopsHpFill.fillAmount = clampedCurrent / clampedMax;
        }

        if (cyclopsHpText != null)
        {
            cyclopsHpText.text = $"{Mathf.CeilToInt(clampedCurrent)} / {Mathf.CeilToInt(clampedMax)}";
        }
    }

    public void SetEnergySystem(PlayerEnergySystem system)
    {
        if (energySystem != null)
        {
            energySystem.EnergyChanged -= UpdatePlayerEnergy;
        }

        energySystem = system;

        if (energySystem != null)
        {
            energySystem.EnergyChanged -= UpdatePlayerEnergy;
            energySystem.EnergyChanged += UpdatePlayerEnergy;
            UpdatePlayerEnergy(energySystem.CurrentEnergy, energySystem.MaxEnergy);
        }
    }

    public void UpdatePlayerEnergy(float current, float max)
    {
        float clampedCurrent = Mathf.Max(0f, current);
        float clampedMax = Mathf.Max(1f, max);
        float fill01 = Mathf.Clamp01(clampedCurrent / clampedMax);

        if (playerEnergyFill != null)
        {
            playerEnergyFill.fillAmount = fill01;
        }

        if (playerEnergyText != null)
        {
            playerEnergyText.text = $"SP: {Mathf.RoundToInt(clampedCurrent)} / {Mathf.RoundToInt(clampedMax)}";
        }
    }

    public void SetPlayerName(string heroName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = heroName;
        }
    }

    #region Pause Management
    public void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        Level01TurnManager tm = FindAnyObjectByType<Level01TurnManager>();
        if ((tm != null && tm.IsBattleOver) || (resultPanel != null && resultPanel.activeSelf))
            return;

        isPaused = true;
        IsGamePaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        PlayerMovementDebugInput playerInput = FindAnyObjectByType<PlayerMovementDebugInput>();
        if (playerInput != null)
            playerInput.ClearInput();

        Level01TouchControls touchControls = FindAnyObjectByType<Level01TouchControls>();
        if (touchControls != null)
            touchControls.ClearAllHeldInputs();

        Debug.Log("[Level01BattleUI] Game PAUSED.");
    }

    public void ResumeGame()
    {
        isPaused = false;
        IsGamePaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Debug.Log("[Level01BattleUI] Game RESUMED.");
    }
    #endregion

    #region Navigation & Scene Routing
    public void RestartCurrentBattle()
    {
        Time.timeScale = 1f;
        isPaused = false;
        IsGamePaused = false;
        Debug.Log($"[Level01BattleUI] Reloading Level 01 for {BattleSessionData.SelectedHero}...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToHome()
    {
        Time.timeScale = 1f;
        isPaused = false;
        IsGamePaused = false;
        BattleSessionData.ReturnToCharacterSelect = false;
        BattleSessionData.ReturnToLevelSelect = false;
        Debug.Log("[Level01BattleUI] Returning to Home screen...");
        SceneManager.LoadScene(0); // Load SCN_Home
    }

    public void GoToNextLevel()
    {
        Time.timeScale = 1f;
        isPaused = false;
        IsGamePaused = false;
        BattleSessionData.ReturnToLevelSelect = true;
        Debug.Log("[Level01BattleUI] Progressing to Level Select...");
        SceneManager.LoadScene(0); // Load SCN_Home with Level Select active
    }

    public void RestartBattle()
    {
        RestartCurrentBattle();
    }
    #endregion

    public void ShowResult(bool isWin)
    {
        string heroName = BattleSessionData.IsOdysseus ? "Odysseus" : "Achilles";
        ShowResult(isWin, heroName);
    }

    public void ShowResult(bool isWin, string heroName)
    {
        if (isPaused)
            ResumeGame();

        if (pauseButton != null)
            pauseButton.gameObject.SetActive(false);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = isWin ? "VICTORY" : "DEFEAT";
            resultTitleText.color = isWin
                ? new Color(1f, 0.85f, 0.2f, 1f)    // Greek Gold
                : new Color(0.95f, 0.25f, 0.2f, 1f); // Crimson Red
        }

        if (resultSubtitleText != null)
        {
            string enemyName = cyclopsNameText != null && !string.IsNullOrEmpty(cyclopsNameText.text)
                ? cyclopsNameText.text
                : "Cyclops";
            resultSubtitleText.text = isWin
                ? $"{enemyName} Defeated!"
                : $"{heroName} Has Fallen!";
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(isWin);
        }
    }

    private void HookAllButtons()
    {
        HookButton(pauseButton, TogglePause);
        HookButton(resumeButton, ResumeGame);
        HookButton(pauseRestartButton, RestartCurrentBattle);
        HookButton(pauseHomeButton, ReturnToHome);

        HookButton(restartButton, RestartCurrentBattle);
        HookButton(homeButton, ReturnToHome);
        HookButton(nextLevelButton, GoToNextLevel);
    }

    private void HookButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);

        Image btnImg = btn.GetComponent<Image>();
        if (btnImg != null)
        {
            btn.targetGraphic = btnImg;
            btnImg.raycastTarget = true;
        }

        var textChildren = btn.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var t in textChildren)
        {
            t.raycastTarget = false;
        }
    }

    private void EnsureEventSystemExists()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null && FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[Level01BattleUI] Automatically initialized EventSystem with InputSystemUIInputModule.");
        }
    }

    private void EnsureFillSprites()
    {
        Sprite sprite = GetWhiteSprite();
        if (playerHpFill != null && playerHpFill.sprite == null)
        {
            playerHpFill.sprite = sprite;
            playerHpFill.type = Image.Type.Filled;
            playerHpFill.fillMethod = Image.FillMethod.Horizontal;
            playerHpFill.fillOrigin = 0;
        }

        if (cyclopsHpFill != null && cyclopsHpFill.sprite == null)
        {
            cyclopsHpFill.sprite = sprite;
            cyclopsHpFill.type = Image.Type.Filled;
            cyclopsHpFill.fillMethod = Image.FillMethod.Horizontal;
            cyclopsHpFill.fillOrigin = 0;
        }
    }

    public static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }
        return cachedWhiteSprite;
    }

    private void OnDestroy()
    {
        if (energySystem != null)
            energySystem.EnergyChanged -= UpdatePlayerEnergy;
    }
}
