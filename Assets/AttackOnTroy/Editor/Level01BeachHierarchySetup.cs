using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the missing Level 1 hierarchy while preserving existing scene content.
/// </summary>
public static class Level01BeachHierarchySetup
{
    private const string MenuPath = "Attack on Troy/Setup/Complete Level 1 Beach Hierarchy";
    private const string ExpectedSceneName = "SCN_Level01_Beach";
    private const string BackupFolder = "Assets/AttackOnTroy/Scenes/Backups";
    private const string BackupPath = BackupFolder + "/SCN_Level01_Beach_PreHierarchyBackup.unity";
    private const string RuntimeReportPath = "Docs/Reports/level01-hierarchy-setup-report.md";

    private sealed class RunResults
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Reused = new List<string>();
        public readonly List<string> Reparented = new List<string>();
        public readonly List<string> DuplicateConflicts = new List<string>();
        public readonly List<string> Conflicts = new List<string>();
        public readonly List<string> MissingVisuals = new List<string>();
        public readonly List<string> PreservedTransforms = new List<string>();
        public readonly List<string> ManualPlacement = new List<string>();
        public string BackupResult = "Not attempted";
        public bool SceneMarkedDirty;
    }

    [MenuItem(MenuPath)]
    private static void CompleteLevel01BeachHierarchy()
    {
        Scene scene = SceneManager.GetActiveScene();
        var results = new RunResults();

        if (!scene.IsValid() || !string.Equals(scene.name, ExpectedSceneName, StringComparison.Ordinal))
        {
            string activeName = scene.IsValid() ? scene.name : "<no valid active scene>";
            results.Conflicts.Add("Wrong active scene: " + activeName + ". No scene changes were made.");
            TryWriteRuntimeReport(scene, results);
            EditorUtility.DisplayDialog(
                "Level 1 Beach Hierarchy",
                "Open SCN_Level01_Beach as the active scene before running this command. No scene changes were made.",
                "OK");
            return;
        }

        if (!EnsureBackup(scene, results))
        {
            TryWriteRuntimeReport(scene, results);
            EditorUtility.DisplayDialog(
                "Level 1 Beach Hierarchy",
                "Setup stopped because a safe scene backup could not be confirmed. No scene hierarchy changes were made.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Complete Level 1 Beach Hierarchy");

        GameObject systems = GetOrCreateChild(scene, null, "Systems", results);
        GameObject level = GetOrCreateChild(scene, null, "Level", results);
        GameObject actors = GetOrCreateChild(scene, null, "Actors", results);
        GetOrCreateChild(scene, null, "Projectiles", results);
        GetOrCreateChild(scene, null, "Effects", results);
        GameObject cameras = GetOrCreateChild(scene, null, "Cameras", results);
        GameObject ui = GetOrCreateChild(scene, null, "UI", results);

        if (systems != null)
        {
            GetOrCreateChild(scene, systems, "GameManager", results);
            GetOrCreateChild(scene, systems, "TurnManager", results);
            GetOrCreateChild(scene, systems, "WindManager", results);
            GetOrCreateChild(scene, systems, "AudioManager", results);
        }

        GameObject background = null;
        GameObject world = null;
        GameObject foreground = null;
        if (level != null)
        {
            background = GetOrCreateChild(scene, level, "Background", results);
            world = GetOrCreateChild(scene, level, "World", results);
            foreground = GetOrCreateChild(scene, level, "Foreground", results);
        }

        if (world != null)
        {
            GetOrCreateChild(scene, world, "Props_Back", results);
            GetOrCreateChild(scene, world, "Props_Gameplay", results);
            GetOrCreateChild(scene, world, "LevelBounds", results);
        }

        if (actors != null)
        {
            ValidateEmptyPlaceholder("Actors/PlayerSpawn", GetOrCreateChild(scene, actors, "PlayerSpawn", results), results);
            ValidateEmptyPlaceholder("Actors/CyclopsSpawn", GetOrCreateChild(scene, actors, "CyclopsSpawn", results), results);
            results.ManualPlacement.Add("Actors/PlayerSpawn");
            results.ManualPlacement.Add("Actors/CyclopsSpawn");
        }

        if (ui != null)
        {
            ValidateEmptyPlaceholder("UI/HUDCanvas", GetOrCreateChild(scene, ui, "HUDCanvas", results), results);
            ValidateEmptyPlaceholder("UI/MinimapCanvas", GetOrCreateChild(scene, ui, "MinimapCanvas", results), results);
        }

        ReuseVisual(scene, "BG_Beach_Sky", background, results);
        ReuseVisual(scene, "BG_Beach_Far", background, results);
        ReuseVisual(scene, "BG_Beach_Mid", background, results);
        ReuseVisual(scene, "ENV_Beach_Terrain", world, results);
        ReuseVisual(scene, "FG_Beach_Props", foreground, results);
        ReuseVisual(scene, "Main Camera", cameras, results);

        EditorSceneManager.MarkSceneDirty(scene);
        results.SceneMarkedDirty = true;
        Undo.CollapseUndoOperations(undoGroup);

        TryWriteRuntimeReport(scene, results);
        EditorUtility.DisplayDialog(
            "Level 1 Beach Hierarchy",
            "Created: " + results.Created.Count + "\n" +
            "Reused: " + results.Reused.Count + "\n" +
            "Conflicts: " + results.Conflicts.Count + "\n\n" +
            "Inspect the scene, then press Ctrl+S to save your reviewed changes.",
            "OK");
    }

    private static bool EnsureBackup(Scene scene, RunResults results)
    {
        try
        {
            EnsureAssetFolder("Assets/AttackOnTroy/Scenes");
            EnsureAssetFolder(BackupFolder);

            string absoluteBackupPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", BackupPath));
            if (File.Exists(absoluteBackupPath))
            {
                results.BackupResult = "Existing backup preserved; no replacement made: " + BackupPath;
                return true;
            }

            // SaveScene's copy mode writes a copy and leaves the active scene unchanged.
            if (!EditorSceneManager.SaveScene(scene, BackupPath, true))
            {
                results.BackupResult = "Failed to save backup copy: " + BackupPath;
                results.Conflicts.Add(results.BackupResult);
                return false;
            }

            results.BackupResult = "Created a copy without switching the active scene: " + BackupPath;
            return true;
        }
        catch (Exception exception)
        {
            results.BackupResult = "Backup failed: " + exception.Message;
            results.Conflicts.Add(results.BackupResult);
            return false;
        }
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parent))
            throw new IOException("Required Unity asset parent folder is missing: " + parent);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureAssetFolder(parent);

        string guid = AssetDatabase.CreateFolder(parent, folderName);
        if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(assetPath))
            throw new IOException("Could not create Unity asset folder: " + assetPath);
    }

    private static GameObject GetOrCreateChild(Scene scene, GameObject parent, string childName, RunResults results)
    {
        GameObject[] matches = FindAllSceneObjectsByName(scene, childName);
        if (matches.Length > 1)
        {
            string location = parent == null ? scene.name : GetHierarchyPath(parent.transform);
            string conflict = "Multiple scene objects named " + childName + " for required parent " + location + "; left unchanged.";
            results.DuplicateConflicts.Add(conflict);
            results.Conflicts.Add(conflict);
            return null;
        }

        if (matches.Length == 1)
        {
            GameObject existing = matches[0];
            results.Reused.Add(GetHierarchyPath(existing.transform));
            if (parent == null && existing.transform.parent != null)
                ReparentPreservingWorldTransform(existing.transform, null, results);
            else if (parent != null && existing.transform.parent != parent.transform)
                ReparentPreservingWorldTransform(existing.transform, parent.transform, results);
            return existing;
        }

        var created = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(created, "Create " + childName);
        if (parent != null)
        {
            Undo.SetTransformParent(created.transform, parent.transform, "Parent " + childName);
            // New placeholders use an identity local transform; existing objects are never reset.
            Undo.RecordObject(created.transform, "Set identity transform for " + childName);
            created.transform.localPosition = Vector3.zero;
            created.transform.localRotation = Quaternion.identity;
            created.transform.localScale = Vector3.one;
        }

        results.Created.Add(GetHierarchyPath(created.transform));
        return created;
    }

    private static GameObject[] FindAllSceneObjectsByName(Scene scene, string objectName)
    {
        GameObject[] loadedObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var matches = new List<GameObject>();
        foreach (GameObject candidate in loadedObjects)
        {
            if (candidate.scene == scene && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                matches.Add(candidate);
        }

        return matches.ToArray();
    }

    private static void ValidateEmptyPlaceholder(string path, GameObject placeholder, RunResults results)
    {
        if (placeholder == null)
            return;

        // Preserve existing components, but flag placeholders that need a manual review.
        if (placeholder.GetComponents<Component>().Length > 1)
            results.Conflicts.Add(path + " has components beyond Transform; they were preserved and not modified.");
    }

    private static void ReuseVisual(Scene scene, string objectName, GameObject targetParent, RunResults results)
    {
        GameObject[] matches = FindAllSceneObjectsByName(scene, objectName);
        if (matches.Length > 1)
        {
            string conflict = "Multiple scene objects named " + objectName + "; left all matches unchanged.";
            results.DuplicateConflicts.Add(conflict);
            results.Conflicts.Add(conflict);
            return;
        }

        if (matches.Length == 0)
        {
            results.MissingVisuals.Add(objectName);
            return;
        }

        GameObject existing = matches[0];
        results.Reused.Add(GetHierarchyPath(existing.transform));
        if (targetParent == null)
        {
            results.Conflicts.Add("Could not resolve the required parent for " + objectName + "; object left in place.");
            return;
        }

        ReparentPreservingWorldTransform(existing.transform, targetParent.transform, results);
    }

    private static void ReparentPreservingWorldTransform(Transform child, Transform newParent, RunResults results)
    {
        if (child.parent == newParent)
            return;

        Vector3 worldPosition = child.position;
        Quaternion worldRotation = child.rotation;
        Vector3 worldScale = child.lossyScale;
        string oldPath = GetHierarchyPath(child);

        // Undo.SetTransformParent preserves world position, rotation, and scale.
        Undo.SetTransformParent(child, newParent, "Reparent " + child.name);

        bool preserved = Approximately(child.position, worldPosition) &&
                         Quaternion.Angle(child.rotation, worldRotation) < 0.01f &&
                         Approximately(child.lossyScale, worldScale);
        string newPath = GetHierarchyPath(child);
        results.Reparented.Add(oldPath + " -> " + newPath);
        if (preserved)
            results.PreservedTransforms.Add(child.name + ": world position, rotation, and scale preserved.");
        else
        {
            string conflict = child.name + ": transform comparison changed after reparent; inspect manually.";
            results.Conflicts.Add(conflict);
        }
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) < 0.0001f;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static void TryWriteRuntimeReport(Scene scene, RunResults results)
    {
        try
        {
            string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RuntimeReportPath));
            string reportDirectory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(reportDirectory))
                throw new IOException("Could not resolve the runtime report directory.");
            Directory.CreateDirectory(reportDirectory);

            var report = new StringBuilder();
            report.AppendLine("# Level 1 Beach Hierarchy Setup Report");
            report.AppendLine();
            report.AppendLine("- Active scene path: `" + EscapeMarkdown(scene.IsValid() ? scene.path : "<invalid scene>") + "`");
            report.AppendLine("- Backup result: " + EscapeMarkdown(results.BackupResult));
            report.AppendLine("- Scene marked dirty: " + (results.SceneMarkedDirty ? "Yes" : "No"));
            report.AppendLine("- Existing transforms preserved: " +
                              (results.PreservedTransforms.Count == results.Reparented.Count
                                  ? "Yes for every reparented object; existing transforms were not reset."
                                  : "Review required; one or more transform comparisons changed after reparenting."));
            report.AppendLine("- Sprite or importer metadata modified: **No**.");
            report.AppendLine("- Scene save: not performed by this tool; review the scene and press Ctrl+S manually.");
            AppendList(report, "Objects created", results.Created);
            AppendList(report, "Objects reused", results.Reused);
            AppendList(report, "Objects reparented", results.Reparented);
            AppendList(report, "Duplicate-name conflicts", results.DuplicateConflicts);
            AppendList(report, "Other conflicts", results.Conflicts);
            AppendList(report, "Missing expected visual objects", results.MissingVisuals);
            AppendList(report, "Transform preservation checks", results.PreservedTransforms);
            AppendList(report, "Objects requiring manual placement", results.ManualPlacement);
            report.AppendLine();
            report.AppendLine("New spawn markers are empty objects at local `(0, 0, 0)`; place them manually. HUDCanvas and MinimapCanvas are empty placeholders. Inspect the complete scene before saving with Ctrl+S.");
            File.WriteAllText(absolutePath, report.ToString(), new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogError("Level 1 hierarchy report could not be written: " + exception.Message);
        }
    }

    private static void AppendList(StringBuilder report, string heading, List<string> entries)
    {
        report.AppendLine();
        report.AppendLine("## " + heading);
        report.AppendLine();
        if (entries.Count == 0)
        {
            report.AppendLine("- None.");
            return;
        }

        foreach (string entry in entries)
            report.AppendLine("- " + EscapeMarkdown(entry));
    }

    private static string EscapeMarkdown(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("|", "\\|")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }
}
