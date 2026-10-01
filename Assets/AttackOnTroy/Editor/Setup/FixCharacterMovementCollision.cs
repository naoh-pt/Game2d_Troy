using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configures frictionless actor collision, 0.35 step allowance, and idle anti-slide without changing authored collider geometry.</summary>
public static class FixCharacterMovementCollision
{
    private const string MenuPath = "Attack on Troy/Setup/Fix Character Movement Collision";
    private const string ScenePath = "Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity";
    private const string MaterialPath = "Assets/AttackOnTroy/Physics/Materials/PM_ActorMovement.physicsMaterial2D";
    private static readonly string[] PrefabPaths =
    {
        "Assets/AttackOnTroy/Characters/Achilles/Prefabs/PF_Achilles.prefab",
        "Assets/AttackOnTroy/Characters/Odysseus/Prefabs/PF_Odysseus.prefab",
        "Assets/AttackOnTroy/Enemies/CyclopsNormal/Prefabs/PF_CyclopsNormal.prefab",
        "Assets/AttackOnTroy/Enemies/CyclopsHeavy/Prefabs/PF_CyclopsHeavy.prefab"
    };

    [MenuItem(MenuPath)]
    private static void Fix()
    {
        Scene scene = SceneManager.GetActiveScene();
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() ||
            !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase) || terrainLayer < 0)
        {
            EditorUtility.DisplayDialog("Fix Character Movement Collision",
                "Open Level 01 as the active scene in Edit Mode and ensure the Terrain physics layer exists. No changes were made.", "OK");
            return;
        }
        // Preflight all assets before any material/prefab changes.
        foreach (string path in PrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !HasRequiredComponents(prefab))
            {
                EditorUtility.DisplayDialog("Fix Character Movement Collision",
                    "Missing prefab or required root Rigidbody2D, CapsuleCollider2D, GroundedActorMotor2D: " + path + ". No changes were made.", "OK");
                return;
            }
        }
        UnityEngine.Object existingMaterial = AssetDatabase.LoadMainAssetAtPath(MaterialPath);
        if (existingMaterial != null && !(existingMaterial is PhysicsMaterial2D))
        {
            EditorUtility.DisplayDialog("Fix Character Movement Collision", "The material path contains a different asset type. No changes were made.", "OK");
            return;
        }

        EnsureFolder("Assets/AttackOnTroy", "Physics");
        EnsureFolder("Assets/AttackOnTroy/Physics", "Materials");
        PhysicsMaterial2D material = existingMaterial as PhysicsMaterial2D;
        if (material == null)
        {
            material = new PhysicsMaterial2D("PM_ActorMovement");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.friction = 0f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine2D.Multiply;
        material.bounceCombine = PhysicsMaterialCombine2D.Multiply;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        LayerMask mask = 1 << terrainLayer;
        foreach (string path in PrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureActor(root, material, mask, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fix Character Movement Collision");
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GroundedActorMotor2D motor in root.GetComponentsInChildren<GroundedActorMotor2D>(true))
            {
                GameObject actor = motor.gameObject;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor);
                if (Array.IndexOf(PrefabPaths, path) < 0 || !HasRequiredComponents(actor))
                    continue;
                ConfigureActor(actor, material, mask, true);
                count++;
            }
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Fix Character Movement Collision",
            "Configured four prefab assets and " + count + " connected Level 01 actors with 0.35 step allowance and idle anti-slide. " +
            "PM_ActorMovement has friction 0 and bounce 0. Collider geometry and visual settings were preserved. " +
            "The scene is dirty and has not been saved. Review it, then test both directions in Play Mode.", "OK");
    }

    private static bool HasRequiredComponents(GameObject actor)
    {
        return actor.GetComponents<Rigidbody2D>().Length == 1 && actor.GetComponents<CapsuleCollider2D>().Length == 1 &&
            actor.GetComponents<GroundedActorMotor2D>().Length == 1;
    }

    private static void ConfigureActor(GameObject actor, PhysicsMaterial2D material, LayerMask mask, bool sceneInstance)
    {
        Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
        CapsuleCollider2D collider = actor.GetComponent<CapsuleCollider2D>();
        GroundedActorMotor2D motor = actor.GetComponent<GroundedActorMotor2D>();
        if (sceneInstance)
            Undo.RecordObjects(new UnityEngine.Object[] { body, collider, motor }, "Configure Actor Movement Collision");
        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        body.gravityScale = 3f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sleepMode = RigidbodySleepMode2D.StartAwake;
        collider.isTrigger = false;
        collider.sharedMaterial = material;
        motor.ConfigureCollisionMovement(mask);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(motor);
        if (sceneInstance)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
