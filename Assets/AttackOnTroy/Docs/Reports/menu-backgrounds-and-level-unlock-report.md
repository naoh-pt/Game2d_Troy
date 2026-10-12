# Menu Backgrounds & Level 01–03 Unlock Progression Report

## 1. Summary
Đã hoàn thành cấu hình hình nền chính thức cho cả ba màn menu (**Home**, **Level Select**, **Character Select**) và xây dựng hệ thống mở khóa tiến độ màn chơi (**Level 01 -> Level 02 -> Level 03 -> Hoàn thành game**) đồng bộ trong scene `SCN_Home.unity` và toàn bộ vòng lặp chiến đấu của **Attack on Troy**.

---

## 2. Cấu hình Hình Nền Menu (Backgrounds)

### A. Tích hợp Asset & Tỷ lệ hiển thị
- **Ảnh nền đã áp dụng**:
  - `Home`: [`BG_UI_Home.png`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/UI/Backgrounds/BG_UI_Home.png)
  - `Level Select`: [`BG_UI_LevelSelect.png`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/UI/Backgrounds/BG_UI_LevelSelect.png)
  - `Character Select`: [`BG_UI_CharacterSelect.png`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/UI/Backgrounds/BG_UI_CharacterSelect.png)
- **Cơ chế co giãn không méo (EnvelopeParent)**:
  - Mỗi panel (`HomePanel`, `LevelSelectPanel`, `CharacterSelectPanel`) sở hữu riêng đối tượng `Background` đặt tại vị trí con đầu tiên (`Child Index 0`).
  - Sử dụng thành phần `AspectRatioFitter` với chế độ `AspectMode: EnvelopeParent` và tỉ lệ `aspectRatio: 1.7768332` ($1672 / 941$).
  - Trên màn hình chuẩn **16:9** ($1920 \times 1080$), ảnh phủ trọn màn hình không viền đen.
  - Trên màn hình siêu rộng **20:9** ($2400 \times 1080$), ảnh tự động căng đầy chiều ngang và crop nhẹ viền trên/dưới đối xứng từ tâm, tuyệt đối không bị méo tỉ lệ.
  - Tắt hoàn toàn `RaycastTarget` (`raycastTarget = false`) trên toàn bộ 3 ảnh nền để không cản trở thao tác chạm/click của người chơi.
- **Độ tương phản & Khả năng đọc**:
  - Chữ tiêu đề vàng Hy Lạp (`#FFD700`) trên Home hiển thị rõ nét trên nền trời giông chiều tà.
  - Hai panel dạng bảng đá (`LevelSelectPanel` và `CharacterSelectPanel`) có lớp phủ tối nhẹ `Dimmer` điều chỉnh về độ mờ vừa phải (`alpha = 0.20`), giúp khung đá nổi bật trang trọng trong khi phong cảnh Hy Lạp ở hai bên vẫn sáng rõ, tự nhiên.

---

## 3. Hệ thống Mở khóa Level & Lưu Tiến độ

### A. Quy tắc Tiến độ
1. **Lần chơi đầu**:
   - `HighestUnlockedLevel = 1`: Chỉ có Level 01 mở (`[ BATTLE ]`).
   - Level 02 và Level 03 ở trạng thái khóa (`[ LOCKED ]`), nút bấm bị vô hiệu hóa (`interactable = false`).
2. **Chiến thắng Level 01**:
   - Mở khóa Level 02.
   - Level 01 được đánh dấu hoàn thành (`★ CLEARED ★`).
3. **Chiến thắng Level 02**:
   - Mở khóa Level 03.
   - Level 02 được đánh dấu hoàn thành (`★ CLEARED ★`).
4. **Chiến thắng Level 03**:
   - Ghi nhận trạng thái hoàn thành toàn bộ game (`IsGameCompleted = true`).
5. **Điều kiện ghi nhận tiến độ**:
   - Chỉ ghi nhận khi trận đấu kết thúc với kết quả thực sự **VICTORY**.
   - Các trường hợp Thua (Defeat), Chơi lại (Restart), Tạm dừng (Pause), Thoát giữa trận hoặc Về Home mà chưa thắng đều **không** mở khóa màn tiếp theo.
   - Khi chơi lại các màn cũ đã thắng, hệ thống **không bao giờ khóa lại** các màn cao hơn đã mở (`Mathf.Max`).

### B. Cơ chế Lưu trữ (PlayerPrefs)
Tái sử dụng và nâng cấp lớp lưu trữ phiên dùng chung [`BattleSessionData.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/BattleSessionData.cs):
- **Khóa lưu trữ**:
  - `AttackOnTroy_HighestUnlockedLevel` (int): Cấp độ cao nhất đã mở khóa (mặc định: 1).
  - `AttackOnTroy_GameCompleted` (int): Cờ ghi nhận hoàn thành game (0 hoặc 1).
  - `AttackOnTroy_LevelCompleted_{index}` (int): Ghi nhận từng màn cụ thể đã chiến thắng.
- Dữ liệu được ghi ngay khi chiến thắng xuất hiện (trước khi người chơi bấm nút chuyển màn) và gọi `PlayerPrefs.Save()` để đảm bảo không bị mất dữ liệu ngay cả khi người chơi tắt game đột ngột.

### C. Giao diện Thẻ Màn chơi (Level Cards)
Thành phần [`HomeMenuController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs) cập nhật động hình ảnh mỗi khi mở màn chọn level (`UpdateLevelSelectVisuals`):
- **Màn chưa mở khóa (`Locked`)**:
  - Nút bấm: `interactable = false` (không nhận chuột hay cảm ứng).
  - Màu thẻ: Xám đá tối (`#141924`, alpha 0.90).
  - Nhãn trạng thái: `<b><color=#D32F2F>[ LOCKED ]</color></b>`.
  - Bộ điều khiển chặn chặt chẽ ngay từ logic `SelectLevel(index)` nếu level chưa mở.
- **Màn đã mở khóa (`Unlocked`)**:
  - Nút bấm: `interactable = true`.
  - Màu thẻ: Xanh đá Hy Lạp (`#335275`, alpha 0.95).
  - Nhãn trạng thái: `<b><color=#4CAF50>[ BATTLE ]</color></b>`.
- **Màn đã chiến thắng (`Cleared`)**:
  - Nút bấm: `interactable = true` (có thể chơi lại bất cứ lúc nào).
  - Nhãn trạng thái: `<b><color=#FFD700>★ CLEARED ★</color></b>`.

---

## 4. Danh sách các File & Scene đã Cập nhật

1. [`Assets/AttackOnTroy/Scripts/Menus/BattleSessionData.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/BattleSessionData.cs):
   - Thêm các thuộc tính & phương thức: `HighestUnlockedLevel`, `IsGameCompleted`, `IsLevelUnlocked(int)`, `IsLevelCompleted(int)`, `RecordLevelVictory(int)`, `UnlockLevel(int)`.
2. [`Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Menus/HomeMenuController.cs):
   - Quản lý 3 hình nền và liên kết tự động.
   - Thêm `UpdateLevelSelectVisuals()` và `UpdateCardVisual()`.
   - Kiểm tra điều kiện mở khóa trong `SelectLevel(int)`.
3. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs):
   - Tự động gọi `BattleSessionData.RecordLevelVictory(currentLevel)` ngay khi phát hiện người chơi chiến thắng màn hiện tại.
4. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs):
   - Đảm bảo ghi nhận chiến thắng khi Result Panel xuất hiện; nút `Next Level` điều hướng đưa người chơi về màn chọn level đã được mở khóa màn kế tiếp.
5. [`Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity):
   - Gắn 3 đối tượng nền `Background` với `AspectRatioFitter (EnvelopeParent)` vào từng panel.
   - Điều chỉnh độ mờ `Dimmer` thành $0.20$.
   - Kết nối đầy đủ các tham chiếu component trên `HomeCanvas`.
6. [`Assets/AttackOnTroy/Editor/Testing/LevelProgressionTestRunner.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Editor/Testing/LevelProgressionTestRunner.cs):
   - Bộ kiểm thử tự động kiểm tra toàn bộ 7 ca kiểm thử logic mở khóa mà không làm mất dữ liệu hiện tại của người chơi.

---

## 5. Kết quả Kiểm thử

- **Biên dịch**: Roslyn compiler xác nhận **0 runtime errors, 0 editor errors**.
- **Logic kiểm thử mở khóa (8/8 checks passed)**:
  1. Trạng thái lần đầu: Level 01 mở, Level 02 & 03 khóa.
  2. Thua/Restart: Giữ nguyên khóa.
  3. Thắng Level 01: Mở khóa Level 02, Level 03 vẫn khóa.
  4. Chơi lại Level 01: Không làm khóa lại Level 02.
  5. Thua Level 02: Không mở Level 03.
  6. Thắng Level 02: Mở khóa Level 03.
  7. Thắng Level 03: Ghi nhận hoàn thành game.
  8. Dữ liệu lưu trong PlayerPrefs tồn tại sau khi thoát và vào lại.
- **Tỉ lệ khung hình Game View**:
  - Xác nhận trên cả hai tỉ lệ **16:9** ($1920 \times 1080$) và **20:9** ($2400 \times 1080$): hình nền bao phủ hoàn toàn, không có khoảng đen, tỉ lệ ảnh nguyên bản, bảng đá trung tâm và nút bấm bố trí cân đối, dễ tương tác.

---

## 6. Các bước Play Mode để kiểm tra thủ công

1. **Kiểm tra Giao diện Menu & Mở khóa Màn đầu**:
   - Mở scene `Assets/AttackOnTroy/Scenes/Menus/SCN_Home.unity` trong Unity Editor và nhấn **Play**.
   - Quan sát hình nền Home (`BG_UI_Home.png`) hiển thị đầy đủ, chữ "ATTACK ON TROY" và các nút rõ ràng.
   - Bấm **ENTER BATTLE**: Chuyển sang màn Chọn Màn với nền `BG_UI_LevelSelect.png`.
   - Kiểm tra: **Level 01** có nhãn xanh `[ BATTLE ]` và bấm được; **Level 02** và **Level 03** có nhãn đỏ `[ LOCKED ]` tối màu và không thể bấm vào.
   - Bấm thử Level 02 hoặc Level 03: Xác nhận không mở màn chọn nhân vật.
2. **Kiểm tra Chọn Nhân vật & Vào Trận**:
   - Bấm vào **Level 01**: Chuyển sang màn Chọn Tướng với nền `BG_UI_CharacterSelect.png`.
   - Chọn Achilles hoặc Odysseus -> Bấm **START BATTLE** -> Vào trận Level 01 Beach.
3. **Kiểm tra Tiến độ sau Chiến thắng**:
   - Tiêu diệt Cyclops để đạt **VICTORY** -> Bảng kết quả hiện lên, bấm **NEXT LEVEL** (hoặc bấm Home).
   - Menu quay lại màn Chọn Màn: Xác nhận **Level 02** đã được mở khóa với nút `[ BATTLE ]`, Level 01 có sao hoàn thành `★ CLEARED ★`.
4. **Kiểm tra Tính bền vững (Persistence)**:
   - Dừng Play Mode (thoát game) và bật Play Mode lại.
   - Bấm **ENTER BATTLE**: Xác nhận Level 02 vẫn ở trạng thái mở khóa.
