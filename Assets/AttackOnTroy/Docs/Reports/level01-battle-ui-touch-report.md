# Level 01 Battle HUD & Android Touch Controls Report

## Overview
This milestone completes the Level 01 battle HUD and Android touch controls for `SCN_Level01_Beach.unity`. It establishes a unified, responsive 1920x1080 landscape HUD layout, integrates imported Greek art assets, provides full touch control capability (hold-to-move, hold-to-aim, weapon selection, hold-to-charge, and release-to-throw), guarantees multi-touch support without sticky state, dims controls during enemy turns and projectile flight, and retains keyboard inputs for Editor development.

---

## 1. UI Layout Architecture (1920x1080 Landscape)

```
+---------------------------------------------------------------------------------------+
| [Achilles HP]                       [TURN TEXT]                       [Cyclops HP]    |
| (Top-Left, 40, -30)             (Top-Center, 0, -30)              (Top-Right, -40, -30)|
|                                                                                       |
|                                                                                       |
|                                 [PLAYFIELD & AIM ARC]                                 |
|                                 (Clean Central View)                                  |
|                                                                                       |
|                                                                                       |
|                                  [AIM / POWER HUD]                                    |
|                              (Lower-Center, 0, 30)                                    |
|                                                                                       |
| [MOVE ◀]  [MOVE ▶]                                      [AIM ▲]   [SPEAR]    (THROW)  |
| (Bottom-Left, 50, 40)                                   [AIM ▼]   [ BOMB]  (-50, 40)  |
+---------------------------------------------------------------------------------------+
```

### Layout Specifications
1. **Achilles HP Bar (`PlayerHealthPanel`)**:
   - **Position**: Top-Left (`anchorMin: (0, 1)`, `anchorMax: (0, 1)`, `pivot: (0, 1)`, `anchoredPosition: (40, -30)`, `sizeDelta: (340, 85)`).
   - **Art**: Framed with `SPR_UI_CharacterFrame_0` (`preserveAspect = true`).
   - **Elements**: Name label ("ACHILLES"), horizontal green fill (`#2ED159`), numeric current/max HP ("100 / 100") using `TMP_Greek_SDF`.
2. **Cyclops HP Bar (`CyclopsHealthPanel`)**:
   - **Position**: Top-Right (`anchorMin: (1, 1)`, `anchorMax: (1, 1)`, `pivot: (1, 1)`, `anchoredPosition: (-40, -30)`, `sizeDelta: (340, 85)`).
   - **Art**: Framed with `SPR_UI_CharacterFrame_0` (`preserveAspect = true`).
   - **Elements**: Name label ("CYCLOPS NORMAL"), horizontal crimson fill (`#EA382E`), numeric current/max HP ("100 / 100") using `TMP_Greek_SDF`.
3. **Turn Text (`TurnText`)**:
   - **Position**: Top-Center (`anchorMin: (0.5, 1)`, `anchorMax: (0.5, 1)`, `pivot: (0.5, 1)`, `anchoredPosition: (0, -30)`, `sizeDelta: (400, 50)`).
   - **Typography**: `TMP_Greek_SDF` bold gold font, displays `PLAYER TURN`, `CYCLOPS TURN`, `VICTORY`, `DEFEAT`.
4. **Aim, Power & Wind HUD (`ProjectileAimHUD`)**:
   - **Position**: Lower-Center (`anchorMin: (0.5, 0)`, `anchorMax: (0.5, 0)`, `pivot: (0.5, 0)`, `anchoredPosition: (0, 30)`, `sizeDelta: (440, 115)`).
   - **Art**: Styled stone tablet relief using `SPR_UI_AimPanel.png_0` (`preserveAspect = true`).
   - **Grouped Elements**:
     - Current elevation angle label (`ANGLE: 45°`).
     - Wind velocity and directional arrow (`WIND: 1.2 ▶`).
     - Charging power bar (`380 x 18`, gold fill `#FFC726`).
     - Selected weapon indicator label (`SPEAR` / `CLUB BOMB`).
5. **Movement Controls (`TouchMovementPanel`)**:
   - **Position**: Bottom-Left (`anchorMin: (0, 0)`, `anchorMax: (0, 0)`, `pivot: (0, 0)`, `anchoredPosition: (50, 40)`, `sizeDelta: (240, 110)`).
   - **Buttons**: Left (`◀`) and Right (`▶`) round buttons (`100 x 100`) using `SPR_UI_ButtonRound_0`.
6. **Action Controls (`TouchActionPanel`)**:
   - **Position**: Bottom-Right (`anchorMin: (1, 0)`, `anchorMax: (1, 0)`, `pivot: (1, 0)`, `anchoredPosition: (-50, 40)`, `sizeDelta: (470, 210)`).
   - **Buttons**:
     - Aim Elevation Up (`▲`) and Down (`▼`) round buttons (`75 x 75`) using `SPR_UI_ButtonRound_0`.
     - Weapon Selection: Spear (`85 x 85`) with `SPR_UI_Icon_Spear_0`, Club Bomb (`85 x 85`) with `SPR_UI_Icon_ClubBomb_1`.
     - Prominent Throw Button: Large round button (`145 x 145`) with `SPR_UI_ButtonRound_0` labeled "THROW" in bold Greek font.
7. **Battle Result (`ResultPanel`)**:
   - Centered on screen (`anchorMin: (0.5, 0.5)`, `anchorMax: (0.5, 0.5)`, `pivot: (0.5, 0.5)`, `sizeDelta: (480, 300)`).
   - Framed with Greek temple asset `SPR_UI_ResultPanel_0`.
   - Clickable Restart button reloads `SCN_Level01_Beach` cleanly.

---

## 2. Touch Controls & Gameplay Integration

### Hold-to-Move
- `PlayerMovementDebugInput.cs` was updated with `SetExternalMoveInput(float value)`.
- Holding `ButtonMoveLeft` supplies `-1.0f`; holding `ButtonMoveRight` supplies `+1.0f`.
- Releasing or dragging pointer off button resets `externalMoveInput` to `0.0f`.
- Keyboard keys (`A`/`D`, arrows) continue to provide fallback input when no touch button is held.

### Hold-to-Aim
- `AchillesProjectileDebugLauncher.cs` was updated with `SetAimInput(float direction)`.
- Holding `ButtonAimUp` increases elevation angle at `angleAdjustmentSpeed` (45°/s) up to `80°`.
- Holding `ButtonAimDown` decreases elevation angle down to `10°`.
- Releasing or dragging off stops aim adjustment immediately.
- Keyboard keys (`W`/`S`) adjust the exact same angle variable.

### Weapon Selection
- Tapping `ButtonWeaponSpear` calls `launcher.TrySelectWeapon(AchillesWeapon.Spear)`.
- Tapping `ButtonWeaponBomb` calls `launcher.TrySelectWeapon(AchillesWeapon.ClubBomb)`.
- Visual Feedback:
  - Active weapon button displays a bright golden selection ring indicator (`SelectionIndicator`) and full 100% brightness.
  - Inactive weapon dims slightly.
  - Buttons become non-interactable outside `PlayerTurn`.
  - Keyboard keys `1` and `2` continue to toggle weapons.

### Hold-to-Charge & Release-to-Throw
- `AchillesProjectileDebugLauncher.cs` exposes:
  - `StartCharging()`: Starts the charge timer if `CanAim` is true.
  - `ReleaseChargeAndThrow()`: Calculates launch velocity matching power charge, triggers `Attack` animation trigger, and releases projectile at `0.45` normalized animation time.
  - `CancelCharging()`: Safely aborts charging without throwing if the touch gesture is cancelled.
- Holding the `THROW` button advances `chargeTimer` over `1.5s` and fills the lower-center power bar.
- Releasing inside the `THROW` button triggers the throw sequence and launches the projectile.
- Sliding the finger off the button cancels the throw cleanly.
- Keyboard `Space` uses the exact same `StartCharging()` and `ReleaseChargeAndThrow()` lifecycle.

### Multi-Touch & Anti-Stuck Safety (`TouchHoldButton.cs`)
- Each button tracks its own `eventData.pointerId`.
- Multi-touch actions (e.g. holding Aim Up while charging Throw) work concurrently without cross-talk.
- Releasing, pointer exit, or disabling the component fires `HoldEnded`/`Cancelled`, resetting held inputs.
- When `IsPlayerTurn` becomes false or a projectile is in flight:
  - The touch controls root `CanvasGroup` dims to `0.35` alpha and disables raycasts.
  - `Level01TouchControls.ClearAllHeldInputs()` releases all active holds immediately.

---

## 3. Files Created & Modified

1. [`Assets/AttackOnTroy/Scripts/Characters/Shared/PlayerMovementDebugInput.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Characters/Shared/PlayerMovementDebugInput.cs)
   - Added `SetExternalMoveInput(float value)` and combined touch + keyboard inputs.
2. [`Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs)
   - Added `SetAimInput(float)`, `StartCharging()`, `ReleaseChargeAndThrow()`, `CancelCharging()`.
   - Wired external aim and touch charging into `Update()` and cleanup in `OnDisable()`.
3. [`Assets/AttackOnTroy/Combat/Scripts/Turns/TouchHoldButton.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/TouchHoldButton.cs) *(New)*
   - UI component handling `IPointerDownHandler`, `IPointerUpHandler`, `IPointerExitHandler` with multi-touch tracking and safe cancellation.
4. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs) *(New)*
   - Touch controls controller managing Move, Aim, Weapon Selection, Throw, CanvasGroup alpha states, and weapon selection highlights.
5. [`Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01BattleUIAndTouchControls.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01BattleUIAndTouchControls.cs) *(New)*
   - Idempotent Editor setup tool under `Attack on Troy > Setup > Configure Level 01 Battle UI and Touch Controls`.

---

## 4. Verification Performed

- **Roslyn Compiler (`csc.dll`) Verification**:
  - Compiled 19 runtime scripts and 17 editor scripts against Unity 6 engine DLLs (`UnityEngine.CoreModule`, `UnityEngine.UIModule`, `UnityEngine.Physics2DModule`, `UnityEngine.AnimationModule`, `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEditor.CoreModule`, `netstandard.dll`).
  - Result: **0 compilation errors**.
- **Idempotence & Lifecycle Inspection**:
  - Checked event unsubscriptions (`UnhookButtonEvents` in `OnDestroy`, `-=` before `+=`).
  - Checked fallback auto-finding in `Level01TouchControls.InitializeReferences()`.
  - Verified no second `HUDCanvas` or duplicate `EventSystem` was created.

---

## 5. Editor Setup & Manual Play Mode Checklist

### Editor Setup Command
- **Menu Path**: `Attack on Troy > Setup > Configure Level 01 Battle UI and Touch Controls`
- **When to Run**: Run in Unity Editor when `SCN_Level01_Beach.unity` is open to wire and lay out the new HUD and touch buttons in the scene hierarchy.
- **Undo Support**: All created and modified scene objects are registered with Undo. The scene is marked dirty without auto-saving.

### Manual Play Mode Checks
1. **Check 1: Landscape Layout & Safe Areas**
   - Open `SCN_Level01_Beach.unity` in 1920x1080 (or 16:9 phone aspect ratio).
   - Verify Achilles HP is at top-left, Cyclops HP is at top-right, Turn text is at top-center.
   - Verify Aim/Power HUD is centered at the bottom of the screen.
   - Verify movement buttons are at bottom-left and action/throw buttons are at bottom-right.
2. **Check 2: Hold-to-Move & Hold-to-Aim**
   - Press and hold `ButtonMoveLeft` / `ButtonMoveRight`: Achilles moves horizontally; release: Achilles stops immediately.
   - Press and hold `ButtonAimUp` / `ButtonAimDown`: Angle smoothly adjusts between 10° and 80°; release: angle locks.
3. **Check 3: Weapon Selection**
   - Tap Spear: Spear button displays bright golden ring indicator; ProjectileAimHUD weapon label displays `SPEAR`.
   - Tap Bomb: Bomb button displays bright golden ring indicator; HUD weapon label displays `CLUB BOMB`.
4. **Check 4: Hold-to-Charge & Release-to-Throw**
   - Press and hold `THROW`: Power bar in Lower-Center HUD charges up over 1.5 seconds.
   - Release `THROW`: Achilles plays Throw animation, launches the selected projectile at the calculated speed, and the camera follows the projectile.
   - While in flight and during `CYCLOPS TURN`, verify all touch buttons visually dim to 0.35 alpha and disable interaction.
5. **Check 5: Multi-Touch & Cancel Gestures**
   - Press and hold `THROW`, then drag your finger outside the button: verify charging cancels without firing.
   - Hold `ButtonAimUp` with one finger while holding `THROW` with another: verify aiming and charging function simultaneously without conflict.
6. **Check 6: Result & Restart**
   - Deplete either Achilles or Cyclops HP to 0.
   - Verify WIN/LOSE panel appears at screen center.
   - Click `RESTART`: verify the scene reloads cleanly with full HP, reset state, and responsive controls.
