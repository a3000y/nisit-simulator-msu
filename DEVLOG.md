# บันทึกการพัฒนา — Nisit Simulator

เกมจำลองชีวิตนิสิต 3D (ปริญญานิพนธ์ สาขาเทคโนโลยีสารสนเทศ ม.มหาสารคาม)
Unity 6.1 (6000.5.1f1) · URP · C#

---

## ภาพรวมสถาปัตยกรรม
- **Core**: GameManager (Singleton + State Machine: MainMenu/Playing/Paused/GameOver/Win), event-driven
- **แยก UI ออกจาก logic ด้วย event** — เพิ่ม/แก้ UI ได้โดยไม่แตะระบบ
- **ScriptableObject** เก็บข้อมูล (วิชา/ไอเทม)
- โครง `Assets/_Project/Scripts/` แยกตามระบบ (Core/Player/Stats/TimeSystem/Interaction/Systems/UI/SaveLoad)
- **Editor tools** อัตโนมัติ (เมนู Nisit → ...) สร้างฉาก/HUD/อาคาร ด้วยการกดปุ่มเดียว

---

## บันทึกตามลำดับ (Milestones)

### M1 — ตัวละครเดิน + กล้อง Isometric
- CharacterController + PlayerMovement (WASD สัมพันธ์มุมกล้อง, วิ่งด้วย Shift)
- IsometricCameraRig (ภายหลังเพิ่มโหมด Third-person สลับด้วยปุ่ม V)
- ระบบปฏิสัมพันธ์ IInteractable + PlayerInteraction (กด E)

### M2 — โมเดลตัวละคร + แอนิเมชัน
- โมเดลจาก Mixamo (Ch29/Jackie) Humanoid rig
- Animator blend tree (ยืน/เดิน/วิ่ง) ขับด้วยพารามิเตอร์ Speed
- บทเรียน: ต้องแตก texture ที่ฝังใน FBX เอง + เปิด Loop Time เอง + เลี่ยงท่าที่ไม่มี In Place

### M3 — ค่าสถานะ + HUD + นาฬิกา
- PlayerStats (พลังงาน/สุขภาพ/ความอิ่ม/ความรู้/ความพึงพอใจ/เงิน/EXP) แบบ event
- GameClock (นาฬิกาในเกม + วัน) · StatDecay (สถานะลดตามเวลา)
- HUD สไตล์การ์ตูน + พอร์ตเทรตตัวละคร + สีหน้าอารมณ์ + ป้าย "กด E"

### งานภาพ (Visual polish)
- แต่งฉาก: พื้นหญ้า/ต้นไม้/ทางเดิน + ฟอนต์ไทย (Leelawadee UI dynamic TMP)
- Post-processing (Bloom/Vignette/สี), แสงเงานุ่ม, หมอก, หลังคา/หน้าต่าง/เสาไฟ

### M5 — ระบบเรียน + เลื่อนชั้นปี + แพ้/ชนะ
- ProgressionManager: ปี 1-4, เป้าความรู้เพิ่มขึ้น (80/180/300/440)
- ClassStation: เข้าเรียนตามคาบ (9-12 / 13-16 น.), คาบละครั้งต่อวัน
- เงื่อนไขจบ: Graduation (ชนะ) / Game Over (ตาย) / Flunked Out (ตก) + คะแนน
- EndScreenController 3 หน้าจอ (ตามสตอรี่บอร์ดฉาก 6/7/8)

### อาคาร + Interior
- อาคาร 8 หลังเข้าไปข้างในได้ (คณะ IT, ร้านค้า, หอพัก, ห้องสมุด, อาคารชมรม, อาคารบริหาร, อาคารเรียน, โรงอาหาร)
- โมเดล KayKit City + Quaternius Buildings
- เฟอร์นิเจอร์: โต๊ะ/เก้าอี้ (นั่งได้), คอมพิวเตอร์, ชั้นหนังสือ
- ระบบประตู BuildingDoor + InteriorManager + InteriorExit

### M6 — ร้านค้า + ระบบเสริม
- ShopStation/ShopController (ซื้อของ), DailyAllowance (เงินรายวัน)
- SleepStation (นอนหอพัก), ระบบนั่งเก้าอี้ (Sittable)

### M4 — เมนูหลัก + Save/Load
- เมนู Scene1: เล่นคนเดียว/เล่นต่อ/ตั้งค่า/ออก (ตามสตอรี่บอร์ดฉาก 1)
- SettingsController: ตั้งค่าเสียง Master/Music/SFX (ฉาก 5)
- Save/Load: GameSession + SaveManager + GameplayBootstrap (ออโต้เซฟทุกวัน, ลบเซฟเมื่อจบเกม)
- พื้นหลังเมนู 3D: กล้องหมุนรอบมหาลัย + ตัวละครยืนไอเดิล

---

## Version Control
- git init + .gitignore (Unity) · commit แรก 2026-07-24 (1,110 ไฟล์)
- ยังเป็น local — แนะนำ push ขึ้น GitHub เพื่อ backup + ทำงานร่วมกัน

---

## เหลือทำ (ตามขอบเขตปริญญานิพนธ์)
- **Phase B — เนื้อหาเกม**: ภารกิจ, สอบกลางภาค/ปลายภาค, สุ่มเหตุการณ์, งานพาร์ทไทม์, เนื้อหาแต่ละชั้นปี
- ปรับแต่งตัวละคร (เพศ/ผม/ชุด) · Minimap
- Multiplayer + Trading (Netcode/Photon)
- แก้จุดที่เอกสารขัดกัน 7 จุด (Netcode vs Photon, บรรณานุกรม ฯลฯ)

---

## เครื่องมือ (Editor Tools) ที่สร้างไว้
เมนู **Nisit →** : Build M1 Scene · Build M3 HUD · Make It Pretty · Add Depth & Mood ·
Build M5 Gameplay · Setup Character (M2) · Fix Character Materials · Fix Animation Loops ·
Build M4 Menu · Menu Background (3D) · CampusBuilder · InteriorBuilder · ShopBuilder ฯลฯ
