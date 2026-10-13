using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace AttackOnTroy.Menus
{
    public enum MenuScreenState
    {
        Home,
        LevelSelect,
        CharacterSelect
    }

    /// <summary>
    /// Controls the initial Home, Level Select, and Character Select menu flow.
    /// Manages panel transitions (Home <-> Level Select <-> Character Select) and routes level loading.
    /// Supports selecting between Achilles and Odysseus with clear highlight, Idle art presentation,
    /// Back navigation, Start Battle button, and persistent Level 01-03 unlock progression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject characterSelectPanel;

        [Header("Backgrounds")]
        [SerializeField] private Image homeBackgroundImage;
        [SerializeField] private Image levelSelectBackgroundImage;
        [SerializeField] private Image characterSelectBackgroundImage;

        [Header("Home Screen Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button soundButton;

        [Header("Level Select Buttons & Cards")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button level01Button;
        [SerializeField] private Button level02Button;
        [SerializeField] private Button level03Button;
        [SerializeField] private Image level01CardImage;
        [SerializeField] private Image level02CardImage;
        [SerializeField] private Image level03CardImage;
        [SerializeField] private TextMeshProUGUI level01Text;
        [SerializeField] private TextMeshProUGUI level02Text;
        [SerializeField] private TextMeshProUGUI level03Text;

        [Header("Character Select UI")]
        [SerializeField] private Button achillesCardButton;
        [SerializeField] private Button odysseusCardButton;
        [SerializeField] private Image achillesSelectedBorder;
        [SerializeField] private Image odysseusSelectedBorder;
        [SerializeField] private Button characterSelectBackButton;
        [SerializeField] private Button startBattleButton;

        [Header("Scene Routing")]
        [SerializeField] private string level01SceneName = "SCN_Level01_Beach";
        [SerializeField] private string level02SceneName = "SCN_Level02_Scamander";
        [SerializeField] private string level03SceneName = "SCN_Level03_TroyWall";

        private MenuScreenState currentState = MenuScreenState.Home;
        private int selectedLevelIndex = 1;

        public MenuScreenState CurrentState => currentState;
        public int SelectedLevelIndex => selectedLevelIndex;

        private void Awake()
        {
            EnsureEventSystem();
            TryAutoFindReferences();
            Time.timeScale = 1f;
            HookButtons();
        }

        private void TryAutoFindReferences()
        {
            if (homePanel == null)
            {
                Transform tr = transform.Find("HomePanel");
                if (tr != null) homePanel = tr.gameObject;
            }
            if (levelSelectPanel == null)
            {
                Transform tr = transform.Find("LevelSelectPanel");
                if (tr != null) levelSelectPanel = tr.gameObject;
            }
            if (characterSelectPanel == null)
            {
                Transform tr = transform.Find("CharacterSelectPanel");
                if (tr != null) characterSelectPanel = tr.gameObject;
            }

            if (homePanel != null)
            {
                if (homeBackgroundImage == null)
                {
                    Transform bg = homePanel.transform.Find("Background");
                    if (bg != null) homeBackgroundImage = bg.GetComponent<Image>();
                }
                if (playButton == null)
                {
                    Transform btn = homePanel.transform.Find("PlayButton");
                    if (btn != null) playButton = btn.GetComponent<Button>();
                }
                if (exitButton == null)
                {
                    Transform btn = homePanel.transform.Find("ExitButton");
                    if (btn != null) exitButton = btn.GetComponent<Button>();
                }
                if (soundButton == null)
                {
                    Transform btn = homePanel.transform.Find("SoundButton");
                    if (btn != null) soundButton = btn.GetComponent<Button>();
                }
            }

            if (levelSelectPanel != null)
            {
                if (levelSelectBackgroundImage == null)
                {
                    Transform bg = levelSelectPanel.transform.Find("Background");
                    if (bg != null) levelSelectBackgroundImage = bg.GetComponent<Image>();
                }

                Transform dialog = levelSelectPanel.transform.Find("DialogFrame");
                if (dialog != null)
                {
                    if (backButton == null)
                    {
                        Transform btn = dialog.Find("BackButton");
                        if (btn != null) backButton = btn.GetComponent<Button>();
                    }
                    if (level01Button == null)
                    {
                        Transform btn = dialog.Find("Level01_Card");
                        if (btn != null) level01Button = btn.GetComponent<Button>();
                    }
                    if (level02Button == null)
                    {
                        Transform btn = dialog.Find("Level02_Card");
                        if (btn != null) level02Button = btn.GetComponent<Button>();
                    }
                    if (level03Button == null)
                    {
                        Transform btn = dialog.Find("Level03_Card");
                        if (btn != null) level03Button = btn.GetComponent<Button>();
                    }
                }
            }

            if (characterSelectPanel != null)
            {
                if (characterSelectBackgroundImage == null)
                {
                    Transform bg = characterSelectPanel.transform.Find("Background");
                    if (bg != null) characterSelectBackgroundImage = bg.GetComponent<Image>();
                }

                Transform dialog = characterSelectPanel.transform.Find("DialogFrame");
                if (dialog != null)
                {
                    if (achillesCardButton == null)
                    {
                        Transform card = dialog.Find("AchillesCard");
                        if (card != null) achillesCardButton = card.GetComponent<Button>();
                    }
                    if (odysseusCardButton == null)
                    {
                        Transform card = dialog.Find("OdysseusCard");
                        if (card != null) odysseusCardButton = card.GetComponent<Button>();
                    }
                    if (achillesSelectedBorder == null && achillesCardButton != null)
                    {
                        Transform border = achillesCardButton.transform.Find("SelectedHighlight");
                        if (border != null) achillesSelectedBorder = border.GetComponent<Image>();
                    }
                    if (odysseusSelectedBorder == null && odysseusCardButton != null)
                    {
                        Transform border = odysseusCardButton.transform.Find("SelectedHighlight");
                        if (border != null) odysseusSelectedBorder = border.GetComponent<Image>();
                    }
                    if (characterSelectBackButton == null)
                    {
                        Transform btn = dialog.Find("BackButton");
                        if (btn != null) characterSelectBackButton = btn.GetComponent<Button>();
                    }
                    if (startBattleButton == null)
                    {
                        Transform btn = dialog.Find("StartButton");
                        if (btn != null) startBattleButton = btn.GetComponent<Button>();
                    }
                }
            }

            if (level01Button != null)
            {
                if (level01CardImage == null) level01CardImage = level01Button.GetComponent<Image>();
                if (level01Text == null) level01Text = level01Button.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (level02Button != null)
            {
                if (level02CardImage == null) level02CardImage = level02Button.GetComponent<Image>();
                if (level02Text == null) level02Text = level02Button.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (level03Button != null)
            {
                if (level03CardImage == null) level03CardImage = level03Button.GetComponent<Image>();
                if (level03Text == null) level03Text = level03Button.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        private void Start()
        {
            if (BattleSessionData.ReturnToCharacterSelect)
            {
                BattleSessionData.ReturnToCharacterSelect = false;
                ShowCharacterSelectScreen();
            }
            else if (BattleSessionData.ReturnToLevelSelect)
            {
                BattleSessionData.ReturnToLevelSelect = false;
                ShowLevelSelectScreen();
            }
            else
            {
                ShowHomeScreen();
            }
            UpdateCharacterSelectionVisuals();
            UpdateLevelSelectVisuals();
        }

        private void OnEnable()
        {
            UpdateLevelSelectVisuals();
        }

        private void HookButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitClicked);
                exitButton.onClick.AddListener(OnExitClicked);
            }

            if (soundButton != null)
            {
                soundButton.onClick.RemoveListener(OnSoundButtonClicked);
                Audio.AudioSettingsControls.InstallHome(soundButton);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
                backButton.onClick.AddListener(OnBackClicked);
            }

            if (level01Button != null)
            {
                level01Button.onClick.RemoveListener(OnLevel01Clicked);
                level01Button.onClick.AddListener(OnLevel01Clicked);
            }

            if (level02Button != null)
            {
                level02Button.onClick.RemoveListener(OnLevel02Clicked);
                level02Button.onClick.AddListener(OnLevel02Clicked);
            }

            if (level03Button != null)
            {
                level03Button.onClick.RemoveListener(OnLevel03Clicked);
                level03Button.onClick.AddListener(OnLevel03Clicked);
            }

            if (achillesCardButton != null)
            {
                achillesCardButton.onClick.RemoveListener(OnAchillesClicked);
                achillesCardButton.onClick.AddListener(OnAchillesClicked);
            }

            if (odysseusCardButton != null)
            {
                odysseusCardButton.onClick.RemoveListener(OnOdysseusClicked);
                odysseusCardButton.onClick.AddListener(OnOdysseusClicked);
            }

            if (characterSelectBackButton != null)
            {
                characterSelectBackButton.onClick.RemoveListener(OnBackClicked);
                characterSelectBackButton.onClick.AddListener(OnBackClicked);
            }

            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveListener(OnStartBattleClicked);
                startBattleButton.onClick.AddListener(OnStartBattleClicked);
            }

            UpdateLevelSelectVisuals();
        }

        public void ShowHomeScreen()
        {
            currentState = MenuScreenState.Home;
            if (homePanel != null) homePanel.SetActive(true);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        }

        public void ShowLevelSelectScreen()
        {
            currentState = MenuScreenState.LevelSelect;
            if (homePanel != null) homePanel.SetActive(false);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            UpdateLevelSelectVisuals();
        }

        public void ShowCharacterSelectScreen()
        {
            currentState = MenuScreenState.CharacterSelect;
            if (homePanel != null) homePanel.SetActive(false);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(true);
            UpdateCharacterSelectionVisuals();
        }

        public void OnPlayClicked()
        {
            ShowLevelSelectScreen();
        }

        public void OnBackClicked()
        {
            if (currentState == MenuScreenState.CharacterSelect)
            {
                ShowLevelSelectScreen();
            }
            else
            {
                ShowHomeScreen();
            }
        }

        public void OnExitClicked()
        {
            Debug.Log("[HomeMenuController] Exit requested.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnSoundButtonClicked()
        {
            // Retain the public entry point for older scenes; the new UI binds the two channels.
            Audio.AudioManager.EnsureInstance().ToggleMusic();
        }

        public void OnLevel01Clicked()
        {
            SelectLevel(1);
        }

        public void OnLevel02Clicked()
        {
            SelectLevel(2);
        }

        public void OnLevel03Clicked()
        {
            SelectLevel(3);
        }

        public void SelectLevel(int levelIndex)
        {
            if (levelIndex < 1 || levelIndex > 3)
            {
                Debug.LogWarning($"[HomeMenuController] Invalid Level {levelIndex}.");
                return;
            }

            if (!BattleSessionData.IsLevelUnlocked(levelIndex))
            {
                Debug.LogWarning($"[HomeMenuController] Level {levelIndex} is currently locked. Complete previous level to unlock.");
                return;
            }

            selectedLevelIndex = levelIndex;
            ShowCharacterSelectScreen();
        }

        public void UpdateLevelSelectVisuals()
        {
            UpdateCardVisual(level01Button, level01CardImage, level01Text, 1,
                BattleSessionData.IsLevelUnlocked(1),
                BattleSessionData.IsLevelCompleted(1),
                "BEACH OF TROY", "BOSS: CYCLOPS");

            UpdateCardVisual(level02Button, level02CardImage, level02Text, 2,
                BattleSessionData.IsLevelUnlocked(2),
                BattleSessionData.IsLevelCompleted(2),
                "SCAMANDER", "RIVER CROSSING");

            UpdateCardVisual(level03Button, level03CardImage, level03Text, 3,
                BattleSessionData.IsLevelUnlocked(3),
                BattleSessionData.IsLevelCompleted(3),
                "WALLS OF TROY", "THE GATES");
        }

        private void UpdateCardVisual(Button button, Image cardImage, TextMeshProUGUI cardText, int levelIndex, bool isUnlocked, bool isCompleted, string title, string subtitle)
        {
            if (button != null)
            {
                button.interactable = isUnlocked;
            }

            if (cardImage != null)
            {
                cardImage.color = isUnlocked
                    ? new Color(0.20f, 0.32f, 0.46f, 0.95f)
                    : new Color(0.08f, 0.10f, 0.14f, 0.90f);
            }

            if (cardText != null)
            {
                string levelNum = $"LEVEL 0{levelIndex}";
                if (isUnlocked)
                {
                    string statusTag = isCompleted
                        ? "<b><color=#FFD700><size=24>★ CLEARED ★</size></color></b>"
                        : "<b><color=#4CAF50><size=26>[ BATTLE ]</size></color></b>";

                    cardText.text = $"<b><color=#FFD700>{levelNum}</color></b>\n" +
                                    $"<size=28><b>{title}</b></size>\n\n" +
                                    $"<color=#D4B26F><size=20>{subtitle}</size></color>\n\n\n" +
                                    $"{statusTag}";
                }
                else
                {
                    cardText.text = $"<b><color=#777777>{levelNum}</color></b>\n" +
                                    $"<size=28><color=#888888>{title}</color></size>\n\n" +
                                    $"<color=#555555><size=20>{subtitle}</size></color>\n\n\n" +
                                    $"<b><color=#D32F2F><size=24>[ LOCKED ]</size></color></b>";
                }
            }
        }

        public void SelectCharacter(string heroName)
        {
            BattleSessionData.SelectedHero = heroName;
            UpdateCharacterSelectionVisuals();
        }

        private void OnAchillesClicked()
        {
            SelectCharacter(BattleSessionData.HeroAchilles);
        }

        private void OnOdysseusClicked()
        {
            SelectCharacter(BattleSessionData.HeroOdysseus);
        }

        private void OnStartBattleClicked()
        {
            LaunchBattle();
        }

        public void UpdateCharacterSelectionVisuals()
        {
            bool isOdysseus = BattleSessionData.IsOdysseus;
            if (achillesSelectedBorder != null)
                achillesSelectedBorder.gameObject.SetActive(!isOdysseus);
            if (odysseusSelectedBorder != null)
                odysseusSelectedBorder.gameObject.SetActive(isOdysseus);
        }

        public void LaunchBattle()
        {
            Time.timeScale = 1f;
            if (selectedLevelIndex == 1)
            {
                Debug.Log($"[HomeMenuController] Loading Level 01: {level01SceneName} with Hero: {BattleSessionData.SelectedHero}");
                SceneManager.LoadScene(level01SceneName);
            }
            else if (selectedLevelIndex == 2)
            {
                Debug.Log($"[HomeMenuController] Loading Level 02: {level02SceneName} with Hero: {BattleSessionData.SelectedHero}");
                SceneManager.LoadScene(level02SceneName);
            }
            else if (selectedLevelIndex == 3)
            {
                Debug.Log($"[HomeMenuController] Loading Level 03: {level03SceneName} with Hero: {BattleSessionData.SelectedHero}");
                SceneManager.LoadScene(level03SceneName);
            }
            else
            {
                Debug.LogWarning($"[HomeMenuController] Level {selectedLevelIndex} is currently locked.");
            }
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null && FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[HomeMenuController] Auto-created EventSystem with InputSystemUIInputModule.");
            }
        }
    }
}
