# Level 01 Dialogue System Implementation Report

## Summary
Added a lightweight, extensible fantasy dialogue system to `SCN_Level01_Beach.unity` under the existing `HUDCanvas`. Dialogue lines are driven by a serialized `LevelDialogueData` ScriptableObject supporting branching based on selected player (Achilles vs. Odysseus) and enemy IDs (extensible for multi-enemy levels such as Level 03). Battle input is gated cleanly during dialogue, mid-battle banter plays exactly once before the enemy's first throw without repeating or causing duplicate throws, and victory dialogue transitions gracefully into the result screen and restart flow.

---

## Architecture & Implementation

### 1. Data Layer (`DialogueLine.cs` & `LevelDialogueData.cs`)
- **`DialogueLine`**: Serializable struct containing `speakerName`, `text` (multi-line textarea), and `speakerColor`.
- **`EnemyDialogueEntry`**: Data structure associating an `enemyId` (e.g. `"CyclopsNormal"`) with pre-attack dialogue lines. Allows later levels (such as Level 03 with multiple enemies) to route lines cleanly by enemy ID without hardcoding single enemy objects.
- **`LevelDialogueData` (ScriptableObject)**: Per-level asset containing:
  - `achillesOpeningLines`: Opening exchange for Achilles vs Cyclops (3 lines).
  - `odysseusOpeningLines`: Opening exchange for Odysseus vs Cyclops (3 lines).
  - `enemyEntries`: Pre-first-throw banter for Cyclops Normal (1 line).
  - `achillesVictoryLines`: Victorious quote for Achilles upon Cyclops defeat (1 line).
  - `odysseusVictoryLines`: Victorious quote for Odysseus upon Cyclops defeat (1 line).
  - Public query methods: `GetOpeningLines(heroName)`, `GetEnemyPreAttackLines(enemyId)`, `GetVictoryLines(heroName)`.
- **Asset**: `Assets/AttackOnTroy/Dialogue/Data/SO_Dialogue_Level01_Beach.asset`.

### 2. UI Layer (`DialoguePanelUI.cs`)
- **Placement**: Under `HUDCanvas` (`AnchorMin = (0.5, 0)`, `AnchorMax = (0.5, 0)`, `Pivot = (0.5, 0)`, `AnchoredPosition = (0, 35)`, `SizeDelta = (820, 160)`).
- **Layout & Readability**: Centered in landscape between bottom-left movement controls and bottom-right weapon/throw controls (leaving >200px gap on each side) and well below top HP bars and minimap.
- **Components**:
  - Dark translucent slate background (`Color(0.06f, 0.07f, 0.10f, 0.93f)`) with Greek gold top accent border (`Color(0.85f, 0.70f, 0.25f, 0.85f)`).
  - `SpeakerNameText`: 24pt bold `TMP_Greek_SDF`, tinted with speaker's signature color (Gold for Achilles, Cyan for Odysseus, Crimson for Cyclops).
  - `BodyText`: 20pt `TMP_Greek_SDF` with word wrapping enabled.
  - `NextButton`: Greek-styled button displaying "Next >" (or "Close" on final line) to advance line by line.
  - `SkipButton`: Compact button displaying "Skip >>" to immediately conclude the current dialogue sequence.
- **State Handling**: Disabled (`m_IsActive: 0`) at scene load, activated only during active dialogue sequences.

### 3. Turn & Battle Flow Integration (`Level01TurnManager.cs`)
- **Dynamic Camera Tracking per Speaker**:
  - `DialoguePanelUI` invokes an `onLineDisplayed` callback on each line progression.
  - If the line speaker is an enemy (`Cyclops`), `Level01TurnManager` calls `cameraController.FocusActor(cyclopsTransform)`, smoothly panning the camera over to Cyclops.
  - If the speaker is the active hero (`Achilles` or `Odysseus`), camera smoothly pans back to the player.
  - On Next/Skip exit, camera returns cleanly to the active player.
- **Per-Turn Cyclops Taunts Before Throwing**:
  - After each player throw resolves (projectile impact/completion), the battle enters `CyclopsPreparation`.
  - Camera focuses on Cyclops. Once preparation delay completes, Cyclops speaks a taunt line before attacking.
  - Supported via `GetEnemyTurnTauntLines(enemyId, enemyTurnCount)` with a pool of varied Greek fantasy taunts in `SO_Dialogue_Level01_Beach.asset` that cycle across turns.
  - Cyclops only begins his throw animation (`cyclopsController.BeginTurn()`) after the player clicks Next/Close or Skip.
- **Victory Dialogue**:
  - On lethal hit on Cyclops (`EndBattle(true)`), camera focuses on Cyclops and plays death animation.
  - After `battleResultDelay` (1.0s), displays the active hero's victory line (camera focuses on the hero).
  - When dismissed via Next or Skip, shows the VICTORY result panel with the Restart button.
  - Defeat (`isWin == false`) skips victory dialogue and shows the DEFEAT result panel directly.
  - Restart flow (`BattleSessionData.ReturnToCharacterSelect = true; SceneManager.LoadScene(0)`) remains intact and unblocked.

### 4. Enemy Integration (`CyclopsNormalTurnController.cs`)
- Added `[SerializeField] private string enemyId = "CyclopsNormal"` and public property `EnemyId` for dialogue system identification.

---

## Changed & Created Files
1. `Assets/AttackOnTroy/Dialogue/Scripts/DialogueLine.cs` & `.meta` (New struct)
2. `Assets/AttackOnTroy/Dialogue/Scripts/LevelDialogueData.cs` & `.meta` (New ScriptableObject data class)
3. `Assets/AttackOnTroy/Dialogue/Scripts/DialoguePanelUI.cs` & `.meta` (New UI controller)
4. `Assets/AttackOnTroy/Dialogue/Data/SO_Dialogue_Level01_Beach.asset` & `.meta` (New Level 01 dialogue asset)
5. `Assets/AttackOnTroy/Dialogue.meta`, `Dialogue/Scripts.meta`, `Dialogue/Data.meta` (Directory meta files)
6. `Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs` (Added `enemyId` property)
7. `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs` (Integrated dialogue gating, pre-attack trigger, victory sequence)
8. `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` (Added `DialoguePanel` to `HUDCanvas`, wired `dialogueData` and `dialogueUI` on `Level01TurnManager`)

---

## Play Mode Verification Checks
1. **Opening Dialogue & Input Lock Check**:
   - Start battle as Achilles (or Odysseus).
   - Verify `DialoguePanel` appears at bottom-center with speaker name in gold/cyan and Greek TMP font.
   - Verify movement (`A`/`D`) and touch controls are completely disabled while dialogue is active.
   - Click "Next >" to step through all 3 lines, or click "Skip >>" to dismiss immediately.
   - Verify `PLAYER TURN` appears, player controls unlock, and aiming/movement functions normally.
2. **Cyclops First Throw Banter Check**:
   - Fire a projectile at Cyclops to complete Player Turn 1.
   - During `CYCLOPS TURN`, verify Cyclops speaks his pre-throw banter line once.
   - Click "Next >" or "Skip >>". Verify Cyclops begins his throw animation immediately, launches exactly one stone axe, and repositions.
   - In subsequent turns (Turn 2, 3+), verify Cyclops does NOT repeat the pre-throw dialogue and begins his throw normally.
3. **Victory Dialogue & Restart Check**:
   - Reduce Cyclops HP to 0.
   - Confirm Cyclops death animation plays.
   - Verify active hero's victory dialogue line is displayed.
   - Click "Close" / "Next" or "Skip" on the victory dialogue.
   - Verify the VICTORY result panel appears and clicking Restart returns to Character Select with the previous hero selected.
