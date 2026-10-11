# Attack on Troy - Character Select Milestone Report

## 1. Overview
This milestone introduces a dedicated **Character Select Screen** integrated between Level Select and the Level 01 Beach battle. Players can choose between **Achilles** and **Odysseus**, both fully playable with unique combat behaviors while reusing shared battle systems.

---

## 2. Completed Features & Architecture

### A. Navigation & Character Select Flow (`SCN_Home`)
- **Flow**: `Home Screen` -> `Level Select` -> `Level 01: Beach` -> `Character Select Screen` -> `SCN_Level01_Beach`.
- **Character Select UI**:
  - Displays **Achilles** and **Odysseus** cards side by side.
  - Features high-resolution idle character art (`SPR_Achilles_Idle`, `SPR_Odysseus_Idle`).
  - Clear visual selection indicator: Gold border glow (`#FFD733`) and "SELECTED" badge.
  - Buttons: **BACK** (returns to Level Select) and **START BATTLE** (launches `SCN_Level01_Beach`).
  - Greek font styling (`TMP_Greek_SDF`) and consistent dialog panel frame (`SPR_UI_ResultPanel`).
- **Session Persistence (`BattleSessionData.cs`)**:
  - Remembers chosen hero across scene transitions (`PlayerPrefs` + in-memory cache).
  - Handles the **Restart** flow: Upon tapping Restart in the WIN/LOSE battle panel, the game returns directly to the Character Select screen in `SCN_Home` with the previously selected hero pre-highlighted.

### B. Hero Management in Battle (`SCN_Level01_Beach`)
- **`Level01HeroManager.cs` (`[DefaultExecutionOrder(-100)]`)**:
  - At scene startup, activates the selected hero GameObject (`Player_Achilles` or `Player_Odysseus`) and deactivates the unselected hero.
  - Dynamically rebinds all Level 01 scene systems to the active hero:
    - `Level01TurnManager`: Turn transitions and player control locking.
    - `Level01CameraController`: Camera tracking during movement and aiming.
    - `CyclopsNormalTurnController`: AI target tracking and trajectory calculation.
    - `Level01TouchControls`: Movement, angle, power charging, and weapon toggles.
    - `Level01Minimap`: Player blip tracking on the minimap.
    - `Level01BattleUI`: Dynamic HP display, player name tag, and victory/defeat labels.

### C. Odysseus Combat Implementation
- **Arrow Projectile (`PF_Projectile_Odysseus_Arrow.prefab`)**:
  - Uses `SPR_Odysseus_Arrow`, correctly scaled and oriented along the +X launch vector.
  - Configured with `Projectile2D`:
    - `impactType: 0 (Spear / Direct Hit)`.
    - Deals `25` direct hit damage.
    - Embeds into terrain on contact without destructive crater carving.
- **Weapon & Animation Binding**:
  - Connected to Odysseus's `Attack` state in `AC_Odysseus` (normalized release time `~0.55`).
  - Wind influence, launch angle, and hold-to-charge power physics shared with the standard trajectory engine.
  - `Level01TouchControls` disables the Club Bomb button when Odysseus is selected (Odysseus uses his signature bow and arrows).

### D. Battle UI & Results Feedback
- HUD player label dynamically reflects the chosen champion ("ACHILLES" / "ODYSSEUS").
- Defeat message dynamically adapts (e.g., "ODYSSEUS HAS FALLEN!").
- Restart button correctly routes back to Character Select in `SCN_Home`.

---

## 3. Files Created & Modified

### Created Files
- `Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Odysseus_Arrow.prefab` & `.meta`
- `Assets/AttackOnTroy/Scripts/Menus/BattleSessionData.cs` & `.meta`
- `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01HeroManager.cs` & `.meta`
- `Assets/AttackOnTroy/Docs/Reports/character-select-report.md` & `.meta`

### Modified Files
- `Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs`: Character select screen logic, card clicks, selection states.
- `Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity`: Added `CharacterSelectPanel` with Achilles and Odysseus cards.
- `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`: Added `Player_Odysseus` instance and `Level01HeroManager`.
- `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs`: Dynamic player name, hero defeat title, Restart flow.
- `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`: Player reference rebinding support.
- `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs`: Hero rebinding, disabled bomb button for Odysseus.
- `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01Minimap.cs`: Hero transform rebinding support.
- `Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs`: AI target rebinding support.
- `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`: Dynamic weapon display name support.

---

## 4. Manual Play Mode Verification Checklist

### Test Path 1: Achilles Battle & Restart Flow
1. Open `SCN_Home` in Unity Editor and enter Play Mode.
2. Tap **PLAY** -> Tap **LEVEL 01: BEACH**.
3. Confirm the Character Select screen opens with Achilles highlighted by default.
4. Tap **START BATTLE**.
5. Verify `SCN_Level01_Beach` loads with **Achilles** active on the beach.
6. Verify Spear and Bomb selection, movement, aiming, and firing.
7. End battle (win or lose) -> Tap **RESTART** on the result panel.
8. Verify the game returns to the Character Select screen in `SCN_Home` with Achilles highlighted.

### Test Path 2: Odysseus Battle Flow
1. On the Character Select screen, tap the **Odysseus** card.
2. Verify Odysseus card shows the gold border and "SELECTED" badge while Achilles card dims.
3. Tap **START BATTLE**.
4. Verify `SCN_Level01_Beach` loads with **Odysseus** active on the beach (`Player_Achilles` is disabled).
5. Verify HUD shows "ODYSSEUS" at the top-left HP bar.
6. Check touch/aim controls: angle adjustment works, bomb button is hidden/disabled.
7. Charge power and release: verify Odysseus plays bow shoot animation and releases an arrow at ~55% animation time.
8. Verify arrow deals 25 damage on direct hit to Cyclops and embeds into the terrain without carving a crater.
9. Verify Cyclops targets Odysseus accurately and camera tracks Odysseus.

---

## 5. Odysseus Playable Path Fixes & Verification

### Root Cause Analysis
1. **Idle Animation Hyperventilation**:
   - `AC_Odysseus.controller` ran the 4-frame Idle clip at `m_Speed: 1.0` (0.33s per cycle = 3 loops/sec), causing frantic breathing motion compared to Achilles (`m_Speed: 0.4`).
   - *Fix*: Configured Idle state speed to `0.38` (~0.88s cycle) and set smooth hysteresis thresholds (`0.1` enter Run, `0.05` exit to Idle).
2. **Odysseus Stuck / No World Movement**:
   - Root collider in `PF_Odysseus.prefab` had a diminutive CapsuleCollider2D (`size: 0.505 x 1.845, offset: 0, 0.9225`), placing bottom of collider 1.32m above beach terrain. Dropping down with radius `0.25m` caused beach contour step contacts to constantly trip `IsBlockedBySteepTerrain()`.
   - `SCN_Level01_Beach.unity` also had an instance override `maximumStepHeight: 0.8`, failing raised clearance checks.
   - *Fix*: Standardized humanoid CapsuleCollider2D (`size: 1.013 x 3.589, offset: 0, 0.473`) resting directly on ground at local $y = -1.32$, matching Achilles' 0.506m radius, and set `maximumStepHeight: 0.35`.
3. **Arrow Invisible During BowAttack**:
   - `PF_Projectile_Odysseus_Arrow.prefab` had `m_Sprite: {fileID: 7290630705119437457}` from a legacy sliced importer, but `SPR_Odysseus_Arrow.png` is in `spriteMode: 1` (Single). Single sprites in Unity have sub-asset `fileID: 21300000`. The mismatched ID rendered the SpriteRenderer missing/invisible.
   - *Fix*: Re-pointed `m_Sprite` in the arrow prefab to `{fileID: 21300000, guid: 45697f51a3243f14c8bb37ce919cd879, type: 3}`.
4. **Art Size Discrepancy & Visual Scaling**:
   - `Visual` child in `PF_Odysseus.prefab` had a tiny scale `(0.407, 0.407, 1)`, and none of the 5 Odysseus animation clips had transform curves on `Visual`.
   - Odysseus Idle sprite (553px @ 100 PPU) requires scale `0.85` to match Achilles' height (~4.7 units), while Run (387px) requires scale `1.215` and BowAttack (~420px) requires scale `1.18`.
   - *Fix*: Injected position and scale curves into all 5 Odysseus `.anim` clips (`Idle`, `Run`, `BowAttack`, `Hurt`, `Death`) and initialized `PF_Odysseus.prefab` `Visual` to position `(0, 1.21, 0)` and scale `(0.85, 0.85, 1)`.
5. **Aim HUD Tracking Selected Hero**:
   - `ProjectileAimHUD` only observed the initial `Player_Achilles` launcher.
   - *Fix*: Added `SetLauncher` method to `ProjectileAimHUD` and rewired `Level01HeroManager.cs` to rebind `aimHUD` on startup.

### Play Mode Verification Checks
- **Check 1 (Locomotion & Idle Cadence)**: Select Odysseus -> Start Level 01. Observe relaxed, natural breathing Idle. Press A/D or touch joystick left/right: Odysseus smoothly walks across beach terrain steps in both directions; releasing input stops him immediately without sliding or jitter.
- **Check 2 (Scale & Visual Consistency)**: Compare Odysseus height against Achilles (~4.7 units tall, feet planted on beach collider at $y \approx -2.26$). Transition through Idle -> Run -> BowAttack -> Hurt: character height remains stable and feet stay aligned to ground.
- **Check 3 (BowAttack Arrow Release & Turn Advancement)**: Adjust aim angle and charge power bar. Release to fire: at ~55% normalized time of BowAttack, a visible arrow spawns in front at chest/bow level `(0.8, 1.2, 0)`, flies with power/wind/gravity, camera follows it, direct hit on Cyclops deals 25 damage once without creating a crater, and turn advances cleanly to Cyclops.
