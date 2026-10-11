using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable dialogue panel UI placed under HUDCanvas.
/// Displays speaker name and short dialogue text using Greek TMP font,
/// with Next and Skip buttons to progress or dismiss dialogue sequences.
/// Positioned in landscape to stay clear of top HP bars and bottom touch controls.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialoguePanelUI : MonoBehaviour
{
    [Header("UI Component References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI nextButtonText;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipButtonText;

    [Header("Font Reference")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Default Colors")]
    [SerializeField] private Color defaultSpeakerColor = new Color(1f, 0.85f, 0.2f, 1f); // Greek Gold
    [SerializeField] private Color defaultBodyColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    private DialogueLine[] currentLines;
    private int currentLineIndex;
    private Action onSequenceCompleted;
    private Action<DialogueLine, int> onLineDisplayedCallback;
    private bool isDisplaying;

    public bool IsDisplaying => isDisplaying;
    public event Action<DialogueLine, int> LineDisplayed;

    private void Awake()
    {
        InitializeComponents();

        if (!isDisplaying)
        {
            SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
        UnhookButtons();
    }

    private void InitializeComponents()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        AutoFindReferences();
        HookButtons();
    }

    private void AutoFindReferences()
    {
        Transform searchRoot = panelRoot != null ? panelRoot.transform : transform;

        if (speakerNameText == null)
        {
            Transform tr = searchRoot.Find("SpeakerNameText");
            if (tr != null) speakerNameText = tr.GetComponent<TextMeshProUGUI>();
        }

        if (bodyText == null)
        {
            Transform tr = searchRoot.Find("BodyText");
            if (tr != null) bodyText = tr.GetComponent<TextMeshProUGUI>();
        }

        if (nextButton == null)
        {
            Transform tr = searchRoot.Find("NextButton");
            if (tr != null) nextButton = tr.GetComponent<Button>();
        }

        if (nextButton != null && nextButtonText == null)
        {
            nextButtonText = nextButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (skipButton == null)
        {
            Transform tr = searchRoot.Find("SkipButton");
            if (tr != null) skipButton = tr.GetComponent<Button>();
        }

        if (skipButton != null && skipButtonText == null)
        {
            skipButtonText = skipButton.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void HookButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnNextClicked);
            nextButton.onClick.AddListener(OnNextClicked);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipClicked);
            skipButton.onClick.AddListener(OnSkipClicked);
        }
    }

    private void UnhookButtons()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextClicked);

        if (skipButton != null)
            skipButton.onClick.RemoveListener(OnSkipClicked);
    }

    /// <summary>
    /// Displays a sequence of dialogue lines. Invokes onComplete callback when the sequence finishes or is skipped,
    /// and optionally invokes onLineDisplayed each time a new line is shown.
    /// </summary>
    public void ShowSequence(DialogueLine[] lines, Action onComplete, Action<DialogueLine, int> onLineDisplayed = null)
    {
        if (lines == null || lines.Length == 0)
        {
            Debug.Log("[DialoguePanelUI] ShowSequence called with null or empty lines. Completing immediately.");
            onComplete?.Invoke();
            return;
        }

        currentLines = lines;
        currentLineIndex = 0;
        onSequenceCompleted = onComplete;
        onLineDisplayedCallback = onLineDisplayed;
        isDisplaying = true;

        InitializeComponents();
        SetPanelVisible(true);
        DisplayLine(currentLineIndex);

        Debug.Log($"[DialoguePanelUI] ShowSequence started ({lines.Length} lines). Line 1: [{lines[0].speakerName}] {lines[0].text}");
    }

    private void SetPanelVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private void DisplayLine(int index)
    {
        if (currentLines == null || index < 0 || index >= currentLines.Length)
            return;

        DialogueLine line = currentLines[index];

        if (speakerNameText != null)
        {
            speakerNameText.text = line.speakerName;
            speakerNameText.color = line.speakerColor.a > 0.05f ? line.speakerColor : defaultSpeakerColor;
        }

        if (bodyText != null)
        {
            bodyText.text = line.text;
            bodyText.color = defaultBodyColor;
        }

        if (nextButtonText != null)
        {
            bool isLastLine = index == currentLines.Length - 1;
            nextButtonText.text = isLastLine ? "Close" : "Next";
        }

        onLineDisplayedCallback?.Invoke(line, index);
        LineDisplayed?.Invoke(line, index);
    }

    public void OnNextClicked()
    {
        if (!isDisplaying || currentLines == null)
            return;

        currentLineIndex++;
        if (currentLineIndex < currentLines.Length)
        {
            DisplayLine(currentLineIndex);
            Debug.Log($"[DialoguePanelUI] Advanced to line {currentLineIndex + 1}/{currentLines.Length}: [{currentLines[currentLineIndex].speakerName}]");
        }
        else
        {
            FinishSequence();
        }
    }

    public void OnSkipClicked()
    {
        if (!isDisplaying)
            return;

        Debug.Log("[DialoguePanelUI] Skip clicked. Finishing dialogue sequence.");
        FinishSequence();
    }

    private void FinishSequence()
    {
        isDisplaying = false;
        currentLines = null;
        currentLineIndex = 0;

        SetPanelVisible(false);

        Action callback = onSequenceCompleted;
        onSequenceCompleted = null;
        onLineDisplayedCallback = null;
        callback?.Invoke();
    }

    public void Hide()
    {
        isDisplaying = false;
        onSequenceCompleted = null;
        onLineDisplayedCallback = null;
        SetPanelVisible(false);
    }

    public void Configure(
        GameObject root,
        TextMeshProUGUI speakerName,
        TextMeshProUGUI body,
        Button nextBtn,
        TextMeshProUGUI nextBtnText,
        Button skipBtn,
        TextMeshProUGUI skipBtnText,
        TMP_FontAsset font,
        CanvasGroup group = null)
    {
        panelRoot = root;
        canvasGroup = group != null ? group : (root != null ? root.GetComponent<CanvasGroup>() : null);
        speakerNameText = speakerName;
        bodyText = body;
        nextButton = nextBtn;
        nextButtonText = nextBtnText;
        skipButton = skipBtn;
        skipButtonText = skipBtnText;
        fontAsset = font;

        InitializeComponents();
    }
}
