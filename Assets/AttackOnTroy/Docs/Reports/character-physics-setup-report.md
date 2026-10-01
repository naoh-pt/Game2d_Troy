# Character Physics Setup Report

## Result and files

Implemented reusable Dynamic Rigidbody2D ground movement, development keyboard input for player prefabs, development fall recovery, and an idempotent prefab setup command.

Files created:

- `Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs`
- `Assets/AttackOnTroy/Scripts/Characters/Shared/PlayerMovementDebugInput.cs`
- `Assets/AttackOnTroy/Scripts/Characters/Shared/ActorFallRecovery2D.cs`
- `Assets/AttackOnTroy/Editor/Setup/ConfigureCharacterPhysics.cs`
- `Assets/AttackOnTroy/Docs/Reports/character-physics-setup-report.md`

Unity metadata was added for the new folders and files. No prefab asset or scene was directly edited during implementation, and the setup command was not run.

When the command is run it updates these existing prefab assets through `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`:

- `PF_Achilles`
- `PF_Odysseus`
- `PF_CyclopsNormal`
- `PF_CyclopsHeavy`

No PhysicsMaterial2D was created. The prototype uses the existing collider materials and Physics2D response; a shared material can be introduced later only if device testing exposes slope sticking or unwanted bounce.

## Inspection findings

All four prefabs have the same structural pattern: a unit-scale root with one Kinematic Rigidbody2D and one non-trigger CapsuleCollider2D, plus a direct `Visual` child containing one SpriteRenderer and Animator. Visual child scales differ by character and are intentionally preserved. All four assigned Animator Controllers contain a float parameter named `Speed`; player controllers also contain Attack/Hurt/Die triggers, and Cyclops controllers contain Throw/Die triggers. This phase writes only `Speed`.

Current prefab Rigidbody2D values before setup are Kinematic, gravity scale 0, interpolation None, Discrete collision detection, Start Awake, and Freeze Rotation already enabled. Level 01 instances remain connected prefab instances. Their scene overrides are limited to names, spawn transforms, collider sizes/offsets, and Visual transforms; notably, the Level 01 Achilles and Cyclops have substantial collider/Visual overrides. The setup command does not touch or apply/revert any of those overrides.

## Rigidbody2D settings applied by the command

Each prefab root is configured as:

- Body Type: `Dynamic`
- Gravity Scale: `3`
- Constraints: `Freeze Rotation` (Z rotation)
- Interpolation: `Interpolate`
- Collision Detection: `Continuous`
- Sleeping Mode: `Start Awake`
- Existing mass and damping retained
- Root scale retained at `(1, 1, 1)`
- Root CapsuleCollider2D remains enabled and non-trigger
- Collider size, offset, direction, density, and material are preserved

Unity 6000.6 exposes `Rigidbody2D.linearVelocityX`, allowing the motor to change horizontal velocity without overwriting gravity-driven vertical velocity. Relevant Unity 6 references: [Rigidbody2D](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D.html), [linearVelocityX](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D-linearVelocityX.html), [Collider2D.Cast](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider2D.Cast.html), and [ContactFilter2D.SetLayerMask](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ContactFilter2D.SetLayerMask.html).

## Ground detection

`GroundedActorMotor2D` caches its Rigidbody2D, root collider, Visual SpriteRenderer, Animator, Terrain-only `ContactFilter2D`, a 12-element contact array, and a 12-element cast-hit array. It performs no scene search and creates no deliberate garbage in `Update` or `FixedUpdate`.

Each physics step it:

1. Reads existing Terrain-layer contact points from the actor's actual Collider2D.
2. If no valid contact exists, shape-casts that same collider downward by the configurable ground-check distance.
3. Accepts only non-trigger Terrain-layer surfaces whose normal meets both the minimum normal-Y and maximum slope-angle limits.

This avoids a fixed ray origin that would break when collider offsets/sizes vary. Wall normals have low Y and cannot count as ground. `TerrainInterior` cannot appear in results because it has no Collider2D.

Ground defaults:

- Terrain mask: layer 6 (`Terrain`)
- Ground-check distance: `0.12` world units
- Maximum walkable slope: `45°`
- Minimum ground normal Y: `0.5`

The effective required normal Y is the stricter of `minimumGroundNormalY` and `cos(maximumWalkableSlopeAngle)`; at defaults it is approximately `0.707`.

## Movement and slope handling

Public API:

- `SetMoveInput(float input)` clamps normalized input to `[-1, 1]`.
- `SetMovementEnabled(bool enabled)` clears input when disabled and lets deceleration bring horizontal speed safely toward zero without disabling gravity.
- `IsGrounded` exposes the last physics-step result.
- `FacingDirection` returns `-1` or `1`.

`FixedUpdate` changes only `Rigidbody2D.linearVelocityX` using `Mathf.MoveTowards`; vertical velocity remains owned by gravity and collision response. Acceleration is used with active input and deceleration with zero/disabled input. A short collider cast in the requested horizontal direction rejects movement into Terrain contacts whose normal is steeper than the walkable threshold. Walkable slopes remain available to the normal physics solver.

Facing changes only `Visual/SpriteRenderer.flipX`; no Transform scale is changed. The Animator `Speed` float is the magnitude of actual horizontal Rigidbody2D velocity, not requested input.

Movement defaults applied only when a motor is first added:

| Character group | Max speed | Acceleration | Deceleration |
| --- | ---: | ---: | ---: |
| Achilles / Odysseus | 3 | 18 | 24 |
| Cyclops Normal / Heavy | 2 | 12 | 18 |

Re-running the command repairs references, layer masks, and mandatory Rigidbody settings but preserves movement/slope tuning on an existing motor.

## Debug input

`PlayerMovementDebugInput` is added only to Achilles and Odysseus prefabs. It uses `Keyboard.current` from the New Input System:

- A or Left Arrow: `-1`
- D or Right Arrow: `+1`
- Both or neither: `0`

It sends input only through `GroundedActorMotor2D.SetMoveInput`. The Inspector toggle defaults enabled. In non-Editor players, it immediately clears input unless `Debug.isDebugBuild` is true, so release Android builds cannot drive characters through this tester. No mobile controls were added. Cyclops prefabs receive no input or AI component and therefore fall/stand but do not move automatically.

## Fall recovery

`ActorFallRecovery2D` records the Rigidbody2D spawn position/rotation in `Awake`. In Editor and Development builds only, falling below its configured world-Y threshold restores the pose through Rigidbody2D properties, clears linear/angular velocity, and wakes the body.

Public replacement hooks are `RecoverNow`, `RecordSpawnPose`, `SetRecoveryEnabled`, and `SetRecoveryThreshold`.

When Level 01 is active, the setup command derives the initial threshold as `LevelBounds.bounds.min.y - 2`. With the inspected LevelBounds this is approximately `-11.9`. If Level 01 is not active or the bounds cannot be resolved, the safe fallback is `-12`. Existing customized thresholds are preserved on repeat setup runs.

## Runtime terrain/layer verification

- The `Terrain` physics layer exists at layer 6.
- `ENV_Beach_Terrain` is on Terrain.
- `DestructibleTerrain2D.CreateColliderContainer` explicitly initializes `RuntimeTerrainColliders.layer` from `gameObject.layer`, so its runtime PolygonCollider2D inherits Terrain correctly.
- No terrain algorithm change was required or made.
- `TerrainInterior` is on Terrain but contains only Transform + SpriteRenderer and remains visual-only.
- `LevelBounds` remains a Default-layer trigger BoxCollider2D and is excluded by the Terrain-only, non-trigger ground filter.
- The current Physics2D collision matrix permits Default actors to collide with Terrain.

## Setup command

Menu path: **Attack on Troy > Setup > Configure Character Physics**

The command:

1. Requires the existing Terrain layer.
2. Preflights all four prefab paths, root Rigidbody2D/collider uniqueness, direct Visual child, SpriteRenderer, Animator, Speed float, and duplicate runtime components before making changes.
3. Opens each prefab with PrefabUtility, updates mandatory Rigidbody2D settings, and keeps collider/Visual data intact.
4. Adds or reuses one motor and fall-recovery component.
5. Adds or reuses player debug input only for Achilles/Odysseus.
6. Saves the four prefab assets through PrefabUtility/AssetDatabase.
7. Counts connected instances in the active scene. If Level 01 is active and instances are present, it marks the scene dirty but does not save it.

Because the Level 01 actors are connected instances and do not override the new components or Rigidbody properties, they inherit the prefab changes while retaining their existing collider, Visual, name, and spawn overrides. The command does not unpack or replace instances and is safe to run repeatedly. There are no direct scene-instance property mutations requiring a separate Undo operation; prefab asset writes themselves are performed through PrefabUtility and should be reviewed in version control before committing.

## Exact Unity steps

1. Open the project in Unity 6000.6 and wait for compilation.
2. Open and activate `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` so the recovery threshold can be derived from LevelBounds.
3. Confirm the Console has no compile errors.
4. Run **Attack on Troy > Setup > Configure Character Physics**.
5. Wait for prefab imports to finish. Do not enter Play Mode during the import.
6. Inspect each of the four prefab assets and confirm the Rigidbody2D settings listed above.
7. Confirm one motor and one fall-recovery component on every root; confirm debug input only on player roots.
8. Inspect Player_Achilles and Enemy_CyclopsNormal in Level 01. Confirm their prefab connections and existing collider/Visual overrides remain intact.
9. Confirm TerrainInterior still has no Collider2D and RuntimeTerrainColliders is Terrain-layer during Play Mode.
10. Save the scene manually only after reviewing inherited prefab changes and the dirty scene state.

## Test checklist

- Enter Play Mode and confirm Achilles and Cyclops fall onto the generated terrain, remain upright, and do not collide with TerrainInterior visuals.
- With Achilles selected/followed, hold A/D and Left/Right Arrow; verify acceleration, maximum speed, deceleration, camera following, and no Transform-position movement script.
- Verify the Animator Speed value follows actual horizontal speed and Idle transitions to Run/Walk and back.
- Verify only the Visual SpriteRenderer flipX changes and root/Visual scale stays positive and unchanged.
- Walk Achilles across shallow and moderate slopes; confirm ground state remains true and the capsule does not treat a near-vertical wall as ground.
- Push against a slope steeper than 45°; confirm uphill motion is blocked reasonably without suppressing gravity.
- Confirm Cyclops falls and stands but stays horizontally idle because no AI/debug input is present.
- Right-click to destroy terrain beneath each actor; confirm they fall naturally when collider support disappears.
- Drop an actor below approximately Y `-11.9`; confirm it returns to its recorded spawn pose with zero velocity in Editor/Development builds.
- Make a non-Development Android build and confirm keyboard input/fall recovery are inactive.
- Confirm camera target switching/parallax behavior remains unchanged while following moving/falling actors.

## Known limitations and manual verification

- The command was not run, so prefab propagation, exact LevelBounds-derived recovery threshold, runtime contacts, animations, and camera behavior require Unity Play Mode verification.
- Level 01 currently overrides Achilles and Cyclops collider sizes/offsets and Visual positions/scales substantially relative to their prefab defaults. These are preserved as requested, but their fit against the rendered feet must be reviewed before judging grounding or slope behavior.
- Horizontal control remains available while airborne; no separate air-control multiplier was requested.
- This is velocity-based walking, not a full character controller. It has no stepping, jumping, ledge snapping, moving-platform compensation, or custom friction model.
- The steep-surface cast is a practical blocker, not a full predictive slope solver. Capsule shape, terrain contour resolution, fixed timestep, and material friction can affect edge behavior.
- The motor uses a fixed preallocated capacity of 12 ground contacts/hits. Highly pathological overlapping collider arrangements could truncate query results; the current single terrain polygon should not.
- Continuous collision detection improves fast contact reliability but does not replace device profiling.
- Fall recovery is intentionally development-only and has no fade, invulnerability, death, or game-over flow.
- A dynamic body may initially overlap terrain if a scene spawn/collider override begins below the alpha contour. Confirm both Level 01 spawn poses with Physics 2D gizmos.
