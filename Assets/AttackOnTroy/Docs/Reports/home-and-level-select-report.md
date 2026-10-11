# Home & Level Select Navigation Report

## 1. Overview & Objective
This milestone establishes the initial frontend navigation flow for **Attack on Troy**:
- Creates a dedicated **Home scene** (`SCN_Home.unity`) under `Assets/AttackOnTroy/Scenes/Menus/`.
- Implements a responsive landscape UI styled with the Greek-themed font (`TMP_Greek_SDF`), Greek parchment/dialog frames (`SPR_UI_ResultPanel_0`), and beach sky background (`BG_Beach_Sky`).
- Provides **Play** and **Exit** actions on the Home view.
- Provides a **Level Select** panel displaying **Level 01 Beach** (unlocked and playable), while **Level 02 Scamander** and **Level 03 Troy Wall** are visibly locked.
- Provides a **Back** button to return from Level Select to Home.
- Routes Level 01 selection directly to `SCN_Level01_Beach`, starting the battle unchanged with all existing HUD, minimap, touch controls, and Restart logic intact.
- Prepares an extensible navigation structure for inserting a **Character Select** screen in the next milestone (Odysseus is not exposed as playable yet).
- Registers scenes in `EditorBuildSettings.asset` with `SCN_Home` as Scene 0 and `SCN_Level01_Beach` as Scene 1.

---

## 2. Navigation Architecture (`HomeMenuController`)

The navigation logic is encapsulated in [`HomeMenuController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs):

### State Machine & Panel Transitions
```csharp
public enum MenuScreenState
{
    Home,
    LevelSelect,
    CharacterSelect // Structured hook for next milestone
}
```
- **Home State**:
  - Displays game title *"ATTACK ON TROY"* and subtitle *"THE SIEGE OF ILION"*.
  - `PlayButton` triggers `ShowLevelSelectScreen()`.
  - `ExitButton` calls `Application.Quit()` (and stops Editor Play Mode in editor builds).
- **Level Select State**:
  - Displays dialog frame with header *"SELECT BATTLEFIELD"*.
  - Three battlefield cards:
    - **Stage 01: Beach of Troy** (`level01Button`): interactable, status `[ BATTLE ]`.
    - **Stage 02: Scamander** (`level02Button`): non-interactable (`interactable = false`), status `[ LOCKED ]`.
    - **Stage 03: Walls of Troy** (`level03Button`): non-interactable (`interactable = false`), status `[ LOCKED ]`.
  - `BackButton` returns to the Home view.
- **Milestone Extensibility (Character Select Hook)**:
  - `SelectLevel(int levelIndex)` inspects `enableCharacterSelect` (default `false`) and `characterSelectPanel`.
  - When enabled in the upcoming character select milestone, selecting Level 01 routes to `ShowCharacterSelectScreen()` before starting battle.
  - While disabled (current milestone), selecting Level 01 directly executes `LaunchBattle()`, loading `SCN_Level01_Beach`.
  - Odysseus is not displayed or selected as playable.

---

## 3. Scene Layout & Visual Design (`SCN_Home.unity`)

The scene was constructed directly with Unity 6000 YAML serialization:
- **Reference Resolution**: 1920x1080 landscape (`CanvasScaler` match width/height 0.5).
- **Camera**: Orthographic Camera with deep Aegean blue background (`#0F1A28`), URP additional camera data, and AudioListener.
- **EventSystem**: Configured with Unity's Input System UI module (`InputSystemUIInputModule`) for both mouse and touch input.
- **Background**: Full-screen image using `BG_Beach_Sky` (`fileID: 5719156151568855086, guid: 06431e8980818c14eb5546212d8d5c03`).
- **Typography**: TextMeshProUGUI using `TMP_Greek_SDF` font (`guid: 27aca764d8836a94c8b16091aec4ebad`) with classical Greek gold (`#FFEA88`) and bronze (`#D4B26F`) palette.
- **Panels & Framing**: Dialogue box utilizing `SPR_UI_ResultPanel_0` (`guid: 098ffb9f7d3b0e84b8b36ad360ab838e`).

---

## 4. Build Settings Integration

`ProjectSettings/EditorBuildSettings.asset` updated to establish the active game launch order:
1. `Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity` (Build Index 0, Enabled)
2. `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` (Build Index 1, Enabled)

> [!NOTE]
> All Level 01 battle components, scene overrides, minimap, HP bars, touch controls, and Restart logic (`SceneManager.LoadScene(activeScene.buildIndex)`) remain completely intact and functional when loaded from Home.

---

## 5. Changed & Created Files

| File | Status | Description |
| :--- | :--- | :--- |
| [`HomeMenuController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs) | **Created** | Navigation controller managing Home, Level Select, Back, Exit, and extensible Character Select routing. |
| [`SCN_Home.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity) | **Created** | Complete Home and Level Select menu scene with Canvas, Greek fonts, art assets, and EventSystem. |
| [`SCN_Home.unity.meta`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity.meta) | **Created** | Asset meta file with persistent GUID `446a961100434dd2b3d3d6b26f315552`. |
| [`EditorBuildSettings.asset`](file:///d:/nam5/semester_1/game_android/game_2d/ProjectSettings/EditorBuildSettings.asset) | **Updated** | Added `SCN_Home` as Scene 0 and `SCN_Level01_Beach` as Scene 1. |

---

## 6. Manual Play Mode Verification

### Play Mode Check 1: Home → Level Select Navigation & Back/Exit
1. Open [`SCN_Home.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity) in the Unity Editor and press **Play**.
2. **Verify Home Screen**:
   - Game Title *"ATTACK ON TROY"* and subtitle *"THE SIEGE OF ILION"* render centered in Greek gold/bronze font.
   - Buttons **PLAY** and **EXIT** are visible and interactable.
3. **Verify Transition to Level Select**:
   - Click **PLAY**.
   - Home panel hides and the **Level Select** dialogue opens with header *"SELECT BATTLEFIELD"*.
   - **Stage 01 (Beach of Troy)** is displayed in vivid colors with `[ BATTLE ]`.
   - **Stage 02 (Scamander)** and **Stage 03 (Walls of Troy)** are dimmed with `[ LOCKED ]` badges; clicking them does not start battle.
4. **Verify Back & Exit**:
   - Click **BACK** at the bottom of the Level Select dialog -> Dialogue closes and Home panel reappears.
   - Click **EXIT** -> Editor stops Play Mode (verifying quit handler).

### Play Mode Check 2: Level 01 Selection & Battle Start
1. In [`SCN_Home.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity), click **PLAY**.
2. Click **LEVEL 01: BEACH OF TROY** (`[ BATTLE ]`).
3. **Verify Scene Load**:
   - `SCN_Level01_Beach` loads immediately.
   - Achilles and Cyclops Normal are spawned in their correct initial positions.
   - HUD Canvas displays Achilles HP (100/100), Cyclops HP (100/100), wind indicator, angle/power HUD, and minimap.
   - Touch controls / keyboard controls operate normally.
4. **Verify Battle & Restart**:
   - Complete a turn or play through battle resolution (Victory or Defeat).
   - Click the **Restart** button on the result panel -> Level 01 reloads smoothly with full health, fresh wind, and battle loop functioning.
