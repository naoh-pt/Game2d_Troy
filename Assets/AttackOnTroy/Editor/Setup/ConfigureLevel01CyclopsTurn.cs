using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent setup tool for the Level 01 Cyclops Normal turn.
/// Connects CyclopsNormalTurnController on Enemy_CyclopsNormal, ensures the
/// Stone Axe projectile prefab is configured, and wires references to Level01TurnManager and camera.
/// </summary>
public static class ConfigureLevel01CyclopsTurn
{
    private const string MenuPath = "Attack on Troy/Setup/Configure Level 01 Cyclops Turn";
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string AxePrefabPath = "Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_CyclopsNormal_StoneAxe.prefab";
    private const string SpearPrefabPath = "Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_Spear.prefab";
    private const string AxeArtPath = "Assets/AttackOnTroy/Art/Projectiles/CyclopsNormal/SPR_CyclopsNormal_StoneAxe.png";

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
                "Configure Level 01 Cyclops Turn",
                "Cannot configure turn in Play Mode. Exit Play Mode first.",
                "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Configure Level 01 Cyclops Turn",
                "Open and activate SCN_Level01_Beach before running this command. No changes were made.",
                "OK");
            return;
        }

        GameObject player = FindUnique(scene, "Player_Achilles");
        GameObject cyclops = FindUnique(scene, "Enemy_CyclopsNormal");
        Level01TurnManager turnManager = FindUniqueComponent<Level01TurnManager>(scene);
        Level01CameraController cameraController = FindUniqueComponent<Level01CameraController>(scene);

        AchillesProjectileDebugLauncher launcher = player.GetComponent<AchillesProjectileDebugLauncher>();
        GroundedActorMotor2D cyclopsMotor = cyclops.GetComponent<GroundedActorMotor2D>();
        Animator cyclopsAnimator = cyclops.GetComponentInChildren<Animator>();
        Collider2D cyclopsCollider = cyclops.GetComponent<Collider2D>();

        // Find level bounds collider
        Collider2D levelBounds = null;
        if (cameraController != null)
        {
            var camData = new SerializedObject(cameraController);
            levelBounds = camData.FindProperty("levelBounds").objectReferenceValue as Collider2D;
        }
        if (levelBounds == null)
        {
            GameObject boundsObj = GameObject.Find("LevelBounds");
            if (boundsObj != null)
                levelBounds = boundsObj.GetComponent<Collider2D>();
        }

        // Configure or load Stone Axe projectile prefab
        GameObject spearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpearPrefabPath);
        Sprite axeSprite = LoadLargestSprite(AxeArtPath);
        if (axeSprite == null)
            throw new InvalidOperationException("Could not load stone axe sprite at " + AxeArtPath);

        GameObject axePrefab = ConfigureStoneAxePrefab(spearPrefab, axeSprite);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Level 01 Cyclops Turn");

        // Ensure Cyclops motor is configured to face left by default
        if (cyclopsMotor != null)
        {
            Undo.RecordObject(cyclopsMotor, "Configure Cyclops Motor Facing");
            cyclopsMotor.SpriteFacesLeftByDefault = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cyclopsMotor);
            EditorUtility.SetDirty(cyclopsMotor);
        }

        // Ensure Cyclops Visual sprite renderer flipX is reset so it natively faces left toward Achilles
        SpriteRenderer cyclopsVisualRenderer = cyclops.GetComponentInChildren<SpriteRenderer>(true);
        if (cyclopsVisualRenderer != null)
        {
            Undo.RecordObject(cyclopsVisualRenderer, "Configure Cyclops Visual flipX");
            cyclopsVisualRenderer.flipX = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cyclopsVisualRenderer);
            EditorUtility.SetDirty(cyclopsVisualRenderer);
        }

        // Ensure CyclopsNormalTurnController on Enemy_CyclopsNormal
        CyclopsNormalTurnController cyclopsController = cyclops.GetComponent<CyclopsNormalTurnController>();
        if (cyclopsController == null)
        {
            cyclopsController = Undo.AddComponent<CyclopsNormalTurnController>(cyclops);
        }

        Undo.RecordObject(cyclopsController, "Configure CyclopsNormalTurnController");
        cyclopsController.Configure(
            cyclopsMotor,
            cyclopsAnimator,
            cyclopsCollider,
            player.transform,
            axePrefab,
            cameraController,
            levelBounds,
            launcher);

        var controllerData = new SerializedObject(cyclopsController);
        var walkSpeedProp = controllerData.FindProperty("walkAnimationSpeedMultiplier");
        if (walkSpeedProp != null)
            walkSpeedProp.floatValue = 0.7f;
        var repositionDistProp = controllerData.FindProperty("repositionDistance");
        if (repositionDistProp != null && repositionDistProp.floatValue <= 0.05f)
            repositionDistProp.floatValue = 1.0f;
        var postHitHoldProp = controllerData.FindProperty("postHitHoldDuration");
        if (postHitHoldProp != null && postHitHoldProp.floatValue <= 0.05f)
            postHitHoldProp.floatValue = 1.2f;
        var cameraFocusDelayProp = controllerData.FindProperty("cameraFocusDelayBeforeMove");
        if (cameraFocusDelayProp != null && cameraFocusDelayProp.floatValue <= 0.05f)
            cameraFocusDelayProp.floatValue = 0.5f;
        var postMoveSettleProp = controllerData.FindProperty("postMoveSettleDuration");
        if (postMoveSettleProp != null && postMoveSettleProp.floatValue <= 0.05f)
            postMoveSettleProp.floatValue = 0.3f;
        controllerData.ApplyModifiedProperties();

        PrefabUtility.RecordPrefabInstancePropertyModifications(cyclopsController);
        EditorUtility.SetDirty(cyclopsController);

        // Connect cyclopsController to Level01TurnManager
        Undo.RecordObject(turnManager, "Wire Cyclops turn to TurnManager");
        var turnData = new SerializedObject(turnManager);
        turnData.FindProperty("cyclopsController").objectReferenceValue = cyclopsController;
        turnData.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(turnManager);
        EditorUtility.SetDirty(turnManager);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);

        Debug.Log("[ConfigureLevel01CyclopsTurn] Cyclops Normal turn configured successfully. " +
                  "Stone Axe prefab connected. Scene marked dirty but NOT saved. " +
                  "Do not run old builders.");
    }

    private static GameObject ConfigureStoneAxePrefab(GameObject spearPrefab, Sprite axeSprite)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(AxePrefabPath) != null;
        GameObject root = exists
            ? PrefabUtility.LoadPrefabContents(AxePrefabPath)
            : new GameObject("PF_Projectile_CyclopsNormal_StoneAxe");

        try
        {
            Projectile2D projectile;
            Transform visual;
            Transform spriteChild;
            SpriteRenderer renderer;

            if (!exists)
            {
                if (spearPrefab != null)
                    root.layer = spearPrefab.layer;

                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                if (spearPrefab != null)
                {
                    Rigidbody2D templateBody = spearPrefab.GetComponent<Rigidbody2D>();
                    body.gravityScale = templateBody.gravityScale;
                    body.mass = templateBody.mass;
                    body.linearDamping = templateBody.linearDamping;
                }
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(0.8f, 0.8f);
                if (spearPrefab != null)
                    collider.sharedMaterial = spearPrefab.GetComponent<CapsuleCollider2D>()?.sharedMaterial;

                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);

                spriteChild = new GameObject("Sprite").transform;
                spriteChild.SetParent(visual, false);

                float targetWidth = 1.2f;
                float scale = targetWidth / Mathf.Max(axeSprite.bounds.size.x, axeSprite.bounds.size.y);
                spriteChild.localScale = new Vector3(scale, scale, 1f);
                spriteChild.localPosition = -axeSprite.bounds.center * scale;

                renderer = spriteChild.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = axeSprite;
                if (spearPrefab != null)
                {
                    SpriteRenderer templateRenderer = spearPrefab.GetComponentInChildren<SpriteRenderer>();
                    if (templateRenderer != null)
                    {
                        renderer.sharedMaterial = templateRenderer.sharedMaterial;
                        renderer.sortingLayerID = templateRenderer.sortingLayerID;
                        renderer.sortingOrder = templateRenderer.sortingOrder;
                    }
                }

                projectile = root.AddComponent<Projectile2D>();
            }
            else
            {
                projectile = root.GetComponent<Projectile2D>();
                visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(root.transform, false);
                }
                spriteChild = visual.Find("Sprite");
                if (spriteChild == null)
                {
                    spriteChild = new GameObject("Sprite").transform;
                    spriteChild.SetParent(visual, false);
                }
                renderer = spriteChild.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    renderer = spriteChild.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = axeSprite;
                float targetWidth = 1.2f;
                float scale = targetWidth / Mathf.Max(axeSprite.bounds.size.x, axeSprite.bounds.size.y);
                spriteChild.localScale = new Vector3(scale, scale, 1f);
                spriteChild.localPosition = -axeSprite.bounds.center * scale;
            }

            var projData = new SerializedObject(projectile);
            projData.FindProperty("impactType").enumValueIndex = (int)ProjectileImpactType.StoneAxe;
            projData.FindProperty("visualTransform").objectReferenceValue = visual;
            projData.FindProperty("directHitDamage").floatValue = 25f;
            projData.FindProperty("embeddedDuration").floatValue = 0.5f;
            projData.FindProperty("axeVisualDegreesPerSecond").floatValue = 720f;
            projData.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, AxePrefabPath);
        }
        finally
        {
            if (exists)
                PrefabUtility.UnloadPrefabContents(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Sprite LoadLargestSprite(string path)
    {
        Sprite result = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite && (result == null || sprite.rect.size.sqrMagnitude > result.rect.size.sqrMagnitude))
                result = sprite;
        return result;
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
