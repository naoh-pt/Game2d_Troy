# Level 01 Actor Movement and Presentation Fix

## Scope and status

Implemented the shared motor repair and an idempotent Editor repair command. The menu command was **not run**. The Level 01 scene, character prefabs, TagManager sorting order, sprites, animation clips, and PPU settings were not modified by this task. Running the menu later will reorder the existing sorting-layer entries and mark the active scene dirty; it will not save the scene.

## Files created or modified

- Modified `Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs` (existing script GUID preserved).
- Created `Assets/AttackOnTroy/Editor/Setup/FixLevel01ActorPresentationAndMovement.cs` and its Unity `.meta` file.
- Created this report.
- No scene, prefab, texture, animation, or ProjectSettings asset was edited.

## Findings and exact movement cause

The current Level 01 scene has the Achilles prefab instance under `Actors` at world root position approximately **(-2.335, -2.094)**. The `PlayerSpawn` marker is at approximately **(-2.655, -1.524)**, so the instance is about **0.32 units to the right and 0.57 units below** that marker. Its scene override capsule is approximately **1.013 × 3.878** with offset **(-0.324, 0.329)**. This puts the capsule's lowest point near world Y **-3.704**, centered horizontally near X **-2.659**.

The terrain renderer is at world position approximately **(13.55, 1.32)**. Its source is **2172 × 724**, PPU **32**, centered pivot, and readable with alpha. At X ≈ -2.659, the PNG's first alpha≥128 pixel maps to an upper terrain boundary near Y **-3.27**. Thus the Achilles capsule starts about **0.43 world units intersecting** the alpha-derived terrain boundary at its center (the overlap varies across the capsule because the beach is sloped). This is a real initial-overlap condition; the scene position and collider are intentionally left untouched here.

The short movement also follows directly from the old motor implementation:

1. While grounded it changed only horizontal velocity, so the actor had no uphill tangent component on a slope.
2. Its forward horizontal cast treated opposing low-Y-normal hits as walls. The terrain collider is a sampled polygon contour (sampling resolution **4 pixels**, about **0.125 world units** per sample at PPU 32), so small vertical contour risers could trip that wall check.
3. The old blocking cast did not discard zero-fraction/initial-overlap hits. The observed starting penetration makes those hits especially relevant.

The responsible colliders are the Achilles **CapsuleCollider2D** and the generated **PolygonCollider2D** under `RuntimeTerrainColliders` on `ENV_Beach_Terrain`. `ENV_Beach_Terrain` is already on physics layer **Terrain (6)**, and `DestructibleTerrain2D` explicitly gives its generated child the parent's layer, so the child is also on Terrain. The actor motor filters to that layer. `LevelBounds` has a trigger collider on Default and is therefore not a motor ground/blocking collider. `TerrainInterior` has a SpriteRenderer but no collider. `Props_Gameplay` has no separate actor-blocking colliders in this scene; beach rocks/ground shapes in the tested path are part of the terrain artwork/alpha rather than separate scene colliders. The Cyclops is far from Achilles, so actor-to-actor collision does not explain this short initial path. The Physics 2D collision matrix currently enables the layer pairs; the motor's query mask is the limiting filter.

This is a source/configuration diagnosis, not a captured Play Mode contact trace. The scene's initial overlap is confirmed from its serialized transform/collider overrides and the source alpha mask; the exact sequence of runtime contacts should still be checked after the repair.

## Motor changes

`GroundedActorMotor2D` retains the existing public `SetMoveInput`, `SetMovementEnabled`, `IsGrounded`, `FacingDirection`, and editor `Configure(...)` API.

- Grounded movement now samples the Terrain surface ahead and builds a signed ground tangent, giving uphill travel the required vertical component and following downhill contours without injecting a jump. Airborne input still changes only horizontal velocity; gravity-driven vertical velocity is preserved.
- A configurable maximum contour step height (default **0.20**) and profile distance (default **0.45**) distinguish 4-pixel sampling risers from walls. A blocker is ignored only if its height and the sampled ahead profile both fit the configured step and maximum slope; otherwise it remains blocking. Walkable-slope limits remain configurable (existing defaults: 45° and minimum normal Y 0.5).
- Terrain shape casts reject the actor's own collider, triggers, wrong layers, and zero-fraction initial-overlap hits. The ahead-profile cast lifts its query by the measured support-to-collider overlap depth plus a small step margin, so a modest starting penetration does not leave the profile sample embedded. Ground contact data is still used to recover support during overlap resolution.
- No Transform-based movement, per-frame scene lookup, or deliberately per-frame collection creation was added. Cast/contact buffers are reused; the profile query uses a pre-sized reusable hit list.
- Optional selected-object Scene gizmos (off by default) display grounded state, accepted ground normal, movement tangent, and forward blocking cast.

## Foreground sorting

Current `ProjectSettings/TagManager.asset` order is:

`Default → BG_Sky → BG_Far → BG_Mid → World → Character → Projectile → Effect → Foreground → UI → Actors`

`FG_Beach_Props` already uses the existing `Foreground` sorting layer, but that layer currently sorts **behind** `Actors`; actor SpriteRenderers use `Actors`. The repair menu will keep the existing layer IDs and reorder the entries to:

`Default → BG_Sky → BG_Far → BG_Mid → World → Character → Projectile → Effect → Actors → Foreground → UI`

This makes the existing background/world and behind-actor props remain behind actors, allows `FG_Beach_Props` to overlay the lower body, and keeps UI last. The menu locates exactly one `FG_Beach_Props` SpriteRenderer in the active target scene and assigns it to existing `Foreground` if necessary. It does not move all props or add gameplay colliders. Scene renderer changes use Undo and mark the scene dirty. Sorting-layer order is a ProjectSettings change; it is saved as a project setting, not as a scene save.

## Achilles Idle versus Run alpha audit

Measured the raw source PNG alpha bounds for every sliced frame. Both sheets are **100 PPU**; animation clips swap sprites only and have no position, scale, or float transform curves. Root/Visual transforms were not changed.

| Sheet | Frames | Median slice rect (px) | Median opaque bounds (px) | Median perceived opaque size at 100 PPU |
|---|---:|---:|---:|---:|
| `SPR_Achilles_Idle.png` (1587 × 522) | 6 | 216 × 492 | 201 × 476.5 | 2.01 × 4.765 world units |
| `SPR_Achilles_Run.png` (1983 × 374) | 8 | 203 × 325 | 198.5 × 302.5 | 1.985 × 3.025 world units |

Run's median opaque height is **36.5% shorter** than Idle, while its median opaque width differs by only about **-1.2%**. Run frames have little/no extra transparent top padding; the shorter appearance is the artwork inside the slice, not a PPU mismatch, transform curve, or large transparent border. Frame rectangles also differ because each sheet uses tightly cut, differently sized slices.

A purely theoretical Run PPU of about **63.5** would match median height (302.5 px / 4.765 units), but would make the Run character about **3.13 units wide**, roughly 55.5% wider than Idle. Therefore **no safe PPU-only correction is recommended**. Do not apply that theoretical value. The art should be reviewed/re-authored for consistent character proportions if desired; no PNG, sprite GUID, slicing, animation binding, or PPU was changed.

## Setup menu

`Attack on Troy > Setup > Fix Level 01 Actor Presentation and Movement`

The command requires `SCN_Level01_Beach` to be the active scene, is safe to run repeatedly, preserves sorting-layer IDs and renderer appearance settings, supports Undo for scene renderer assignment, marks the scene dirty, and does not save the scene. It does not change the Run-sheet PPU or actor position/collider overrides. The motor code/configuration update is already in the shared component; no prefab resave is required to update its runtime behavior.

## Unity steps and test checklist

1. Open `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` and make it active. Let scripts compile.
2. Run the menu above. Confirm the Scene becomes dirty; inspect `Project Settings > Tags and Layers > Sorting Layers` and verify `Actors < Foreground < UI`.
3. Verify only `FG_Beach_Props` was assigned to Foreground and its material, color, transform, and sprite remain unchanged. Confirm terrain/world, background, and behind-actor props still render behind Actors.
4. Select Achilles and optionally enable `GroundedActorMotor2D > Show Ground Debug Gizmos`. In Scene view verify green grounded state, upward support normal, tangent direction, and cast/blocker visualization.
5. Enter Play Mode and test A/D and arrow movement both ways on the opening beach. Confirm uphill/downhill travel, deceleration, facing, and Run/Idle Animator Speed transitions.
6. Check that walls or surfaces beyond maximum slope still block, but the 4-pixel stair contour does not stop ordinary beach traversal. Temporarily inspect collider contacts if movement still stalls.
7. Confirm Achilles settles onto the terrain, Cyclops stays standing and idle, and deleting terrain under an actor allows a gravity-driven fall. Confirm camera follow remains functional.
8. Confirm `TerrainInterior` remains visual-only and that foreground grass overlays the lower body after movement.
9. Review Achilles's starting capsule overlap. If it remains embedded after physics settles, manually place the scene instance/capsule so the collider bottom rests on the alpha contour before final testing; this task deliberately does not alter spawn position or collider overrides.
10. Inspect the Run/Idle artwork with the measurements above; no scale or PPU changes are part of this repair.
11. Save the scene manually only after reviewing all scene changes.

## Manual verification and limitations

- The **~0.43-unit initial penetration** needs visual/Play Mode review. If the scene instance is meant to spawn standing on the slope, its current world position is inconsistent with that result; move the scene instance upward enough for its CapsuleCollider2D bottom to meet the terrain contour, rather than changing shared prefab scale. This repair tool intentionally preserves the override.
- The height-profile logic handles sampled steps within the configured tolerance; truly steep slopes and high walls remain blocking. Tune the step height only after observing actual contacts, and avoid increasing it enough to permit wall climbing.
- Scene gizmos are drawn for the selected motor owner in the Editor; they are not runtime debug rendering and default off.
- The static alpha audit uses the imported source RGBA mask at threshold 128 for a human-readable surface estimate. Runtime terrain uses its configured alpha threshold and 4-pixel contour sampling, so the exact collision boundary can differ by a fraction of a world unit.
- Compiled runtime scripts against Unity **6000.6.0f1** references successfully. The Editor setup assembly also compiled; it reported only two pre-existing obsolete-API warnings in `Level01BeachHierarchySetup.cs`.
