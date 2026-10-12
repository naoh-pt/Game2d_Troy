# Attack on Troy - Project Scope, Combat Calibration & Design Decisions

## 1. Overview
Tài liệu quy định và chuẩn hóa phạm vi phát triển, hệ thống cân bằng chỉ số chiến đấu, gió, ngắm bắn, AI và di chuyển xuyên suốt 3 màn chơi chính của **Attack on Troy** (Level 01 Beach, Level 02 Scamander, Level 03 Troy Wall).

---

## 2. Các thay đổi phạm vi & Quyết định thiết kế chính

### A. Nhân vật Playable Heroes (Achilles & Odysseus)
- **Odysseus là nhân vật chính thức có thể chơi hoàn chỉnh (Playable Hero)**, sánh ngang cùng Achilles trên màn hình Character Select:
  - Bỏ hoàn toàn khái niệm hình nộm luyện tập / dummy mục tiêu.
  - Có đầy đủ hoạt ảnh (Idle, Run, BowAttack, Hurt, Death) và tỷ lệ vóc dáng cân đối trên địa hình 2D.
- **Hệ thống HP & Giảm trừ sát thương**:
  - **Achilles**: 150 HP tối đa; nhận 80% sát thương từ đòn đánh của Cyclops (`damageMultiplier = 0.8`).
  - **Odysseus**: 100 HP tối đa; nhận 100% sát thương (`damageMultiplier = 1.0`).
- **Cơ chế Tự hồi phục (Self-Heal 1 lần/màn)**:
  - Achilles tự hồi 30 HP, Odysseus tự hồi 25 HP.
  - Tối đa 1 lần duy nhất mỗi màn đấu/trận.
  - Tự động kích hoạt ở đầu lượt người chơi (`PlayerTurn`) khi còn sống và `HP <= 50% HP tối đa`.
  - Không vượt quá HP tối đa, không kích hoạt khi đã chết, không cộng năng lượng vì hồi máu.
  - Reset trạng thái hồi máu khi bắt đầu màn mới hoặc bấm Restart.

### B. Kỹ năng Năng Lượng Đòn Đặc Biệt: DOUBLE-SHOT (2 vật thể)
- Chuẩn hóa kỹ năng nộ thành **DOUBLE-SHOT (2 vật thể đồng thời)**.
- **Cơ chế tích lũy năng lượng (Dựa trên số lần trúng đòn)**:
  - Tích lũy theo số lần bị Cyclops ném trúng gây mất HP thực tế:
    - 0 lần trúng: 0 SP.
    - 1 lần trúng: 50 SP.
    - 2 lần trúng: 100 SP (Đầy bình, nút `SPECIAL` sẵn sàng).
  - Không tích lũy từ tự gây sát thương, sát thương thiêu đốt theo thời gian (DoT), hồi máu hay đòn bị chặn.
  - Chỉ kích hoạt được trong lượt `PlayerTurn` khi SP đạt 100%.
  - Kích hoạt Special sẽ đưa trạng thái vào thế sẵn sàng cho phát bắn tiếp theo. Nếu bấm Hủy (`CANCEL SPECIAL`) trước khi bắn, giữ nguyên 100 SP.
  - Năng lượng chỉ tiêu hao về 0 khi vũ khí đặc biệt thực sự được phóng đi. Mở chọn vũ khí không làm mất năng lượng.
- **Quy cách phóng Double-Shot**:
  - Tại điểm buông vũ khí (release point), phóng đồng thời 2 bản sao của vũ khí đang chọn.
  - Cùng lực bắn (power) và cùng giá trị gió hiện hành.
  - Góc bắn đối xứng qua góc ngắm với độ lệch góc $\pm 3^\circ$, kẹp trong giới hạn hợp lệ $[0^\circ, 85^\circ]$.
  - Bỏ qua va chạm giữa 2 vật thể (`Physics2D.IgnoreCollision`) để tránh nổ tức thời khi xuất hiện.
  - Camera theo dõi trung điểm `(A + B)/2` khi cả 2 vật thể đang bay, sau đó chuyển sang viên còn lại khi một viên chạm đích. Giữ camera trong `LevelBounds`.
  - Cả 2 vật thể tính là 1 lượt duy nhất: input khóa cho đến khi cả hai hoàn tất va chạm hoặc timeout; chỉ sinh gió mới một lần sau khi kết thúc lượt.

---

## 3. Hệ thống Vũ Khí & Sát Thương

| Vũ khí | Tướng | Sát thương | Bán kính nổ / Crater | Độ nhạy gió | Cơ chế đặc biệt |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Giáo (`Spear`)** | Achilles | 34 trực tiếp | Không phá địa hình | 0.75 | Bay theo đường đạn cong, cắm mục tiêu |
| **Bom chùy (`Club Bomb`)** | Achilles | 30 diện rộng | R=2.0 (sát thương), R=1.1 (đào hố) | 1.00 | Một vụ nổ chỉ gây damage 1 lần mỗi mục tiêu |
| **Mũi tên (`Arrow`)** | Odysseus | 31 trực tiếp | Không phá địa hình | 0.00 | Bay thẳng theo góc ngắm, miễn nhiễm gió hoàn toàn |
| **Bình dầu (`Oil Jar`)** | Odysseus | 10 va chạm + 8/s trong 3s | Tối đa 34 damage tổng | 1.00 | Vùng lửa thiêu đốt không chồng sát thương; ở Level 03 khoét lỗ R=0.8 trên tường thành |

- **Kháng sát thương của Cyclops**:
  - **Cyclops Normal**: Nhận 100% sát thương (`damageMultiplier = 1.0`).
  - **Cyclops Heavy**: Nhận 90% sát thương (`damageMultiplier = 0.9`).

---

## 4. Chỉ số Kẻ địch (Cyclops) & AI Bắn chuẩn hóa

### A. Chỉ số Cyclops theo Level
- **Level 01 Beach**: 1 Cyclops Normal (150 HP, Rìu gây 18 damage).
- **Level 02 Scamander**: 1 Cyclops Heavy (250 HP, Rìu gây 26 damage, kích thước scale 1.4x).
- **Level 03 Troy Wall**:
  - 1 Cyclops Normal ngoài tường (150 HP, Rìu gây 20 damage).
  - 1 Cyclops Heavy trong tường (250 HP, Rìu gây 28 damage, kích thước scale 1.4x).
  - Thứ tự lượt luân phiên: `Player -> Normal -> Player -> Heavy -> lặp lại`. Thắng khi cả hai bị tiêu diệt.

### B. Độ chính xác AI & Giới hạn chuỗi trúng đích (`Hit Streak Limiter`)
- **Tỷ lệ trúng mục tiêu**:
  - Normal Level 01: 40% – 50% (cấu hình chuẩn ~45%).
  - Heavy Level 02: 50% – 60% (cấu hình chuẩn ~55%).
  - Normal Level 03: 55% – 65% (cấu hình chuẩn ~60%).
  - Heavy Level 03: 65% – 75% (cấu hình chuẩn ~70%).
- **Thuật toán quỹ đạo rìu**:
  - Rìu đá Cyclops chịu tác động gió với hệ số nhạy `0.8` (`effectiveWind = wind * 0.8f`).
- **Hit Streak Limiter**:
  - Nếu đòn đánh của địch gây mất HP cho người chơi trong **2 lượt liên tiếp của địch**, lượt ném thứ 3 của địch bắt buộc phải chọn quỹ đạo hụt an toàn (lệch tối thiểu 2.5 đơn vị khỏi người chơi).
  - Đòn ném hụt lập tức đặt chuỗi trúng về 0.
  - Trong Level 03, bộ đếm chuỗi được dùng chung giữa Cyclops Normal và Heavy.

---

## 5. Ngắm bắn, Tích lực, Gió & Di chuyển

### A. Ngắm bắn & Tích lực
- **Góc ngắm người chơi**: Mở rộng từ $[0^\circ, 85^\circ]$ (loại bỏ chặn $10^\circ$). Điểm xuất hiện đạn ở góc $0^\circ$ được bù trừ cao độ để không va chạm vào mặt đất hoặc bản thân người chơi.
- **Thanh lực**: Dải lực 25% – 100%, thời gian tích đầy chu kỳ là 1.25 giây (`chargeDuration = 1.25s`).
- **Dải vận tốc vũ khí**:
  - Giáo Achilles: 16 – 32.
  - Bom chùy Achilles: 12 – 28.
  - Mũi tên Odysseus: 32 – 50.
  - Bình dầu Odysseus: 12 – 27.

### B. Giới hạn Gió & HUD
- **Dải gió theo Level**:
  - Level 01 Beach: $\pm 0.8$.
  - Level 02 Scamander: $\pm 1.5$.
  - Level 03 Troy Wall: $\pm 2.0$.
- **Độ nhạy gió của từng loại đạn**:
  - Giáo: 0.75 | Bom chùy: 1.00 | Mũi tên: 0.00 | Bình dầu: 1.00 | Rìu Cyclops: 0.80.
- **HUD Gió**: Hiển thị hướng mũi tên và vạch cường độ chuẩn xác theo dải gió tối đa của từng màn; gió bằng 0 hiển thị trạng thái trung hòa.

### C. Di chuyển & Va chạm Địa hình
- Đi lên dốc có góc nghiêng $< 60^\circ$ (`maximumWalkableSlopeAngle = 60f`, `minimumGroundNormalY = 0.5f`).
- Bước qua bậc gờ địa hình $\le 50\%$ chiều cao collider nhân vật (`EffectiveStepHeight`).
- Người chơi có thể tự di chuyển bước ra khỏi hố bom nếu góc dốc lòng hố $< 60^\circ$.
- Cơ chế chống trượt dốc khi đứng yên (`Anti-slide`).
- Không đi xuyên tường thành Troy. Không bị dịch chuyển tức thời (teleport) hoặc chạy tại chỗ khi không có input.

---

## 6. Trạng thái các tính năng liên quan
- **Character Select**: Hỗ trợ chọn Achilles hoặc Odysseus với lưu vết qua `BattleSessionData`.
- **Level 01, 02, 03 Combat**: Cả 3 màn hoạt động độc lập, hỗ trợ Pause (Tiếp tục, Chơi lại, Về Home), Result Panel (Màn tiếp theo, Chơi lại, Về Home), camera theo dõi và minimap phù hợp.
