# Character movement collision repair

## Delivery and execution status

Updated the shared motor and added `Attack on Troy > Setup > Fix Character Movement Collision`. The command has not been run. No Play Mode session was started and no scene was saved. Existing prefab, scene and sorting-layer edits already present in the worktree were preserved.

Files modified/created in this task:

- Modified `Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs`; existing script GUID and public movement API preserved.
- Created `Assets/AttackOnTroy/Editor/Setup/FixCharacterMovementCollision.cs` and `.meta`.
- Created this report and `.meta`.

When the user runs the command, it creates or updates exactly one material at `Assets/AttackOnTroy/Physics/Materials/PM_ActorMovement.physicsMaterial2D` through AssetDatabase, saves changes to the four prefab assets, and configures connected actors in the active Level 01 scene with Undo. It marks the scene dirty without saving it. The material asset is intentionally created by that command rather than by editing Unity asset serialization manually.

## Confirmed cause in the current motor, and limits of the diagnosis

The current motor had a concrete geometry/velocity defect: it calculated slope from `aheadHit.point.y - groundContact.point.y`, divided by the requested horizontal query offset. Those points can occur at different places around the capsule. Their actual X separation is not necessarily the query offset. On sampled PolygonCollider2D contours, selecting the highest contact further changes which tread supplies the reference point. This can give the wrong slope magnitude or direction. It then used `Vector2.MoveTowards` to overwrite BOTH velocity axes, including a negative target Y on a downhill profile. A downward target into a stair tread is cancelled by collision response and increases contact pressure/friction rather than producing travel.

The earlier fix also merely ignored a shallow riser in its blocker logic. It did not establish clearance above that riser or provide upward assistance when already touching it. All regular casts already rejected fraction-zero hits; nevertheless a touching riser can have genuine side contacts while its cast only reports initial overlap. A nearby supporting tread can also be missed by the old 0.12-unit ground query, which is shorter than a 4-pixel / 32-PPU step (0.125 units).

Continued Run is consistent with this pipeline: the motor writes a nonzero velocity and sets Animator `Speed` in FixedUpdate BEFORE the physics solver resolves collisions. That assigned velocity is not proof of actual displacement. Animator input handling was left unchanged in this task, so diagnostics now explicitly distinguish requested, assigned, observed-after-previous-physics and displacement velocities.

These are confirmed code defects. The exact contact that stops the user's live actor, whether it remains embedded at that moment, and how much friction contributes cannot be measured without a runtime physics observation. No such observation was performed, in accordance with the requested restrictions. The diagnostics below are designed to supply that missing evidence. This report does not claim a captured live contact trace.

## Inspection of current actor and terrain

Achilles prefab root currently has one Dynamic Rigidbody2D, one vertical non-trigger CapsuleCollider2D, one motor, one development input adapter and one fall-recovery component. The motor references its own root body/collider and Visual renderer/Animator. The New Input System adapter supplies signed input and writes no position or velocity.

The Level 01 Achilles instance is still connected to `PF_Achilles`. Its serialized overrides are root position/rotation/name, capsule size/offset, and Visual XY scale/position. There are no Rigidbody2D, material, motor-reference or movement-parameter overrides in this instance. Root scale inherits (1,1,1). The overrides are:

- Root local position (-6.78, -0.94, 0) under Actors at (4.4448, -1.15386, 0), giving world position approximately (-2.3352, -2.09386).
- Capsule size (1.0127312, 3.8777845), offset (-0.32364267, 0.32875144), compared with prefab size (0.36544585, 1.845), offset (0, 0.9225).
- Visual local XY scale (1,1), local position (-0.1,0.7). These are preserved.

The scene capsule's lowest point initially lies near Y -3.704, at center X -2.659. The source alpha boundary there is near Y -3.274, so the authored start still intersects the terrain by approximately 0.43 units at its bottom center. The user's improved landing behavior indicates physics can resolve the start; this overlap alone is not established as the cause of the later stop. The larger, left-offset scene capsule can contact terrain with its side when approaching risers. The repair preserves its size/offset and uses its actual shape for all clearance queries.

Terrain inspection:

- ENV_Beach_Terrain source: 2172 × 724, PPU 32, centered pivot, readable RGBA; renderer world origin approximately (13.55,1.32), unrotated unit scale, no flip.
- Scene destruction settings: alpha threshold 0.5, sampling 4 pixels, simplification tolerance 0.05.
- Runtime generation creates a non-trigger PolygonCollider2D with multiple traced contour paths under `RuntimeTerrainColliders`, parented with identity local transform. It inherits Terrain physics layer 6, has no explicit material, and is recreated/rebuilt only by the existing terrain system. No terrain algorithm or appearance was changed.
- The grid is conservative: a cell is solid when any pixel in its 4 × 4 region passes the alpha threshold. It can therefore have small vertical tread/riser boundaries.
- Source-grid top rows near the starting path, at pixel X 564/568/572/576/580/584/588, are approximately 508/504/500/500/500/496/492. Several local risers are 4 pixels = 0.125 world units. Source surface Y rises from about -3.524 at world X -3.165 to -2.930 at X -2.0 (about a 27-degree overall incline), then descends further right.
- Those source-mask measurements are approximations: the runtime system reconstructs logical sprite pixels from the imported texture, then simplifies its generated contour. No runtime PolygonCollider2D/contact was inspected in Play Mode.
- No separate rock/grass collider is authored in the initial path. Ground details are part of the terrain alpha. TerrainInterior is visual-only. LevelBounds is a trigger on Default and is excluded by the motor's Terrain-only, non-trigger contact filter.
- The actual **Physics2DSettings.asset** has automatic FixedUpdate simulation, gravity (0,-9.81), default material null, default contact offset 0.01, QueriesStartInColliders enabled, and an all-enabled 2D layer collision matrix. Actor Rigidbody2D/collider include/exclude masks have no overrides. These settings remain unchanged.

## Friction and velocity writers

No authored PhysicsMaterial2D asset was found. All four actor bodies/capsules currently have null shared materials; the generated terrain polygon also has no explicit material. Thus zero friction was not configured. Friction can contribute to sticking at sloped or stair-edge contacts, particularly when the motor presses downward, but its contribution to this particular live stop is unproven. The new command assigns an explicit friction-0/bounciness-0 material to actor capsules; Unity 6000.6 Multiply combine modes are set for both values. Diagnostics read the effective contact friction to verify the result rather than assuming it.

Project-wide C# writer search found:

| Script | Writes | Relevance |
|---|---|---|
| GroundedActorMotor2D | Rigidbody2D linearVelocity / X / Y | Normal actor movement writer; corrected here |
| ActorFallRecovery2D | Rigidbody2D position, rotation, zero linear/angular velocity | Only below recoveryWorldY (~-13.051874 for Achilles) or explicit RecoverNow; not a normal beach stop |
| Level01CameraController | Its camera Transform.position | Does not write actor body |
| Level01ParallaxLayer | Its layer Transform.position | Does not write actor body |

No other actor Rigidbody2D velocity, position or MovePosition writer, or actor Transform translation script was found. Input only calls SetMoveInput. No external component overwrite was established. Runtime scripts added outside the inspected project later would need a fresh audit.

## Correction

The motor now measures the capsule's required clearance height with two identical downward shape casts: one at its current horizontal position, one offset in the movement direction, both starting at the same lifted height. `currentCast.distance - aheadCast.distance` gives the required rise of the same shape. This avoids inferring capsule displacement from unrelated contact-point heights. The profile is accepted only within the maximum walkable slope/minimum-normal limits.

Horizontal acceleration/deceleration is independent of vertical velocity. When grounded and traveling uphill, the motor supplies the necessary positive vertical velocity, also respecting the accepted support normal. Downhill movement never writes a downward tangent; gravity and collision response control descent. Significant incoming fall velocity is retained until actual ground contact, rather than cancelled by a nearby ground cast. Upward assistance is a bounded velocity request, not cumulative displacement or Transform movement.

For low risers, the motor uses both valid forward cast hits and genuine side contacts. It verifies ground support ahead and performs a second horizontal shape sweep with only the QUERY lifted by the permitted step height. The forward sweeps cover next-step travel plus a 0.02-unit margin; they do not span the entire slope-profile offset, which could mistake several future treads for one tall wall. If terrain still occupies that elevated clearance, the obstacle remains blocking. A cleared small riser receives positive velocity assistance instead of being ignored while the capsule remains pressed into it. Tall walls and over-limit profiles remain blocking. All regular casts reject self/sibling bodies, triggers, wrong layers, zero fraction and zero distance. A zero-distance result in the raised clearance check conservatively denies stepping; it is never accepted as valid ground or movement support.

Ground queries now reach 0.20 units, above the 0.125-unit grid step. Defaults configured by the new command: ground check 0.20, step allowance 0.20, slope profile offset 0.45. Existing speed/acceleration/deceleration, slope angle/minimum-normal, input-enable settings, animation parameters, and visual transforms are retained.

All buffers are cached. The positioned Collider2D.Cast overload in installed Unity 6000.6 uses a reusable pre-sized List; it is not created each tick. It can expand once if a query exceeds its initial capacity. Logging creates strings only when explicitly enabled and its rate limit expires. No scene search runs in gameplay ticks.

## Settings before and after running the command

| Setting | Before | After command |
|---|---|---|
| Body type / simulated | Dynamic / true | Dynamic / true |
| Gravity scale / mass | 3 / 1 | 3 / unchanged |
| Constraints | FreezeRotation Z | FreezeRotation Z |
| Interpolation / collision detection | Interpolate / Continuous | Interpolate / Continuous |
| Sleeping | Start Awake | Start Awake |
| Collider trigger | false | false |
| Capsule geometry/offset/root or Visual transforms | Authored prefab and scene overrides | Preserved |
| Capsule material | None | PM_ActorMovement, friction 0, bounce 0, Multiply combines |
| Terrain material/collision/layers | Existing generated polygon / Terrain | Preserved |
| Ground query | 0.12 | 0.20 |
| Step allowance/profile offset | Code defaults 0.20 / 0.45 | Explicit 0.20 / 0.45 |
| Movement speeds | Players 3, Cyclops 2; existing acceleration/deceleration | Preserved |

The setup uses AssetDatabase and PrefabUtility APIs, validates all four prefabs before making changes, creates no duplicate components/materials, keeps prefab connections, and records scene component changes with Undo. Existing scene geometry overrides remain in place. Prefab asset changes are saved by the command; scene saving is always manual. The command requires the active Level 01 scene in Edit Mode.

## Exact Unity test steps

1. Open SCN_Level01_Beach and make it the active scene. Wait for compilation.
2. Run **Attack on Troy > Setup > Fix Character Movement Collision** once. The command may safely be repeated.
3. Check the material at Assets/AttackOnTroy/Physics/Materials/PM_ActorMovement.physicsMaterial2D: friction 0, bounciness 0, Multiply combines. Confirm both Level 01 actors' CapsuleCollider2D reference it and retain their authored size/offset. Confirm bodies are Dynamic, gravity 3 and only rotation is frozen.
4. On Player_Achilles's motor enable **Movement Diagnostics Enabled** if needed; interval defaults to 0.5 seconds. Optional existing Scene gizmos show accepted ground normal/tangent and forward cast. Diagnostics default off.
5. Enter Play Mode manually, let Achilles land, hold D along the beach for several seconds, release, then hold A back through the same path. Repeat a direct D-to-A reversal. Compare Rigidbody2D world position and the log's displacementVelocity with requested/assigned velocities.
6. Verify ordinary slopes and 4-pixel risers allow travel in both directions; a real tall wall/over-limit slope must stop travel and still allow retreat in the opposite direction.
7. Use the existing right-click terrain tester under the actor. Verify gravity-driven falling, then development recovery below the map. Confirm collider geometry and terrain destruction behavior remain unchanged, Cyclops receives no automatic input, and camera follow still works.
8. Exit Play Mode, disable diagnostics when finished, review the scene's changes and save it manually when approved.

## If Achilles still stops

Copy several `[GroundedActorMotor2D]` log entries before and during the stop for BOTH held D and held A. Include:

- input, position, requested, assigned, observedAfterPreviousPhysics, displacementVelocity;
- grounded, blocking, stepAssist, profileWalkable, profileRise;
- hitNormal, hitFraction, hitCollider;
- contacts, minSeparation and effectiveFriction.

`hitFraction=-1` means a contact supplied the normal; it does not mean an accepted overlap cast. No hit is normal zero with collider none. Small negative separation is normal solver contact tolerance; a persistent large negative separation suggests embedding. After material setup, effectiveFriction should be zero for terrain contacts. Nonzero assigned X with near-zero observed/displacement X indicates collision solving cancels motion; blocking=true indicates the motor intentionally blocks. Blocking that remains true for both directions needs the opposing normals and collider names. A reset-sized position discontinuity near the recovery threshold points to fall recovery.

Also report stopped world position, Rigidbody2D constraints/simulated/body type, collider material and current geometry overrides, and the generated terrain collider around that point. A Physics 2D contact visualization can identify a real wall versus a sampled tread. Those live observations remain necessary to establish the exact incident contact and demonstrate the repair on-device; none were performed automatically.

## Compile verification

Runtime and Editor code compiled successfully with the installed Unity 6000.6.0f1 Roslyn compiler and project-generated Unity/package reference response files. The Editor compile uses the newly compiled runtime reference, including the new ConfigureCollisionMovement method. Outputs and a generated compiler response file go to the temporary directory, leaving Unity's loaded project assemblies untouched. The existing hierarchy setup's two obsolete FindObjectsByType/FindObjectsSortMode warnings are unrelated to this change. Compile verification checks C# and installed API compatibility; live physics behavior still requires the manual tests above.
