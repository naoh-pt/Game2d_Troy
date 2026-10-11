# Level 01 Pause System & Result Panel Navigation Report

## Summary
Hoàn thiện chức năng **Pause** và hệ thống điều hướng trên giao diện **Result Panel (Victory / Defeat)** trong màn chơi Level 01 Beach (`SCN_Level01_Beach.unity`). Toàn bộ tính năng tái sử dụng triệt để kiến trúc UI, scene `SCN_Home`, session data (`BattleSessionData`), sprite chủ đề Hy Lạp (`SPR_UI_ResultPanel`, `SPR_UI_ButtonRound`) và phông chữ TMP Hy Lạp hiện có.

---

## Architecture & Implementation

### 1. Nút Pause trên HUD ([`ButtonPause`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity))
- **Vị trí**: Đặt tại mép trên HUD (`AnchorMin: (0, 1)`, `AnchoredPosition: (485, -45)`, kích thước `64x64`), ngay bên phải thanh máu người chơi (`PlayerHealthPanel`).
- **Không chồng lấn**:
  - Cách thanh máu người chơi hơn 30px về bên phải.
  - Cách Minimap hơn 370px về bên trái.
  - Cách xa vùng điều khiển cảm ứng (Movement ở góc dưới trái, Aim/Power/Weapons ở góc dưới phải) và nhãn lượt `TurnText` (`Y: -176`).
- **Thao tác**: Dễ bấm bằng chuột trên Editor và cảm ứng trên thiết bị di động. Hỗ trợ phím tắt bàn phím `Escape` hoặc `P` để bật/tắt nhanh Pause.

### 2. Panel Pause ([`PausePanel`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity))
- **Chắn tương tác toàn màn hình**: Nền đen mờ (`Color: (0, 0, 0, 0.65)`) với `RaycastTarget: 1` che toàn bộ màn hình, ngăn hoàn toàn mọi thao tác chạm/click xuyên qua các nút chiến đấu hay phím di chuyển phía dưới.
- **Khung Greek Panel**: Khung tranh `DialogFrame` dùng chung asset `SPR_UI_ResultPanel.png` với phông chữ Hy Lạp `TMP_Greek_SDF`.
- **Dừng toàn bộ trạng thái Gameplay**:
  - `Time.timeScale = 0f`: Vật lý Rigidbody2D, đường đạn `Projectile2D`, trọng lực, gió, hoạt ảnh, AI Cyclops và các bộ đếm thời gian lượt/trận dừng tuyệt đối.
  - Ngay khi Pause, các bộ điều khiển [`PlayerMovementDebugInput.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Characters/Shared/PlayerMovementDebugInput.cs) và [`Level01TouchControls.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs) xóa toàn bộ input đang giữ (`ClearInput()`, `ClearAllHeldInputs()`).
  - [`AchillesProjectileDebugLauncher.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs) chặn mọi lệnh ném hay căn góc khi `Time.timeScale <= 0`.
- **Ba nút chức năng trong Pause Panel**:
  1. **Tiếp tục (`CONTINUE`)**: Khôi phục `Time.timeScale = 1f`, đóng panel Pause. Trận đấu tiếp diễn trơn tru từ đúng trạng thái trước khi dừng (kể cả khi đạn đang bay giữa không trung, bảo toàn chính xác vận tốc và góc xoay).
  2. **Chơi lại (`RESTART`)**: Khôi phục `Time.timeScale = 1f`, tải lại ngay scene Level 01 bằng luồng `SceneManager.LoadScene(activeScene)`, giữ nguyên lựa chọn tướng hiện tại trong `BattleSessionData.SelectedHero`.
  3. **Về Home (`HOME`)**: Khôi phục `Time.timeScale = 1f` trước khi chuyển scene, tải scene `SCN_Home` (scene index 0), đưa người chơi về màn hình chính mà không bị đứng trận sau.
- **Chống mở đè lên màn kết thúc trận**: Phương thức `PauseGame()` kiểm tra chặt chẽ `if (isBattleOver || resultPanel.activeSelf) return;`. Đồng thời khi Victory/Defeat xuất hiện, nút Pause tự động ẩn đi.

### 3. Mở rộng Result Panel (Victory / Defeat)
Theo định hướng tái sử dụng và nâng cấp bảng kết quả trận đấu:
- **`NEXT LEVEL` (Màn tiếp theo)**:
  - Chỉ hiển thị khi **VICTORY** (chiến thắng Cyclops).
  - Khôi phục `Time.timeScale = 1f`, đặt cờ `BattleSessionData.ReturnToLevelSelect = true` và tải `SCN_Home` mở sẵn màn hình Chọn Level (`LevelSelectPanel`).
- **`RESTART` (Chơi lại)**: Tải lại trận đấu Level 01 ngay lập tức với tướng đang chơi.
- **`HOME` (Về Home)**: Khôi phục thời gian và đưa người chơi về màn hình Home chính.

---

## Changed Files

1. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs):
   - Thêm các trường tham chiếu nút Pause, panel Pause, các nút điều hướng (Tiếp tục, Chơi lại, Về Home, Màn tiếp).
   - Thêm phương thức `TogglePause()`, `PauseGame()`, `ResumeGame()`, `RestartCurrentBattle()`, `ReturnToHome()`, `GoToNextLevel()`.
   - Bắt phím nóng bàn phím `Escape`/`P`.
2. [`Assets/AttackOnTroy/Scripts/Characters/Shared/PlayerMovementDebugInput.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Characters/Shared/PlayerMovementDebugInput.cs):
   - Bỏ qua và xóa input bàn phím khi `Time.timeScale <= 0f`.
3. [`Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs):
   - Chặn căn góc và lệnh ném `RequestThrow` khi `Time.timeScale <= 0f`.
4. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TouchControls.cs):
   - Hủy bỏ toàn bộ cảm ứng giữ phím khi `Time.timeScale <= 0f`.
5. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs):
   - Dừng cập nhật bộ đếm trạng thái lượt khi `Time.timeScale <= 0f`.
6. [`Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs):
   - Dừng hành vi AI Cyclops khi `Time.timeScale <= 0f`.
7. [`Assets/AttackOnTroy/Scripts/Menus/BattleSessionData.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/BattleSessionData.cs):
   - Thêm cờ `ReturnToLevelSelect` để điều hướng quay lại Level Select.
8. [`Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs):
   - Mở màn hình Chọn Level khi `BattleSessionData.ReturnToLevelSelect` được kích hoạt.
9. [`Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity):
   - Thêm `ButtonPause` trên HUD, thêm cây phân cấp `PausePanel`, gắn thêm `NextLevelButton` và `HomeButton` trên `ResultPanel`.
   - Kết nối đầy đủ các tham chiếu vào `Level01BattleUI`.
10. [`Assets/AttackOnTroy/Docs/Reports/project-scope-update.md`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Docs/Reports/project-scope-update.md):
    - Tài liệu cập nhật phạm vi: bỏ hình nộm Odysseus (Odysseus playable hero) và chuẩn hóa đòn đặc biệt từ ba thành hai vật thể (Double-Shot).

---

## Verification & Compilation

- **Biên dịch**: Roslyn compiler xác nhận **0 runtime errors, 0 editor errors**.
- **Cấu trúc Scene**: Xác nhận 42 đối tượng YAML mới và tất cả liên kết component của hệ thống Pause và Result Panel.
