# Achilles locomotion presentation

## Delivery and scope

Runtime animation stabilization is implemented. Achilles visual correction and controller thresholds are implemented in an idempotent Editor setup window, **not applied yet**.

Files changed/created for this task:

- Modified `Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs`.
- Created `Assets/AttackOnTroy/Editor/Setup/StabilizeAchillesLocomotionPresentation.cs` and its Unity-generated `.meta`.
- Created this report and its `.meta`.

No setup command was run, no Play Mode was entered, and no scene was edited or saved by this task. Existing worktree changes to prefabs, scene, physics setup and sorting settings predate this task and were preserved.

When you apply the new setup, it updates these existing assets in place, preserving GUIDs:

- `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/AC_Achilles.controller`.
- `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/ANIM_Achilles_Idle.anim`.
- `ANIM_Achilles_Run.anim`, `ANIM_Achilles_Throw.anim`, `ANIM_Achilles_Hurt.anim`, `ANIM_Achilles_Death.anim` in that same animation directory.

The command saves only these animation/controller assets through `AssetDatabase.SaveAssetIfDirty`. It never saves, marks dirty, or changes the scene. No prefab/component is added or changed by this command.

## Flicker diagnosis

The motor previously wrote `Animator.Speed = Abs(body.linearVelocityX)` **after assigning the upcoming physics tick's velocity**. That number was a pre-solver command, not completed movement. Against a steep surface, the blocker/contact decision and collision solver could alternate between permitting a horizontal command and stopping it. The parameter therefore alternated even when actual travel was negligible.

The current controller compounds this: Idle enters Run at `Speed > 0.01`; Run exits at `Speed < 0.010001`. There is effectively no dead band, and both transitions blend over 0.05 seconds with no exit time. Input works; it is not the animation movement measurement. The exact per-tick contact sequence was not captured in Play Mode, but this incorrect pre-solver parameter source and nearly identical controller thresholds are confirmed in the current source/assets.

`PlayerMovementDebugInput` only calls `SetMoveInput`; it does not write Speed. The project runtime Speed writes are in the shared motor. Animator parameters remain `Speed` (float), `Attack`, `Hurt`, `Die` (triggers). No new parameter is needed.

### Correction

At the beginning of FixedUpdate, before any new velocity assignment, the motor measures:

`completedTravelVelocity = (body.position - previousPhysicsPosition) / fixedDeltaTime`

Animation uses the absolute **actual horizontal displacement speed**, not input, requested velocity, or newly assigned velocity. The existing observed post-physics velocity remains available in movement diagnostics but is not the animation source.

Inspector defaults:

| Setting | Value | Meaning |
|---|---:|---|
| Run Enter Speed | 0.15 world units/s | Enter Run immediately when measured travel reaches this value |
| Run Exit Speed | 0.05 world units/s | Treat travel at/below this as stationary |
| Idle Grace Duration | 0.08 seconds | Return to Idle after sustained stationary travel |

Above the exit threshold while already running, the stationary timer resets. Between thresholds the current state is retained. A latched Run writes `max(measuredSpeed, runEnterSpeed)` during its short exit grace; Idle writes exactly zero. This prevents controller transitions from responding to near-zero solver noise. Entering Run has no grace timer: the only measurement latency is a completed physics tick, plus the existing blend. At a stopped wall, Speed settles to zero after approximately four 0.02-second stationary ticks.

Large position discontinuities (greater than `max(0.5, maximumSpeed * fixedDeltaTime * 4)` units between samples) clear locomotion so fall-recovery teleports do not cause a spurious Run. Disable also clears the sample and latch.

The setup changes only these controller conditions:

- Idle -> Run: `Speed > 0.10` (previously 0.01).
- Run -> Idle: `Speed < 0.05` (previously 0.010001).

It preserves transition durations, interruption settings, state speeds, clip frame rate/order, trigger transitions and Write Defaults. In particular, the authored Idle state speed **0.4** remains untouched. If tuning motor animation thresholds later, keep its Run-enter speed above the controller's 0.10 entry threshold.

No ground detection, tangent, step assistance, velocity assignment, wall/slope limits, friction, gravity or collision code was changed. Facing still uses positive Visual scale and `SpriteRenderer.flipX`; the animation latch never resets facing when Idle begins. Existing motor facing behavior while blocked is preserved.

Diagnostics, still optional/rate-limited/off by default, now also report `measuredHorizontalSpeed`, `animationSpeed`, `running`, and `idleTimer`.

## Visual binding and alpha audit

The Achilles Animator and SpriteRenderer both reside directly on the prefab's **child Visual**, not on the Rigidbody2D root. Apply Root Motion is disabled. All five current Achilles clips have a SpriteRenderer `m_Sprite` object-reference curve at binding path `""`; they have no position/scale curves. Thus a Transform curve at path `""` in these clips binds to **Visual**, not to the physical actor root. Adding curves at `"Visual"` would incorrectly target a grandchild in this hierarchy.

Prefab-authored Visual scale is `(0.477707, 0.477707, 1)` and local position `(0, 0, 0)`. The current Level 01 Visual overrides are scale `(1, 1, 1)` and position `(-0.1, 0.7, 0)`. The window defaults to that inspected Level 01 baseline and offers an explicit read-baseline button with Animation preview stopped.

Both sprite sheets still use 100 PPU and center-aligned slices. Idle's serialized unused custom-pivot field differs from Run's, but `alignment: 0` selects Center; the setup uses Unity's actual `Sprite.pivot`, not that unused serialized custom-pivot field. Source sizes are Idle 1587 x 522 and Run 1983 x 374. No PNG/importer/PPU is changed.

The supplied previous audit was Idle **201 x 476.5** and Run **198.5 x 302.5**, giving multipliers X **1.012594**, Y **1.575207**. A fresh scan of the current original PNGs and current slice rectangles at alpha >=128 gives the following. These height values do **not** reproduce the previous audit; current measurements are reported separately rather than silently using old lower bounds.

| Frame | Slice px | Opaque px | Lower opaque Y relative to centered pivot, at 100 PPU |
|---|---|---|---:|
| Idle 0 | 225 x 471 | 188 x 464 | -2.315 |
| Idle 1 | 235 x 475 | 202 x 466 | -2.305 |
| Idle 2 | 209 x 492 | 191 x 486 | -2.410 |
| Idle 3 | 209 x 492 | 200 x 484 | -2.410 |
| Idle 4 | 220 x 494 | 209 x 482 | -2.410 |
| Idle 5 | 212 x 493 | 202 x 489 | -2.435 |
| Run 0 | 207 x 321 | 199 x 302 | -1.575 |
| Run 2 | 214 x 324 | 210 x 317 | -1.580 |
| Run 3 | 219 x 324 | 212 x 317 | -1.580 |
| Run 4 | 255 x 324 | 250 x 316 | -1.580 |
| Run 5 | 202 x 328 | 196 x 322 | -1.610 |
| Run 6 | 176 x 330 | 169 x 323 | -1.610 |
| Run 7 | 204 x 326 | 198 x 321 | -1.600 |
| Run 8 | 184 x 339 | 178 x 333 | -1.665 |

Current medians: Idle **201 x 483 px**, Run **198.5 x 319 px**. The current height-match ratio would be approximately **1.514107**, not 1.575207. The difference remains an artwork/slice-height issue, not a PPU or existing transform-scale animation issue. Opaque bounds include equipment/cape, not only anatomical body height; a running crouch should not necessarily have exactly the same full bounding-box height as standing Idle.

### Selected correction

- Run X multiplier: **201 / 198.5 = 1.01259446**.
- Run Y multiplier: **1.45**, a conservative correction below both full-height ratios.
- With Level 01 baseline, absolute Run Visual scale is **(1.01259446, 1.45, 1)**.
- Idle, Throw, Hurt and Death explicitly restore baseline scale **(1, 1, 1)** and position **(-0.1, 0.7, 0)**.

At current measured bounds, corrected median Run height is **4.6255** world units vs Idle **4.83**, about **4.23% shorter**. Using the older audit figures it would remain about 7.95% shorter. The conservative Y choice retains some crouched-run height difference and reduces the risk of the full 1.575 stretch elongating the head/body. This is a tunable presentation compromise, not anatomical re-authoring; final appearance still needs visual review.

### Foot-line compensation

For each Run sprite the command calculates lower opaque Y from original PNG alpha and the actual sprite rect/pivot/PPU. It reads temporary PNG copies in Editor memory and does not require or change texture Read/Write settings.

`runVisualY = baselinePosition.y + baselineScale.y * (medianIdleLowerY - runYMultiplier * currentRunLowerY)`

Median Idle lower Y is currently **-2.410**. At the default baseline the reference line is **0.7 - 2.410 = -1.710** relative to the actor root. This preserves the existing Idle visual line; it does not relocate the character or adjust the collider to that line.

Resulting Run local Y values, in existing sprite-key order:

| Sprite | Run local Y | Offset from baseline 0.7 |
|---|---:|---:|
| Run 0 | 0.57375 | -0.12625 |
| Run 2 | 0.58100 | -0.11900 |
| Run 3 | 0.58100 | -0.11900 |
| Run 4 | 0.58100 | -0.11900 |
| Run 5 | 0.62450 | -0.07550 |
| Run 6 | 0.62450 | -0.07550 |
| Run 7 | 0.61000 | -0.09000 |
| Run 8 | 0.70425 | +0.00425 |

Y keys use constant/stepped tangents synchronized with existing sprite switch times. Run X/Z positions remain baseline. Run scale is constant, positive and absolute, never multiplied into the current transform on each update. Sprite flipX does not affect lower Y or invert transform scale. All five clips receive the same six explicit position/scale bindings; repeated Apply replaces those bindings instead of duplicating them. Existing sprite curves, GUIDs, frame order, sample rates, loop settings and root-motion settings are preserved.

**Shared-clip caveat:** transform animation values are absolute, not relative to each prefab instance's overrides. These shared Achilles clips will impose the selected Level 01 baseline on every Achilles instance that uses them (including legacy controllers referencing them). The smaller authored Achilles prefab Visual transform is not edited, but animated playback/preview will use the new clip baseline. Odysseus/Cyclops clips/controllers are not edited. If differently sized Achilles instances are needed later, use a separate unanimated presentation parent or separate clip variants; that hierarchy/asset expansion is not introduced here.

## Setup and Unity testing

1. Open `SCN_Level01_Beach` as the active scene in Unity 6000.6.0f1. Allow scripts to compile. Do not rerun broad character animation-building tools, which may regenerate these clips/controllers.
2. Stay in Edit Mode and stop any Animation window preview/recording.
3. Choose **Attack on Troy > Setup > Stabilize Achilles Locomotion Presentation**.
4. In the window, verify baseline `(1,1,1)` / `(-0.1,0.7,0)`, Run X `1.012594`, Run Y `1.45`. If your actual Visual baseline has changed, use **Read baseline from Level 01 Visual** with preview stopped and review the values.
5. Click **Apply Achilles presentation repair**. This saves the affected animation/controller assets, supports Undo, and leaves scene/prefab data alone. Validation rejects wrong scene, Play Mode, animation preview, unexpected Animator location/controller, missing sprite bindings, duplicate/unexpected locomotion transitions, and invalid scales before asset mutation.
6. Inspect controller Idle->Run and Run->Idle conditions. Idle state speed must remain 0.4. Check Attack/Hurt/Die transitions remain unchanged.
7. Select Achilles Visual and preview Idle/Run in the Animation window. Check Scale/Position bindings address the selected Visual only. Scrub every Run sprite and inspect the foot line, body proportions and both flipX orientations. Stop preview when done.
8. Enter Play Mode manually when ready. Verify A/D and arrows enter Run promptly during actual travel. Hold D against the same real steep wall: world X must remain blocked, Run should settle into Idle without alternating. Reverse away with A: movement/Run should resume normally. Repeat left-facing wall tests.
9. Release input, compare Idle/Run size and feet, test slopes/falling/destruction/recovery, and confirm gravity and real-wall behavior remain intact. Brief collision noise should not repeatedly toggle locomotion.
10. Verify Rigidbody2D, CapsuleCollider2D size/offset/material, root scale and terrain/camera are unchanged. Existing Level 01 root scale is `(1,1,1)`; Achilles scene capsule is approximately size `(1.0127312,3.8777845)`, offset `(-0.32364267,0.32875144)` and remains untouched.

If wall flicker remains, enable the motor's optional Movement Diagnostics temporarily and report `displacementVelocity.x`, `measuredHorizontalSpeed`, `animationSpeed`, `running`, `idleTimer`, `blocking`, requested/assigned/observed velocity, and Animator active transition/state. Distinguish genuine horizontal oscillation from a parameter-only transition problem. Diagnostics are rate-limited and should be disabled after testing.

## Tuning without Play Mode

Stop Animation preview, reopen the setup window, keep the baseline fixed, change Run X/Y multipliers and Apply. Then preview/scrub Idle and Run again in Edit Mode. The command recomputes every foot offset for the selected Y multiplier; do not hand-adjust scale alone and leave old foot offsets. Reapplying the same values is idempotent and cannot accumulate scale. Full Y 1.575 is available to try, but inspect head/body distortion before accepting. Never capture the baseline from a sampled Run frame. Undo is available; save affected assets again after Undo if reverting saved changes.

## Compile validation and remaining verification

All runtime scripts and all Editor scripts, including the new window, were compiled with Unity 6000.6.0f1's installed Roslyn/.NET SDK and the project's generated Bee response-file assembly/package references. Both compilations exited **0**. Runtime produced no warnings. Editor produced only two pre-existing obsolete `FindObjectsByType`/`FindObjectsSortMode` warnings in `Level01BeachHierarchySetup.cs`; no errors or warnings from changed/new scripts.

Compiler outputs/response files were written only to the OS temporary directory `AttackOnTroy.LocomotionCompile`; Unity Editor/Play Mode was not launched for testing. The already-running Editor may automatically refresh source/meta files.

Manual verification remains necessary for the runtime contact sequence, perceived proportions, transient foot alignment during Animator crossfades, the alpha-bound assumption that the lowest opaque pixel is a foot rather than equipment, and appearances of other Achilles instances sharing the clips. No claim of a Play Mode visual/physics test is made.
