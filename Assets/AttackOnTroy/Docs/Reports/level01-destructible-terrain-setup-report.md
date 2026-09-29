# Level 01 Destructible Terrain Setup Report

## Result and files

Implemented a non-destructive runtime terrain prototype. The runtime system copies the source sprite into private mutable memory, clears alpha through `DestroyCircle`, updates the affected texture block, and rebuilds alpha-derived filled collision. Destroying the runtime objects or reloading Play Mode restores the untouched imported sprite automatically.

Files created:

- `Assets/AttackOnTroy/Scripts/World/Terrain/DestructibleTerrain2D.cs`
- `Assets/AttackOnTroy/Scripts/World/Terrain/TerrainDestructionDebugTester.cs`
- `Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01DestructibleTerrain.cs`
- `Assets/AttackOnTroy/Docs/Reports/level01-destructible-terrain-setup-report.md`

Unity metadata files were added for the new folders and assets. No existing runtime, camera, actor, prefab, Project Settings, PNG, or scene file was modified. The setup command was not run and `SCN_Level01_Beach.unity` was not saved.

## Inspection findings

### Terrain sprite and scene object

- Source: `Assets/AttackOnTroy/Art/Environment/Beach/Terrain/ENV_Beach_Terrain.png`
- Source PNG: 2172 × 724, 32-bit RGBA.
- Import type: Sprite (2D and UI), Single mode, Tight mesh, bilinear filtering, clamp wrapping, mipmaps disabled, alpha-is-transparency enabled.
- Sprite logical rect: 2172 × 724 pixels at `(0, 0)`.
- Pixels Per Unit: 32.
- Pivot: centered, normalized `(0.5, 0.5)`; pixel pivot approximately `(1086, 362)`.
- Read/Write before setup: disabled.
- Default/Android maximum imported texture size: 2048 with inherited compressed platform settings. Runtime code resamples the imported readable texture back into the original logical sprite rect before making the mutable sprite, preserving the 2172 × 724 rect dimensions, pivot, and 32 PPU instead of shrinking the world image.
- Scene transform: position `(18.85, 2.92, 0)`, zero rotation, scale `(1, 1, 1)`.
- SpriteRenderer: enabled, no X/Y flip, Simple draw mode, white color, existing material retained, `World` sorting layer, order `0`.
- Observed renderer bounds from the existing project reports: approximately `67.875 × 22.636` world units. The rect/PPU calculation is `67.875 × 22.625`; Unity's exact imported renderer bounds should be rechecked after reimport.
- There is no gameplay collider on `ENV_Beach_Terrain`.

The runtime coordinate conversion uses the sprite's logical rect, pixel pivot, and PPU. It converts world points through `Transform.InverseTransformPoint`, so translation, rotation, parent transforms, non-uniform/negative scale, and normal SpriteRenderer X/Y flips are handled. Every candidate crater pixel is transformed back into world space before the radius test, so a world circle stays circular even under non-uniform scale. Rotated sprite-atlas packing and non-Simple draw modes are rejected with one validation error because their texture/layout mapping is not supported.

### Alpha-mask suitability

Direct source-PNG analysis found:

- 1,572,528 total pixels.
- 965,046 fully transparent pixels.
- 607,445 partially transparent pixels.
- 37 fully opaque pixels.
- 590,992 pixels at alpha ≥ 128 (`37.58%` of the image).
- Alpha ≥ 128 source bounds: X `0–2171`, Y `321–723` in top-origin PNG coordinates.

The artwork is suitable for alpha-derived terrain: the upper image is transparent, the irregular beach surface is represented in alpha, and material continues to the lower image boundary. It is not suitable for an “alpha equals 255” test because nearly all visible pixels use partial alpha. The default `0.5` threshold is therefore intentional, but the edge fit needs visual collider verification and may benefit from tuning.

### Existing physics setup

- No `Terrain` physics layer exists. The setup command therefore keeps/sets this terrain on `Default` and does not create a layer or edit the collision matrix.
- The Physics 2D layer matrix currently enables all layer pairs.
- `LevelBounds` is a separate trigger `BoxCollider2D` on `Default`, used for camera clamping. It is not referenced or reused by the terrain system.
- Achilles has a non-trigger capsule collider (`0.36544585 × 1.845`) and Kinematic `Rigidbody2D`.
- Cyclops Normal has a non-trigger capsule collider (`1.2503077 × 3.075`) and Kinematic `Rigidbody2D`.
- Kinematic actor movement/gravity behavior was not changed. Future movement must use appropriate Rigidbody2D physics movement for collision response.

## Runtime design

`DestructibleTerrain2D` is the only terrain owner. In `Awake` it:

1. Validates the SpriteRenderer/sprite layout and reads the source pixels once.
2. Reconstructs a private logical-resolution RGBA32 pixel array and mutable `Texture2D`.
3. Creates a runtime `Sprite` using the original logical rect, normalized pivot, PPU, border, and full-rect mesh.
4. Replaces only `SpriteRenderer.sprite`; material, color, sorting layer/order, transform, and other renderer settings remain untouched.
5. Creates a non-saved child named `RuntimeTerrainColliders` with one `PolygonCollider2D`.
6. Builds the initial collision paths from cached alpha.

The runtime texture, sprite, collider child, and collider geometry use `HideFlags.DontSave`, are created only in Play Mode lifecycle, and are destroyed with the component. The original sprite is restored during teardown. Nothing writes to the PNG or importer at runtime.

Public future-projectile API:

```csharp
terrain.DestroyCircle(projectileImpactWorldPosition, explosionRadiusInWorldUnits);
```

Calls with a non-positive radius, a point outside the terrain, or an already empty overlap safely do nothing. Repeated and overlapping calls are supported.

## Collision-generation algorithm

The collider uses sampled alpha cell-boundary contouring followed by closed-path Ramer–Douglas–Peucker simplification:

1. The cached sprite pixels are divided into configurable square sample cells.
2. A cell is solid when any contained pixel meets the alpha threshold. This preserves narrow opaque features better than center-only sampling.
3. Directed boundary edges are emitted only where a solid cell borders empty space or the sprite edge.
4. Edges are stitched into consistently wound closed contours. Opposite winding naturally represents enclosed transparent craters; separate contours represent disconnected islands.
5. Contours are converted from sprite pixels to local coordinates, simplified, and assigned as multiple paths on one filled `PolygonCollider2D`.

Unity's multi-path polygon collider supports holes and discontinuous parts. This gives filled collision without pixel colliders, thousands of boxes, or a full-rectangle fallback. A single runtime child/component is reused on every rebuild, preventing duplicates.

Relevant Unity 6 API references: [PolygonCollider2D paths and holes](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PolygonCollider2D.GetPath.html), [PolygonCollider2D.SetPath](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PolygonCollider2D.SetPath.html), and [partial Texture2D.SetPixels32 updates](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture2D.SetPixels32.html).

Default collision settings:

- Alpha threshold: `0.5` / 128-level equivalent.
- Collider sampling resolution: `4` pixels, or about `0.125` world units at 32 PPU.
- Simplification tolerance: `0.05` local/world units at the current unit scale.
- Polygon Delaunay mesh: disabled.

Collision is scanned/rebuilt once during initialization and only after a destruction call clears pixels that previously met the collision threshold. There are no terrain `Update`/`LateUpdate` methods, scene searches, `GetPixels` calls, or collections allocated every frame. Texture upload uses only the affected rectangular pixel block, although Unity's final `Texture2D.Apply` upload cost remains implementation-dependent.

## Runtime memory estimate

At logical 2172 × 724 RGBA32:

- Cached mutable `Color32[]`: about 6.0 MiB.
- Readable runtime texture CPU data: about 6.0 MiB.
- Runtime texture GPU data: about 6.0 MiB.
- Readable imported source CPU copy at its 2048-limited size: approximately 5–6 MiB.
- Contour/grid data varies with alpha complexity; the 4-pixel grid contains about 98,000 cells and is small relative to pixel storage.

Expected steady terrain-related memory is approximately 23–26 MiB plus the already existing imported GPU texture and collider internals. Initialization temporarily also holds the imported `GetPixels32` result and contour-build collections, so peak can be roughly 30–36 MiB. This is reasonable for a single prototype terrain but should be profiled on the lowest Android target before adding multiple destructible layers.

## Editor debug tester

`TerrainDestructionDebugTester` uses `Mouse.current` from the New Input System. A right click converts the Game View pointer to the terrain plane and calls the same `DestroyCircle` public API. Left mouse remains reserved for camera dragging. The tester ignores clicks over the active `EventSystem` UI.

- Testing Enabled default: `true`, Inspector-configurable.
- Test Radius default: `1.5` world units.
- In non-Editor players, input exits immediately unless `Debug.isDebugBuild` is true, so release Android builds do not activate the tester.

## Setup command

Menu path: **Attack on Troy > Setup > Configure Level 01 Destructible Terrain**

The command requires the active scene path to be exactly:

`Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`

It preflights exactly one `ENV_Beach_Terrain` and `Main Camera`, rejects duplicate terrain/test components, enables only the required texture Read/Write importer flag, adds/reuses and configures both components, uses an existing `Terrain` physics layer when available, otherwise uses `Default`, supports Undo for scene changes, marks the scene dirty, and does not save it. Re-running it does not create duplicate objects/components. It does not touch LevelBounds, camera, parallax, actors, spawn points, animation, terrain transform, or SpriteRenderer visual settings.

## Exact Unity steps

1. Open `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` and make it active.
2. Wait for compilation and confirm the Console has no errors.
3. Run **Attack on Troy > Setup > Configure Level 01 Destructible Terrain**.
4. Allow the terrain texture reimport to complete.
5. Select `Level/World/ENV_Beach_Terrain`.
6. Confirm one `DestructibleTerrain2D` and one `TerrainDestructionDebugTester` are present.
7. Confirm alpha threshold `0.5`, sampling resolution `4`, simplification tolerance `0.05`, test radius `1.5`, and the Main Camera/test references.
8. Confirm the terrain remains on `Default` because no `Terrain` physics layer currently exists.
9. Confirm transform, material, color, `World` sorting layer/order, background/props, LevelBounds, camera, parallax, actors, spawns, and animations are unchanged.
10. Save the scene manually only after reviewing the Undo-able scene changes. The importer Read/Write change is an asset reimport and should also be reviewed in version control.

## Test checklist

- Enter Play Mode and confirm a visible `RuntimeTerrainColliders` child appears only at runtime with one non-trigger `PolygonCollider2D`.
- Enable Physics 2D collider gizmos and verify the initial collider follows the opaque beach surface rather than the full rectangle.
- Confirm transparent sky regions have no terrain collision and disconnected alpha regions generate separate paths.
- Right-click near the surface in Game View: confirm a roughly 1.5-unit circular visual crater and matching collider hole/walls appear.
- Right-click repeatedly with partially overlapping circles and near all four texture edges; confirm clipping is safe and there are no exceptions.
- Right-click completely outside the terrain and over UI; confirm nothing changes.
- Verify clicks after camera pan/zoom and while camera target modes are active land at the expected world position.
- Temporarily test X/Y SpriteRenderer flips and translated/scaled terrain in an unsaved test state; confirm visual/collider alignment, then revert.
- Inspect Achilles/Cyclops contact only as a collider sanity check; do not add gravity or movement in this phase.
- Exit and re-enter Play Mode: confirm the original terrain is fully restored.
- Build a Development Android player and exercise several craters while profiling CPU time, managed allocations, texture upload, Physics2D rebuild cost, and peak memory.
- Build a non-Development Android player and confirm click destruction is inactive.

## Expected limitations and manual verification

- The setup command has deliberately not been run, so Unity must verify the exact imported `Sprite.rect`, `textureRect`, renderer bounds, and visual alignment after Read/Write reimport.
- The 4-pixel any-solid sampling can expand collision outward by up to roughly one sample cell and removes features smaller than the sampling grid. Tune resolution/tolerance against Android performance if necessary.
- Every collision rebuild scans the complete logical alpha mask and regenerates all contours. This is appropriate for discrete turn-based explosions, not continuous painting every frame or dozens of simultaneous blasts.
- `Texture2D.Apply` may upload more than the requested changed block internally. Profile the actual device cost.
- Very aggressive simplification can self-intersect or erase narrow crater walls; keep the tolerance below the gameplay-critical feature size.
- Polygon collider decomposition cost depends on resulting contour complexity. Large numbers of tiny isolated alpha flecks should be cleaned from artwork or filtered in a future refinement if profiling exposes a problem.
- Runtime sprite-atlas rotation and non-Simple tiled/sliced render modes are intentionally unsupported and log once. The inspected Level 01 terrain uses a compatible Simple, non-flipped, non-atlas configuration.
- Terrain is static and pieces do not fall. Transform changes are mapped correctly for destruction/alignment, but continuously moving static terrain is outside this prototype.
- Kinematic actors do not automatically become gravity-driven characters; movement/gravity remains future work.
- No persistence is implemented. Play Mode restart/scene reload intentionally resets all destruction.
