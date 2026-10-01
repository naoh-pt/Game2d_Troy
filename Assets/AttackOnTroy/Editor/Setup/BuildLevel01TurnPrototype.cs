using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent setup tool for the Level 01 turn prototype.
/// Configures Level01TurnManager under Systems, TurnText under HUDCanvas,
/// and links player, Cyclops, launcher, motor, camera, and HUD references.
/// </summary>
public static class BuildLevel01TurnPrototype
{
    private const string MenuPath = "Attack on Troy/Setup/Build Level 01 Turn Prototype";
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";

    [MenuItem(MenuPath)]
    public static void BuildPrototype()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Build Level 01 Turn Prototype",
                "Cannot build turn prototype while in Play Mode. Exit Play Mode first.",
                "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Build Level 01 Turn Prototype",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        var problems = new List<string>();

        // 1. Validate Player_Achilles
        GameObject playerObject = FindUniqueObject(scene, "Player_Achilles", problems);
        AchillesProjectileDebugLauncher launcher = null;
        GroundedActorMotor2D motor = null;
        if (playerObject != null)
        {
            launcher = playerObject.GetComponent<AchillesProjectileDebugLauncher>();
            if (launcher == null)
                problems.Add("Player_Achilles is missing AchillesProjectileDebugLauncher component. Run 'Build Achilles Projectile Prototype' first.");

            motor = playerObject.GetComponent<GroundedActorMotor2D>();
            if (motor == null)
                problems.Add("Player_Achilles is missing GroundedActorMotor2D component.");
        }

        // 2. Validate Enemy_CyclopsNormal
        GameObject cyclopsObject = FindUniqueObject(scene, "Enemy_CyclopsNormal", problems);

        // 3. Validate Camera
        Level01CameraController cameraController = UnityEngine.Object.FindFirstObjectByType<Level01CameraController>();
        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<Level01CameraController>();
        if (cameraController == null)
            problems.Add("Level01CameraController not found in scene.");

        // 4. Validate Systems root
        GameObject systemsObject = FindUniqueObject(scene, "Systems", null);
        if (systemsObject == null)
        {
            systemsObject = new GameObject("Systems");
            Undo.RegisterCreatedObjectUndo(systemsObject, "Create Systems");
        }

        // 5. Validate HUDCanvas
        GameObject hudCanvasObject = FindUniqueObject(scene, "HUDCanvas", null);
        if (hudCanvasObject == null)
        {
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                hudCanvasObject = canvas.gameObject;
            else
                problems.Add("HUDCanvas or Canvas not found in scene.");
        }

        if (problems.Count > 0)
        {
            string message = "Cannot build turn prototype due to missing dependencies:\n- " + string.Join("\n- ", problems);
            EditorUtility.DisplayDialog("Build Level 01 Turn Prototype", message, "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Level 01 Turn Prototype");

        // --- Configure Level01TurnManager under Systems ---
        Transform turnManagerTr = systemsObject.transform.Find("TurnManager");
        if (turnManagerTr == null)
            turnManagerTr = systemsObject.transform.Find("Level01TurnManager");

        GameObject turnManagerObj;
        if (turnManagerTr == null)
        {
            turnManagerObj = new GameObject("TurnManager", typeof(Level01TurnManager));
            Undo.RegisterCreatedObjectUndo(turnManagerObj, "Create TurnManager");
            Undo.SetTransformParent(turnManagerObj.transform, systemsObject.transform, "Parent TurnManager");
        }
        else
        {
            turnManagerObj = turnManagerTr.gameObject;
        }

        Level01TurnManager turnManager = turnManagerObj.GetComponent<Level01TurnManager>();
        if (turnManager == null)
            turnManager = Undo.AddComponent<Level01TurnManager>(turnManagerObj);

        Undo.RecordObject(turnManager, "Configure Level01TurnManager");

        // --- Configure TurnText under HUDCanvas ---
        Transform turnTextTr = hudCanvasObject.transform.Find("TurnText");
        GameObject turnTextObj;
        if (turnTextTr == null)
        {
            turnTextObj = new GameObject("TurnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(turnTextObj, "Create TurnText");
            Undo.SetTransformParent(turnTextObj.transform, hudCanvasObject.transform, "Parent TurnText");
        }
        else
        {
            turnTextObj = turnTextTr.gameObject;
        }

        RectTransform turnRect = turnTextObj.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.5f, 1f);
        turnRect.anchorMax = new Vector2(0.5f, 1f);
        turnRect.pivot = new Vector2(0.5f, 1f);
        turnRect.anchoredPosition = new Vector2(0f, -25f);
        turnRect.sizeDelta = new Vector2(320f, 40f);

        TextMeshProUGUI turnTMP = turnTextObj.GetComponent<TextMeshProUGUI>();
        if (turnTMP == null)
            turnTMP = Undo.AddComponent<TextMeshProUGUI>(turnTextObj);

        // Copy font asset from existing AngleText if available, preserving custom font
        Transform angleTr = hudCanvasObject.transform.Find("ProjectileAimHUD/AngleText");
        if (angleTr != null)
        {
            TextMeshProUGUI angleTMP = angleTr.GetComponent<TextMeshProUGUI>();
            if (angleTMP != null && angleTMP.font != null)
            {
                turnTMP.font = angleTMP.font;
            }
        }
        if (turnTMP.font == null && TMP_Settings.defaultFontAsset != null)
        {
            turnTMP.font = TMP_Settings.defaultFontAsset;
        }

        turnTMP.fontSize = 24f;
        turnTMP.fontStyle = FontStyles.Bold;
        turnTMP.alignment = TextAlignmentOptions.Center;
        turnTMP.color = new Color(1f, 0.92f, 0.65f, 1f);
        turnTMP.text = "PLAYER TURN";

        // Wire TurnManager references
        turnManager.Configure(
            launcher,
            playerObject.transform,
            cyclopsObject.transform,
            motor,
            cameraController,
            turnTMP);

        EditorUtility.SetDirty(turnManager);
        EditorUtility.SetDirty(turnManagerObj);
        EditorUtility.SetDirty(turnTMP);
        EditorUtility.SetDirty(turnTextObj);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "Build Level 01 Turn Prototype",
            "Level 01 Turn Prototype successfully configured:\n" +
            "- Level01TurnManager created/updated under Systems\n" +
            "- TurnText created/updated under HUDCanvas (Top-Center)\n" +
            "- References linked: Player_Achilles, Enemy_CyclopsNormal, Launcher, Motor, Camera, HUD\n" +
            "- Turn state sequence: PlayerTurn -> PlayerThrowing -> ProjectileFlight -> ResolutionDelay -> CyclopsTurn -> TurnTransition\n\n" +
            "Scene marked dirty without saving. Inspect and save manually with Ctrl+S.",
            "OK");
    }

    private static GameObject FindUniqueObject(Scene scene, string name, List<string> problems)
    {
        GameObject match = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject current = roots[i];
            if (string.Equals(current.name, name, StringComparison.Ordinal))
            {
                match = current;
                break;
            }

            Transform child = current.transform.Find(name);
            if (child != null)
            {
                match = child.gameObject;
                break;
            }
        }

        if (match == null)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                match = FindChildRecursive(roots[i].transform, name);
                if (match != null)
                    break;
            }
        }

        if (match == null && problems != null)
        {
            problems.Add("Missing required scene object: '" + name + "'");
        }

        return match;
    }

    private static GameObject FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child.gameObject;

            GameObject deeper = FindChildRecursive(child, name);
            if (deeper != null)
                return deeper;
        }

        return null;
    }
}
