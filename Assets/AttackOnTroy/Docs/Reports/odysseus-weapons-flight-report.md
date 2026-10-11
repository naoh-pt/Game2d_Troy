# Odysseus Weapons and Flight Model Implementation Report

## Summary
Completed Odysseus' dual weapon arsenal (Slot 1: Arrow, Slot 2: Oil Jar) and corrected the straight-line Arrow flight physics and curved Oil Jar trajectory in Level 01 Beach battle.

## Implemented Features

### 1. Arrow (Slot 1)
- **Flight Model**: Straight-line flight along the selected aim angle at constant velocity (`gravityScale = 0`, `horizontalWind = 0`). Charging power scales release speed; W/S and touch aim controls steer direction.
- **Orientation & Anti-Tunneling**: Arrow tip continuously aligns with flight direction; continuous `Physics2D.Raycast` sweep in `FixedUpdate` eliminates tunneling at high speeds.
- **Impact & Turn Completion**: Direct hit inflicts single-instance damage (25 HP) to Cyclops; terrain hit embeds the tip into ground without crater deformation. Turn completes cleanly once after each shot.
- **Aim Guide**: Draws a straight directional line originating from Odysseus' actual arrow release spawn point.

### 2. Oil Jar (Slot 2)
- **Flight Model**: Arced ballistic flight affected by standard gravity (`gravityScale = 1.0`) and prepared wind. Physics root rotation is locked (`FreezeRotationZ`) while its visual sprite rolls end-over-end at 360°/s.
- **Aim Guide**: Previews an approximate curved parabolic trajectory incorporating launch velocity, gravity, and wind.
- **Impact & Ground Fire**: Shatters on impact with actor or terrain, spawning `PF_FirePatch` snapped to ground. The jar completes immediately to advance the turn without terrain destruction.
- **Fire Patch Mechanics**: Persists at world position for 4 seconds across turns. Animated with 11-frame looping flipbook VFX (`SPR_Odysseus_JarFire`). Deals 8 damage per 0.5s interval to all actors within a 1.6m radius (including Odysseus if inside, deduped per actor). Lethal fire ticks trigger the existing WIN/LOSE flow.
- **Timeout/Out-of-Bounds**: Skips fire patch instantiation if expired or outside boundary collider.

### 3. UI and Controls Integration
- **Touch Controls**: Replaced hardcoded weapon state with dynamic hero weapon binding. Touch weapon buttons show Arrow and Ceramic Jar icons when Odysseus is active, and Spear and Club Bomb icons when Achilles is active.
- **Aim HUD**: Weapon label dynamically displays `ARROW` vs. `OIL JAR` (or `SPEAR` vs. `CLUB BOMB`).
- **Hero Manager**: Auto-rebinds `AimGuideRenderer` and `Level01TouchControls` to the active hero instance upon battle start.

---

## Changed Files
1. `Assets/AttackOnTroy/Combat/Scripts/Damage/FirePatch2D.cs` & `.meta` (New runtime ground fire script)
2. `Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs` (Straight arrow flight, raycast sweep, Oil Jar visual spin & fire patch spawn)
3. `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs` (Exposed `SpawnPoint` & `SelectedProjectilePrefab`, updated weapon selection and naming for Arrow & Oil Jar)
4. `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AimGuideRenderer.cs` (Curved trajectory for Oil Jar, straight line for Arrow/Spear)
5. `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs` (Dynamic button icons for Slot 1 and Slot 2 based on active hero)
6. `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01HeroManager.cs` (Rebinding `AimGuideRenderer` to active hero)
7. `Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Odysseus_Arrow.prefab` (Updated `impactType: 3` Arrow)
8. `Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Odysseus_OilJar.prefab` & `.meta` (New Oil Jar projectile prefab)
9. `Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_FirePatch.prefab` & `.meta` (New Fire Patch VFX & damage prefab)
10. `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` (Wired `clubBombPrefab` on Odysseus, icon fields on Touch Controls, and `aimGuide` on Hero Manager)

### 4. Aim Guide Decoupling & Oil Jar Collider Fix
- **Aim Guide Visibility**: Previously, `AimGuideRenderer` was attached to `Player_Achilles`'s spawn point. Selecting Odysseus deactivated Achilles, inadvertently deactivating the Aim Guide. It has now been decoupled into a dedicated, persistent scene GameObject parented to `Level01HeroManager`. `AimGuideRenderer` also auto-detects and binds to whichever hero is active, uses `Sprites/Default` material with order 10, and renders a bold golden line.
- **Oil Jar Flight Fix**: `Projectile2D.InitializeComponents` previously hardcoded `GetComponent<CapsuleCollider2D>()`. Because the Oil Jar uses `CircleCollider2D`, `projectileCollider` was null, which caused `Physics2D.IgnoreCollision` to be skipped. Consequently, the jar collided with Odysseus on frame 0 and threw a `MissingComponentException`. Updated `Projectile2D` to use `GetComponent<Collider2D>()`, added null checks in `StopFlight()`, and added `ignoredColliders` check in `OnCollisionEnter2D`, allowing the jar to launch cleanly and fly in its parabolic arc.

---

## Short Play Mode Checks
1. **Odysseus Aim Guide & Arrow Check**:
   - Start from Character Select, pick Odysseus, load Level 01 Beach.
   - Confirm the golden aim guide line is immediately visible pointing from Odysseus. Adjust angle with `W`/`S` (or touch Up/Down) and observe the line rotating up and down.
   - Fire an arrow and verify it flies straight along that line.
2. **Odysseus Oil Jar Flight Check**:
   - Press `2` (or touch the Oil Jar button).
   - Observe the aim guide preview the curved trajectory.
   - Fire the oil jar; confirm the jar flies forward through the air in an arc without hitting Odysseus, spins during flight, shatters on impact, and spawns the burning fire patch.
3. **Achilles Regression Check**:
   - Switch back to Achilles.
   - Confirm aim guide is visible and works for Spear and Bomb as expected.
