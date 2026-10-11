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
    /// Back navigation, and Start Battle button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject characterSelectPanel;

        [Header("Home Screen Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;

        [Header("Level Select Buttons")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button level01Button;
        [SerializeField] private Button level02Button;
        [SerializeField] private Button level03Button;

        [Header("Character Select UI")]
        [SerializeField] private Button achillesCardButton;
        [SerializeField] private Button odysseusCardButton;
        [SerializeField] private Image achillesSelectedBorder;
        [SerializeField] private Image odysseusSelectedBorder;
        [SerializeField] private Button characterSelectBackButton;
        [SerializeField] private Button startBattleButton;

        [Header("Scene Routing")]
        [SerializeField] private string level01SceneName = "SCN_Level01_Beach";

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
            }

            if (levelSelectPanel != null)
            {
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
        }

        private void Start()
        {
            if (BattleSessionData.ReturnToCharacterSelect)
            {
                BattleSessionData.ReturnToCharacterSelect = false;
                ShowCharacterSelectScreen();
            }
            else
            {
                ShowHomeScreen();
            }
            UpdateCharacterSelectionVisuals();
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

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
                backButton.onClick.AddListener(OnBackClicked);
            }

            if (level01Button != null)
            {
                level01Button.onClick.RemoveListener(OnLevel01Clicked);
                level01Button.onClick.AddListener(OnLevel01Clicked);
                level01Button.interactable = true;
            }

            if (level02Button != null)
            {
                level02Button.interactable = false;
            }

            if (level03Button != null)
            {
                level03Button.interactable = false;
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

        public void OnLevel01Clicked()
        {
            SelectLevel(1);
        }

        public void SelectLevel(int levelIndex)
        {
            selectedLevelIndex = levelIndex;
            if (selectedLevelIndex == 1)
            {
                ShowCharacterSelectScreen();
            }
            else
            {
                Debug.LogWarning($"[HomeMenuController] Level {selectedLevelIndex} is currently locked.");
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
