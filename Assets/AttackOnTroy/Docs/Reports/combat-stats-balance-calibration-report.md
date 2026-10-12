# Combat Stats, Wind, Aiming & Movement Calibration Report

## 1. Summary
Đã hoàn thành chuẩn hóa và đồng bộ toàn diện các chỉ số chiến đấu, sát thương vũ khí, cơ chế tích nộ SP (hit-based), hồi máu một lần (self-heal), chỉ số AI quái Cyclops, giới hạn chuỗi bắn trúng (hit streak limiter), cơ chế gió và điều khiển di chuyển vượt chướng ngại vật trên toàn bộ 3 level của **Attack on Troy** (`SCN_Level01_Beach.unity`, `SCN_Level02_Scamander.unity`, `SCN_Level03_TroyWall.unity`).

---

## 2. Chi tiết các tinh chỉnh & Cấu hình thống nhất

### A. Chỉ số Người chơi (Achilles & Odysseus) & Cơ chế Hồi máu
1. **Chỉ số sinh mệnh & Giảm thương**:
   - **Achilles**: 150 HP tối đa, nhận 80% sát thương từ đòn đánh của Cyclops (`damageMultiplier = 0.8`).
   - **Odysseus**: 100 HP tối đa, nhận 100% sát thương (`damageMultiplier = 1.0`).
2. **Tự hồi máu một lần/màn (`Self-Heal`)**:
   - Achilles tự hồi **30 HP**, Odysseus tự hồi **25 HP**.
   - Tối đa 1 lần duy nhất trong toàn bộ trận đấu (`hasUsedSelfHeal`).
   - Kích hoạt tự động ở đầu lượt `PlayerTurn` khi nhân vật còn sống và lượng máu hiện tại $\le 50\%$ HP tối đa (Achilles $\le 75$ HP, Odysseus $\le 50$ HP).
   - Được kẹp chính xác không vượt quá `maximumHealth`.
   - Không kích hoạt khi nhân vật đã tử trận; hồi máu không làm tăng thanh năng lượng SP.
   - Trạng thái quyền hồi máu được đặt lại khi chuyển màn mới hoặc khi bấm `Restart`.

### B. Cân bằng Vũ khí Người chơi & Cyclops
1. **Giáo Achilles (`Spear`)**:
   - Sát thương trúng đích trực tiếp: **34 HP**.
   - Không phá hủy địa hình.
   - Hệ số nhạy gió: **0.75**.
2. **Bom chùy Achilles (`Club Bomb`)**:
   - Sát thương nổ diện rộng: **30 HP**, bán kính sát thương: **2.0 world units**.
   - Bán kính khoét địa hình (`craterRadius`): **1.1 world units**.
   - Mỗi mục tiêu chỉ chịu sát thương nổ một lần duy nhất trong mỗi quả bom.
   - Hệ số nhạy gió: **1.00**.
3. **Mũi tên Odysseus (`Arrow`)**:
   - Sát thương trúng đích: **31 HP**.
   - Quỹ đạo bay thẳng tắp theo góc ngắm với vận tốc không đổi, hoàn toàn miễn nhiễm tác động của gió (`windSensitivity = 0.0`).
   - Không phá hủy địa hình.
4. **Bình dầu Odysseus (`Oil Jar`)**:
   - Sát thương va chạm ban đầu: **10 HP**.
   - Vùng lửa bốc cháy gây **8 HP/giây** trong thời gian **3 giây** (3 nhịp tick). Một mục tiêu trúng trực tiếp và cháy trọn vẹn 3 giây chịu tổng cộng đúng **34 HP**.
   - Cơ chế chống dồn sát thương: Khi nhiều vệt lửa chồng lên nhau, hệ thống điều tiết nhịp sát thương toàn cục (`global per-target fire tick cooldown = 1.0s`) đảm bảo mục tiêu không bị nhân đôi sát thương mỗi giây.
   - Phá tường Level 03: Đòn va chạm trực tiếp khoét lỗ có bán kính **0.8** trên tường thành Troy; các vệt lửa rơi trên đất không khoét thêm lỗ.
   - Hệ số nhạy gió: **1.00**.
5. **Đặc tính chịu đòn của Cyclops**:
   - **Cyclops Normal**: Nhận 100% sát thương (`damageMultiplier = 1.0`).
   - **Cyclops Heavy**: Nhận 90% sát thương (`damageMultiplier = 0.9`).

### C. Năng Lượng & Kỹ Năng Đòn Đôi (`DOUBLE-SHOT`)
1. **Tích lũy theo số lần trúng đòn thực tế (Hit-based)**:
   - 0 đòn trúng: 0 SP.
   - 1 đòn trúng: 50 SP.
   - 2 đòn trúng: 100 SP (Đầy bình, nút `SPECIAL` sẵn sàng kích hoạt).
   - Chỉ tính các đòn rìu của Cyclops gây mất HP thực tế cho người chơi. Tự gây sát thương, sát thương thiêu đốt DoT, hồi máu hay đòn bị chặn không tính.
2. **Sử dụng & Hủy nộ**:
   - Bấm `SPECIAL` để đưa đòn bắn kế tiếp vào trạng thái nộ.
   - Người chơi có thể bấm hủy (`CANCEL SPECIAL`) trước khi bắn mà không bị mất 100 SP.
   - Chỉ tiêu hao về 0 SP khi đòn đánh thực sự được phóng đi khỏi tay nhân vật. Đổi vũ khí không làm mất SP.

### D. Chỉ số Cyclops, Rìu Đá & Giới hạn Chuỗi Trúng Đích (`Hit Streak Limiter`)
1. **HP & Sát thương Rìu theo màn**:
   - **Level 01 Beach**: Cyclops Normal 150 HP, Rìu gây **18 HP**.
   - **Level 02 Scamander**: Cyclops Heavy 250 HP, Rìu gây **26 HP**, kích thước tỷ lệ scale 1.4x.
   - **Level 03 Troy Wall**:
     - Cyclops Normal ngoài tường: 150 HP, Rìu gây **20 HP**.
     - Cyclops Heavy trong tường: 250 HP, Rìu gây **28 HP**, kích thước scale 1.4x.
2. **Độ chính xác AI**:
   - Tỷ lệ trúng thực tế được hiệu chuẩn:
     - Level 01 Normal: ~40% – 50% (chuẩn 45%).
     - Level 02 Heavy: ~50% – 60% (chuẩn 55%).
     - Level 03 Normal: ~55% – 65% (chuẩn 60%).
     - Level 03 Heavy: ~65% – 75% (chuẩn 70%).
3. **Giới hạn chuỗi trúng đích (`Hit Streak Limiter`)**:
   - Khi kẻ địch ném trúng gây sát thương cho người chơi trong **2 lượt liên tiếp của địch**, lượt ném thứ 3 của địch bắt buộc phải chọn quỹ đạo hụt an toàn (lệch tối thiểu 2.5 đơn vị khỏi người chơi).
   - Đòn hụt lập tức đặt lại chuỗi về 0.
   - Trong Level 03, bộ đếm chuỗi được dùng chung giữa cả hai Cyclops Normal và Heavy.
   - Công thức tính quỹ đạo kinematic của AI sử dụng chính xác hệ số nhạy gió của rìu: $0.8 \times \text{wind}$.

### E. Góc Ngắm, Lực Bắn, Dải Gió & HUD
1. **Góc ngắm**: Mở rộng từ $0^\circ$ đến $85^\circ$ (gỡ bỏ giới hạn $10^\circ$). Vị trí spawn đạn ở góc $0^\circ$ được nâng cao độ để không chạm người chơi hay cày xuống đất.
2. **Lực bắn**: Tích lực trong khoảng 25% đến 100%, thời gian tích 1 chu kỳ là **1.25s**.
   - Dải tốc độ vũ khí:
     - Giáo: 16 – 32.
     - Bom chùy: 12 – 28.
     - Mũi tên: 32 – 50.
     - Bình dầu: 12 – 27.
3. **Dải gió theo Level**:
   - Level 01: $\pm 0.8$.
   - Level 02: $\pm 1.5$.
   - Level 03: $\pm 2.0$.
4. **HUD Gió**: Tự động lấy dải gió tối đa tương ứng với scene hiện hành (0.8 / 1.5 / 2.0) để vạch đo cường độ hiển thị tỉ lệ chuẩn xác; gió bằng 0 hiển thị trạng thái trung hòa.

### F. Cơ Chế Di Chuyển & Vượt Chướng Ngại Vật
1. **Khả năng leo dốc & bước gờ**:
   - Góc dốc có thể đi bộ tối đa: $60^\circ$ (`maximumWalkableSlopeAngle = 60f`, `minimumGroundNormalY = 0.5f`).
   - Chiều cao bước gờ tối đa (`EffectiveStepHeight`): Tự động lấy giá trị lớn hơn giữa `maximumStepHeight` và $50\%$ chiều cao collider của nhân vật.
   - Người chơi có thể tự di chuyển bước ra khỏi lòng hố bom đã khoét nếu góc nghiêng miệng hố $< 60^\circ$.
2. **Ổn định di chuyển**:
   - Kích hoạt chống trượt khi đứng yên (`anti-slide`).
   - Không đi xuyên qua tường thành Troy.
   - Không bị teleport hoặc giật hoạt ảnh chạy khi input di chuyển bằng 0.

---

## 3. Danh sách File Cập nhật

1. **Scripts Gameplay Core**:
   - [`Assets/AttackOnTroy/Combat/Scripts/Damage/ActorDamageReceiver2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Damage/ActorDamageReceiver2D.cs): Bổ sung `Heal(float amount)`.
   - [`Assets/AttackOnTroy/Combat/Scripts/Damage/FirePatch2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Damage/FirePatch2D.cs): Thời lượng 3.0s, chu kỳ 1.0s, 8 HP/tick, điều tiết chống dồn sát thương trên cùng mục tiêu $\ge 0.95$s.
   - [`Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs): Thêm `windSensitivity`, `SetDirectHitDamage()`, khoét tường Level 03 bán kính 0.8 cho bình dầu.
   - [`Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs): Góc ngắm $[0^\circ, 85^\circ]$, thời gian nạp 1.25s, dải lực 25%–100%, dải tốc độ riêng cho từng loại vũ khí, dải gió theo scene, kẹp góc double-shot $[0^\circ, 85^\circ]$.
   - [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs): Tự hồi máu 1 lần ở đầu lượt người chơi khi $HP \le 50\%$, theo dõi chuỗi trúng đòn của địch, cờ `MustEnemyMissNext`.
   - [`Assets/AttackOnTroy/Combat/Scripts/Turns/PlayerEnergySystem.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/PlayerEnergySystem.cs): Tích lũy 2 đòn trúng (0/50/100 SP), hủy trước khi bắn giữ nguyên 100 SP, bắn mới tiêu hao về 0 SP, loại trừ sát thương DoT và tự gây sát thương.
   - [`Assets/AttackOnTroy/Combat/Scripts/UI/ProjectileAimHUD.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/UI/ProjectileAimHUD.cs): Tự động xác định `maxWindSpeed` (0.8 / 1.5 / 2.0) cho từng level.
   - [`Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs): Sát thương rìu theo level, tỷ lệ trúng theo level, quỹ đạo kinematic dùng gió hiệu dụng $0.8 \times \text{wind}$, ép bắn hụt an toàn khi `MustEnemyMissNext == true`.
   - [`Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs): Góc dốc leo $60^\circ$, `EffectiveStepHeight` tối đa $50\%$ chiều cao collider.

2. **Prefabs Vũ Khí & Kẻ Địch**:
   - [`PF_Projectile_Achilles_Spear.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_Spear.prefab): `directHitDamage: 34`, `windSensitivity: 0.75`.
   - [`PF_Projectile_Achilles_ClubBomb.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_ClubBomb.prefab): `damageRadius: 2.0`, `craterRadius: 1.1`, `areaDamage: 30`, `windSensitivity: 1.0`.
   - [`PF_Projectile_Odysseus_Arrow.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Odysseus_Arrow.prefab): `directHitDamage: 31`, `windSensitivity: 0.0`.
   - [`PF_Projectile_Odysseus_OilJar.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Odysseus_OilJar.prefab): `directHitDamage: 10`, `oilJarCraterRadius: 0.8`, `windSensitivity: 1.0`.
   - [`PF_FirePatch.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Damage/PF_FirePatch.prefab): `duration: 3`, `damageInterval: 1`, `damagePerTick: 8`.
   - [`PF_Projectile_CyclopsNormal_StoneAxe.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_CyclopsNormal_StoneAxe.prefab): `directHitDamage: 18`, `windSensitivity: 0.8`.
   - [`PF_Projectile_CyclopsHeavy_StoneAxe.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_CyclopsHeavy_StoneAxe.prefab): `directHitDamage: 26`, `windSensitivity: 0.8`.

3. **Scenes**:
   - [`SCN_Level01_Beach.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity):
     - Achilles: HP 150, mult 0.8.
     - Cyclops Normal: HP 150, mult 1.0.
     - Launchers: angle $[0^\circ, 85^\circ]$, charge 1.25s, wind $[-0.8, 0.8]$.
   - [`SCN_Level02_Scamander.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level02_Scamander/SCN_Level02_Scamander.unity):
     - Achilles: HP 150, mult 0.8.
     - Cyclops Heavy: HP 250, mult 0.9.
     - Launchers: angle $[0^\circ, 85^\circ]$, charge 1.25s, wind $[-1.5, 1.5]$.
   - [`SCN_Level03_TroyWall.unity`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scenes/Levels/Level03_TroyWall/SCN_Level03_TroyWall.unity):
     - Achilles: HP 150, mult 0.8.
     - Cyclops Normal: HP 150, mult 1.0.
     - Cyclops Heavy: HP 250, mult 0.9.
     - Launchers: angle $[0^\circ, 85^\circ]$, charge 1.25s, wind $[-2.0, 2.0]$.
