# Achilles weapons and impact separation

## Changes

Implemented; **setup has not been run, Play Mode was not entered, and the scene was not saved**. Existing spear-art/prefab, Greek-font, scene and turn-prototype worktree changes were preserved. No old builder was invoked.

Modified:

- `Combat/Scripts/Projectiles/Projectile2D.cs`: shared flight with explicit Spear/ClubBomb impact modes and guarded resolution/completion.
- `Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`: PlayerTurn-only weapon selection, snapshot of the selected prefab at throw start, and once-only completion/wind/camera handoff. Existing throw-release timing, aiming, charging and turn integration remain.
- `Combat/Scripts/Projectiles/ProjectileAimHUD.cs`: updates only the assigned weapon label's text when selection changes.

Created (paths relative to `Assets/AttackOnTroy/`):

- `Combat/Scripts/Damage/IDamageable2D.cs` and `ActorDamageReceiver2D.cs`.
- `Combat/Scripts/Projectiles/ProjectileExplosionEffect.cs`.
- `Editor/Setup/ConfigureAchillesWeapons.cs`.
- This report and new-script Unity `.meta` files.

No existing damage/health API was found. The new receiver has 100 HP, damage notifications and optional damage logging; it resets on runtime initialization. It does not implement death, change movement/physics/animation, or add health UI. Existing/future root components implementing `IDamageable2D` are reused rather than supplemented with duplicate receivers.

## Behavior and defaults

| Weapon | Impact behavior | Inspector defaults |
|---|---|---|
| Spear | Damageable actor: direct damage once, then completion/despawn immediately. Terrain: stop physics, align the authored sprite tip to the contact, remain visible, then complete/despawn. Other solid props: end without damage/crater. | Damage 25; embedded hold 0.8 s; tip penetration 0.08 units |
| Club Bomb | Any solid impact: stop physics, hide club visual, damage each nearby receiver once, carve only `DestructibleTerrain2D`, play a separate effect, then complete after resolution. | Damage 30; damage radius 2; crater radius 1; resolution 0.45 s; visual spin 360 degrees/s |

Spear code never reaches `DestroyCircle`: the sole projectile destruction call is inside the bomb-only impact method. Merely assigning a terrain reference or crater radius to the spear cannot enable terrain destruction.

Bomb damage uses one overlap query/snapshot per explosion and deduplicates actors across multiple colliders. The radius reaches an actor when its collider's closest point lies inside the radius; damage layers are configurable. The owner is ignored for direct flight collision, as before, but **can receive bomb self-damage** inside the blast. There is no distance falloff or wall/line-of-sight shielding in this prototype. Terrain targets are deduplicated; a configured terrain outside the crater has no pixels removed. Runtime terrain collision/artwork algorithms are unchanged.

Flight retains the existing wind acceleration, gravity, bounds allowance and 10-second lifetime. Spear root/capsule orientation remains trajectory-aligned. Bomb root is spawned unrotated with frozen Rigidbody2D rotation; only its `Visual` child spins. The sprite is centered inside that spin pivot, so differing sprite pivots cannot cause orbital movement. The new bomb uses a fixed 0.65 x 0.65 capsule and approximately 1.4-unit visual length; spin never modifies that collider or velocity. All new values can be tuned in prefab Inspector.

Impact is latched **before** damage, collider rebuilds or effect spawning. Flight wind, rotation, bounds and lifetime processing stop during embedding/explosion resolution. Completion marks itself done and clears its callback before invoking it; deferred destruction/disable cannot fire it twice. Timeout/out-of-bounds complete immediately without explosion or crater. External disable/cancel also closes a launched shot once.

The launcher guards the entire completion handoff: one turn event, one next-wind generation, and one camera-return call per shot. Terrain completions keep the existing `NotifyProjectileEnded(player)` impact hold; actor/other hits and timeout/out-of-bounds keep `FocusActor(player)`. Normal turn-manager resolution/Cyclops/next-player camera transitions remain unchanged. Completion happens after the spear hold or bomb resolution, not when impact first begins.

## Weapon selection and presentation

- **1 / numpad 1:** Spear; **2 / numpad 2:** Club Bomb. Uses New Input System 1.20.0, not legacy input.
- Selection requires the existing turn manager's exact `PlayerTurn` state and no throw/active projectile. It stays locked throughout embedding/explosion as well as flight. Selection while charging is allowed; throw start snapshots the selected prefab, angle, power, wind and facing.
- Both weapons use the current Attack/Throw animation and existing normalized release point. No clip/controller is rebuilt. Spear remains the initial/default weapon; setup does not force a new selection.
- `TrySelectWeapon(AchillesWeapon)` returns whether selection succeeded; `SelectedWeapon`, `SelectedWeaponName`, and `CanSelectWeapon` support future UI.
- Keyboard handling remains under the existing launcher Editor/Development-build debug-input policy. Mobile buttons are not implemented.
- The existing HUD gets one `WeaponText` child, cloned from its current Angle label so the exact font/material/color/typography are inherited. Only the **new** label is placed just below the existing panel; existing panel/labels/styles/layout are untouched. An already-assigned weapon label is reused without restyling.

The club sheet currently has one large artwork slice (`SPR_Achilles_ClubBomb_1`, 2134 x 657 source pixels) plus three tiny fragment slices. Setup chooses the large sprite, not the misleading 8 x 6 `_0` slice; no importer is changed.

An explosion sheet is already imported, with seven custom slices. Its default effect sequence follows inspected artwork layout: `_6, _0, _1, _2, _33, _3, _4` (small flash, expansion, dispersal). Slice names do not reflect spatial order. Existing sprite GUIDs/pivots/slicing are left intact; the separate effect recenters each frame visually. If art/effect references are absent, an expanding orange/yellow runtime ring provides a visible fallback. Replace `Explosion Effect Prefab` on the bomb or edit the effect's serialized Frames array later. No damage/collision code lives in the effect.

## Manual setup

1. Let Unity 6000.6.0f1 finish importing/compiling. Open active `SCN_Level01_Beach` in Edit Mode with its existing launcher, turn manager and aim HUD correctly linked.
2. Run **Attack on Troy > Setup > Configure Achilles Weapons** only. Do **not** rerun the old projectile, turn, character or HUD builders.
3. The command patches only the existing spear prefab's impact-mode field (preserving its sprite, Visual transform, material and collider), and creates/saves:
   - `Combat/Prefabs/Projectiles/PF_Projectile_Achilles_ClubBomb.prefab`.
   - `Combat/Prefabs/Effects/PF_Effect_ClubBombExplosion.prefab`.
4. It assigns bomb/turn references to the current launcher, adds/reuses the styled weapon label, and adds a damage receiver to the two current actor scene roots if none exists. Scene changes support Undo; prefab asset writes require source-control/manual rollback. It marks the scene dirty **without saving it**. Review and save the scene yourself if desired.
5. Repeating setup preserves existing bomb/effect tuning and HUD styling and does not duplicate components/labels/prefabs. Existing prefab GUIDs are retained. Scene-instance overrides/connections are preserved. No texture/font asset, camera, actor prefab, spawn point or terrain setting is changed by this setup.

## Test checklist

1. During PlayerTurn, press 1/2: verify HUD says SPEAR/CLUB BOMB, with the same Greek font/style. Try both keys during PlayerThrowing, ProjectileFlight, embedding/explosion, CyclopsTurn and transition: selection must not change.
2. W/S aim, Space charge/release: confirm the selected prefab appears only at the existing Throw release point. Wind stays locked through the shot and changes once on completion. Check both facing directions.
3. Spear into terrain: it stops, visibly embeds for 0.8 s, then disappears. Compare terrain before/after: **no crater**, including repeated shots. Check sloped/vertical contacts for tip alignment with the customized spear sprite.
4. Spear into Cyclops: enable the receiver's **Log Damage** temporarily; expect exactly one 25-point deduction (100 -> 75), no terrain change and one completion. Extra colliders/contacts must not produce extra damage.
5. Bomb in flight: Visual spins smoothly; physics-root rotation remains zero and collider shape/trajectory do not spin. Bomb impact: club hides, separate explosion appears, nearby actors take one 30-point deduction each, terrain gets one configured crater, then turn/camera completion follows resolution.
6. Test multiple actor colliders, overlapping terrain collider paths, self-damage, and explosions beside/above terrain. LevelBounds and TerrainInterior must remain untouched/visual-only as applicable. Damage does not introduce death/game-over behavior at zero HP.
7. Force Timeout on each weapon (e.g. short lifetime with high shot), and shoot outside LevelBounds: no crater/explosion, one completion, camera returns, and exactly one subsequent turn sequence. Test rapid multi-contact impacts and cancellation/disable as well.
8. Rerun only the new setup in Edit Mode: confirm one label/receiver per actor and unchanged spear sprite/Visual/collider, Greek font and old HUD properties. Existing new bomb/effect tuning must survive.

All runtime and Editor scripts compile against the installed Unity 6000.6.0f1 assemblies and installed packages (exit 0); only existing obsolete scene-search warnings remain in older setup tools. Compilation writes only to the OS temporary directory. No Play Mode/collision simulation was performed. Manual checks remain for spear tip/transparent-padding alignment, bomb/effect visibility and scale, imported effect-frame ordering, new-label placement on your screen resolution, and actual once-only turn/camera/contact behavior.
