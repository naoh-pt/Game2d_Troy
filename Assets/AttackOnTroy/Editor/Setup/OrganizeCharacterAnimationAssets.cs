using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Relocates the audited character clips and action-specific legacy controllers without changing their contents.
/// </summary>
public static class OrganizeCharacterAnimationAssets
{
    private const string MenuPath = "Attack on Troy/Setup/Organize Character Animation Assets";
    private const string UndoMenuPath = "Attack on Troy/Setup/Undo Last Character Animation Asset Organization";
    private const string LogPath = "Library/AttackOnTroyCharacterAnimationAssetMoves.json";

    [Serializable]
    private sealed class MoveSpec
    {
        public string source;
        public string destination;
        public string kind;

        public MoveSpec(string sourcePath, string destinationPath, string assetKind)
        {
            source = sourcePath;
            destination = destinationPath;
            kind = assetKind;
        }
    }

    [Serializable]
    private sealed class MoveRecord
    {
        public string source;
        public string destination;
        public string kind;
        public string guid;
        public string result;
    }

    [Serializable]
    private sealed class MoveJournal
    {
        public string timestampUtc;
        public bool complete;
        public List<MoveRecord> records = new List<MoveRecord>();
    }

    private static readonly MoveSpec[] MovePlan = BuildMovePlan();

    [MenuItem(MenuPath)]
    private static void Organize()
    {
        MoveJournal journal = new MoveJournal { timestampUtc = DateTime.UtcNow.ToString("o") };
        List<MoveRecord> pendingMoves;
        string error;

        if (!ValidateAll(journal, out pendingMoves, out error))
        {
            EditorUtility.DisplayDialog("Character Animation Asset Organization", error + "\n\nNo changes were made.", "OK");
            return;
        }

        if (pendingMoves.Count == 0)
        {
            journal.complete = true;
            WriteJournal(journal);
            Debug.Log("Character animation assets are already organized. No assets were moved. Complete log: " + LogPath);
            EditorUtility.DisplayDialog("Character Animation Asset Organization", "All planned assets are already at their destinations. No assets were moved.", "OK");
            return;
        }

        if (!ValidateDestinationFolders(out error))
        {
            EditorUtility.DisplayDialog("Character Animation Asset Organization", error + "\n\nNo changes were made.", "OK");
            return;
        }
        if (!EnsureDestinationFolders(out error))
        {
            EditorUtility.DisplayDialog("Character Animation Asset Organization", error + "\n\nNo assets were moved.", "OK");
            return;
        }

        // Persist the complete plan before the first move. Progress is rewritten after each AssetDatabase operation,
        // allowing the companion rollback command to recover from an interrupted run.
        WriteJournal(journal);
        try
        {
            foreach (MoveRecord record in pendingMoves)
            {
                string moveError = AssetDatabase.MoveAsset(record.source, record.destination);
                if (!string.IsNullOrEmpty(moveError))
                    throw new InvalidOperationException("Move failed for " + record.source + ": " + moveError);

                record.result = "Moved";
                WriteJournal(journal);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            string verifyError;
            if (!VerifyOrganized(journal, out verifyError))
                throw new InvalidOperationException("Post-move verification failed: " + verifyError);

            journal.complete = true;
            WriteJournal(journal);
            Debug.Log("Character animation assets organized and verified. Move log: " + LogPath);
            EditorUtility.DisplayDialog("Character Animation Asset Organization", "Moved " + pendingMoves.Count + " asset(s). GUIDs and animation references verified.\n\nMove log: " + LogPath, "OK");
        }
        catch (Exception exception)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            WriteJournal(journal);
            Debug.LogError("Character animation organization stopped. Completed moves are recorded in " + LogPath + ". Use the Undo Last command after resolving any reported conflict.\n" + exception);
            EditorUtility.DisplayDialog("Character Animation Asset Organization", "The operation stopped. No automatic destructive cleanup was attempted. Completed moves are recorded in:\n" + LogPath + "\n\n" + exception.Message, "OK");
        }
    }

    [MenuItem(UndoMenuPath)]
    private static void UndoLastOrganization()
    {
        if (!File.Exists(LogPath))
        {
            EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "No move log exists at " + LogPath + ".", "OK");
            return;
        }

        MoveJournal journal = JsonUtility.FromJson<MoveJournal>(File.ReadAllText(LogPath));
        if (journal == null || journal.records == null || journal.records.Count == 0)
        {
            EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "The move log contains no completed moves.", "OK");
            return;
        }

        List<MoveRecord> movedRecords = journal.records.FindAll(record => record.result == "Moved");
        if (movedRecords.Count == 0)
        {
            EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "The latest logged run contains no completed moves to undo.", "OK");
            return;
        }

        for (int i = 0; i < movedRecords.Count; i++)
        {
            MoveRecord record = movedRecords[i];
            string currentGuid = AssetDatabase.AssetPathToGUID(record.destination);
            if (string.IsNullOrEmpty(currentGuid) || currentGuid != record.guid || AssetDatabase.LoadMainAssetAtPath(record.source) != null)
            {
                EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "Rollback preflight failed at " + record.destination + ". The destination GUID must match the log and the original path must be free. No rollback moves were made.", "OK");
                return;
            }
        }

        List<MoveRecord> reversed = new List<MoveRecord>();
        for (int i = movedRecords.Count - 1; i >= 0; i--)
        {
            MoveRecord record = movedRecords[i];
            string parent = ParentFolder(record.source);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "Rollback destination folder is missing: " + parent + ". No rollback moves were made.", "OK");
                return;
            }
            reversed.Add(record);
        }

        foreach (MoveRecord record in reversed)
        {
            string moveError = AssetDatabase.MoveAsset(record.destination, record.source);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError("Rollback stopped. Earlier rollback moves are preserved in the move log. Failure: " + moveError);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "Rollback stopped at " + record.destination + ". See Console and " + LogPath + ".", "OK");
                return;
            }
            record.result = "RolledBack";
            WriteJournal(journal);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        journal.complete = false;
        WriteJournal(journal);
        Debug.Log("Character animation asset moves were rolled back through AssetDatabase.MoveAsset. Log: " + LogPath);
        EditorUtility.DisplayDialog("Undo Character Animation Asset Organization", "All logged moves were rolled back.", "OK");
    }

    private static bool ValidateAll(MoveJournal journal, out List<MoveRecord> pending, out string error)
    {
        pending = new List<MoveRecord>();
        error = string.Empty;
        HashSet<string> destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (MoveSpec spec in MovePlan)
        {
            if (!destinations.Add(spec.destination))
            {
                error = "The move plan contains a duplicate destination: " + spec.destination;
                return false;
            }

            UnityEngine.Object sourceAsset = AssetDatabase.LoadMainAssetAtPath(spec.source);
            UnityEngine.Object destinationAsset = AssetDatabase.LoadMainAssetAtPath(spec.destination);
            bool sourceExists = sourceAsset != null;
            bool destinationExists = destinationAsset != null;

            if (!sourceExists && !destinationExists)
            {
                error = "Required asset is missing from both expected paths: " + spec.source;
                return false;
            }
            if (sourceExists && !HasExpectedType(sourceAsset, spec.kind))
            {
                error = "Unexpected source asset type at " + spec.source + ". Expected " + spec.kind + ".";
                return false;
            }
            if (destinationExists && !HasExpectedType(destinationAsset, spec.kind))
            {
                error = "Unexpected destination asset type at " + spec.destination + ". Expected " + spec.kind + ".";
                return false;
            }
            if (sourceExists && destinationExists)
            {
                error = "Destination conflict: both source and destination exist for " + spec.source + " -> " + spec.destination;
                return false;
            }

            if (destinationExists)
            {
                journal.records.Add(new MoveRecord
                {
                    source = spec.source,
                    destination = spec.destination,
                    kind = spec.kind,
                    guid = AssetDatabase.AssetPathToGUID(spec.destination),
                    result = "AlreadyAtDestination (skipped)"
                });
                continue;
            }

            string guid = AssetDatabase.AssetPathToGUID(spec.source);
            if (string.IsNullOrEmpty(guid))
            {
                error = "AssetDatabase returned no GUID for required source: " + spec.source;
                return false;
            }
            pending.Add(new MoveRecord { source = spec.source, destination = spec.destination, kind = spec.kind, guid = guid, result = "Planned" });
        }

        // Include untouched action sheets in preflight so a partial/missing character inventory is caught before moving.
        foreach (string sheet in SpriteSheets)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet);
            if (texture == null || AssetDatabase.LoadAllAssetsAtPath(sheet).Length < 2 || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sheet)))
            {
                error = "Required sliced character sheet is missing, has no sprite sub-assets, or has no GUID: " + sheet;
                return false;
            }
        }

        // Write the full planned/skipped inventory, then append moved records as work succeeds.
        foreach (MoveRecord move in pending)
            journal.records.Add(move);
        return true;
    }

    private static bool HasExpectedType(UnityEngine.Object asset, string kind)
    {
        if (kind == "AnimationClip") return asset is AnimationClip;
        if (kind == "AnimatorController") return asset is AnimatorController;
        return false;
    }

    private static bool EnsureDestinationFolders(out string error)
    {
        error = string.Empty;
        HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MoveSpec spec in MovePlan)
            folders.Add(ParentFolder(spec.destination));

        foreach (string folder in folders)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (AssetDatabase.LoadMainAssetAtPath(next) != null)
                    {
                        error = "A non-folder asset blocks required destination folder " + next;
                        return false;
                    }
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        error = "Could not create required folder " + next;
                        return false;
                    }
                }
                current = next;
            }
        }
        return true;
    }

    private static bool ValidateDestinationFolders(out string error)
    {
        error = string.Empty;
        HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MoveSpec spec in MovePlan)
        {
            string[] parts = ParentFolder(spec.destination).Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                current += "/" + parts[i];
                folders.Add(current);
            }
        }

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder) && AssetDatabase.LoadMainAssetAtPath(folder) != null)
            {
                error = "A non-folder asset blocks a required destination folder: " + folder;
                return false;
            }
        }
        return true;
    }

    private static bool VerifyOrganized(MoveJournal journal, out string error)
    {
        error = string.Empty;
        foreach (MoveRecord record in journal.records)
        {
            if (record.result == "AlreadyAtDestination (skipped)") continue;
            string actualGuid = AssetDatabase.AssetPathToGUID(record.destination);
            if (actualGuid != record.guid || AssetDatabase.LoadMainAssetAtPath(record.source) != null)
            {
                error = "GUID/path verification failed for " + record.destination;
                return false;
            }
        }

        foreach (MoveRecord record in journal.records)
        {
            string path = AssetDatabase.LoadMainAssetAtPath(record.destination) != null ? record.destination : record.source;
            if (record.kind == "AnimationClip")
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || !VerifySpriteCurves(clip))
                {
                    error = "Sprite reference curves no longer resolve for " + path;
                    return false;
                }
            }
            else
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null || !VerifyControllerMotions(controller))
                {
                    error = "Controller motion clip no longer resolves for " + path;
                    return false;
                }
            }
        }
        foreach (string sheet in SpriteSheets)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(sheet) == null || AssetDatabase.LoadAllAssetsAtPath(sheet).Length < 2)
            {
                error = "Character sheet or sliced sprite sub-assets are missing: " + sheet;
                return false;
            }
        }
        return true;
    }

    private static bool VerifySpriteCurves(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings.Length == 0) return false;
        foreach (EditorCurveBinding binding in bindings)
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0) return false;
            foreach (ObjectReferenceKeyframe key in keys)
            {
                if (!(key.value is Sprite)) return false;
                string path = AssetDatabase.GetAssetPath(key.value);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;
            }
        }
        return true;
    }

    private static bool VerifyControllerMotions(AnimatorController controller)
    {
        bool hasMotion = false;
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            foreach (ChildAnimatorState state in layer.stateMachine.states)
            {
                if (state.state.motion == null) return false;
                hasMotion = true;
                if (!VerifyMotion(state.state.motion)) return false;
            }
        }
        return hasMotion;
    }

    private static bool VerifyMotion(Motion motion)
    {
        AnimationClip clip = motion as AnimationClip;
        if (clip != null) return AssetDatabase.Contains(clip);
        BlendTree tree = motion as BlendTree;
        if (tree == null) return false;
        foreach (ChildMotion child in tree.children)
            if (child.motion == null || !VerifyMotion(child.motion)) return false;
        return tree.children.Length > 0;
    }

    private static void WriteJournal(MoveJournal journal)
    {
        string directory = Path.GetDirectoryName(LogPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(LogPath, JsonUtility.ToJson(journal, true));
    }

    private static string ParentFolder(string path)
    {
        return path.Substring(0, path.LastIndexOf('/'));
    }

    private static MoveSpec[] BuildMovePlan()
    {
        List<MoveSpec> moves = new List<MoveSpec>();
        AddCharacter(moves, "Assets/AttackOnTroy/Art/Characters/Achilles", "ANIM_Achilles_Death", "ANIM_Achilles_Hurt", "ANIM_Achilles_Idle", "ANIM_Achilles_Run", "ANIM_Achilles_Throw");
        AddCharacter(moves, "Assets/AttackOnTroy/Art/Characters/Odysseus", "ANIM_Odysseus_BowAttack", "ANIM_Odysseus_Death", "ANIM_Odysseus_Hurt", "ANIM_Odysseus_Idle", "ANIM_Odysseus_Run");
        AddCharacter(moves, "Assets/AttackOnTroy/Art/Characters/Cyclops/Normal", "ANIM_CyclopsNormal_Death", "ANIM_CyclopsNormal_Idle", "ANIM_CyclopsNormal_Throw", "ANIM_CyclopsNormal_Walk");
        AddCharacter(moves, "Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy", "ANIM_CyclopsHeavy_Death", "ANIM_CyclopsHeavy_Idle", "ANIM_CyclopsHeavy_Throw", "ANIM_CyclopsHeavy_Walk");
        return moves.ToArray();
    }

    private static void AddCharacter(List<MoveSpec> moves, string root, params string[] clipNames)
    {
        string sprites = root + "/Sprites/";
        string animations = root + "/Animations/";
        string controllers = root + "/AnimatorControllers/Legacy/";
        foreach (string clipName in clipNames)
        {
            moves.Add(new MoveSpec(sprites + clipName + ".anim", animations + clipName + ".anim", "AnimationClip"));
        }

        // Exact controller filenames from the authoritative audit inventory.
        Dictionary<string, string> controllerNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ANIM_Achilles_Death", "achiles_die_0" }, { "ANIM_Achilles_Hurt", "achiles_hur_0" },
            { "ANIM_Achilles_Idle", "achiles_idle_0" }, { "ANIM_Achilles_Run", "achiles_run_0" }, { "ANIM_Achilles_Throw", "achiles_attack_0" },
            { "ANIM_Odysseus_BowAttack", "odyse_attck_4" }, { "ANIM_Odysseus_Death", "odyse_death_0" },
            { "ANIM_Odysseus_Hurt", "odyse_hurt_0" }, { "ANIM_Odysseus_Idle", "odyse_idle_0" }, { "ANIM_Odysseus_Run", "odyse_run_6" },
            { "ANIM_CyclopsNormal_Death", "CyclopsNormal_Death_0" }, { "ANIM_CyclopsNormal_Idle", "ANIM_CyclopsNormal_Idle_0" },
            { "ANIM_CyclopsNormal_Throw", "CyclopsNormal_Throw_1" }, { "ANIM_CyclopsNormal_Walk", "CyclopsNormal_Walk_3" },
            { "ANIM_CyclopsHeavy_Death", "c1_death_0" }, { "ANIM_CyclopsHeavy_Idle", "c1_idle_4" },
            { "ANIM_CyclopsHeavy_Throw", "c1_throw_3" }, { "ANIM_CyclopsHeavy_Walk", "c1_walk_0" }
        };
        foreach (string clipName in clipNames)
        {
            string controllerName;
            if (!controllerNames.TryGetValue(clipName, out controllerName))
                throw new InvalidOperationException("No audited controller filename is configured for " + clipName);
            moves.Add(new MoveSpec(sprites + controllerName + ".controller", controllers + controllerName + ".controller", "AnimatorController"));
        }
    }

    private static readonly string[] SpriteSheets =
    {
        "Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Death.png",
        "Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Hurt.png",
        "Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Idle.png",
        "Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Run.png",
        "Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Throw.png",
        "Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_BowAttack.png",
        "Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_Hurt.png",
        "Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_Idle.png",
        "Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_Run.png",
        "Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus__Death.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Idle.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Walk.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_death.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_idle.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_throw.png",
        "Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_walk.png"
    };
}
