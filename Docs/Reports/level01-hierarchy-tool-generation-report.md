# Level 1 Hierarchy Tool Generation Report

## Result

Created the self-contained Unity Editor tool at `Assets/AttackOnTroy/Editor/Level01BeachHierarchySetup.cs`. The project root was validated first: `Assets/`, `Packages/`, and `ProjectSettings/` are present.

## Files created or modified

- Created: `Assets/AttackOnTroy/Editor/Level01BeachHierarchySetup.cs`
- Created: `Docs/Reports/level01-hierarchy-tool-generation-report.md`
- Runtime output created by the tool after execution: `Docs/Reports/level01-hierarchy-setup-report.md`
- Unity scene, image, importer metadata, package, and project setting files modified while generating this tool: **None**.

## Safety mechanisms

- The command checks the active scene name and stops without scene changes unless it is exactly `SCN_Level01_Beach`.
- Before any hierarchy changes, it ensures the backup folder exists and creates a save-as-copy backup only when the named backup file is absent. Existing backups are preserved, and the active scene is not switched to the copy. If backup setup fails, the command stops before hierarchy changes.
- Existing visual objects are searched by exact name across the complete active scene, including inactive objects. A single match is reused and reparented with `Undo.SetTransformParent`; multiple matches are left unchanged and reported. Missing visuals and a missing Main Camera are reported; no visual or camera is fabricated.
- Existing visual components, sprites, render settings, sorting values, and world transforms are not assigned or reset. Transform values are compared after reparenting and discrepancies are reported.
- Empty hierarchy objects use Undo registration; newly created child placeholders receive an identity local transform. Existing containers are reused without resetting their transforms.
- Newly created spawn points and canvas-named objects are empty placeholders. Existing objects are preserved; if a spawn/canvas-named placeholder has components beyond Transform, the runtime report flags it for review. No scripts, colliders, rigidbodies, Canvas components, or camera behavior are added.
- Scene changes use Undo APIs, then mark the scene dirty. The tool does not save the modified active scene. It writes the runtime report and asks the user to inspect the scene and press Ctrl+S.
- The tool writes its runtime report only to `Docs/Reports/level01-hierarchy-setup-report.md`. It does not directly edit scene or `.meta` YAML.

## Menu path

`Attack on Troy/Setup/Complete Level 1 Beach Hierarchy`

## User execution steps

1. Open the project in Unity and open `SCN_Level01_Beach` as the active scene.
2. Select **Attack on Troy → Setup → Complete Level 1 Beach Hierarchy**.
3. Review the completion dialog and the runtime report at `Docs/Reports/level01-hierarchy-setup-report.md`.
4. Inspect the hierarchy, existing visual placement, and the new spawn markers in the Scene view. Place `PlayerSpawn` and `CyclopsSpawn` manually.
5. Press **Ctrl+S** only after reviewing the dirty scene.

## Manual verification still required

- Compile and run the menu command in the installed Unity Editor.
- Confirm the backup copy appears and the original scene remains active.
- Inspect the hierarchy, reused visual transforms, duplicate-name findings, and placeholder positions in Unity.
- Confirm the runtime report is written successfully and that Undo restores the command’s scene changes.
- Position the two spawn markers manually and decide final camera framing separately.
