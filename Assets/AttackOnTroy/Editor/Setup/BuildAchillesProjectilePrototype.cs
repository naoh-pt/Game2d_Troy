using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
