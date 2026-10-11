# Attack on Troy - Project Scope & Design Decisions Update

## 1. Overview
Tài liệu cập nhật phạm vi phát triển và các quyết định thiết kế mới nhất cho dự án **Attack on Troy (Level 01 Beach & Core Combat)**.

---

## 2. Các thay đổi phạm vi & Quyết định thiết kế chính

### A. Loại bỏ hình nộm Odysseus (Odysseus Playable Hero)
- **Thiết kế ban đầu**: Odysseus từng được dự kiến như một hình nộm luyện tập / dummy mục tiêu hoặc chỉ là nguyên mẫu kiểm thử.
- **Quyết định mới nhất (Đã triển khai)**:
  - Bỏ hoàn toàn khái niệm hình nộm Odysseus.
  - **Odysseus là nhân vật chính thức có thể chơi hoàn chỉnh (Playable Hero)**, sánh ngang cùng Achilles trên màn hình Character Select.
  - Odysseus sở hữu bộ vũ khí riêng biệt:
    1. **Mũi tên thẳng (Straight Arrow)**: Bay đường thẳng với vận tốc không đổi theo góc ngắm, không chịu ảnh hưởng của trọng lực và gió.
    2. **Bình dầu lửa (Oil Jar)**: Bay theo đường đạn cong chịu trọng lực và gió; vỡ khi va chạm và tạo vệt lửa thiêu đốt diện rộng gây sát thương theo chu kỳ (tick).
  - Có đầy đủ hoạt ảnh (Idle, Run, BowAttack, Hurt, Death) với tỷ lệ vóc dáng cân đối và hệ thống điều khiển chân thực trên địa hình biển.

### B. Kỹ năng Năng Lượng Đòn Đặc Biệt: Đổi từ Ba thành Hai vật thể (Double-Shot)
- **Thiết kế trước đây**: Đòn nộ năng lượng từng được dự tính bắn 3 vật thể (Triple Shot).
- **Quyết định mới nhất (Đã triển khai)**:
  - Chuẩn hóa kỹ năng nộ thành **DOUBLE-SHOT (2 vật thể)** để tối ưu tính chiến thuật, độ rõ ràng của góc ngắm, cân bằng sát thương và hiệu năng camera.
  - Khi thanh SP đạt 100% và kích hoạt nút `SPECIAL`:
    - Đòn đánh tiếp theo bắn cùng lúc **2 vật thể** từ vị trí buông vũ khí với cùng lực bắn và gió.
    - Hai vật thể bay theo góc đối xứng qua hướng ngắm với độ lệch góc $\pm 3^\circ$.
    - Áp dụng cho cả 4 loại vũ khí: Giáo (`Spear`), Bom chùy (`Club Bomb`) của Achilles, Mũi tên (`Arrow`), Bình dầu (`Oil Jar`) của Odysseus.
    - Bỏ qua va chạm giữa 2 vật thể (`Physics2D.IgnoreCollision`) để tránh nổ tức thời khi xuất hiện.
    - Camera bám điểm giữa `(A + B)/2` khi cả 2 đang bay và chuyển sang bám viên còn lại khi một viên chạm đích.
    - Cả 2 vật thể được tính là một lượt duy nhất: input chỉ mở và gió mới chỉ đổi sau khi cả 2 viên hoàn tất va chạm.

---

## 3. Trạng thái các tính năng liên quan
- **Character Select**: Hỗ trợ chọn Achilles hoặc Odysseus với lưu vết qua `BattleSessionData`.
- **Level 01 Combat**: Hai anh hùng hoạt động ổn định với cơ chế ném đạn, phá hủy địa hình, cháy địa hình, camera theo dõi và hệ thống đối thoại.
- **Hệ thống Pause & Điều hướng trận đấu**: Hoàn thiện nút Pause, panel Pause (Tiếp tục, Chơi lại, Về Home) và mở rộng Result Panel (Chuyển Level, Về Home, Chơi lại).
