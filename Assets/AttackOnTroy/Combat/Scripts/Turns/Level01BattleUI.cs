using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Screen HUD component managing player (Achilles) and enemy (Cyclops Normal) health bars,
/// numeric current/max HP labels, and the post-battle Victory/Defeat result panel with Restart.
/// Built using standard Unity UI Image components and the existing Greek TMP font.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01BattleUI : MonoBehaviour
{
    [Header("Player HP (Achilles)")]
    [SerializeField] private Image playerHpFill;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI playerNameText;

    [Header("Enemy HP (Cyclops)")]
    [SerializeField] private Image cyclopsHpFill;
    [SerializeField] private TextMeshProUGUI cyclopsHpText;
    [SerializeField] private TextMeshProUGUI cyclopsNameText;

    [Header("Battle Result Panel")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultSubtitleText;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI restartButtonText;

    [Header("Font Reference")]
    [SerializeField] private TMP_FontAsset fontAsset;

    private static Sprite cachedWhiteSprite;

    public Image PlayerHpFill => playerHpFill;
    public TextMeshProUGUI PlayerHpText => playerHpText;
    public Image CyclopsHpFill => cyclopsHpFill;
    public TextMeshProUGUI CyclopsHpText => cyclopsHpText;
    public GameObject ResultPanel => resultPanel;

    private void Awake()
    {
        EnsureEventSystemExists();
        if (playerHpFill == null || cyclopsHpFill == null || resultPanel == null)
        {
            TryAutoFindReferences();
        }
        EnsureFillSprites();
        HookRestartButton();
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
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
        HookRestartButton();

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
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

    public void SetPlayerName(string heroName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = heroName;
        }
    }

    public void ShowResult(bool isWin)
    {
        string heroName = BattleSessionData.IsOdysseus ? "Odysseus" : "Achilles";
        ShowResult(isWin, heroName);
    }

    public void ShowResult(bool isWin, string heroName)
    {
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
            resultSubtitleText.text = isWin
                ? "Cyclops Normal Defeated!"
                : $"{heroName} Has Fallen!";
        }
    }

    public void RestartBattle()
    {
        Time.timeScale = 1f;
        // On Restart, return to Character Select with the previous choice highlighted.
        BattleSessionData.ReturnToCharacterSelect = true;
        SceneManager.LoadScene(0); // Load SCN_Home
    }

    private void HookRestartButton()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartBattle);
            restartButton.onClick.AddListener(RestartBattle);

            Image btnImg = restartButton.GetComponent<Image>();
            if (btnImg != null)
            {
                restartButton.targetGraphic = btnImg;
                btnImg.raycastTarget = true;
            }

            if (restartButtonText != null)
            {
                restartButtonText.raycastTarget = false;
            }
            else
            {
                var textChild = restartButton.GetComponentInChildren<TextMeshProUGUI>();
                if (textChild != null)
                    textChild.raycastTarget = false;
            }
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
}
