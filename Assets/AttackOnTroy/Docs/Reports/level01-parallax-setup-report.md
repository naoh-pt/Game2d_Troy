# Level 01 Beach Parallax Setup Report

## Summary

Created an allocation-free runtime parallax component and an idempotent Editor setup command for the existing Sky, Far, and Mid background objects. The setup command has not been run. No scene, sprite, texture, importer metadata, camera input, terrain, bounds, or gameplay object was modified during this implementation.

## Files created

- `Assets/AttackOnTroy/Scripts/Camera/Parallax/Level01ParallaxLayer.cs`
- `Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01Parallax.cs`
- `Assets/AttackOnTroy/Docs/Reports/level01-parallax-setup-report.md` (this report)

No existing files were modified. Unity may generate `.meta` files when it next imports the new scripts and report.

## Inspected scene and assets

Active target scene: `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`.

| Layer | Sprite asset | Source dimensions | Sprite/world bounds (observed) | Transform position | Sorting layer / order |
|---|---|---:|---:|---:|---|
| Sky | `Assets/AttackOnTroy/Art/Environment/Beach/Background/BG_Beach_Sky.png` | 2172 × 724 px | 67.875 × 22.636 world units | (18.76, 14.97, 0) | BG_Sky / 0 |
| Far | `Assets/AttackOnTroy/Art/Environment/Beach/Background/BG_Beach_Far.png` | 2172 × 724 px | 67.875 × 22.636 world units | (18.85, 6.20, 0) | BG_Far / 0 |
| Mid | `Assets/AttackOnTroy/Art/Environment/Beach/Background/BG_Beach_Mid.png` | 2159 × 728 px | 67.46875 × 22.764 world units | (18.64, 4.66, 0) | BG_Mid / 0 |

All three renderers are active, use scale (1, 1, 1), and have SpriteRenderer bounds as listed. Their positions are intentionally offset. SpriteRenderer bounds are rectangular and include transparent image regions, so opaque pixel coverage can be smaller; verify the rendered composition in Unity.

Main Camera is Orthographic, at (0, 0, -10), with Orthographic Size 6. The existing Level01CameraController has minimum size 3 and maximum size 12. LevelBounds and the terrain are not changed or used by this system.

## Selected factors and coverage

| Layer | Factor | Coverage at minimum size 3 | Coverage at maximum size 12 |
|---|---:|---|---|
| Sky | 0.01 | Does not individually cover the full initial viewport vertically; its sprite lower edge is about y=3.652 while the viewport reaches y=3. This gap is already present in the scene composition and is preserved as the baseline. | Cannot cover the full viewport vertically: viewport height 24, sprite height 22.636 (1.364 units short). |
| Far | 0.08 | Bounds cover the initial viewport at the inspected position. | Cannot cover the full viewport vertically: 1.364 units short. |
| Mid | 0.16 | Bounds cover the initial viewport at the inspected position. | Cannot cover the full viewport vertically: 1.236 units short. |

Coverage above compares the renderer’s full rectangular bounds with the camera viewport at the inspected camera position. Horizontal coverage depends on aspect ratio: at size 12 the 67.5–67.9 unit sprite widths cover up to an aspect ratio of roughly 2.81–2.83. Typical phone ratios fit the width, but verify the project’s target aspect ratios. Transparent pixels mean bounds coverage does not guarantee opaque visual coverage.

The component captures original transform and renderer bounds as immutable anchors when enabled, derives desired movement from camera displacement, and constrains each axis against the startup viewport’s edge exposure. This preserves original gaps, prevents parallax from increasing them when feasible, and avoids cumulative drift. If a viewport is larger than a sprite, the component centers unavoidable excess exposure. No individual background can guarantee full vertical coverage at Orthographic Size 12. Consider reducing the camera controller’s maximum size to about 11.0 (theoretical full-height limit is about 11.32, before any safety margin) if full sprite-bounds coverage is required. The camera limit was not changed.

## Setup command

Menu path: **Attack on Troy > Setup > Configure Level 01 Parallax**.

The command only operates on active scene `SCN_Level01_Beach`. It requires exactly one `Main Camera` and exactly one of each named background object, with a Camera and SpriteRenderer respectively. It aborts on missing or duplicate targets before making changes. It adds at most one `Level01ParallaxLayer` per layer, sets the camera/renderer references and factors, records changes through Undo, and marks the scene dirty. It does not create scene objects or save the scene.

## Unity steps

1. Open `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` and make it the active scene.
2. Wait for Unity to compile/import the new scripts.
3. Select **Attack on Troy > Setup > Configure Level 01 Parallax**.
4. Inspect the three components and rendered framing in Scene and Game views. Confirm no visual object settings changed.
5. Use Ctrl+S only after reviewing and accepting the scene changes. Use Undo to revert the setup if needed.

## Test checklist

### Mouse / Editor

- Pan horizontally and vertically using the existing camera controls; Sky should move almost imperceptibly, Far slowly, and Mid more strongly.
- Zoom in and out through the existing controller limits; inspect all viewport edges for newly exposed gaps.
- Test on the widest target aspect ratio and at the maximum zoom. Individual layers cannot cover the full height at size 12, as noted above.
- Confirm the terrain and foreground/gameplay props stay fixed and the existing camera pan/zoom behavior remains unchanged.
- Disable/re-enable Play Mode and repeat to confirm stable anchors and no cumulative drift.

### Android touch

- Build/run on a device and pan with one finger; verify the same relative motion and edge constraint.
- Pinch zoom through the full permitted range; inspect edges and verify stable placement.
- Repeat at the target device’s aspect ratio and with UI interaction if UI is later introduced.

## Android performance

Each layer performs constant-time vector and bounds arithmetic in `LateUpdate`. References are serialized; there are no per-frame scene searches, collections, coroutines, or deliberate allocations. The component uses the existing camera’s movement and zoom and does not add input handling or alter the Input System.

## Manual verification required

- Run the setup menu yourself after reviewing these files; this implementation did not run it or save the scene.
- Inspect the real Game view for transparent pixel gaps, edge exposure, sorting, and visual depth. Static SpriteRenderer bounds cannot determine opaque coverage.
- Check maximum zoom and the widest target aspect ratio on device. A maximum size near 11.0 is recommended only if full sprite-rectangle vertical coverage is a requirement.
- Confirm the three layers’ visual response feels appropriate during camera-follow movement when that feature is added later.

## Implementation validation

Both new scripts were checked for C# compilation using the installed Unity 6000.6.0f1 Editor assemblies plus the project’s installed Input System and UGUI assemblies. Compilation succeeded. The setup command was not invoked. No scene YAML was edited, and no scene was saved.
