using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Idempotent setup tool for Level 01 health bars, battle win/lose result panel, and turn-pacing wiring.
/// Creates and links Achilles and Cyclops Normal HP bars under HUDCanvas, the Victory/Defeat
/// result panel with a Restart button, and connects damage receivers and pacing timers to Level01TurnManager.
/// </summary>
public static class ConfigureLevel01HealthAndBattleResult
{
    private const string MenuPath = "Attack on Troy/Setup/Configure Level 01 Health and Battle Result";
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string GreekFontPath = "Assets/AttackOnTroy/Art/UI/Fonts/TMP/TMP_Greek_SDF.asset";

    [MenuItem(MenuPath)]
    public static void Configure()
    {
        try
        {
            Apply();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Health & Battle Result",
                "Cannot configure while in Play Mode. Exit Play Mode first.",
                "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Health & Battle Result",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        GameObject player = FindUnique(scene, "Player_Achilles");
        GameObject cyclops = FindUnique(scene, "Enemy_CyclopsNormal");
        GameObject hudCanvas = FindUnique(scene, "HUDCanvas");
        Level01TurnManager turnManager = FindUniqueComponent<Level01TurnManager>(scene);

        ActorDamageReceiver2D playerDamage = player.GetComponent<ActorDamageReceiver2D>();
        ActorDamageReceiver2D cyclopsDamage = cyclops.GetComponent<ActorDamageReceiver2D>();
        GroundedActorMotor2D cyclopsMotor = cyclops.GetComponent<GroundedActorMotor2D>();

        TMP_FontAsset greekFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GreekFontPath);
        if (greekFont == null)
        {
            TextMeshProUGUI anyTmp = hudCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (anyTmp != null)
                greekFont = anyTmp.font;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Health & Battle Result");

        // 0. Ensure EventSystem exists in scene for UI interaction
        UnityEngine.EventSystems.EventSystem existingEventSystem = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingEventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 1. Ensure BattleUI root under HUDCanvas
        Transform battleUiTr = hudCanvas.transform.Find("BattleUI");
        GameObject battleUiObj;
        if (battleUiTr == null)
        {
            battleUiObj = new GameObject("BattleUI", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(battleUiObj, "Create BattleUI");
            Undo.SetTransformParent(battleUiObj.transform, hudCanvas.transform, "Parent BattleUI");
        }
        else
        {
            battleUiObj = battleUiTr.gameObject;
        }

        RectTransform battleUiRect = battleUiObj.GetComponent<RectTransform>();
        battleUiRect.anchorMin = Vector2.zero;
        battleUiRect.anchorMax = Vector2.one;
        battleUiRect.offsetMin = Vector2.zero;
        battleUiRect.offsetMax = Vector2.zero;

        Level01BattleUI battleUI = battleUiObj.GetComponent<Level01BattleUI>();
        if (battleUI == null)
            battleUI = Undo.AddComponent<Level01BattleUI>(battleUiObj);

        // 2. Build or find PlayerHealthPanel (Top-Left under ProjectileAimHUD)
        Image playerFill;
        TextMeshProUGUI playerHpTmp;
        TextMeshProUGUI playerNameTmp;
        BuildHealthPanel(
            battleUiObj.transform,
            "PlayerHealthPanel",
            "ACHILLES",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(30f, -155f),
            new Vector2(260f, 52f),
            new Color(0.18f, 0.82f, 0.35f, 1f),
            greekFont,
            out playerFill,
            out playerHpTmp,
            out playerNameTmp);

        // 3. Build or find CyclopsHealthPanel (Top-Right)
        Image cyclopsFill;
        TextMeshProUGUI cyclopsHpTmp;
        TextMeshProUGUI cyclopsNameTmp;
        BuildHealthPanel(
            battleUiObj.transform,
            "CyclopsHealthPanel",
            "CYCLOPS NORMAL",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-30f, -25f),
            new Vector2(260f, 52f),
            new Color(0.92f, 0.22f, 0.18f, 1f),
            greekFont,
            out cyclopsFill,
            out cyclopsHpTmp,
            out cyclopsNameTmp);

        // 4. Build or find ResultPanel (Screen Center)
        GameObject resultPanelObj;
        TextMeshProUGUI resultTitleTmp;
        TextMeshProUGUI resultSubtitleTmp;
        Button restartBtn;
        TextMeshProUGUI restartBtnTmp;
        BuildResultPanel(
            battleUiObj.transform,
            greekFont,
            out resultPanelObj,
            out resultTitleTmp,
            out resultSubtitleTmp,
            out restartBtn,
            out restartBtnTmp);

        // Configure Level01BattleUI component
        Undo.RecordObject(battleUI, "Configure Level01BattleUI");
        battleUI.Configure(
            playerFill,
            playerHpTmp,
            playerNameTmp,
            cyclopsFill,
            cyclopsHpTmp,
            cyclopsNameTmp,
            resultPanelObj,
            resultTitleTmp,
            resultSubtitleTmp,
            restartBtn,
            restartBtnTmp,
            greekFont);

        // Initial HP values
        if (playerDamage != null)
            battleUI.UpdatePlayerHealth(playerDamage.CurrentHealth, playerDamage.MaximumHealth);
        if (cyclopsDamage != null)
            battleUI.UpdateCyclopsHealth(cyclopsDamage.CurrentHealth, cyclopsDamage.MaximumHealth);

        PrefabUtility.RecordPrefabInstancePropertyModifications(battleUI);
        EditorUtility.SetDirty(battleUI);

        // 5. Connect to Level01TurnManager
        Undo.RecordObject(turnManager, "Wire BattleUI & Pacing to TurnManager");
        var turnData = new SerializedObject(turnManager);
        turnData.FindProperty("battleUI").objectReferenceValue = battleUI;
        turnData.FindProperty("playerDamageReceiver").objectReferenceValue = playerDamage;
        turnData.FindProperty("cyclopsDamageReceiver").objectReferenceValue = cyclopsDamage;
        turnData.FindProperty("cyclopsMotor").objectReferenceValue = cyclopsMotor;

        var delayProp = turnData.FindProperty("cyclopsTurnDelay");
        if (delayProp != null)
            delayProp.floatValue = 1.5f;

        var resultDelayProp = turnData.FindProperty("battleResultDelay");
        if (resultDelayProp != null)
            resultDelayProp.floatValue = 1.0f;

        turnData.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(turnManager);
        EditorUtility.SetDirty(turnManager);

        // 6. Ensure ActorDamageReceiver2D animators and triggers are wired
        if (playerDamage != null)
        {
            Undo.RecordObject(playerDamage, "Configure Player Damage Feedback");
            Animator pAnim = player.GetComponentInChildren<Animator>(true);
            playerDamage.ConfigureAnimator(pAnim, "Hurt", "Die");
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerDamage);
            EditorUtility.SetDirty(playerDamage);
        }
        if (cyclopsDamage != null)
        {
            Undo.RecordObject(cyclopsDamage, "Configure Cyclops Damage Feedback");
            Animator cAnim = cyclops.GetComponentInChildren<Animator>(true);
            cyclopsDamage.ConfigureAnimator(cAnim, "Hurt", "Die");
            PrefabUtility.RecordPrefabInstancePropertyModifications(cyclopsDamage);
            EditorUtility.SetDirty(cyclopsDamage);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);

        Debug.Log("[ConfigureLevel01HealthAndBattleResult] Health bars (Achilles & Cyclops), Result Panel (WIN/LOSE), " +
                  "hit/death animation feedback, and turn-pacing pauses configured successfully. Scene marked dirty with Undo (NOT auto-saved).");
    }

    private static void BuildHealthPanel(
        Transform parent,
        string panelName,
        string characterName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color fillColor,
        TMP_FontAsset font,
        out Image fillImage,
        out TextMeshProUGUI hpText,
        out TextMeshProUGUI nameText)
    {
        Transform panelTr = parent.Find(panelName);
        GameObject panelObj;
        if (panelTr == null)
        {
            panelObj = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(panelObj, "Create " + panelName);
            Undo.SetTransformParent(panelObj.transform, parent, "Parent " + panelName);
        }
        else
        {
            panelObj = panelTr.gameObject;
        }

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.pivot = pivot;
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = sizeDelta;

        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.14f, 0.85f);

        // Name Text
        Transform nameTr = panelObj.transform.Find("NameText");
        GameObject nameObj;
        if (nameTr == null)
        {
            nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(nameObj, "Create NameText");
            Undo.SetTransformParent(nameObj.transform, panelObj.transform, "Parent NameText");
        }
        else
        {
            nameObj = nameTr.gameObject;
        }

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(10f, -5f);
        nameRect.sizeDelta = new Vector2(-20f, 18f);

        nameText = nameObj.GetComponent<TextMeshProUGUI>();
        nameText.text = characterName;
        if (font != null) nameText.font = font;
        nameText.fontSize = 14;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(1f, 0.92f, 0.65f, 1f);

        // Bar Background
        Transform barBgTr = panelObj.transform.Find("BarBackground");
        GameObject barBgObj;
        if (barBgTr == null)
        {
            barBgObj = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(barBgObj, "Create BarBackground");
            Undo.SetTransformParent(barBgObj.transform, panelObj.transform, "Parent BarBackground");
        }
        else
        {
            barBgObj = barBgTr.gameObject;
        }

        RectTransform barBgRect = barBgObj.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0f);
        barBgRect.anchorMax = new Vector2(1f, 0f);
        barBgRect.pivot = new Vector2(0.5f, 0f);
        barBgRect.anchoredPosition = new Vector2(0f, 6f);
        barBgRect.sizeDelta = new Vector2(-20f, 18f);

        Image barBgImage = barBgObj.GetComponent<Image>();
        barBgImage.color = new Color(0.04f, 0.04f, 0.06f, 0.95f);

        // Bar Fill
        Transform fillTr = barBgObj.transform.Find("BarFill");
        GameObject fillObj;
        if (fillTr == null)
        {
            fillObj = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(fillObj, "Create BarFill");
            Undo.SetTransformParent(fillObj.transform, barBgObj.transform, "Parent BarFill");
        }
        else
        {
            fillObj = fillTr.gameObject;
        }

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fillImage = fillObj.GetComponent<Image>();
        fillImage.sprite = Level01BattleUI.GetWhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;
        fillImage.color = fillColor;

        // HP Value Text
        Transform hpTextTr = barBgObj.transform.Find("HpText");
        GameObject hpTextObj;
        if (hpTextTr == null)
        {
            hpTextObj = new GameObject("HpText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(hpTextObj, "Create HpText");
            Undo.SetTransformParent(hpTextObj.transform, barBgObj.transform, "Parent HpText");
        }
        else
        {
            hpTextObj = hpTextTr.gameObject;
        }

        RectTransform hpTextRect = hpTextObj.GetComponent<RectTransform>();
        hpTextRect.anchorMin = Vector2.zero;
        hpTextRect.anchorMax = Vector2.one;
        hpTextRect.offsetMin = Vector2.zero;
        hpTextRect.offsetMax = Vector2.zero;

        hpText = hpTextObj.GetComponent<TextMeshProUGUI>();
        hpText.text = "100 / 100";
        if (font != null) hpText.font = font;
        hpText.fontSize = 12;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.color = Color.white;
    }

    private static void BuildResultPanel(
        Transform parent,
        TMP_FontAsset font,
        out GameObject resultPanelObj,
        out TextMeshProUGUI resultTitleTmp,
        out TextMeshProUGUI resultSubtitleTmp,
        out Button restartBtn,
        out TextMeshProUGUI restartBtnTmp)
    {
        Transform panelTr = parent.Find("ResultPanel");
        if (panelTr == null)
        {
            resultPanelObj = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(resultPanelObj, "Create ResultPanel");
            Undo.SetTransformParent(resultPanelObj.transform, parent, "Parent ResultPanel");
        }
        else
        {
            resultPanelObj = panelTr.gameObject;
        }

        RectTransform panelRect = resultPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(460f, 260f);

        Image panelBg = resultPanelObj.GetComponent<Image>();
        panelBg.color = new Color(0.06f, 0.06f, 0.09f, 0.96f);

        // Title Text
        Transform titleTr = resultPanelObj.transform.Find("TitleText");
        GameObject titleObj;
        if (titleTr == null)
        {
            titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(titleObj, "Create TitleText");
            Undo.SetTransformParent(titleObj.transform, resultPanelObj.transform, "Parent TitleText");
        }
        else
        {
            titleObj = titleTr.gameObject;
        }

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -25f);
        titleRect.sizeDelta = new Vector2(400f, 55f);

        resultTitleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        resultTitleTmp.text = "VICTORY";
        if (font != null) resultTitleTmp.font = font;
        resultTitleTmp.fontSize = 42;
        resultTitleTmp.fontStyle = FontStyles.Bold;
        resultTitleTmp.alignment = TextAlignmentOptions.Center;
        resultTitleTmp.color = new Color(1f, 0.85f, 0.2f, 1f);

        // Subtitle Text
        Transform subTr = resultPanelObj.transform.Find("SubtitleText");
        GameObject subObj;
        if (subTr == null)
        {
            subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(subObj, "Create SubtitleText");
            Undo.SetTransformParent(subObj.transform, resultPanelObj.transform, "Parent SubtitleText");
        }
        else
        {
            subObj = subTr.gameObject;
        }

        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -90f);
        subRect.sizeDelta = new Vector2(400f, 30f);

        resultSubtitleTmp = subObj.GetComponent<TextMeshProUGUI>();
        resultSubtitleTmp.text = "Cyclops Normal Defeated!";
        if (font != null) resultSubtitleTmp.font = font;
        resultSubtitleTmp.fontSize = 20;
        resultSubtitleTmp.alignment = TextAlignmentOptions.Center;
        resultSubtitleTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        // Restart Button
        Transform btnTr = resultPanelObj.transform.Find("RestartButton");
        GameObject btnObj;
        if (btnTr == null)
        {
            btnObj = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(btnObj, "Create RestartButton");
            Undo.SetTransformParent(btnObj.transform, resultPanelObj.transform, "Parent RestartButton");
        }
        else
        {
            btnObj = btnTr.gameObject;
        }

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 35f);
        btnRect.sizeDelta = new Vector2(200f, 50f);

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = new Color(0.38f, 0.28f, 0.14f, 1f);
        btnImg.raycastTarget = true;

        restartBtn = btnObj.GetComponent<Button>();
        restartBtn.targetGraphic = btnImg;

        // Restart Button Text
        Transform btnTextTr = btnObj.transform.Find("ButtonText");
        GameObject btnTextObj;
        if (btnTextTr == null)
        {
            btnTextObj = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(btnTextObj, "Create ButtonText");
            Undo.SetTransformParent(btnTextObj.transform, btnObj.transform, "Parent ButtonText");
        }
        else
        {
            btnTextObj = btnTextTr.gameObject;
        }

        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        restartBtnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
        restartBtnTmp.text = "RESTART";
        if (font != null) restartBtnTmp.font = font;
        restartBtnTmp.fontSize = 22;
        restartBtnTmp.fontStyle = FontStyles.Bold;
        restartBtnTmp.alignment = TextAlignmentOptions.Center;
        restartBtnTmp.color = new Color(1f, 0.92f, 0.65f, 1f);
        restartBtnTmp.raycastTarget = false;

        // Result panel is initially hidden
        resultPanelObj.SetActive(false);
    }

    private static GameObject FindUnique(Scene scene, string name)
    {
        GameObject result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                {
                    if (result != null) throw new InvalidOperationException("Duplicate " + name);
                    result = child.gameObject;
                }
        return result != null ? result : throw new InvalidOperationException("Missing " + name);
    }

    private static T FindUniqueComponent<T>(Scene scene) where T : Component
    {
        T result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T candidate in root.GetComponentsInChildren<T>(true))
            {
                if (result != null) throw new InvalidOperationException("Duplicate " + typeof(T).Name);
                result = candidate;
            }
        return result != null ? result : throw new InvalidOperationException("Missing " + typeof(T).Name);
    }
}
