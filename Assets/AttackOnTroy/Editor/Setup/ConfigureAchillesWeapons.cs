using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Narrow weapon setup. Does not invoke existing prototype/HUD/animation builders.</summary>
public static class ConfigureAchillesWeapons
{
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string SpearPath = "Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_Spear.prefab";
    private const string BombPath = "Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_ClubBomb.prefab";
    private const string EffectPath = "Assets/AttackOnTroy/Combat/Prefabs/Effects/PF_Effect_ClubBombExplosion.prefab";
    private const string ClubArt = "Assets/AttackOnTroy/Art/Projectiles/Achilles/SPR_Achilles_ClubBomb.png";
    private const string ExplosionArt = "Assets/AttackOnTroy/Art/VFX/Explosions/SPR_Explosion.png";
    // Current sheet has renamed/custom slices: _6 is the small first flash, not frame six.
    // Follow its inspected top row left-to-right, then the two lower-row dispersal frames.
    private static readonly string[] ExplosionOrder =
    {
        "SPR_Explosion_6", "SPR_Explosion_0", "SPR_Explosion_1", "SPR_Explosion_2",
        "SPR_Explosion_33", "SPR_Explosion_3", "SPR_Explosion_4"
    };

    [MenuItem("Attack on Troy/Setup/Configure Achilles Weapons")]
    private static void Configure()
    {
        try { Apply(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || scene.path != ScenePath)
            throw new InvalidOperationException("Open active SCN_Level01_Beach in Edit Mode. No changes made.");
        GameObject player = FindUnique(scene, "Player_Achilles");
        GameObject enemy = FindUnique(scene, "Enemy_CyclopsNormal");
        AchillesProjectileDebugLauncher launcher = player.GetComponent<AchillesProjectileDebugLauncher>();
        Level01TurnManager turns = FindUniqueComponent<Level01TurnManager>(scene);
        ProjectileAimHUD hud = FindUniqueComponent<ProjectileAimHUD>(scene);
        if (launcher == null) throw new InvalidOperationException("Expected the existing Achilles launcher; do not rebuild customized assets.");
        var launcherData = new SerializedObject(launcher);
        GameObject spear = launcherData.FindProperty("projectilePrefab").objectReferenceValue as GameObject;
        if (spear == null) spear = AssetDatabase.LoadAssetAtPath<GameObject>(SpearPath);
        string spearAssetPath = AssetDatabase.GetAssetPath(spear);
        if (spear == null || !PrefabUtility.IsPartOfPrefabAsset(spear) || spear.GetComponent<Projectile2D>() == null ||
            spear.GetComponent<Rigidbody2D>() == null || spear.GetComponent<CapsuleCollider2D>() == null ||
            spear.GetComponentInChildren<SpriteRenderer>() == null)
            throw new InvalidOperationException("Expected a valid existing spear prefab. Nothing will rebuild its sprite or geometry.");
        if (new SerializedObject(turns).FindProperty("launcher").objectReferenceValue != launcher ||
            new SerializedObject(hud).FindProperty("launcher").objectReferenceValue != launcher)
            throw new InvalidOperationException("Existing turn manager/HUD must already reference this Achilles launcher.");
        var hudData = new SerializedObject(hud);
        TextMeshProUGUI angle = hudData.FindProperty("angleLabel").objectReferenceValue as TextMeshProUGUI;
        if (angle == null || angle.font == null)
            throw new InvalidOperationException("Expected the styled Angle label with its existing font.");
        TextMeshProUGUI weapon = hudData.FindProperty("weaponLabel").objectReferenceValue as TextMeshProUGUI;
        Transform existingLabel = hud.transform.Find("WeaponText");
        if (weapon == null && existingLabel != null)
        {
            weapon = existingLabel.GetComponent<TextMeshProUGUI>();
            if (weapon == null) throw new InvalidOperationException("Existing WeaponText is not a TMP label; inspect it manually.");
        }
        Sprite club = LoadLargestSprite(ClubArt);
        if (club == null) throw new InvalidOperationException("Import SPR_Achilles_ClubBomb as sprites first.");
        ValidateExistingPrefab(BombPath, typeof(Projectile2D));
        ValidateExistingPrefab(EffectPath, typeof(ProjectileExplosionEffect));
        GameObject existingBomb = AssetDatabase.LoadAssetAtPath<GameObject>(BombPath);
        if (existingBomb != null && (existingBomb.transform.Find("Visual") == null ||
            existingBomb.GetComponent<Rigidbody2D>() == null || existingBomb.GetComponent<CapsuleCollider2D>() == null))
            throw new InvalidOperationException("Existing bomb prefab needs a Visual child, Rigidbody2D and CapsuleCollider2D. Inspect instead of overwriting.");

        // Only the spear's impact enum is patched. All user-authored sprite/geometry settings survive.
        PatchSpear(spearAssetPath);
        GameObject effect = CreateEffectIfMissing(spear.GetComponentInChildren<SpriteRenderer>());
        GameObject bomb = ConfigureBomb(spear, club, effect.GetComponent<ProjectileExplosionEffect>());

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Achilles Weapons");
        Undo.RecordObject(launcher, "Assign Achilles weapons");
        launcher.ConfigureWeapons(spear, bomb, turns);
        PrefabUtility.RecordPrefabInstancePropertyModifications(launcher);
        if (weapon == null)
        {
            // Clone only one new label, keeping the exact authored font, material, color and typography.
            // Existing HUD labels/panel are not resized or restyled.
            GameObject label = UnityEngine.Object.Instantiate(angle.gameObject, hud.transform, false);
            label.name = "WeaponText";
            Undo.RegisterCreatedObjectUndo(label, "Create styled weapon label");
            weapon = label.GetComponent<TextMeshProUGUI>();
            RectTransform rect = weapon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(320f, Mathf.Max(36f, angle.rectTransform.rect.height));
            float hudHeight = ((RectTransform)hud.transform).rect.height;
            rect.anchoredPosition = new Vector2(angle.rectTransform.anchoredPosition.x, -hudHeight - 10f);
            weapon.raycastTarget = false;
            weapon.text = launcher.SelectedWeaponName;
        }
        Undo.RecordObject(hud, "Assign weapon label");
        hud.ConfigureWeaponLabel(weapon);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        AddDamageReceiverIfMissing(player);
        AddDamageReceiverIfMissing(enemy);
        EditorUtility.SetDirty(launcher);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("Achilles weapons configured. Spear preserved; Club Bomb/effect prefabs saved. " +
            "New HUD label inherits the existing font/style. Scene dirty but NOT saved. " +
            "1=Spear, 2=Club Bomb during PlayerTurn only. Do not rerun older prototype builders.");
    }

    private static void PatchSpear(string path)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data = new SerializedObject(contents.GetComponent<Projectile2D>());
            data.FindProperty("impactType").enumValueIndex = (int)ProjectileImpactType.Spear;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static GameObject ConfigureBomb(GameObject spear, Sprite sprite, ProjectileExplosionEffect effect)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(BombPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(BombPath) : new GameObject("PF_Projectile_Achilles_ClubBomb");
        try
        {
            Projectile2D projectile;
            Transform visual;
            if (!exists)
            {
                root.layer = spear.layer;
                Rigidbody2D template = spear.GetComponent<Rigidbody2D>();
                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = template.gravityScale;
                body.mass = template.mass;
                body.linearDamping = template.linearDamping;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(0.65f, 0.65f);
                collider.sharedMaterial = spear.GetComponent<CapsuleCollider2D>().sharedMaterial;
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                // Center the sprite inside a spin pivot; custom sprite pivots cannot cause orbiting.
                Transform art = new GameObject("Sprite").transform;
                art.SetParent(visual, false);
                float scale = 1.4f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                art.localScale = new Vector3(scale, scale, 1f);
                art.localPosition = -sprite.bounds.center * scale;
                SpriteRenderer renderer = art.gameObject.AddComponent<SpriteRenderer>();
                SpriteRenderer templateRenderer = spear.GetComponentInChildren<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = templateRenderer.sharedMaterial;
                renderer.sortingLayerID = templateRenderer.sortingLayerID;
                renderer.sortingOrder = templateRenderer.sortingOrder;
                projectile = root.AddComponent<Projectile2D>();
            }
            else
            {
                projectile = root.GetComponent<Projectile2D>();
                visual = root.transform.Find("Visual");
            }
            var data = new SerializedObject(projectile);
            data.FindProperty("impactType").enumValueIndex = (int)ProjectileImpactType.ClubBomb;
            data.FindProperty("visualTransform").objectReferenceValue = visual;
            if (data.FindProperty("explosionEffectPrefab").objectReferenceValue == null)
                data.FindProperty("explosionEffectPrefab").objectReferenceValue = effect;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, BombPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateEffectIfMissing(SpriteRenderer templateRenderer)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
        if (existing != null) return existing;
        GameObject root = new GameObject("PF_Effect_ClubBombExplosion");
        try
        {
            GameObject child = new GameObject("Visual");
            child.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = templateRenderer.sharedMaterial;
            renderer.sortingLayerID = templateRenderer.sortingLayerID;
            renderer.sortingOrder = 10;
            ProjectileExplosionEffect effect = root.AddComponent<ProjectileExplosionEffect>();
            var data = new SerializedObject(effect);
            data.FindProperty("visual").objectReferenceValue = renderer;
            var frames = new List<Sprite>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ExplosionArt))
                if (asset is Sprite frame) frames.Add(frame);
            frames.Sort((a, b) => ExplosionRank(a.name).CompareTo(ExplosionRank(b.name)));
            SerializedProperty array = data.FindProperty("frames");
            array.arraySize = frames.Count;
            for (int i = 0; i < frames.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            if (frames.Count == 0) Debug.LogWarning("No explosion sprites imported: visible runtime ring fallback will be used.");
            return PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static int FrameIndex(string name) => int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out int index) ? index : int.MaxValue;
    private static int ExplosionRank(string name)
    {
        int known = Array.IndexOf(ExplosionOrder, name);
        return known >= 0 ? known : 100 + Mathf.Min(FrameIndex(name), 10000);
    }
    private static Sprite LoadLargestSprite(string path)
    {
        Sprite result = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite && (result == null || sprite.rect.size.sqrMagnitude > result.rect.size.sqrMagnitude)) result = sprite;
        return result;
    }

    private static void ValidateExistingPrefab(string path, Type component)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset != null && (!(asset is GameObject root) || root.GetComponent(component) == null))
            throw new InvalidOperationException("Unexpected asset at " + path + "; inspect manually before configuring.");
    }

    private static void AddDamageReceiverIfMissing(GameObject actor)
    {
        if (actor.GetComponent<IDamageable2D>() != null) return;
        ActorDamageReceiver2D receiver = Undo.AddComponent<ActorDamageReceiver2D>(actor);
        PrefabUtility.RecordPrefabInstancePropertyModifications(receiver);
        EditorUtility.SetDirty(receiver);
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
