using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Idempotent setup tool for the Achilles projectile prototype.
/// Validates scene dependencies, builds/updates the spear projectile prefab,
/// configures the ProjectileSpawnPoint, and attaches the debug launcher to Player_Achilles.
/// </summary>
public static class BuildAchillesProjectilePrototype
{
    private const string MenuPath = "Attack on Troy/Setup/Build Achilles Projectile Prototype";
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string SpritePath = "Assets/AttackOnTroy/Art/Projectiles/Achilles/SPR_Projectile_Achilles_Spear.png";
    private const string PrefabFolder = "Assets/AttackOnTroy/Combat/Prefabs/Projectiles";
    private const string PrefabPath = "Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_Spear.prefab";
    private const string MaterialPath = "Assets/AttackOnTroy/Physics/Materials/PM_ActorMovement.physicsMaterial2D";

    [MenuItem(MenuPath)]
    public static void BuildPrototype()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Build Achilles Projectile Prototype",
                "Cannot build prototype while in Play Mode. Exit Play Mode first.",
                "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Build Achilles Projectile Prototype",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        var problems = new List<string>();

        // 1. Validate spear sprite
        Sprite spearSprite = LoadSpearSprite(SpritePath);
        if (spearSprite == null)
            problems.Add("Spear sprite not found at: " + SpritePath);

        // 2. Validate scene objects
        GameObject playerObject = FindUniqueObject(scene, "Player_Achilles", problems);

        GameObject terrainObject = FindUniqueObject(scene, "ENV_Beach_Terrain", null);
        if (terrainObject == null)
        {
            DestructibleTerrain2D foundTerrain = UnityEngine.Object.FindAnyObjectByType<DestructibleTerrain2D>();
            if (foundTerrain != null)
                terrainObject = foundTerrain.gameObject;
            else
                problems.Add("Could not find ENV_Beach_Terrain or any DestructibleTerrain2D in active scene.");
        }

        GameObject cameraObject = FindUniqueObject(scene, "Main Camera", null);
        if (cameraObject == null)
        {
            Level01CameraController foundCam = UnityEngine.Object.FindAnyObjectByType<Level01CameraController>();
            if (foundCam != null)
                cameraObject = foundCam.gameObject;
            else
                problems.Add("Could not find Main Camera or any Level01CameraController in active scene.");
        }

        GameObject boundsObject = FindUniqueObject(scene, "LevelBounds", problems);

        GroundedActorMotor2D motor = null;
        Collider2D playerCollider = null;
        Animator playerAnimator = null;
        if (playerObject != null)
        {
            motor = playerObject.GetComponent<GroundedActorMotor2D>();
            if (motor == null)
                problems.Add("Player_Achilles is missing GroundedActorMotor2D.");
            playerCollider = playerObject.GetComponent<Collider2D>();
            if (playerCollider == null)
                problems.Add("Player_Achilles is missing a Collider2D.");
            playerAnimator = playerObject.GetComponentInChildren<Animator>();
            if (playerAnimator == null)
                problems.Add("Player_Achilles is missing an Animator component in its hierarchy.");
        }

        DestructibleTerrain2D terrain = null;
        if (terrainObject != null)
        {
            terrain = terrainObject.GetComponent<DestructibleTerrain2D>();
            if (terrain == null)
                problems.Add("ENV_Beach_Terrain is missing DestructibleTerrain2D.");
        }

        Level01CameraController cameraController = null;
        if (cameraObject != null)
        {
            cameraController = cameraObject.GetComponent<Level01CameraController>();
            if (cameraController == null)
                problems.Add("Main Camera is missing Level01CameraController.");
        }

        Collider2D boundsCollider = null;
        if (boundsObject != null)
        {
            boundsCollider = boundsObject.GetComponent<Collider2D>();
            if (boundsCollider == null)
                problems.Add("LevelBounds is missing a Collider2D.");
        }

        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Build Achilles Projectile Prototype",
                string.Join("\n", problems) + "\n\nNo changes were made.",
                "OK");
            return;
        }

        // 3. Create or update projectile prefab
        EnsureFolder(PrefabFolder);
        GameObject savedPrefab = CreateOrUpdateProjectilePrefab(spearSprite);
        if (savedPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Build Achilles Projectile Prototype",
                "Failed to create or update projectile prefab at: " + PrefabPath,
                "OK");
            return;
        }

        // 4. Configure scene instance under Undo
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Achilles Projectile Prototype");

        // 4a. Add or update ProjectileSpawnPoint under Player_Achilles
        Transform spawnPoint = playerObject.transform.Find("ProjectileSpawnPoint");
        if (spawnPoint == null)
        {
            GameObject spawnPointObj = new GameObject("ProjectileSpawnPoint");
            Undo.RegisterCreatedObjectUndo(spawnPointObj, "Create ProjectileSpawnPoint");
            Undo.SetTransformParent(spawnPointObj.transform, playerObject.transform, "Parent ProjectileSpawnPoint");
            spawnPointObj.transform.localPosition = new Vector3(0.8f, 1.2f, 0f);
            spawnPointObj.transform.localRotation = Quaternion.identity;
            spawnPointObj.transform.localScale = Vector3.one;
            spawnPoint = spawnPointObj.transform;
        }
        else
        {
            Undo.RecordObject(spawnPoint, "Update ProjectileSpawnPoint");
            if (spawnPoint.localPosition == Vector3.zero)
                spawnPoint.localPosition = new Vector3(0.8f, 1.2f, 0f);
        }

        // 4b. Add or update AchillesProjectileDebugLauncher on Player_Achilles
        AchillesProjectileDebugLauncher[] existingLaunchers = playerObject.GetComponents<AchillesProjectileDebugLauncher>();
        AchillesProjectileDebugLauncher launcher;
        if (existingLaunchers.Length == 0)
        {
            launcher = Undo.AddComponent<AchillesProjectileDebugLauncher>(playerObject);
        }
        else
        {
            launcher = existingLaunchers[0];
            for (int i = 1; i < existingLaunchers.Length; i++)
            {
                Undo.DestroyObjectImmediate(existingLaunchers[i]);
            }
        }

        Undo.RecordObject(launcher, "Configure Achilles Projectile Launcher");
        launcher.Configure(
            savedPrefab,
            spawnPoint,
            motor,
            playerCollider,
            cameraController,
            terrain,
            boundsCollider,
            playerAnimator,
            "Attack",
            "Attack",
            0.45f);

        // 4c. Add or update AimGuideRenderer on ProjectileSpawnPoint
        AimGuideRenderer aimGuide = spawnPoint.GetComponent<AimGuideRenderer>();
        if (aimGuide == null)
            aimGuide = Undo.AddComponent<AimGuideRenderer>(spawnPoint.gameObject);

        LineRenderer lineRenderer = spawnPoint.GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = Undo.AddComponent<LineRenderer>(spawnPoint.gameObject);

        Material lineMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
        if (lineMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
                lineMaterial = new Material(spriteShader) { name = "M_AimGuideLine" };
        }
        if (lineMaterial != null)
            lineRenderer.sharedMaterial = lineMaterial;

        aimGuide.Configure(launcher, spawnPoint);
        EditorUtility.SetDirty(aimGuide);
        EditorUtility.SetDirty(lineRenderer);

        // 4d. Ensure HUDCanvas and add or update ProjectileAimHUD
        GameObject hudCanvasObject = FindUniqueObject(scene, "HUDCanvas", null);
        if (hudCanvasObject == null)
        {
            Canvas sceneCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (sceneCanvas != null)
            {
                hudCanvasObject = sceneCanvas.gameObject;
            }
            else
            {
                GameObject uiRoot = FindUniqueObject(scene, "UI", null);
                hudCanvasObject = new GameObject("HUDCanvas");
                Undo.RegisterCreatedObjectUndo(hudCanvasObject, "Create HUDCanvas");
                if (uiRoot != null)
                    Undo.SetTransformParent(hudCanvasObject.transform, uiRoot.transform, "Parent HUDCanvas");
            }
        }

        Canvas canvas = hudCanvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = Undo.AddComponent<Canvas>(hudCanvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = hudCanvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = Undo.AddComponent<CanvasScaler>(hudCanvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (hudCanvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            Undo.AddComponent<GraphicRaycaster>(hudCanvasObject);
        }

        Transform existingHUDTransform = hudCanvasObject.transform.Find("ProjectileAimHUD");
        GameObject hudObject;
        if (existingHUDTransform == null)
        {
            hudObject = new GameObject("ProjectileAimHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(ProjectileAimHUD));
            Undo.RegisterCreatedObjectUndo(hudObject, "Create ProjectileAimHUD");
            Undo.SetTransformParent(hudObject.transform, hudCanvasObject.transform, "Parent ProjectileAimHUD");
        }
        else
        {
            hudObject = existingHUDTransform.gameObject;
            Undo.RecordObject(hudObject, "Update ProjectileAimHUD");
        }

        RectTransform hudRect = hudObject.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(30f, -30f);
        hudRect.sizeDelta = new Vector2(250f, 115f);

        Image hudBg = hudObject.GetComponent<Image>();
        if (hudBg == null) hudBg = Undo.AddComponent<Image>(hudObject);
        hudBg.color = new Color(0.08f, 0.10f, 0.14f, 0.80f);

        CanvasGroup canvasGroup = hudObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = Undo.AddComponent<CanvasGroup>(hudObject);

        ProjectileAimHUD aimHUD = hudObject.GetComponent<ProjectileAimHUD>();
        if (aimHUD == null) aimHUD = Undo.AddComponent<ProjectileAimHUD>(hudObject);
        Undo.RecordObject(aimHUD, "Configure ProjectileAimHUD");

        // AngleText
        Transform angleTr = hudObject.transform.Find("AngleText");
        GameObject angleObj;
        if (angleTr == null)
        {
            angleObj = new GameObject("AngleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(angleObj, "Create AngleText");
            Undo.SetTransformParent(angleObj.transform, hudObject.transform, "Parent AngleText");
        }
        else
        {
            angleObj = angleTr.gameObject;
        }
        RectTransform angleRect = angleObj.GetComponent<RectTransform>();
        angleRect.anchorMin = new Vector2(0f, 1f);
        angleRect.anchorMax = new Vector2(1f, 1f);
        angleRect.pivot = new Vector2(0f, 1f);
        angleRect.anchoredPosition = new Vector2(16f, -12f);
        angleRect.sizeDelta = new Vector2(-32f, 24f);

        TextMeshProUGUI angleTMP = angleObj.GetComponent<TextMeshProUGUI>();
        if (angleTMP == null) angleTMP = Undo.AddComponent<TextMeshProUGUI>(angleObj);
        angleTMP.fontSize = 18f;
        angleTMP.fontStyle = FontStyles.Bold;
        angleTMP.color = Color.white;
        angleTMP.text = "ANGLE: 45°";
        if (angleTMP.font == null && TMP_Settings.defaultFontAsset != null)
            angleTMP.font = TMP_Settings.defaultFontAsset;

        // PowerBar
        Transform powerBarTr = hudObject.transform.Find("PowerBar");
        GameObject powerBarObj;
        if (powerBarTr == null)
        {
            powerBarObj = new GameObject("PowerBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(powerBarObj, "Create PowerBar");
            Undo.SetTransformParent(powerBarObj.transform, hudObject.transform, "Parent PowerBar");
        }
        else
        {
            powerBarObj = powerBarTr.gameObject;
        }
        RectTransform powerBarRect = powerBarObj.GetComponent<RectTransform>();
        powerBarRect.anchorMin = new Vector2(0f, 1f);
        powerBarRect.anchorMax = new Vector2(1f, 1f);
        powerBarRect.pivot = new Vector2(0f, 1f);
        powerBarRect.anchoredPosition = new Vector2(16f, -44f);
        powerBarRect.sizeDelta = new Vector2(-32f, 20f);

        Image powerBarBg = powerBarObj.GetComponent<Image>();
        if (powerBarBg == null) powerBarBg = Undo.AddComponent<Image>(powerBarObj);
        powerBarBg.color = new Color(0.20f, 0.22f, 0.26f, 0.95f);

        // Fill
        Transform fillTr = powerBarObj.transform.Find("Fill");
        GameObject fillObj;
        if (fillTr == null)
        {
            fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(fillObj, "Create Fill");
            Undo.SetTransformParent(fillObj.transform, powerBarObj.transform, "Parent Fill");
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

        Image fillImage = fillObj.GetComponent<Image>();
        if (fillImage == null) fillImage = Undo.AddComponent<Image>(fillObj);
        fillImage.color = new Color(1f, 0.65f, 0.1f, 1f);
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (uiSprite != null)
        {
            fillImage.sprite = uiSprite;
        }
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 0f;

        // Label
        Transform labelTr = powerBarObj.transform.Find("Label");
        GameObject labelObj;
        if (labelTr == null)
        {
            labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(labelObj, "Create Label");
            Undo.SetTransformParent(labelObj.transform, powerBarObj.transform, "Parent Label");
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

        TextMeshProUGUI powerLabelTMP = labelObj.GetComponent<TextMeshProUGUI>();
        if (powerLabelTMP == null) powerLabelTMP = Undo.AddComponent<TextMeshProUGUI>(labelObj);
        powerLabelTMP.fontSize = 11f;
        powerLabelTMP.fontStyle = FontStyles.Bold;
        powerLabelTMP.alignment = TextAlignmentOptions.Center;
        powerLabelTMP.color = new Color(1f, 1f, 1f, 0.85f);
        powerLabelTMP.text = "POWER";
        if (powerLabelTMP.font == null && TMP_Settings.defaultFontAsset != null)
            powerLabelTMP.font = TMP_Settings.defaultFontAsset;

        // WindText
        Transform windTr = hudObject.transform.Find("WindText");
        GameObject windObj;
        if (windTr == null)
        {
            windObj = new GameObject("WindText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(windObj, "Create WindText");
            Undo.SetTransformParent(windObj.transform, hudObject.transform, "Parent WindText");
        }
        else
        {
            windObj = windTr.gameObject;
        }
        RectTransform windRect = windObj.GetComponent<RectTransform>();
        windRect.anchorMin = new Vector2(0f, 1f);
        windRect.anchorMax = new Vector2(1f, 1f);
        windRect.pivot = new Vector2(0f, 1f);
        windRect.anchoredPosition = new Vector2(16f, -74f);
        windRect.sizeDelta = new Vector2(-32f, 24f);

        TextMeshProUGUI windTMP = windObj.GetComponent<TextMeshProUGUI>();
        if (windTMP == null) windTMP = Undo.AddComponent<TextMeshProUGUI>(windObj);
        windTMP.fontSize = 16f;
        windTMP.fontStyle = FontStyles.Bold;
        windTMP.color = new Color(0.55f, 0.85f, 1f, 1f);
        windTMP.text = "WIND: 0.0 •";
        if (windTMP.font == null && TMP_Settings.defaultFontAsset != null)
            windTMP.font = TMP_Settings.defaultFontAsset;

        aimHUD.Configure(launcher, angleTMP, fillImage, windTMP, canvasGroup);
        EditorUtility.SetDirty(aimHUD);
        EditorUtility.SetDirty(hudObject);

        EditorUtility.SetDirty(launcher);
        EditorUtility.SetDirty(playerObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "Build Achilles Projectile Prototype",
            "Achilles projectile prototype successfully configured:\n" +
            "- Prefab created/updated: " + PrefabPath + "\n" +
            "- ProjectileSpawnPoint placed at upper body (mirrored by FacingDirection)\n" +
            "- AchillesProjectileDebugLauncher configured on Player_Achilles\n" +
            "- Achilles Animator and Throw animation release settings configured (State: Attack, NormalizedTime: 0.45)\n" +
            "- AimGuideRenderer line guide configured on ProjectileSpawnPoint\n" +
            "- ProjectileAimHUD configured under HUDCanvas (Angle, Power Bar, Prepared Wind with direction arrow)\n" +
            "- Camera target and terrain destruction references linked\n\n" +
            "Scene marked dirty. Inspect and save manually with Ctrl+S.",
            "OK");
    }

    private static GameObject CreateOrUpdateProjectilePrefab(Sprite spearSprite)
    {
        GameObject root = new GameObject("PF_Projectile_Achilles_Spear");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // Rigidbody2D Dynamic, gravity enabled, Interpolate, Continuous collision, rotation free
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 1f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.None;

        // CapsuleCollider2D fitted horizontally to spear shaft
        CapsuleCollider2D col = root.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        col.size = new Vector2(2.0f, 0.25f);
        col.offset = Vector2.zero;

        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (material != null)
            col.sharedMaterial = material;

        // Child Visual
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = spearSprite;
        renderer.sortingLayerName = SortingLayerExists("Projectile") ? "Projectile" : "Default";
        renderer.sortingOrder = 0;

        // Runtime component
        Projectile2D projectileComponent = root.AddComponent<Projectile2D>();
        SerializedObject serializedObj = new SerializedObject(projectileComponent);
        SerializedProperty visualProp = serializedObj.FindProperty("visualTransform");
        if (visualProp != null)
            visualProp.objectReferenceValue = visual.transform;
        SerializedProperty lifetimeProp = serializedObj.FindProperty("maxLifetime");
        if (lifetimeProp != null)
            lifetimeProp.floatValue = 10f;
        SerializedProperty craterProp = serializedObj.FindProperty("craterRadius");
        if (craterProp != null)
            craterProp.floatValue = 1.0f;
        serializedObj.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return savedPrefab;
    }

    private static Sprite LoadSpearSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite s)
                return s;
        }

        return null;
    }

    private static bool SortingLayerExists(string name)
    {
        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (string.Equals(layers[i].name, name, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static GameObject FindUniqueObject(Scene scene, string objectName, List<string> problems)
    {
        GameObject match = null;
        int matchCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (!string.Equals(transforms[j].name, objectName, StringComparison.Ordinal))
                    continue;

                match = transforms[j].gameObject;
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            if (problems != null)
                problems.Add("Could not find " + objectName + " in active scene.");
        }
        else if (matchCount > 1)
        {
            if (problems != null)
                problems.Add("Found " + matchCount + " objects named " + objectName + "; resolve duplicate names first.");
        }

        return matchCount == 1 ? match : null;
    }
}
