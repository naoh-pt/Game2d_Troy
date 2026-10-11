using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Idempotent Editor setup tool for Level 01 Battle HUD and Android Touch Controls.
/// Configures a 1920x1080 landscape HUD layout:
/// - Achilles HP at Top-Left, Cyclops HP at Top-Right, Turn Text at Top-Center
/// - Projectile aim, power, and wind grouped in a compact Lower-Center HUD
/// - Move Left/Right buttons at Bottom-Left
/// - Aim Up/Down, Spear/Bomb selection, and prominent Throw button at Bottom-Right
/// - Centered Victory/Defeat ResultPanel with clickable Restart
/// - Respects phone safe areas, keeps central playfield clear, and preserves Greek typography.
/// </summary>
public static class ConfigureLevel01BattleUIAndTouchControls
{
    private const string MenuPath = "Attack on Troy/Setup/Configure Level 01 Battle UI and Touch Controls";
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";

    // Asset paths
    private const string GreekFontPath = "Assets/AttackOnTroy/Art/UI/Fonts/TMP/TMP_Greek_SDF.asset";
    private const string ButtonRoundPath = "Assets/AttackOnTroy/Art/UI/Buttons/SPR_UI_ButtonRound.png";
    private const string AimPanelPath = "Assets/AttackOnTroy/Art/UI/Panels/SPR_UI_AimPanel.png";
    private const string CharacterFramePath = "Assets/AttackOnTroy/Art/UI/Panels/SPR_UI_CharacterFrame.png";
    private const string ResultPanelPath = "Assets/AttackOnTroy/Art/UI/Panels/SPR_UI_ResultPanel.png";
    private const string SpearIconPath = "Assets/AttackOnTroy/Art/UI/Icons/SPR_UI_Icon_Spear.png";
    private const string ClubBombIconPath = "Assets/AttackOnTroy/Art/UI/Icons/SPR_UI_Icon_ClubBomb.png";

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
                "Configure Level 01 Battle UI & Touch Controls",
                "Cannot configure while in Play Mode. Exit Play Mode first.",
                "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Battle UI & Touch Controls",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        GameObject player = FindUnique(scene, "Player_Achilles");
        GameObject cyclops = FindUnique(scene, "Enemy_CyclopsNormal");
        GameObject hudCanvas = FindUnique(scene, "HUDCanvas");
        Level01TurnManager turnManager = FindUniqueComponent<Level01TurnManager>(scene);

        AchillesProjectileDebugLauncher launcher = player.GetComponent<AchillesProjectileDebugLauncher>();
        PlayerMovementDebugInput playerMovement = player.GetComponent<PlayerMovementDebugInput>();
        GroundedActorMotor2D playerMotor = player.GetComponent<GroundedActorMotor2D>();
        ActorDamageReceiver2D playerDamage = player.GetComponent<ActorDamageReceiver2D>();

        GroundedActorMotor2D cyclopsMotor = cyclops.GetComponent<GroundedActorMotor2D>();
        ActorDamageReceiver2D cyclopsDamage = cyclops.GetComponent<ActorDamageReceiver2D>();
        CyclopsNormalTurnController cyclopsController = cyclops.GetComponent<CyclopsNormalTurnController>();

        Level01CameraController cameraController = Camera.main != null ? Camera.main.GetComponent<Level01CameraController>() : null;

        // Load UI Assets
        TMP_FontAsset greekFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GreekFontPath);
        if (greekFont == null)
        {
            TextMeshProUGUI anyTmp = hudCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (anyTmp != null) greekFont = anyTmp.font;
        }

        Sprite btnRoundSprite = LoadSubSprite(ButtonRoundPath, "SPR_UI_ButtonRound_0");
        Sprite aimPanelSprite = LoadSubSprite(AimPanelPath, "SPR_UI_AimPanel_0");
        Sprite charFrameSprite = LoadSubSprite(CharacterFramePath, "SPR_UI_CharacterFrame_0");
        Sprite resultPanelSprite = LoadSubSprite(ResultPanelPath, "SPR_UI_ResultPanel_0");
        Sprite spearIconSprite = LoadSubSprite(SpearIconPath, "SPR_UI_Icon_Spear_0");
        Sprite bombIconSprite = LoadSubSprite(ClubBombIconPath, "SPR_UI_Icon_ClubBomb_1");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Battle UI & Touch Controls");

        // 0. Ensure single EventSystem exists with InputSystemUIInputModule
        UnityEngine.EventSystems.EventSystem existingEventSystem = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingEventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 1. Ensure CanvasScaler is configured for 1920x1080 landscape
        CanvasScaler scaler = hudCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Undo.RecordObject(scaler, "Configure CanvasScaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        // 2. Configure TurnText at Top-Center
        Transform turnTextTr = hudCanvas.transform.Find("TurnText");
        TextMeshProUGUI turnTextTmp = null;
        if (turnTextTr != null)
        {
            Undo.RecordObject(turnTextTr, "Reposition TurnText");
            RectTransform turnRect = turnTextTr.GetComponent<RectTransform>();
            turnRect.anchorMin = new Vector2(0.5f, 1f);
            turnRect.anchorMax = new Vector2(0.5f, 1f);
            turnRect.pivot = new Vector2(0.5f, 1f);
            turnRect.anchoredPosition = new Vector2(0f, -30f);
            turnRect.sizeDelta = new Vector2(400f, 50f);

            turnTextTmp = turnTextTr.GetComponent<TextMeshProUGUI>();
            if (turnTextTmp != null)
            {
                Undo.RecordObject(turnTextTmp, "Style TurnText");
                if (greekFont != null) turnTextTmp.font = greekFont;
                turnTextTmp.fontSize = 32f;
                turnTextTmp.fontStyle = FontStyles.Bold;
                turnTextTmp.alignment = TextAlignmentOptions.Center;
                turnTextTmp.color = new Color(1f, 0.92f, 0.65f, 1f);
                EditorUtility.SetDirty(turnTextTmp);
            }
            EditorUtility.SetDirty(turnRect);
        }

        // 3. Configure BattleUI (Health Bars Top-Left & Top-Right, ResultPanel Center)
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

        // PlayerHealthPanel at Top-Left
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
            new Vector2(40f, -30f),
            new Vector2(340f, 85f),
            new Color(0.18f, 0.82f, 0.35f, 1f),
            charFrameSprite,
            greekFont,
            out playerFill,
            out playerHpTmp,
            out playerNameTmp);

        // CyclopsHealthPanel at Top-Right
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
            new Vector2(-40f, -30f),
            new Vector2(340f, 85f),
            new Color(0.92f, 0.22f, 0.18f, 1f),
            charFrameSprite,
            greekFont,
            out cyclopsFill,
            out cyclopsHpTmp,
            out cyclopsNameTmp);

        // ResultPanel at Screen Center
        GameObject resultPanelObj;
        TextMeshProUGUI resultTitleTmp;
        TextMeshProUGUI resultSubtitleTmp;
        Button restartBtn;
        TextMeshProUGUI restartBtnTmp;
        BuildResultPanel(
            battleUiObj.transform,
            resultPanelSprite,
            btnRoundSprite,
            greekFont,
            out resultPanelObj,
            out resultTitleTmp,
            out resultSubtitleTmp,
            out restartBtn,
            out restartBtnTmp);

        // Configure Level01BattleUI
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

        if (playerDamage != null)
            battleUI.UpdatePlayerHealth(playerDamage.CurrentHealth, playerDamage.MaximumHealth);
        if (cyclopsDamage != null)
            battleUI.UpdateCyclopsHealth(cyclopsDamage.CurrentHealth, cyclopsDamage.MaximumHealth);

        EditorUtility.SetDirty(battleUI);

        // 4. Configure ProjectileAimHUD at Lower-Center
        Transform aimHudTr = hudCanvas.transform.Find("ProjectileAimHUD");
        if (aimHudTr != null)
        {
            Undo.RecordObject(aimHudTr, "Reposition ProjectileAimHUD");
            RectTransform aimRect = aimHudTr.GetComponent<RectTransform>();
            aimRect.anchorMin = new Vector2(0.5f, 0f);
            aimRect.anchorMax = new Vector2(0.5f, 0f);
            aimRect.pivot = new Vector2(0.5f, 0f);
            aimRect.anchoredPosition = new Vector2(0f, 30f);
            aimRect.sizeDelta = new Vector2(440f, 115f);

            // Style panel background
            Image aimBg = aimHudTr.GetComponent<Image>();
            if (aimBg != null)
            {
                Undo.RecordObject(aimBg, "Style ProjectileAimHUD Background");
                if (aimPanelSprite != null)
                {
                    aimBg.sprite = aimPanelSprite;
                    aimBg.type = Image.Type.Simple;
                    aimBg.preserveAspect = true;
                    aimBg.color = Color.white;
                }
                else
                {
                    aimBg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
                }
                EditorUtility.SetDirty(aimBg);
            }

            // Adjust child positions inside AimHUD
            Transform angleTr = aimHudTr.Find("AngleText");
            if (angleTr != null)
            {
                RectTransform rt = angleTr.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -18f);
                rt.sizeDelta = new Vector2(-25f, 22f);
                TextMeshProUGUI tmp = angleTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null && greekFont != null) { tmp.font = greekFont; tmp.fontSize = 15f; }
            }

            Transform windTr = aimHudTr.Find("WindText");
            if (windTr != null)
            {
                RectTransform rt = windTr.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-25f, -18f);
                rt.sizeDelta = new Vector2(-25f, 22f);
                TextMeshProUGUI tmp = windTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null && greekFont != null) { tmp.font = greekFont; tmp.fontSize = 15f; tmp.alignment = TextAlignmentOptions.Right; }
            }

            Transform pBarTr = aimHudTr.Find("PowerBar");
            if (pBarTr != null)
            {
                RectTransform rt = pBarTr.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 48f);
                rt.sizeDelta = new Vector2(380f, 18f);
            }

            Transform wpnTr = aimHudTr.Find("WeaponText");
            if (wpnTr != null)
            {
                RectTransform rt = wpnTr.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 16f);
                rt.sizeDelta = new Vector2(300f, 20f);
                TextMeshProUGUI tmp = wpnTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null && greekFont != null)
                {
                    tmp.font = greekFont;
                    tmp.fontSize = 14f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(1f, 0.88f, 0.5f, 1f);
                }
            }

            EditorUtility.SetDirty(aimRect);
        }

        // 5. Configure Touch Controls (Bottom-Left Movement & Bottom-Right Action)
        Transform touchRootTr = hudCanvas.transform.Find("TouchControls");
        GameObject touchRootObj;
        if (touchRootTr == null)
        {
            touchRootObj = new GameObject("TouchControls", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(touchRootObj, "Create TouchControls");
            Undo.SetTransformParent(touchRootObj.transform, hudCanvas.transform, "Parent TouchControls");
        }
        else
        {
            touchRootObj = touchRootTr.gameObject;
        }

        RectTransform touchRootRect = touchRootObj.GetComponent<RectTransform>();
        touchRootRect.anchorMin = Vector2.zero;
        touchRootRect.anchorMax = Vector2.one;
        touchRootRect.offsetMin = Vector2.zero;
        touchRootRect.offsetMax = Vector2.zero;

        CanvasGroup touchGroup = touchRootObj.GetComponent<CanvasGroup>();
        if (touchGroup == null) touchGroup = Undo.AddComponent<CanvasGroup>(touchRootObj);

        Level01TouchControls touchControls = touchRootObj.GetComponent<Level01TouchControls>();
        if (touchControls == null) touchControls = Undo.AddComponent<Level01TouchControls>(touchRootObj);

        // Build Movement Panel (Bottom-Left)
        TouchHoldButton moveLeftBtn;
        TouchHoldButton moveRightBtn;
        BuildMovementPanel(
            touchRootObj.transform,
            btnRoundSprite,
            greekFont,
            out moveLeftBtn,
            out moveRightBtn);

        // Build Action Panel (Bottom-Right: Aim Up/Down, Spear, Bomb, Throw)
        TouchHoldButton aimUpBtn;
        TouchHoldButton aimDownBtn;
        Button spearBtn;
        Button bombBtn;
        TouchHoldButton throwBtn;
        Image spearSelectImg;
        Image bombSelectImg;
        BuildActionPanel(
            touchRootObj.transform,
            btnRoundSprite,
            spearIconSprite,
            bombIconSprite,
            greekFont,
            out aimUpBtn,
            out aimDownBtn,
            out spearBtn,
            out bombBtn,
            out throwBtn,
            out spearSelectImg,
            out bombSelectImg);

        // Wire Level01TouchControls
        Undo.RecordObject(touchControls, "Configure Level01TouchControls");
        touchControls.Configure(
            launcher,
            playerMovement,
            playerMotor,
            turnManager,
            touchGroup,
            moveLeftBtn,
            moveRightBtn,
            aimUpBtn,
            aimDownBtn,
            throwBtn,
            spearBtn,
            bombBtn,
            spearSelectImg,
            bombSelectImg);
        EditorUtility.SetDirty(touchControls);

        // 6. Connect to Level01TurnManager
        if (turnManager != null)
        {
            Undo.RecordObject(turnManager, "Wire TurnManager References");
            var turnData = new SerializedObject(turnManager);
            turnData.FindProperty("launcher").objectReferenceValue = launcher;
            turnData.FindProperty("playerTransform").objectReferenceValue = player.transform;
            turnData.FindProperty("cyclopsTransform").objectReferenceValue = cyclops.transform;
            turnData.FindProperty("playerMotor").objectReferenceValue = playerMotor;
            turnData.FindProperty("cyclopsMotor").objectReferenceValue = cyclopsMotor;
            turnData.FindProperty("cyclopsController").objectReferenceValue = cyclopsController;
            turnData.FindProperty("playerDamageReceiver").objectReferenceValue = playerDamage;
            turnData.FindProperty("cyclopsDamageReceiver").objectReferenceValue = cyclopsDamage;
            turnData.FindProperty("cameraController").objectReferenceValue = cameraController;
            turnData.FindProperty("turnText").objectReferenceValue = turnTextTmp;
            turnData.FindProperty("battleUI").objectReferenceValue = battleUI;

            var delayProp = turnData.FindProperty("cyclopsTurnDelay");
            if (delayProp != null) delayProp.floatValue = 1.5f;

            var resultDelayProp = turnData.FindProperty("battleResultDelay");
            if (resultDelayProp != null) resultDelayProp.floatValue = 1.0f;

            turnData.ApplyModifiedProperties();
            EditorUtility.SetDirty(turnManager);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);

        Debug.Log("[ConfigureLevel01BattleUIAndTouchControls] Successfully configured Level 01 Battle UI and Android Touch Controls! " +
                  "Scene marked dirty with Undo (NOT auto-saved).");
    }

    private static void BuildMovementPanel(
        Transform parent,
        Sprite buttonSprite,
        TMP_FontAsset font,
        out TouchHoldButton moveLeftBtn,
        out TouchHoldButton moveRightBtn)
    {
        Transform panelTr = parent.Find("MovementPanel");
        GameObject panelObj;
        if (panelTr == null)
        {
            panelObj = new GameObject("MovementPanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panelObj, "Create MovementPanel");
            Undo.SetTransformParent(panelObj.transform, parent, "Parent MovementPanel");
        }
        else
        {
            panelObj = panelTr.gameObject;
        }

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(50f, 40f);
        panelRect.sizeDelta = new Vector2(240f, 110f);

        // Move Left Button
        moveLeftBtn = BuildRoundHoldButton(
            panelObj.transform,
            "ButtonMoveLeft",
            new Vector2(55f, 55f),
            new Vector2(100f, 100f),
            buttonSprite,
            "◀",
            38f,
            font);

        // Move Right Button
        moveRightBtn = BuildRoundHoldButton(
            panelObj.transform,
            "ButtonMoveRight",
            new Vector2(175f, 55f),
            new Vector2(100f, 100f),
            buttonSprite,
            "▶",
            38f,
            font);
    }

    private static void BuildActionPanel(
        Transform parent,
        Sprite buttonSprite,
        Sprite spearIcon,
        Sprite bombIcon,
        TMP_FontAsset font,
        out TouchHoldButton aimUpBtn,
        out TouchHoldButton aimDownBtn,
        out Button spearBtn,
        out Button bombBtn,
        out TouchHoldButton throwBtn,
        out Image spearSelectImg,
        out Image bombSelectImg)
    {
        Transform panelTr = parent.Find("ActionPanel");
        GameObject panelObj;
        if (panelTr == null)
        {
            panelObj = new GameObject("ActionPanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panelObj, "Create ActionPanel");
            Undo.SetTransformParent(panelObj.transform, parent, "Parent ActionPanel");
        }
        else
        {
            panelObj = panelTr.gameObject;
        }

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-50f, 40f);
        panelRect.sizeDelta = new Vector2(470f, 210f);

        // Aim Up Button
        aimUpBtn = BuildRoundHoldButton(
            panelObj.transform,
            "ButtonAimUp",
            new Vector2(-390f, 135f),
            new Vector2(75f, 75f),
            buttonSprite,
            "▲",
            28f,
            font,
            new Vector2(1f, 0f));

        // Aim Down Button
        aimDownBtn = BuildRoundHoldButton(
            panelObj.transform,
            "ButtonAimDown",
            new Vector2(-390f, 45f),
            new Vector2(75f, 75f),
            buttonSprite,
            "▼",
            28f,
            font,
            new Vector2(1f, 0f));

        // Weapon Spear Button
        spearBtn = BuildWeaponButton(
            panelObj.transform,
            "ButtonWeaponSpear",
            new Vector2(-280f, 135f),
            new Vector2(85f, 85f),
            buttonSprite,
            spearIcon,
            out spearSelectImg,
            new Vector2(1f, 0f));

        // Weapon Bomb Button
        bombBtn = BuildWeaponButton(
            panelObj.transform,
            "ButtonWeaponBomb",
            new Vector2(-280f, 45f),
            new Vector2(85f, 85f),
            buttonSprite,
            bombIcon,
            out bombSelectImg,
            new Vector2(1f, 0f));

        // Throw Button (Prominent, large)
        throwBtn = BuildRoundHoldButton(
            panelObj.transform,
            "ButtonThrow",
            new Vector2(-105f, 95f),
            new Vector2(145f, 145f),
            buttonSprite,
            "THROW",
            26f,
            font,
            new Vector2(1f, 0f));

        // Style Throw Button label in bold gold
        var throwText = throwBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (throwText != null)
        {
            throwText.fontStyle = FontStyles.Bold;
            throwText.color = new Color(1f, 0.95f, 0.72f, 1f);
        }
    }

    private static TouchHoldButton BuildRoundHoldButton(
        Transform parent,
        string buttonName,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Sprite sprite,
        string label,
        float fontSize,
        TMP_FontAsset font,
        Vector2? pivot = null)
    {
        Transform btnTr = parent.Find(buttonName);
        GameObject btnObj;
        if (btnTr == null)
        {
            btnObj = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(TouchHoldButton));
            Undo.RegisterCreatedObjectUndo(btnObj, "Create " + buttonName);
            Undo.SetTransformParent(btnObj.transform, parent, "Parent " + buttonName);
        }
        else
        {
            btnObj = btnTr.gameObject;
        }

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = pivot ?? new Vector2(0f, 0f);
        rect.anchorMax = pivot ?? new Vector2(0f, 0f);
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image img = btnObj.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(btnObj);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.25f, 0.22f, 0.18f, 0.95f);
        }
        img.raycastTarget = true;

        TouchHoldButton holdBtn = btnObj.GetComponent<TouchHoldButton>();
        if (holdBtn == null) holdBtn = Undo.AddComponent<TouchHoldButton>(btnObj);

        // Child Label
        Transform labelTr = btnObj.transform.Find("Label");
        GameObject labelObj;
        if (labelTr == null)
        {
            labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(labelObj, "Create Label for " + buttonName);
            Undo.SetTransformParent(labelObj.transform, btnObj.transform, "Parent Label");
        }
        else
        {
            labelObj = labelTr.gameObject;
        }

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.65f, 1f);
        tmp.raycastTarget = false;

        return holdBtn;
    }

    private static Button BuildWeaponButton(
        Transform parent,
        string buttonName,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Sprite buttonSprite,
        Sprite iconSprite,
        out Image selectionIndicator,
        Vector2? pivot = null)
    {
        Transform btnTr = parent.Find(buttonName);
        GameObject btnObj;
        if (btnTr == null)
        {
            btnObj = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(btnObj, "Create " + buttonName);
            Undo.SetTransformParent(btnObj.transform, parent, "Parent " + buttonName);
        }
        else
        {
            btnObj = btnTr.gameObject;
        }

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = pivot ?? new Vector2(0f, 0f);
        rect.anchorMax = pivot ?? new Vector2(0f, 0f);
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image img = btnObj.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(btnObj);
        if (buttonSprite != null)
        {
            img.sprite = buttonSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        }
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();
        if (btn == null) btn = Undo.AddComponent<Button>(btnObj);
        btn.targetGraphic = img;

        // Selection Highlight Ring/Border
        Transform selTr = btnObj.transform.Find("SelectionIndicator");
        GameObject selObj;
        if (selTr == null)
        {
            selObj = new GameObject("SelectionIndicator", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(selObj, "Create SelectionIndicator for " + buttonName);
            Undo.SetTransformParent(selObj.transform, btnObj.transform, "Parent SelectionIndicator");
        }
        else
        {
            selObj = selTr.gameObject;
        }

        RectTransform selRect = selObj.GetComponent<RectTransform>();
        selRect.anchorMin = new Vector2(0.5f, 0.5f);
        selRect.anchorMax = new Vector2(0.5f, 0.5f);
        selRect.pivot = new Vector2(0.5f, 0.5f);
        selRect.anchoredPosition = Vector2.zero;
        selRect.sizeDelta = sizeDelta * 1.18f;

        selectionIndicator = selObj.GetComponent<Image>();
        if (buttonSprite != null)
        {
            selectionIndicator.sprite = buttonSprite;
            selectionIndicator.type = Image.Type.Simple;
            selectionIndicator.preserveAspect = true;
        }
        selectionIndicator.color = new Color(1f, 0.85f, 0.2f, 0.85f); // Bright golden halo
        selectionIndicator.raycastTarget = false;
        selObj.transform.SetAsFirstSibling(); // Render beneath icon but above/around base button

        // Child Weapon Icon
        Transform iconTr = btnObj.transform.Find("WeaponIcon");
        GameObject iconObj;
        if (iconTr == null)
        {
            iconObj = new GameObject("WeaponIcon", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(iconObj, "Create WeaponIcon for " + buttonName);
            Undo.SetTransformParent(iconObj.transform, btnObj.transform, "Parent WeaponIcon");
        }
        else
        {
            iconObj = iconTr.gameObject;
        }

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = sizeDelta * 0.70f;

        Image iconImg = iconObj.GetComponent<Image>();
        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
        }
        iconImg.raycastTarget = false;

        return btn;
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
        Sprite frameSprite,
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
        if (frameSprite != null)
        {
            panelBg.sprite = frameSprite;
            panelBg.type = Image.Type.Simple;
            panelBg.preserveAspect = true;
            panelBg.color = Color.white;
        }
        else
        {
            panelBg.color = new Color(0.1f, 0.1f, 0.14f, 0.85f);
        }

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
        nameRect.anchoredPosition = new Vector2(15f, -8f);
        nameRect.sizeDelta = new Vector2(-30f, 22f);

        nameText = nameObj.GetComponent<TextMeshProUGUI>();
        nameText.text = characterName;
        if (font != null) nameText.font = font;
        nameText.fontSize = 15;
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
        barBgRect.anchoredPosition = new Vector2(0f, 10f);
        barBgRect.sizeDelta = new Vector2(-30f, 22f);

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
        hpText.fontSize = 13;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.color = Color.white;
    }

    private static void BuildResultPanel(
        Transform parent,
        Sprite resultPanelSprite,
        Sprite buttonSprite,
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
        panelRect.sizeDelta = new Vector2(480f, 300f);

        Image panelBg = resultPanelObj.GetComponent<Image>();
        if (resultPanelSprite != null)
        {
            panelBg.sprite = resultPanelSprite;
            panelBg.type = Image.Type.Simple;
            panelBg.preserveAspect = true;
            panelBg.color = Color.white;
        }
        else
        {
            panelBg.color = new Color(0.06f, 0.06f, 0.09f, 0.96f);
        }

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
        titleRect.anchoredPosition = new Vector2(0f, -35f);
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
        subRect.anchoredPosition = new Vector2(0f, -100f);
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
        btnRect.sizeDelta = new Vector2(210f, 54f);

        Image btnImg = btnObj.GetComponent<Image>();
        if (buttonSprite != null)
        {
            btnImg.sprite = buttonSprite;
            btnImg.type = Image.Type.Simple;
            btnImg.preserveAspect = true;
            btnImg.color = Color.white;
        }
        else
        {
            btnImg.color = new Color(0.38f, 0.28f, 0.14f, 1f);
        }
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

        resultPanelObj.SetActive(false);
    }

    private static Sprite LoadSubSprite(string assetPath, string spriteName)
    {
        UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (objects == null || objects.Length == 0)
        {
            string altPath = assetPath.EndsWith(".png.png") ? assetPath.Substring(0, assetPath.Length - 4) : assetPath + ".png";
            objects = AssetDatabase.LoadAllAssetsAtPath(altPath);
        }
        if (objects == null || objects.Length == 0) return null;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is Sprite s && string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase))
                return s;
        }
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is Sprite s && s.name.StartsWith(spriteName.Split('_')[0], StringComparison.OrdinalIgnoreCase))
                return s;
        }
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is Sprite s)
                return s;
        }
        return null;
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
