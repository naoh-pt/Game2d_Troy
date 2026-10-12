# Level 01 Player Energy & Double-Shot Ability Report

## Summary
Implemented the Player Energy Meter (0–100) and Special **DOUBLE-SHOT** ability for Level 01 (`SCN_Level01_Beach.unity`). The system integrates cleanly across both playable heroes (Achilles and Odysseus), all four weapon types (Spear, Club Bomb, Arrow, Oil Jar), the dual-projectile camera tracking system, and the turn manager cycle.

---

## Architecture & Implementation

### 1. Projectile & Damage Ownership Filtering
- **Projectile Ownership ([`Projectile2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs))**:
  - Added `OwnerCollider` and `Owner` properties to `Projectile2D`.
  - Stored during `Launch()` and passed to `FirePatch2D.Initialize(Owner)` as well as `ActorDamageReceiver2D.ApplyDamageSafely(damage, Owner)`.
- **Fire Patch Ownership ([`FirePatch2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Damage/FirePatch2D.cs))**:
  - Tracks `owner` GameObject and passes it into `ApplyDamage(damage, owner)`.
- **Damage Filtering in Energy System**:
  - Subscribes to `playerDamageReceiver.DamageApplied(damage, attacker)`.
  - Filters out self-damage (when `attacker == player`), healing (`damage <= 0`), fire DoT ticks, blocked damage, and post-death damage (`IsAlive == false`).
  - Only enemy attacks (Cyclops Stone Axe) inflicting actual HP loss grant energy: **1 hit = 50 SP, 2 hits = 100 SP (sẵn sàng)**.

### 2. Player Energy System Component ([`PlayerEnergySystem.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/PlayerEnergySystem.cs))
- Manages `currentEnergy` (0 / 50 / 100) and `isSpecialArmed`.
- Full reset on battle start or restart.
- Arming logic:
  - `CanArmSpecial`: true only when `currentEnergy >= 100`, player is alive, and it is currently `PlayerTurn`.
  - `ToggleSpecial()`: arms double-shot if ready; if already armed, cancels double-shot without consuming energy.
  - `ConsumeSpecialOnLaunch()`: clears `isSpecialArmed = false` and resets `currentEnergy = 0` only when the attack is actually released.
  - Weapon switching does not cancel or consume energy.
  - Hotkey support: `E` / `Q` toggles special arming in editor/keyboard mode; `X` immediately charges 100 energy for testing.

### 3. Double-Shot Launch & Turn Completion Latch ([`AchillesProjectileDebugLauncher.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs))
- **Launch Request**:
  - `RequestThrow()` latches `storedDoubleShot = isDoubleShotArmed`.
  - Calls `energySystem.ConsumeSpecialOnLaunch()`.
- **Simultaneous Spawn (`SpawnDoubleProjectiles`)**:
  - Calculates symmetric launch angles around selected aim direction: `clampedAngle - doubleShotSpreadAngle` (-3°) and `clampedAngle + doubleShotSpreadAngle` (+3°).
  - Instantiates two projectiles at the hero's release socket with the exact same power and wind.
  - Calls `Physics2D.IgnoreCollision(colA, colB, true)` so the two projectiles never collide with each other on frame 0.
  - Tracks both in `activeProjectiles` list and subscribes `OnProjectileCompleted` to each.
  - Animation (`Attack` / `BowAttack`) plays only once.
- **Turn Latch**:
  - When projectile A impacts/times out, `activeProjectiles.Remove(pA)`.
  - If `activeProjectiles.Count > 0`, it switches camera focus to the remaining projectile and **returns immediately**. It does NOT advance turn, does NOT unlock input, and does NOT regenerate wind.
  - When projectile B impacts, `activeProjectiles.Count == 0`: triggers `turnManager.GenerateNextWind()` and invokes `ProjectileCompleted` exactly once.
  - **Battle Over Guard**: If Cyclops dies on the first impact, `turnManager.IsBattleOver` immediately halts turn handoff and displays the victory flow.

### 4. Midpoint Camera Tracking ([`Level01CameraController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Camera/Level01CameraController.cs))
- `FollowProjectiles(pA, pB)`: tracks midpoint `(posA + posB) * 0.5f` in `LateUpdate`.
- If one projectile finishes or is destroyed, automatically falls back to tracking the single remaining projectile.
- Returns to default hero/target framing after both finish.
- Position is strictly clamped inside `LevelBounds`.

### 5. UI & Touch Controls Integration
- **Energy Meter UI ([`Level01BattleUI.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs))**:
  - Placed under `PlayerHealthPanel` directly beneath the HP bar (`EnergyBar`).
  - Cyan fill bar (`playerEnergyFill`) and text label (`playerEnergyText`) display `SP 100%` or `SP {energy:0}%`.
- **Special Button ([`Level01TouchControls.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs))**:
  - Placed above the weapon buttons in `RightControls` (`ButtonSpecial`, anchored `[-267, 230]`).
  - States:
    - **Charging (`energy < 100`)**: Dimmed slate background, disabled button, displays `SP {energy:0}%`.
    - **Ready (`energy == 100`)**: Glowing cyan background (`#00E5FF`), button interactable, displays `SPECIAL`.
    - **Armed (`isArmed == true`)**: Glowing gold background (`#FFD700`), displays `CANCEL` with pulsing indicator.
  - Hero manager rebinds the energy system whenever switching between Achilles and Odysseus.

---

## Changed Files

1. [`Assets/AttackOnTroy/Combat/Scripts/Turns/PlayerEnergySystem.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/PlayerEnergySystem.cs) *(New)*:
   - Energy accumulator, damage filtering, arm/cancel state machine, and debug keys.
2. [`Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs):
   - Added `OwnerCollider` and `Owner` properties for damage attribution.
3. [`Assets/AttackOnTroy/Combat/Scripts/Damage/FirePatch2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Damage/FirePatch2D.cs):
   - Stored and passed `owner` to `ApplyDamage` to prevent self-damage energy gain.
4. [`Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs):
   - Added `doubleShotSpreadAngle`, `SpawnDoubleProjectiles`, mutual collision ignore, and multi-projectile completion latch.
5. [`Assets/AttackOnTroy/Scripts/Camera/Level01CameraController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Camera/Level01CameraController.cs):
   - Added `FollowProjectiles(pA, pB)` midpoint tracking and bounds clamping.
6. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs):
   - Added `playerEnergyFill`, `playerEnergyText`, and `SetEnergySystem`.
7. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs):
   - Added `ButtonSpecial` wiring, CANCEL toggle, and visual state styling.
8. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01HeroManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01HeroManager.cs):
   - Rebinds `PlayerEnergySystem` to the active hero (Achilles / Odysseus).
9. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs):
   - Handles energy reset and disarming on battle end.
10. [`Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity):
    - Added `PlayerEnergySystem` to `TurnManager`.
    - Added `EnergyBar` under `PlayerHealthPanel`.
    - Added `ButtonSpecial` under `RightControls`.
    - Connected all references into `Level01BattleUI`, `Level01TouchControls`, `Level01HeroManager`, Achilles launcher, and Odysseus launcher.

---

## Verification & Compilation

1. **Compilation**:
   - Verified via Roslyn compiler: **0 runtime errors, 0 editor errors**.
2. **Scene YAML Integrity**:
   - Verified all 18 new YAML objects and cross-component references in `SCN_Level01_Beach.unity`.
3. **Logic Flow Check**:
   - Verified energy accumulation, clamping, arm/cancel flow, mutual projectile collision avoidance, and two-projectile completion latch (turn advances and wind generates exactly once when the last projectile finishes).

---

## Play Mode Checklist

1. **Energy Charge & Arming**:
   - In Play Mode as Achilles or Odysseus, press `X` (or let Cyclops hit you) to charge energy to 100.
   - Observe `EnergyBar` fill and `ButtonSpecial` turn cyan labeled `SPECIAL`.
   - Click `SPECIAL` (or press `E`/`Q`): button turns gold and displays `CANCEL`. Click again to cancel without losing energy.
2. **Double-Shot Execution & Camera Tracking**:
   - Arm Special and release an attack with Spear, Club Bomb, Arrow, or Oil Jar.
   - Confirm two projectiles launch simultaneously with a ±3° spread.
   - Confirm the camera tracks the midpoint between the two projectiles, seamlessly switching to the surviving projectile if one lands earlier.
3. **Turn Progression & Advance Once**:
   - Confirm when the first projectile hits, turn does **not** end and input stays locked.
   - Confirm after both projectiles finish, wind is rolled once and Cyclops turn begins cleanly (or WIN screen triggers immediately if Cyclops was defeated).
