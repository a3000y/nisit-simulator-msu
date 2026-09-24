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

### เมนูหลัก — ยกเครื่องงานภาพ (2026-07-24)
- **MenuPolish** (Nisit → Polish Menu Layout): การ์ดกลางจอมุมโค้ง+เงา+vignette, หัวเรื่องไล่สีทอง+เงา+เส้นคั่น, ปุ่มไล่สีมีมิติ+เรืองแสงตามสี+hover ยกตัว, footer
- **พื้นหลังเลือกได้ 3 แบบ** (คำสั่งแยก):
  - `Epic Menu Background` — 2D เจนทีละพิกเซล: พระอาทิตย์ตก + aurora + lens flare + ดาวตก + เมฆ + เงาเมือง/ภูเขา + หิ่งห้อย (กดซ้ำ = สุ่มใหม่)
  - `Day Campus Background` — 2D เจน: ฟ้ากลางวัน + เมฆก้อนฟู + ตึกโรงเรียน + ต้นไม้ + สนามหญ้า
  - `Use 3D Background` / `Menu Background (3D)` — ฉาก 3D จริง กล้องหมุนรอบ
- **ฉาก 3D ใช้โมเดล KayKit จริง**: ต้นไม้ Forest (Tree/Bush/Rock สุ่มแบบ), ตึก City 9 หลัง + หอเก็บน้ำ, ถนนกากบาท + เสาไฟ + รถ, NPC เดิน 3 ตัว
  - ถนน **วัดขนาดกระเบื้องจริงจาก Renderer bounds** แล้ววางต่อกัน → ไม่ขาด/ทับ ทุกสเกล
  - ปรับขนาดง่ายด้วยค่าคงที่บนสุดไฟล์: `TreeScale` / `BuildingScale` / `RoadScaleMul` / `DecoScale`
- **Runtime ใหม่** (ใช้ในเกมจริงได้): `MenuParallax` (เลื่อนเลเยอร์ตามเมาส์), `UIButtonHover` (ยกตัว+เรืองแสง), `MenuNPCWalker` (NPC เดินตาม waypoint + อนิเมชัน Speed)
- ภาพ/รูปทรง UI เจนเองเป็น PNG (มุมโค้ง 9-slice, vignette, glow, ปุ่ม gradient) เก็บที่ `Art/UI/`
- บทเรียน: เขียนไฟล์ PNG ใน Editor ต้องใช้ **absolute path** (`Application.dataPath`) ไม่งั้นไฟล์หลงที่; ระวัง **CS0136** ตั้งชื่อ local ซ้ำ (เคสตัวแปร `path`)

### Phase B — ระบบสอบ + ฤดูกาล/เดือน (2026-07-24)
- **ระบบสอบ** ([Systems/ExamController.cs](Assets/_Project/Scripts/Systems/ExamController.cs) + [Editor/M6ExamBuilder.cs](Assets/_Project/Scripts/Editor/M6ExamBuilder.cs))
  - Quiz ป็อปอัป 4 ตัวเลือก (คลังข้อสอบ 12 ข้อ เรื่องการเรียน/วิชาการ)
  - คะแนน = ตอบถูก 60% + ความพร้อม (ความรู้สะสม ÷ เป้าปี) 40% → เกรด A-F + **GPA สะสม**
  - เกรดดี = ได้ความรู้/EXP/ความพอใจ/ทุนการศึกษา (A=100฿), เกรดตก = หักความพอใจ
  - หยุดเวลา + ล็อกเดิน + โชว์เมาส์ ระหว่างสอบ · ปลายภาคคูณรางวัล ×1.5
- **ระบบฤดูกาล + เดือน** ([Systems/AcademicCalendar.cs](Assets/_Project/Scripts/Systems/AcademicCalendar.cs) · [Systems/SeasonManager.cs](Assets/_Project/Scripts/Systems/SeasonManager.cs) · [Editor/M7SeasonBuilder.cs](Assets/_Project/Scripts/Editor/M7SeasonBuilder.cs))
  - **ปีละ 12 วัน = 12 เดือน** (มิ.ย.→พ.ค. ตามปีการศึกษาไทย, 1 วัน = 1 เดือน)
  - 3 ภาคเรียน/ฤดู: ภาคต้น(ฝน) 5 + ภาคปลาย(หนาว) 5 + ภาคฤดูร้อน(ร้อน) 2
  - โทนสีจอเปลี่ยนตามฤดู (overlay ใต้ HUD) + ป้ายบอกเดือน/ภาค/ฤดู + Toast ตอนเปลี่ยนภาค
  - `AcademicCalendar` เป็นศูนย์กลาง: แก้ `TermDays` ที่เดียว เดือน/ฤดู/วันสอบขยับตามอัตโนมัติ
- **ตารางสอบ 5 ครั้ง/ปี**: วันที่ 3,5 (ต้น) · 8,10 (ปลาย) · 12 (ฤดูร้อน — ปลายภาคอย่างเดียว)
- ปรับ [ProgressionManager](Assets/_Project/Scripts/Systems/ProgressionManager.cs): `daysPerYear` 3→12, เป้าความรู้ 180/420/720/1080
- บทเรียน: ค่าที่ serialize บน component ในซีน **ทับ** ค่า default ในโค้ด → ต้องตั้งผ่าน Editor tool (`EditorUtility.SetDirty` + save scene)

### Phase B — ภารกิจ + เหตุการณ์ + งาน + แอคชันจริง (2026-07-24)
- **ระบบภารกิจรายวัน** ([Systems/QuestSystem.cs](Assets/_Project/Scripts/Systems/QuestSystem.cs) · [Editor/M8QuestBuilder.cs](Assets/_Project/Scripts/Editor/M8QuestBuilder.cs))
  - สุ่ม 3/วันจากคลัง 11 แบบ (เรียน/การเงิน/ดูแลตัวเอง) · ติดตามจากค่าสถานะอัตโนมัติ · รีเซ็ตทุกวัน · รางวัลเงิน/EXP/พอใจ
- **ระบบสุ่มเหตุการณ์** ([Systems/EventManager.cs](Assets/_Project/Scripts/Systems/EventManager.cs) · [Editor/M9EventBuilder.cs](Assets/_Project/Scripts/Editor/M9EventBuilder.cs))
  - เช้าวันใหม่ ~55% (เว้นวันสอบ) · คลัง 10 เหตุการณ์ · มีทั้งเลือกทาง 2 ทาง + ผลทันที
- **แอคชันตัวละครจริง** ([Player/PlayerActionController.cs](Assets/_Project/Scripts/Player/PlayerActionController.cs))
  - เข้าเรียน/ทำกิจกรรม = ตัวละคร**นั่งทำจริง**ก่อนได้ผล (ไม่ใช่เด้งตัวเลขทันที) · รองรับเสียบคลิป Mixamo ใหม่ (ส่งชื่อ animBool)
- **สอบเป็นกิจกรรมจริงในโลก** ([Interaction/ExamStation.cs](Assets/_Project/Scripts/Interaction/ExamStation.cs))
  - วันสอบต้องเดินไป **ExamDesk** กด E → ตัวละครนั่งลง → quiz โผล่ตอนนั่ง → ลุกเมื่อเสร็จ (ไม่ auto-pop) · ไม่ไป = พลาดสอบ
- **งานพาร์ทไทม์** ([Interaction/WorkStation.cs](Assets/_Project/Scripts/Interaction/WorkStation.cs) · [Editor/M10JobBuilder.cs](Assets/_Project/Scripts/Editor/M10JobBuilder.cs))
  - 2 งาน: ร้านกาแฟ (+60฿ แต่เปลืองแรง) / ผู้ช่วยห้องสมุด (+45฿ +ความรู้) · จำกัด 2 กะ/วัน · ทำงานจริงก่อนได้ค่าจ้าง
- **งานภาพ/แอนิเมชัน** ([UI/UIPopupAnim.cs](Assets/_Project/Scripts/UI/UIPopupAnim.cs) · [Systems/WeatherOverlay.cs](Assets/_Project/Scripts/Systems/WeatherOverlay.cs))
  - ป็อปอัปสอบ/เหตุการณ์ **เด้งเข้า** (scale+fade, ใช้ unscaledTime) · เกรดเด้งโชว์
  - **ฝน/ละอองหนาว/ละอองแดด** ตกจริงตามฤดู + โทนสีค่อย ๆ ไล่เปลี่ยน (fade)
- แพทเทิร์นกลาง **"เดินไปทำจริง"**: PlayerActionController.Perform/BeginHold–EndHold → ใช้ซ้ำกับสอบ/งาน/กิจกรรม

**ข้อจำกัดที่รู้อยู่ (ต้องเก็บต่อ):**
- ท่าทำกิจกรรมยังเป็น **"นั่ง" เหมือนกันหมด** (เรียน/ทำงาน/กิน) เพราะมีแค่คลิป เดิน/วิ่ง/นั่ง — ต้องโหลดท่าเฉพาะจาก Mixamo (อ่าน/กิน/ชงกาแฟ)
- **สุ่มเหตุการณ์ยังเป็นป็อปอัป** เลือกแล้วบวก/ลบค่า — ยังไม่เดินไปทำในโลกจริง (เช่น เลือก "ไปนอน" ยังไม่เดินไปหอ)
- ระบบที่ "จริงในโลก" แล้ว = เดิน/เข้าเรียน/สอบ/ทำงาน/กิจกรรม (เดินไป + ลงมือทำ)

### Phase B — ระบบคณะ (data-driven) + ผูกอาคารจริง + หน้าสรุป (2026-07-24)
- **ระบบคณะแบบ data-driven** ([Systems/FacultyCatalog.cs](Assets/_Project/Scripts/Systems/FacultyCatalog.cs))
  - 4 คณะ: IT / บริหารธุรกิจ / วิทยาศาสตร์ / นิเทศศาสตร์ · ข้อสอบ = ข้อทั่วไป + ข้อเฉพาะคณะ
  - คณะเก็บเป็น **ค่าต่อผู้เล่น** (`GameSession.SelectedFacultyIndex`) → ออกแบบเผื่อ multiplayer (เพื่อนเลือกคณะเองได้)
- **หน้าเลือกคณะในเมนู** ([Editor/M11FacultyBuilder.cs](Assets/_Project/Scripts/Editor/M11FacultyBuilder.cs) + แก้ MainMenuController)
  - กด New Game → เลือกคณะ → เข้าเกม · ExamController ดึงข้อสอบตามคณะที่เลือก
- **ผูกสอบ/งานกับอาคารจริงในฉาก** ([Editor/M10JobBuilder.cs](Assets/_Project/Scripts/Editor/M10JobBuilder.cs) → "Place Exam & Jobs at Buildings")
  - จุดโต้ตอบล่องหนหน้าประตูอาคารเดิม: สอบ→อาคารเรียน, งานกาแฟ→โรงอาหาร, งานห้องสมุด→ห้องสมุด (อ่านตำแหน่งจาก Door_*)
- **ข้อสอบ IT** เพิ่มในคลัง (บิต/ไบต์, HTML, RAM, SQL, Queue, ภาษาโปรแกรม ฯลฯ)
- **หน้าสรุปจบเกม** ([UI/EndScreenController.cs](Assets/_Project/Scripts/UI/EndScreenController.cs)) โชว์ คณะ / ชั้นปี / GPA / คะแนนรวม
- แก้บั๊ก: แผงภารกิจย้ายไปมุมซ้าย (ไม่ทับ HUD ขวา)

**สถานะระบบคณะ:** เลือกคณะ + ข้อสอบแยกคณะ = ทำงานจริง · แต่**มีตึกคณะเดียว (IT)** — คณะอื่นยังไม่มีตึก (สอบที่อาคารเรียนรวม) · เพิ่มตึก 4 คณะ = เฟสถัดไป (ถ้าทำ multiplayer)

### Event → ลงมือทำในโลก + แผนคลิปท่า Mixamo (2026-07-24)
- **เหตุการณ์สุ่มทำแบบกิจกรรมแล้ว** ([Systems/EventManager.cs](Assets/_Project/Scripts/Systems/EventManager.cs)): เลือกในป็อปอัป → ตัวละครทำท่า (Perform ~1.8 วิ) → ค่อยได้ผล (เหมือน ActivityStation ไม่ใช่บวกเลขทันที)
- **แผนคลิปท่า Mixamo** (ทำให้สมจริง — ต้องโหลดเพิ่ม, retarget Humanoid เป็น Ch29):
  - เรียน/สอบ/ห้องสมุด → "Reading" (param `Reading`)
  - โรงอาหาร/กิน → "Eating" (`Eating`) · หอพัก/นอน → "Sleeping" (`Sleeping`) · งานกาแฟ → "Typing" (`Working`)
  - เสริม: "Cheering" (จบ/เกรดดี), "Sick" (event ป่วย)
  - โค้ดรองรับแล้ว: `PlayerActionController.Perform(dur, onDone, "<param>")` — ไม่มี param จะ fallback เป็นนั่ง
  - ขั้นตอน: Mixamo (FBX, Without Skin, In Place) → Rig=Humanoid, Copy From Ch29 Avatar → เพิ่ม state+bool param → บอกให้ผูก station

### สอบแยกตามคณะ "ตึกใครตึกมัน" (2026-07-24)
- **ExamStation มี `facultyIndex`** ([Interaction/ExamStation.cs](Assets/_Project/Scripts/Interaction/ExamStation.cs)): สอบได้เฉพาะคณะที่เรียน (คณะอื่น → "ไม่ใช่คณะคุณ")
- **จุดสอบ 4 คณะ ที่ตึกจริง** ([Editor/M10JobBuilder.cs](Assets/_Project/Scripts/Editor/M10JobBuilder.cs)) — ใช้ตึกที่มีอยู่ (ไม่วางตึกใหม่ให้เสี่ยงทับ):
  - IT → คณะ IT · บริหาร → อาคารบริหาร · วิทย์ → อาคารเรียน · นิเทศ → อาคารชมรม
- เล่น: เลือกคณะ → ไปสอบที่ตึกคณะตัวเอง → ข้อสอบตรงคณะ · ไปตึกคณะอื่นสอบไม่ได้
- ต่อยอด: ถ้าอยากได้ตึกชื่อคณะจริง 4 หลัง + ป้ายชื่อ ค่อยเพิ่ม (ตอนนี้ reuse ตึกเดิม 8 หลัง)

### Phase C — QoL + ยกเครื่อง UI/UX + จูนระบบ (2026-08-04)
- **ฟีเจอร์ตาม storyboard PDF ที่ขาด**:
  - **กระโดด (Space)** ([Player/PlayerMovement.cs](Assets/_Project/Scripts/Player/PlayerMovement.cs))
  - **โทรศัพท์ (TAB)** ([UI/PhoneController.cs](Assets/_Project/Scripts/UI/PhoneController.cs) · [Editor/M12PhoneBuilder.cs](Assets/_Project/Scripts/Editor/M12PhoneBuilder.cs))
  - **Minimap (กด M เปิด/ปิด)** ([UI/MinimapFollow.cs](Assets/_Project/Scripts/UI/MinimapFollow.cs) · [UI/MinimapToggle.cs](Assets/_Project/Scripts/UI/MinimapToggle.cs) · [Editor/M13MinimapBuilder.cs](Assets/_Project/Scripts/Editor/M13MinimapBuilder.cs)) — กล้อง top-down ตามตัว + หมุนตามกล้องหลัก (ทิศตรงกับฉาก)
- **โทรศัพท์เป็นแอปจริง**: หน้าโฮม + 5 แอป (สถานะ/ปฏิทิน/ภารกิจ/เกรด/แผนที่) · แตะแอป+ปุ่มย้อนกลับ · **สไลด์สลับหน้า** (unscaled) · **แผนที่โชว์ใหญ่ในเครื่อง** (RawImage รับ MinimapRT, เปิดกล้องเฉพาะตอนดู)
  - ดีไซน์หรู: ขอบทอง + เงาลอย + การ์ดไล่เฉด (วาด sprite เอง) + วงไอคอนฝ้า + **wallpaper จอ** (ไล่เฉด/ใส่รูปเองที่ `Art/UI/phone_wallpaper.png`)
- **ฟอนต์ Mitr** ([Editor/M14FontTool.cs](Assets/_Project/Scripts/Editor/M14FontTool.cs)): สร้าง Mitr SDF (Dynamic รองรับไทย) + เปลี่ยนฟอนต์ทั้ง 2 ฉากคลิกเดียว · อัปเดต 11 builder ให้ใช้ Mitr
- **Kenney UI skin** ([Editor/M15KenneySkin.cs](Assets/_Project/Scripts/Editor/M15KenneySkin.cs)): สลับกรอบ/ปุ่ม (Type=Sliced) เป็นสไตล์ Kenney โค้งมน — ไม่แตะบาร์ (Filled)
- **ไอคอนสถานะวาดเอง** ([Editor/M16StatIcons.cs](Assets/_Project/Scripts/Editor/M16StatIcons.cs)): ⚡สายฟ้า/❤️หัวใจ/🍴ส้อม (vector→PNG) วางทับวงกลมบาร์ (Kenney ไม่มีไอคอนพวกนี้)
- **แก้บั๊ก emoji เป็นกล่อง □**: Mitr ไม่มี glyph emoji → เอา emoji ออกจากข้อความในเกมทั้งหมด (phone/event/quest/exam/sleep/endscreen) · emoji ในกล่อง dialog editor ไม่กระทบ
- **HUD ธีมดำ-ทอง หรูหรา** ([Editor/M3HudBuilder.cs](Assets/_Project/Scripts/Editor/M3HudBuilder.cs)): แผงครามทึบ (อ่านง่าย) + นาฬิกาชิปทอง + บาร์สีการ์ตูนสด + ตัวหนังสือใหญ่ขึ้น · panel ภารกิจเข้าธีม
- **เวลาเดินเหมือนนาฬิกาจริง**: `gameMinutesPerRealSecond` 20→**1** (ติ๊กทีละนาที) — จบวันด้วยการ **"นอน"** (ไม่ต้องรอเวลาครบ)
- **บันทึกคณะลงเซฟ** (แก้บั๊ก): [SaveData](Assets/_Project/Scripts/SaveLoad/SaveData.cs) + `facultyIndex` · [SaveManager](Assets/_Project/Scripts/SaveLoad/SaveManager.cs) เซฟ/โหลด → "เล่นต่อ" คณะไม่รีเซ็ตเป็น IT อีก
- **จูนบาลานซ์** ([Editor/M17BalanceTune.cs](Assets/_Project/Scripts/Editor/M17BalanceTune.cs) คลิกเดียว): หิว 0.5→0.12, เหนื่อย 0.2→0.05, เดิน 0.6→0.15, วิ่ง 1.4→0.4 (ให้พอดีเวลาช้า) + ตั้งเวลา=1 ให้ด้วย

### Phase C — เหตุการณ์แบบเกมจริง + ระบบเสียง (2026-08-04)
- **สุ่มเหตุการณ์ = เกมจริง (A+B)** ([Systems/EventManager.cs](Assets/_Project/Scripts/Systems/EventManager.cs)) — 3 ชนิด:
  - **Instant** — ป็อปอัป → ทำท่า → บวก/ลบค่า
  - **Effect (B)** ([Player/PlayerEffects.cs](Assets/_Project/Scripts/Player/PlayerEffects.cs)) — ผลทั้งวัน หายเมื่อนอน: 🤧ป่วย (เดินช้า+เหนื่อยเร็ว, ผูก PlayerMovement+StatDecay) · 🔥ไฟแรง (เข้าเรียนความรู้ ×1.5, ผูก ClassStation)
  - **GoTo (A)** — เลือก "ไป..." → เกิด **เสาแสง** ปักที่อาคารจริง (อ่านตำแหน่งจาก Door_*) → เดินไปถึง (3 ม.) → ทำท่า → ได้ผล · ข้ามวัน = พลาด
- **หมุดภารกิจมืออาชีพ**:
  - [Systems/BeaconFX.cs](Assets/_Project/Scripts/Systems/BeaconFX.cs) — เสาแสงเด้งขึ้นลง + วงแสงพื้นพัลส์/หมุน + เรืองแสง (URP emission)
  - [UI/ObjectiveHUD.cs](Assets/_Project/Scripts/UI/ObjectiveHUD.cs) — แถบภารกิจค้างบนจอ + **ลูกศรทองหมุนชี้ทาง** (คำนวณทิศบนจอ) + บอกระยะ (ม.) · ลูกศรวาดเอง (สามเหลี่ยม→PNG ใน M9)
  - คลังเหตุการณ์ผสม 8 แบบ (GoTo: กินข้าว→โรงอาหาร, ชีทเก่า→ห้องสมุด, ชมรม→อาคารชมรม)
- **ระบบเสียงครบวงจร** — สังเคราะห์เสียงเอง (WAV) ไม่ต้องโหลด:
  - [Core/SFXManager.cs](Assets/_Project/Scripts/Core/SFXManager.cs) — SFX one-shot + เพลง loop · volume จาก vol_sfx/vol_music (Master ผ่าน AudioListener) · ได้เงิน=เหรียญอัตโนมัติ
  - [UI/ButtonSound.cs](Assets/_Project/Scripts/UI/ButtonSound.cs) — เสียงคลิกทุกปุ่ม (Editor แปะให้อัตโนมัติ)
  - [Editor/M18AudioBuilder.cs](Assets/_Project/Scripts/Editor/M18AudioBuilder.cs) — สังเคราะห์ 9 SFX + เพลง ambient (chord pad สแนปให้วนเนียน) · สร้าง AudioManager 2 ฉาก · **ใช้ไฟล์ที่โหลดเองก่อนได้** (Audio/custom/&lt;ชื่อ&gt;.wav/ogg/mp3 → ไม่มีค่อยสังเคราะห์)
  - Hook: Toast→แจ้งเตือน · เดิน→ฝีเท้า · Space→กระโดด · สอบผ่าน/ตก→สำเร็จ/ผิด · นอน→เสียงนอน
- บทเรียน: ฟอนต์ Mitr ไม่มี emoji → เลี่ยง emoji ในข้อความในเกม (ใช้รูปวาด PNG แทน เช่นไอคอนสถานะ/ลูกศร)

### แก้บั๊ก + เก็บงาน (2026-08-05)
- 🐛 **ปุ่มเลือกคณะกดแล้วไม่เข้าเกม** — เหตุ: ต่อ `onClick` แบบ lambda ตอน Editor ซึ่ง Unity **ไม่เซฟลงฉาก** (ต่างจากปุ่มเมนูอื่นที่ต่อตอน runtime) → แก้ด้วย [UI/FacultyButton.cs](Assets/_Project/Scripts/UI/FacultyButton.cs) (ต่อสายตอน runtime, index≥0=เลือกคณะ / <0=ย้อนกลับ) + แก้ M11 ให้ AddComponent แทน lambda
- 📐 **หน้าเลือกคณะล้นจอ** — จัด layout กระชับ (startY 150, gap 92, ปุ่ม 520×74) พอดีจอทุกอัตราส่วน
- 🎧 **ใส่เสียงจริง** — copy จาก Kenney (Interface/RPG/Digital) + Pixabay (`the_mountain-life-story`) → `Assets/_Project/Audio/custom/` ตั้งชื่อตามช่อง (click/notify/coin/success/error/footstep/jump/sleep/whoosh/music) → Build Audio จับใช้แทนเสียงสังเคราะห์
- 🧹 **กล่องสถานีเก่าลอยกลางแมป** (ห้องเรียน/โรงอาหาร/หอพัก จาก M5) → [Editor/M19CleanStations.cs](Assets/_Project/Scripts/Editor/M19CleanStations.cs): ย้ายไปหน้าตึกจริง + ซ่อน mesh + collider เป็น trigger (ยังเข้าเรียน/กิน/นอนได้ แต่ไม่เห็นกล่อง)
- บทเรียนสำคัญ: **listener แบบ lambda/anonymous ที่เพิ่มใน Editor ไม่ถูก serialize** — ปุ่มที่สร้างด้วย Editor tool ต้องต่อสายตอน runtime (component) หรือใช้ `UnityEventTools.AddPersistentListener`

### ท่าทาง Mixamo — กิน/เรียน/นอน (สุ่มท่า) (2026-08-05)
- **นำเข้าท่า Mixamo 9 ท่า** ที่ `Art/Characters/` (Typing, Drinking, Sitting Drinking, Laying Sleeping, Sleeping Idle, Cheering, Talking, Waving, Laying Severe Cough)
- **[Editor/M20CharacterAnims.cs](Assets/_Project/Scripts/Editor/M20CharacterAnims.cs)** (Nisit ▸ Setup Character Animations): ตั้ง import เป็น Humanoid + CopyFromOther(Ch29 avatar) + loopTime · เพิ่ม state เข้า `PlayerAnimator`/`NisitCharacter` controller · ต่อโรงอาหารให้สุ่มท่ากิน
- **[Player/PlayerActionController.cs](Assets/_Project/Scripts/Player/PlayerActionController.cs)** — เพิ่ม `PerformState(dur, onDone, params string[] states)`: CrossFade ไป state (สุ่มถ้าหลายท่า) แล้วกลับ locomotion ด้วย `baseHash` ที่จับไว้ (ไม่ต้องรู้ชื่อ state เดิม) · `HasState` กันพังถ้าไม่มีท่า
- ต่อเข้ากิจกรรม: เรียน/ทำงาน → **Typing** · กิน → **สุ่ม** Eating_A/B · นอน → **สุ่ม** Sleeping_A/B (SleepStation เล่นท่านอน 2 วิ ก่อนข้ามวัน)
- ท่าเสริมพร้อมใช้ (ยังไม่ต่อ): Cheering (จบ) · Coughing (ป่วย) · Talking/Waving (NPC)
- แนวคิด: state แบบ **CrossFade by name** (ไม่ผูก bool param/transition) → เพิ่มท่าใหม่ง่าย + สุ่มได้

### เอกสารปริญญานิพนธ์ + กระเป๋า + ปุ่มเล่นหลายคน (2026-08-06)
- 📄 **เอกสารปริญญานิพนธ์ฉบับเต็ม** (บท 1-5 + สารบัญ/ตาราง/ภาพ + อ้างอิง + ภาคผนวก) → `ปริญญานิพนธ์_ฉบับเต็ม.md` + แปลงเป็น **Word (.docx)** จัดรูปแบบเสร็จ (หัวข้อ/ตาราง/ตัวหนา ฟอนต์ TH Sarabun) ด้วยสคริปต์ `md2docx.py` (python-docx)
  - ยึดเนื้อหา ปพ.1 เป็นหลัก · ระบบที่ยังไม่เสร็จ (multiplayer/customization/trading) คงไว้เป็น "แผนพัฒนาต่อ" (บท 5.3) · จุดแทรกภาพจริงทุกจุด
  - ข้อมูลจากโค้ดจริง: **4 คณะ** (IT/บริหาร/วิทย์/นิเทศ, [FacultyCatalog.cs](Assets/_Project/Scripts/Systems/FacultyCatalog.cs)) · เงื่อนไขจบ = Graduated(ครบปี 4)/Flunked(ความรู้ไม่ถึงเป้า)/Died(พลังงาน/สุขภาพหมด, [EndReason.cs](Assets/_Project/Scripts/Core/EndReason.cs))
  - **หมายเหตุ:** Claude for Microsoft 365 add-in ใช้แก้เอกสารในหน้า Word ได้ (ล็อกอินบัญชี Claude) — คู่กับที่ผมสร้าง .docx ให้
- 🔊 **Voice + Ambient Volume** — เพิ่ม 2 แถบเสียงในหน้าตั้งค่า (รวมเป็น 5: Master/Music/SFX/Voice/Ambient) · [UI/SettingsController.cs](Assets/_Project/Scripts/UI/SettingsController.cs) เก็บ `vol_voice`/`vol_ambient` ใน PlayerPrefs · [M4MenuBuilder.cs](Assets/_Project/Scripts/Editor/M4MenuBuilder.cs) สร้างแถบเพิ่ม (แพตเทิร์น PlayerPrefs ไม่ใช่ AudioMixer)
- 🎒 **ระบบกระเป๋า Inventory (เวอร์ชันย่อ)** — ปิดแถว "กระเป๋า" ในตาราง 3.2 + ยกออกจากแผนพัฒนาต่อ:
  - [Systems/InventoryManager.cs](Assets/_Project/Scripts/Systems/InventoryManager.cs) — singleton, **ใช้ ShopItem ซ้ำเป็นข้อมูลไอเทม** (ไม่ต้องสร้าง SO ใหม่) · Add/Use/นับ · OnChanged event · save/load เป็นชื่อไอเทม
  - [UI/InventoryUI.cs](Assets/_Project/Scripts/UI/InventoryUI.cs) — เปิดด้วยปุ่ม **I** · กริดช่อง · คลิกไอเทม→ใช้ (บวกค่าสถานะ) · [UI/InventoryCloseButton.cs](Assets/_Project/Scripts/UI/InventoryCloseButton.cs) (runtime-wire ปุ่มปิด)
  - [Editor/M21InventoryBuilder.cs](Assets/_Project/Scripts/Editor/M21InventoryBuilder.cs) (Nisit ▸ Build Inventory) — สร้างหน้าต่าง + ต่อทุกอย่าง + ตั้งร้านค้าให้ `storeToInventory`
  - [Systems/ShopController.cs](Assets/_Project/Scripts/Systems/ShopController.cs) — เพิ่มโหมด `storeToInventory` (ร้านค้า=เก็บเข้ากระเป๋า / โรงอาหาร=กินทันที) · [SaveLoad/SaveManager.cs](Assets/_Project/Scripts/SaveLoad/SaveManager.cs) บันทึก/โหลดไอเทม (ช่อง `inventoryItemIds` มีอยู่แล้วใน SaveData)
- 🕹️ **ปุ่ม "เล่นหลายคน" (แบบโชว์)** — เมนูเป็น 5 ปุ่ม · กดปุ่มทอง → ป็อปอัป **"กำลังพัฒนา"** (ตรง storyboard ปพ.1 แต่ยังไม่ต่อ Netcode) · [UI/MainMenuController.cs](Assets/_Project/Scripts/UI/MainMenuController.cs) + M4MenuBuilder + MenuPolish (จัด 5 ปุ่ม/parallax/intro)
- 🖼️ **แก้เมนูหาย/พื้นทึบ** — พบว่า **Build M4 Menu สร้างเมนูใหม่หมด → ลบงาน Polish/พื้นหลังทิ้ง** ทุกครั้ง → ต้องกดเรียง: Build M4 Menu → Polish Menu Layout → Day Campus Background (หรือ Use 3D Background)
- บทเรียน: `InventoryManager` reuse `ShopItem` ลดงานเยอะ · ห้ามเขียนโค้ด Netcode ก่อนติดตั้ง package (ทั้งโปรเจกต์จะคอมไพล์ไม่ผ่าน)

### ธีมการ์ตูนพาสเทล + ตัวช่วยแคป + ปุ่มรวมกดครั้งเดียว (2026-08-06)
- 📸 **ScreenshotHelper** ([Core/ScreenshotHelper.cs](Assets/_Project/Scripts/Core/ScreenshotHelper.cs) + [Editor/M22ScreenshotHelper.cs](Assets/_Project/Scripts/Editor/M22ScreenshotHelper.cs), Nisit ▸ Add Screenshot Helper) — สำหรับแคปรูปทำเอกสาร: **F9** แคป (เซฟที่ `Screenshots/`, แถบช่วยจำไม่ติดในรูป) · **F5/F6/F7** เรียกหน้าจบ (จบ/ตาย/ตก) · **F8** เปิดหน้าสอบ (แข็งแรง + บอกเหตุผลใน Console) · แคปได้แล้ว 7 หน้า
- 🎨 **ยกเครื่อง UI เป็นธีมการ์ตูนพาสเทล** ทุกหน้าเข้าชุดกัน:
  - โทนพาสเทล (มินต์/พีช/ฟ้า/ลาเวนเดอร์/ชมพู) + **ตัวอักษรกรมเข้ม** (อ่านชัดกว่าขาวบนพาสเทล) + **ขอบดำหนา 5px** (สไตล์การ์ตูน) + ปุ่มลูกกวาด (ฐานหนา+เงาวาว+tint hover)
  - ครอบคลุม: เมนู ([MenuPolish](Assets/_Project/Scripts/Editor/MenuPolish.cs)) · เลือกคณะ ([M11](Assets/_Project/Scripts/Editor/M11FacultyBuilder.cs) — การ์ดกลางทึบ) · สอบ ([M6](Assets/_Project/Scripts/Editor/M6ExamBuilder.cs)) · ตั้งค่า/ป็อปอัป ([M4](Assets/_Project/Scripts/Editor/M4MenuBuilder.cs) — dim+การ์ด, แถบเสียง 5 ตัวสไตล์ใหม่) · กระเป๋า ([M21](Assets/_Project/Scripts/Editor/M21InventoryBuilder.cs)) · หน้าจบเกม ([M3HudBuilder](Assets/_Project/Scripts/Editor/M3HudBuilder.cs))
- 🌟 **★ Rebuild All UI (กดครั้งเดียว)** ([Editor/M23RebuildAll.cs](Assets/_Project/Scripts/Editor/M23RebuildAll.cs)) — สร้าง/อัปเดต UI ทั้งเกมในคลิกเดียว (HUD+จบ → สอบ → กระเป๋า → เมนู+คณะ+พื้นหลัง) · เพิ่ม `SuppressDialog` ทุก builder (M3/M4/M6/M11/M21/MenuPolish) เพื่อปิด popup ระหว่าง chain → เด้ง dialog สรุปครั้งเดียว
- 🔧 **แก้เชนเมนูให้ครบ** — Build M4 Menu **auto-chain** Polish + Day Campus Background + Faculty Select อัตโนมัติ (จบปัญหา "เมนูโล้น/คณะหาย/พื้นทึบ" ที่ต้องไล่กดหลาย tool)
- 🐛 **แก้บั๊กของไม่เข้ากระเป๋า** — เหตุ: M21 ไม่ได้ `SaveScene` → พอ tool อื่นเปิดฉากใหม่จากดิสก์ InventoryManager+flag หลุด → แก้ให้ M21 **เซฟทันที** + ตั้ง `storeToInventory` บนทุก ShopController ที่ไม่ใช่โรงอาหาร (หาแบบรวม inactive) + ShopController หา InventoryManager กันพลาด (สร้างใหม่ถ้าไม่มี)
- 🖼️ **แก้ layout หน้าจบเกม** — คะแนน 4 บรรทัด (คณะ/ชั้นปี/GPA/รวม) ล้นกล่องเตี้ย → ทับข้อความ+ปุ่ม → ขยายการ์ด (520→660) + กล่องคะแนน 200px + จัดระยะ หัวเรื่อง/ข้อความ/คะแนน/ปุ่ม (ใช้ร่วม 3 หน้าจบ)
- 🧹 **แก้ Canvas ปนข้ามฉาก** — พบเมนู+HUD+หน้าจบโผล่พร้อมกัน (Canvas หลงข้ามฉาก) → M4 ลบ Canvas เกม (HUD/End/Exam/Inventory/Shop/…) ที่หลงเข้าฉากเมนู · M3 ลบ Menu Canvas ที่หลงเข้าฉากเกม → หน้าจบต้องแคปในฉาก **01_Gameplay** เท่านั้น (มี GameManager/End Canvas)
- บทเรียนสำคัญ: **UI ที่ Editor tool สร้าง = "อบ" ลงฉากแล้ว** → แก้โค้ด builder เฉยๆ ไม่เปลี่ยนของเก่า **ต้องกด tool สร้างใหม่เสมอ** · child ของปุ่ม render บนตัวปุ่มเสมอ (base shadow ต้องเป็น sibling ไม่ใช่ child) · warning (เหลือง) ≠ error (แดง) — warning ไม่บล็อกคอมไพล์ · หน้าจบ/สอบ/GameManager อยู่ในฉากเกม (01_Gameplay) ไม่ใช่ฉากเมนู

### เหตุการณ์ต่อเนื่อง (ไม่มีป็อปอัพ) + NPC/สัตว์ (2026-08-20)
- 🎲 **เพิ่มเหตุการณ์เป็น 17 อัน** ([Systems/EventManager.cs](Assets/_Project/Scripts/Systems/EventManager.cs)) — GoTo 7 (เดินไปทำที่ อาคารเรียน/บริหาร/ห้องสมุด/โรงอาหาร/ร้านค้า/ชมรม/หอพัก) · Effect 4 · Instant 6
- 🎬 **โหมดไม่มีป็อปอัพ (เกมต่อเนื่อง)** — `Trigger()` แยกทาง: **GoTo** → โผล่เสาแสง+แจ้งเตือนในโลกเลย เดินไปเก็บเอง/ปล่อยผ่านได้ · **Effect** → แจ้งเตือน(เล่าเหตุการณ์)+ติดผลทั้งวันทันที · **Instant ปุ่มเดียว** → แจ้งเตือน+ได้ผลเลย · เหลือป็อปอัพเฉพาะ Instant ที่**เลือกได้จริง** (16/17 ลื่นไม่เด้ง) · helper `FindGoTo`/`FindEffect`
- 👥 **NPC คุยได้** ([Interaction/TalkNPC.cs](Assets/_Project/Scripts/Interaction/TalkNPC.cs) + [Editor/M26TalkNPCs.cs](Assets/_Project/Scripts/Editor/M26TalkNPCs.cs), Nisit ▸ Build Talk NPCs) — เข้าใกล้ **โบกมือ (Waving)** · กด E **คุย (Talking)** สุ่มบทพูด + พอใจ +5 (cooldown) · **ยืนคุยหน้าตึกจริง 4** (หาประตูอัตโนมัติ) + **เดินไปมาคุยได้ 3** (reuse `MenuNPCWalker`, หยุดเดินตอนทำท่า) · ใช้ท่า Waving/Talking ที่ [M20](Assets/_Project/Scripts/Editor/M20CharacterAnims.cs) โหลดไว้แล้ว
- 🎨 **ย้อมสี NPC + รองรับหลายโมเดล** ([Interaction/CharacterTint.cs](Assets/_Project/Scripts/Interaction/CharacterTint.cs)) — ย้อม 7 โทนด้วย MaterialPropertyBlock (ไม่แตะ material asset) → แม้โมเดลเดียวก็ดูไม่ซ้ำ · M26 **สแกนโฟลเดอร์ Characters ทุกโมเดลอัตโนมัติ** (วาง Mixamo เพิ่ม = หลากหลายทันที, ต้องตั้ง Rig=Humanoid)
- 🐾 **ระบบสัตว์ (พร้อมรอโมเดล)** ([Interaction/PetAnimal.cs](Assets/_Project/Scripts/Interaction/PetAnimal.cs) + [Editor/M27AnimalBuilder.cs](Assets/_Project/Scripts/Editor/M27AnimalBuilder.cs), Nisit ▸ Build Animals) — เดินไปมา + กด E ลูบ (พอใจ +6) · อ่านโมเดลจาก `Art/Models/Animals/` (ว่าง = แจ้งให้ไปวางก่อน ไม่พัง) · เดาชื่อไทยจากชื่อไฟล์ (cat→แมว ฯลฯ) · แนะนำ Quaternius/KayKit (ฟรี CC0)
- ⚙️ **★ Rebuild All** ([M23](Assets/_Project/Scripts/Editor/M23RebuildAll.cs)) รวม M26 (NPC) + M27 (สัตว์) เข้าคลิกเดียวแล้ว
- หมายเหตุ: EventManager เป็น logic → apply ตอน Play · NPC/สัตว์ ถูก "อบ" ลงฉาก → **ต้องรัน tool** (Build Talk NPCs / Build Animals / ★ Rebuild All)

### ตัวละครหลากหลาย + แยกบทบาท + สัตว์ Quaternius + ปุ่มเดียวจบ (2026-09-04)
- 🧍 **โมเดลคน 12 ตัว** — เพิ่ม Mixamo ซีรีส์ Ch## (Ch06/07/12/21/22/23/27/33/41/46 + Remy) เข้า `Art/Characters/` (สไตล์การ์ตูนเข้าชุดกับ Ch29)
- 🔧 **M28SetupNPCModels** ([Editor/M28SetupNPCModels.cs](Assets/_Project/Scripts/Editor/M28SetupNPCModels.cs), Nisit ▸ Setup NPC Models) — โมเดล Mixamo โหลดมาเป็น **Generic** → แปลงเป็น **Humanoid** (CreateFromThisModel) ทั้งโฟลเดอร์ทีเดียว (ไม่งั้นเล่นท่า Waving/Talking ไม่ได้)
- 👨‍🏫 **แยกบทบาทอาจารย์ vs นักเรียน** ([M26](Assets/_Project/Scripts/Editor/M26TalkNPCs.cs)) — บุคลากร (อาจารย์/บรรณารักษ์): ตัวใหญ่ ×1.10 + โทนเทาสุภาพ + จองโมเดลผู้ใหญ่ (Remy/teacher/prof) อัตโนมัติ · นักเรียน: พาสเทลสดใส + เดินไปมา · M26 สแกนโมเดล Characters ทุกตัว + ย้อมสีไม่ซ้ำ
- 🐾 **สัตว์ Quaternius (Ultimate Animated Animals)** — ก๊อป Deer/Fox/Husky/ShibaInu เข้า `Art/Models/Animals/` · [M27](Assets/_Project/Scripts/Editor/M27AnimalBuilder.cs) **สร้าง AnimatorController อัตโนมัติ** (อ่านคลิป Idle/Walk ในไฟล์ → state machine Speed-driven + ตั้ง loop + ปิด root motion) → สัตว์เดินมีท่าจริง · ชื่อไทยจากชื่อไฟล์ (Husky→หมา)
- 🐕 **"ตัวเดียวหลายตัว"** — เปลี่ยน M27 เป็น `plan` ระบุประชากรได้ (หมา 3 ตัว + จิ้งจอก + กวาง) · `SectorRoute()` กระจายเป็นวงรอบแมพ แต่ละตัวลาดตระเวนคนละมุม
- ⚙️ **★ Rebuild All = ปุ่มเดียวจบทุกอย่าง** — เพิ่ม M28 (Humanoid) + M20 (ท่าตัวละคร) เข้าลูกโซ่ (เพิ่ม `SuppressDialog` ให้ทั้งคู่) → กดครั้งเดียว: แปลง Humanoid → ท่า → UI → NPC(อาจารย์/นักเรียน) → สัตว์ → เมนู
- สถานะ NPC ปัจจุบัน: เดิน + โบกมือ + คุย(สุ่มบทพูด +พอใจ) · **ยังไม่มี**: ทำกิจกรรม/ให้ภารกิจ/ขายของ/มองตาม (เป็นงานต่อไปที่เสนอไว้)

### Phase 1 — พฤติกรรม NPC ครบชุด + Phase 2 — จูน SP ให้นิ่ง (2026-09-07)
- 🎭 **NPC พฤติกรรมครบ 4 อย่าง** ([Interaction/TalkNPC.cs](Assets/_Project/Scripts/Interaction/TalkNPC.cs)):
  - **A) กิจกรรม idle** — ตัวยืนสุ่มทำท่าเป็นระยะ (อาจารย์→Talking · นักเรียน→Talking/Waving/Cheering)
  - **D) มองตามผู้เล่น** — ตัวยืนหันหน้าตามเวลาเข้าใกล้ (Slerp นุ่มนวล)
  - **B) ให้ภารกิจ (quest-giver)** — คุยรุ่นพี่/อาจารย์ → ปักหมุดเดินไปทำ (reuse `EventManager.StartObjectiveExternal()` ใหม่) → ได้รางวัล
  - **C) เปิดร้าน (vendor)** — คุยแม่ค้า → เปิด `ShopController` (เลือก shop/cafeteria จาก `storeToInventory`) · ตั้งบทบาทใน [M26](Assets/_Project/Scripts/Editor/M26TalkNPCs.cs) `ConfigRole()`
- 🌐 **MP tech = Unity Netcode for GameObjects (NGO)** — เคลียร์ความขัดแย้งในเอกสาร (เดิม Netcode vs Photon) → ยึด NGO
- 🔧 **Phase 2 — แก้บั๊ก/จุดเปราะ 4 จุด (จากผลสแกน)**:
  1. [PlayerMovement.cs](Assets/_Project/Scripts/Player/PlayerMovement.cs) — กัน NRE ถ้า `cameraTransform` null (หากล้องใหม่/ข้ามเฟรม)
  2. [PlayerInteraction.cs](Assets/_Project/Scripts/Player/PlayerInteraction.cs) — ไม่รับ E ตอน `timeScale=0` (กันหน้าต่างซ้อน) + **NPC/สัตว์ +1.2m** ในการเลือกเป้า (กันบังประตู/ห้องสอบ)
  3. [EventManager.cs](Assets/_Project/Scripts/Systems/EventManager.cs) — เลื่อนเหตุการณ์ถ้าผู้เล่นติดหน้าต่าง (movement ปิด) + **Esc ปิดป็อปอัพ** (failsafe กันค้าง)
  4. [ExamController.cs](Assets/_Project/Scripts/Systems/ExamController.cs) — กัน save-scum สอบซ้ำ: จำ "สอบเสร็จแล้ว" (`done` mark ตอน Finish แทน offer) + เซฟลง [SaveData](Assets/_Project/Scripts/SaveLoad/SaveData.cs).`doneExams` (คืนก่อน `RestoreState`)
- ยังเหลือ (บรรเทาแล้ว/ผลกระทบต่ำ): modal manager กลาง (1A) · action ค้างถ้าตายกลางท่า (1D) · quest/objective ไม่เซฟ

### Phase 3 — Multiplayer MP-1 (Netcode for GameObjects) (2026-09-10)
- 🌐 **ลง package** `com.unity.netcode.gameobjects` (NGO) — ยึด NGO เป็น tech หลัก (ไม่ใช่ Photon)
- 🎯 **แนวทาง: เสริมทับ SP ไม่แตะระบบเดิม** — Player ในฉาก = ตัวที่เล่น (ทุกระบบ SP ทำงานปกติ) · เพิ่ม "อวตารเครือข่าย" เป็นหุ่นเงาที่ลอกท่าไป broadcast (แก้ปัญหา `GameObject.Find("Player")` พังใน networked-spawn โดยไม่ต้อง refactor SP)
- 🕹️ **NetworkAvatar** ([Net/NetworkAvatar.cs](Assets/_Project/Scripts/Net/NetworkAvatar.cs)) — `NetworkBehaviour` sync ตำแหน่ง/ทิศ/Speed ผ่าน `NetworkVariable` (owner-write) · owner: ลอกจาก "Player" ในฉาก + ซ่อนตัวเอง + ปิด animator · remote: interpolate เดินตาม + เล่นท่า (Speed) · ปิด root motion กันไถล
- 🔘 **NetworkUI** ([Net/NetworkUI.cs](Assets/_Project/Scripts/Net/NetworkUI.cs)) — ปุ่ม Host/Join/ออก (F3 เปิดแผง) + เช็ก PlayerPrefab ว่าง (เตือนชัด)
- 🛠️ **M29NetworkSetup** ([Editor/M29NetworkSetup.cs](Assets/_Project/Scripts/Editor/M29NetworkSetup.cs), Nisit ▸ Setup Multiplayer (MP-1)) — สร้าง NetworkAvatar.prefab (NetworkObject+Animator) + NetworkManager(UnityTransport 127.0.0.1) + UI ปุ่ม อัตโนมัติ
- ทดสอบ: Window ▸ Multiplayer Play Mode (2 หน้าต่าง) → Host + Join → เห็นกันเดิน · ยังไม่ได้รันจริง (รอผู้ใช้เทสต์)
- Roadmap: MP-1 เห็นกันเดิน ✅(code) · MP-2 ป้ายชื่อ+ท่า · MP-3 แชท+เทรด

### MP-2/3 + จูน NPC/สัตว์/เสียง (ทดสอบ 2 หน้าต่างผ่าน) (2026-09-22)
- ✅ **ทดสอบ MP จริงผ่าน** (Multiplayer Play Mode 2 หน้าต่าง Host+Join) — เห็นกันเดิน + ป้ายชื่อ "Player 1/2" ถูกต้อง
- 🏷️ **MP-2 ป้ายชื่อ** ([Net/NetworkAvatar.cs](Assets/_Project/Scripts/Net/NetworkAvatar.cs)) — TextMeshPro 3D ลอยหัวผู้เล่นอื่น + billboard หันเข้ากล้อง
- 🙋 **MP-2 อีโมทซิงก์** — กด Z โบก · X เชียร์ · C ทักทาย (NetworkVariable encode seq×10+kind) → เห็นทั้งตัวเอง (PlayerActionController) + ผู้เล่นอื่น (CrossFade)
- 💬 **MP-3 แชทด่วน** ([Net/ChatRelay.cs](Assets/_Project/Scripts/Net/ChatRelay.cs) NetworkBehaviour บน avatar + [Net/ChatUI.cs](Assets/_Project/Scripts/Net/ChatUI.cs)) — กด Y เปิดแผงข้อความสำเร็จรูป → ServerRpc→ClientRpc กระจายทุกคน (FixedString512) · M29 สร้าง UI ให้
- 🐾 **แก้สัตว์ใหญ่** ([M27](Assets/_Project/Scripts/Editor/M27AnimalBuilder.cs)) — **auto-scale** วัด Renderer bounds แล้วย่อให้สูงตามเป้า (หมา 0.6 · จิ้งจอก 0.5 · กวาง 1.3m) — โมเดล Quaternius base ใหญ่มาก
- 🧍 **แก้ NPC ใหญ่ไม่เท่ากัน** ([M26](Assets/_Project/Scripts/Editor/M26TalkNPCs.cs)) — auto-scale เท่าความสูงผู้เล่น (โมเดล Mixamo แต่ละตัว base ต่างกัน) · เอา staff ×1.10 ออก
- 👕 **แก้ NPC สีเทา (ไม่มีชุด)** ([FixCharacterMaterials.cs](Assets/_Project/Scripts/Editor/FixCharacterMaterials.cs)) — เดิมแก้แค่ตัวแรก (มี break) → **แก้ให้แตก texture+material ครบทุกตัว**
- 🐌 **แก้เดินวนถี่ไป** — NPC/สัตว์ เดินช้าลง + `pauseTime` ยาวขึ้น (ยืนพัก 2.5-10 วิ)
- 🦶 **แก้เสียงเดินไม่ตรงก้าว** ([PlayerMovement.cs](Assets/_Project/Scripts/Player/PlayerMovement.cs)) — เลิกใช้ timer ตายตัว → **ตรวจกระดูกเท้า (Humanoid LeftFoot/RightFoot) จุดต่ำสุด = แตะพื้น** เล่นเสียงตรงจังหวะจริงทุกความเร็ว · ลดความดัง 0.45→0.28
- ⚠️ เจอ (ต้องรัน Build Audio): คลิป fanfare/eat/page ยังไม่ถูกสร้าง (เพิ่มโค้ดแล้วแต่ยังไม่ได้ rebuild เสียง)
- เหลือ: MP-3+ เทรดไอเทม · รัน Build Audio (fanfare/eat/page)

### ยกเครื่องกราฟิก + ฟอนต์การ์ตูน + Build .exe (2026-09-22)
- 🖼️ **แก้ภาพแตก/ไม่คม**: URP asset เปิด **MSAA 4x** + Mobile render scale 0.8→1 ([PC/Mobile_RPAsset](Assets/Settings/)) · พบว่าที่เห็นแตกในเอดิเตอร์ส่วนใหญ่คือ Game view Scale 1.3x (พรีวิว) ไม่ใช่ของจริง
- 🔤 **แก้ตัวหนังสือเบลอ**: ฟอนต์ไทยถูกเบคเป็น **SMOOTH (บิตแมป, RenderMode 4165)** ไม่ใช่ SDF → [M31FixFonts](Assets/_Project/Scripts/Editor/M31FixFonts.cs) เปลี่ยนทุกฟอนต์เป็น **SDFAA (4169)** คมทุกขนาด
- 🎨 **ฟอนต์การ์ตูน Pattaya**: [M32CartoonFont](Assets/_Project/Scripts/Editor/M32CartoonFont.cs) สร้าง Pattaya SDF (SDFAA ชัดเจน + Dynamic ไทยครบ) จาก TTF + สลับทุก TMP_Text ทั้ง 2 ฉาก + ตั้งเป็น TMP default · หมายเหตุ: `CreateFontAsset(src)` แบบไม่ระบุ mode = SMOOTH → ต้องระบุ SDFAA_HINTED เอง
- 📷 **กล้อง**: pitchAngle 35→40 + orthoSize 7→6 ([IsometricCameraRig](Assets/_Project/Scripts/CameraRig/IsometricCameraRig.cs)) แก้พื้นที่ว่างขอบล่างที่ 16:9 + เห็นตัวละครชัด
- 📺 **FHD**: Player Settings default 1024×768(4:3) → **1920×1080 (16:9)**
- 🏗️ **Build tool**: [M33BuildGame](Assets/_Project/Scripts/Editor/M33BuildGame.cs) — Nisit ▸ ★ Build Game (.exe) → StandaloneWindows64 → `Build/NisitSimulator.exe` · **build สำเร็จ เล่นได้จริง!** (HUD ครบ/คม/เต็มจอ)
- 🐾 จูนเพิ่ม: สัตว์ auto-scale (วัด bounds) + เดินช้า/พักนาน · NPC auto-scale เท่าผู้เล่น · FixCharacterMaterials แก้ครบทุกตัว (NPC มีชุด)

### MP-3+ เทรดไอเทม + LAN (IP) + เตรียม Relay (2026-09-22)
- 🤝 **เทรด/ให้ของ** ([Net/TradeRelay.cs](Assets/_Project/Scripts/Net/TradeRelay.cs) NetworkBehaviour บน avatar + [Net/TradeUI.cs](Assets/_Project/Scripts/Net/TradeUI.cs)) — กด G ใกล้ผู้เล่นอื่น → เลือกไอเทม → ServerRpc→ClientRpc(เฉพาะผู้รับ) → เข้ากระเป๋าเขา · InventoryManager เพิ่ม Resolve/AddByName/RemoveByName
- 🌐 **LAN**: [NetworkUI](Assets/_Project/Scripts/Net/NetworkUI.cs) เพิ่มช่องกรอก IP + `UnityTransport.SetConnectionData` (Host ฟัง 0.0.0.0 โชว์ LAN IP · Client ต่อตาม IP ที่กรอก) · M29 สร้างช่อง input
- 🔴 **Relay (ข้ามเน็ต) ยังไม่เสร็จ**: ลง `com.unity.services.multiplayer` แล้ว แต่เชื่อม Unity Cloud ไม่ได้บน **Wi-Fi มมส** (บล็อก services) → ทำต่อบน hotspot/เน็ตบ้าน แล้วเขียน Join-Code (Multiplayer Sessions API) · ดู memory [[multiplayer-relay-pending]]
- MP ครบ: เดิน/ป้าย/อีโมท(วงล้อ B)/แชท(Y)/เทรด(G)/LAN · เหลือ Relay อย่างเดียว

### MP-supporting: ปรับแต่งตัวละคร (ชื่อ+สี) + Co-op โบนัส (2026-09-23)
- 🎨 **ปรับแต่งตัวละคร sync** — [GameSession](Assets/_Project/Scripts/SaveLoad/GameSession.cs) เก็บ PlayerName/PlayerColor · [NetworkAvatar](Assets/_Project/Scripts/Net/NetworkAvatar.cs) sync ชื่อ (FixedString64) + สี (index พาเลตต์ 8 สี) ผ่าน NetworkVariable → ทาสีด้วย MaterialPropertyBlock + ป้ายชื่อแสดงชื่อจริง · owner ทาสี Player ในฉากของตัวเอง · เปลี่ยนสดได้ (`SetIdentity`) · [NetworkUI](Assets/_Project/Scripts/Net/NetworkUI.cs) เพิ่มช่องชื่อ + สวอตช์สี (M29 สร้าง UI)
- 🤝 **Co-op โบนัส** ([Net/CoopBonus.cs](Assets/_Project/Scripts/Net/CoopBonus.cs) บน NetworkManager) — อยู่ใกล้ผู้เล่นอื่น ~6m ทุก 8 วิ ได้ความรู้ +6/พอใจ +4 · คำนวณ client-local (ไม่ sync) · เฉพาะตอนเชื่อม MP
- แก้เพิ่ม: ป้ายชื่อใช้ชื่อจริงแทน "Player N" · ทุกอย่างเทสต์ได้ 2 หน้าต่าง (ไม่ต้องเน็ต)

### MP polish (แชทพิมพ์เอง + รายชื่อ) + แก้บั๊กจากสแกน QA (2026-09-23)
- 💬 **แชทพิมพ์เอง** ([Net/ChatUI.cs](Assets/_Project/Scripts/Net/ChatUI.cs)) — เพิ่มช่องพิมพ์ (Enter ส่ง) + ปุ่มสำเร็จรูป · หยุดเดินระหว่างพิมพ์ (disable PlayerMovement)
- 👥 **รายชื่อผู้เล่น** ([Net/PlayerListUI.cs](Assets/_Project/Scripts/Net/PlayerListUI.cs), F2) — อ่านจาก NetworkAvatar ที่ spawn (เห็นครบทุก client) + NetworkAvatar.DisplayName
- 🐛 **แก้บั๊กจากสแกน QA**:
  - 🔴 **เทรดของหาย** (critical): TradeRelay เปลี่ยนเป็น **ลบของเมื่อผู้รับ ack สำเร็จ** (ServerRpc→ผู้รับ→ผลกลับ→ผู้ให้ลบของ) · TradeUI ไม่ลบล่วงหน้า + เช็คระยะซ้ำตอนให้ + ไม่ toast สำเร็จก่อนยืนยัน
  - เทรด/แชท **หยุดเดินตอนเปิดแผง** (กัน WASD เลื่อนตัว)
  - NetworkUI: cache LocalIP + Refresh ทุก 0.5 วิ (เดิม DNS lookup ทุกเฟรม = สะดุด)
  - CoopBonus: throttle สแกนทุก 0.5 วิ + หยุดตอน pause
  - กันเปิดแผง chat/emote/trade ตอนจบเกม (เช็ก GameManager.IsActive)
- สแกนยืนยันสะอาด: NetworkVariable write permission, RPC ownership/targeting, OnNetworkDespawn unsub, null-guards, core SP loop (สอบ/เลื่อนปี/เซฟ/timeScale) ครบถูกต้อง

### หน้าแต่งตัว: เลือกแบบ/เพศตัวละคร + สี + ชื่อ (sync MP) (2026-09-24)
- 🎭 **CharacterCatalog** ([Systems/CharacterCatalog.cs](Assets/_Project/Scripts/Systems/CharacterCatalog.cs)) — ScriptableObject ใน Resources เก็บโมเดลที่เลือกได้ (index 0 = Ch29 เริ่มต้น) + controller · static `Apply(root, index)` สลับโมเดลลูก (instantiate ใหม่ + ตั้ง controller + ลบเก่า) ใช้ทั้ง player และ avatar
- 🔄 **PlayerModelSwapper** ([Player/PlayerModelSwapper.cs](Assets/_Project/Scripts/Player/PlayerModelSwapper.cs), `[DefaultExecutionOrder(-500)]`) — สลับโมเดล Player ตาม GameSession.PlayerModel ก่อนสคริปต์อื่น cache Animator · `SwapTo(i)` เปลี่ยนสด + เรียก `RefreshAnimator()` ที่เพิ่มใน PlayerMovement/PlayerActionController
- 🌐 **sync MP**: NetworkAvatar เพิ่ม `netModel` (NetworkVariable) → remote สลับโมเดลตามที่เลือก (OnNetworkSpawn/OnModelChanged ผ่าน CharacterCatalog.Apply) · `SetIdentity(name,color,model)`
- 🖱️ **UI แต่งตัว**: NetworkUI + M29 เพิ่มปุ่มเลือกแบบตัวละคร ในแผง F3 (ชื่อ+สี+แบบ) · PickModel → GameSession + SwapTo(local) + sync · M29 `EnsureCatalog()` สร้าง+ใส่โมเดล (Ch29/Ch07/Ch12/Ch21/Remy) + ติด PlayerModelSwapper บน Player
- ปิด gap ตาราง 3.1 (ตัวละครหลายแบบ/เพศ) — เปลี่ยน label เป็น ชาย/หญิง ได้ที่ CharacterCatalog asset
- ⚠️ ยังไม่ได้เทสต์รันจริง (runtime model swap + MP sync) — เทสต์ 2 หน้าต่างแล้วปรับได้

### เมนู Pause ในเกม + รวมเสียงเข้า ★ Rebuild All (2026-09-24)
- ⏸️ **PauseMenu** ([UI/PauseMenu.cs](Assets/_Project/Scripts/UI/PauseMenu.cs)) — กด Esc เปิด/ปิด · เปิดได้เมื่อ `GameManager.IsActive` และไม่มีหน้าต่างอื่นคุมอยู่ (เช็ค `move.enabled` = ไม่มีร้าน/โทรศัพท์/สอบเปิด) · Pause → `GameManager.PauseGame()` (timeScale=0) · เมนู 3 ปุ่ม: เล่นต่อ / กลับเมนูหลัก / ออกจากเกม
- 🔌 **กลับเมนูหลัก** ตัด NGO (`NetworkManager.Shutdown()` ถ้าต่อ MP อยู่) + คืน timeScale=1 ก่อนโหลด `GameSession.MenuScene` ("Scene1", ยืนยันอยู่ index 0 ใน Build Settings)
- 🛠️ **M35PauseBuilder** ([Editor/M35PauseBuilder.cs](Assets/_Project/Scripts/Editor/M35PauseBuilder.cs), เมนู Nisit → Build Pause Menu) — สร้าง Pause Canvas (dim + การ์ด "หยุดชั่วคราว" + 3 ปุ่มพาสเทล) + ต่อ PauseMenu · SuppressDialog
- 🔊 **รวมเสียงเข้า ★ Rebuild All** — M18AudioBuilder เพิ่ม `SuppressDialog` → ★ Rebuild All สร้างเสียง (fanfare/eat/page + แปะปุ่มทุกฉาก) ให้เลยในคลิกเดียว ไม่ต้องกด Build Audio แยก
- ⚠️ ยังไม่ได้เทสต์รันจริง — รัน ★ Rebuild All 1 ครั้งแล้วเทสต์ Esc + ปุ่มกลับเมนู/ออกเกม พร้อมรอบก่อน

### ปุ่ม Multiplayer ในเมนู + หน้าเครดิต + แก้บั๊กเงินซ้ำ (2026-09-24)
- 🌐 **ต่อปุ่ม "เล่นหลายคน" ในเมนูให้ใช้งานจริง** (เดิมโชว์ "กำลังพัฒนา") — ปุ่ม → เริ่มเกมใหม่ + เปิดแผง Multiplayer (F3) อัตโนมัติ เล่น LAN ได้เลย (Host/Join) · เพิ่ม `GameSession.OpenNetworkOnStart` (ตั้งจากเมนู อ่านครั้งเดียวใน [NetworkUI.cs](Assets/_Project/Scripts/Net/NetworkUI.cs)) · ข้อความสถานะบอก IP + วิธี Join · **logic ล้วน ไม่ต้อง re-bake** (Relay ข้ามเน็ตยังไม่เปิด — รอ hotspot/บ้าน)
- ℹ️ **หน้าเกี่ยวกับ/ผู้จัดทำ** — เพิ่มปุ่ม + แผงในเมนู (สร้างใน [M4MenuBuilder.cs](Assets/_Project/Scripts/Editor/M4MenuBuilder.cs) ให้รอด ★ Rebuild All) · แสดงชื่อเกม/สาขา/มหาลัย + ช่อง [ ] ให้กรอกชื่อผู้จัดทำ+อาจารย์ที่ปรึกษา (กรอกหลัง Rebuild All ครั้งสุดท้าย)
- 🐛 **แก้บั๊กเงินตั้งต้นซ้ำตอน "เล่นต่อ"** — `DailyAllowance.Start()` บวก +100 เสมอ ทับกับเงินที่คืนจากเซฟ (order-dependent) · เพิ่ม `GameSession.IsContinue` (ไม่ถูกกิน) → ให้เงินตั้งต้นเฉพาะเกมใหม่
- 📋 ผลสำรวจช่องว่าง: core loop สมบูรณ์ (4 คณะ + ข้อสอบแยกคณะ + จบเกม win/lose ครบ) · ที่ยังเหลือ (เล็ก): เควส/objective/เข้าเรียน/สถานะป่วย ไม่ถูกเซฟ (มีผลเฉพาะออกเกมกลางวัน), สไลเดอร์เสียง vol_voice ยังไม่มีโค้ดอ่าน, ScriptableObject ItemData/CourseData ไม่ถูกใช้

### เซฟให้ครบ + เก็บงานเล็ก ๆ (2026-09-24)
- 💾 **เซฟครบขึ้น** — เพิ่มการเซฟ/คืนค่า 4 อย่างที่เคยหายตอน "เล่นต่อ" กลางวัน:
  - **เควสรายวัน** ([QuestSystem.cs](Assets/_Project/Scripts/Systems/QuestSystem.cs)) — เก็บรายการเควสที่สุ่มได้ + สถานะสำเร็จ + ตัวสะสม (ไม่สุ่มใหม่/ไม่รีเซ็ต) · คืนค่าแบบ "อัปเดต UI ไม่แจกรางวัล" กันเควส Reach สำเร็จผิดจากค่าสถานะที่ยังไม่ถูกคืน
  - **ภารกิจ GoTo ที่ค้าง** ([EventManager.cs](Assets/_Project/Scripts/Systems/EventManager.cs)) — เก็บประตูเป้าหมาย+ข้อความ+ผลตอบแทน แล้วโผล่เสาแสงใหม่ตอนโหลด
  - **เข้าเรียนของวันนี้** ([ClassStation.cs](Assets/_Project/Scripts/Interaction/ClassStation.cs)) — คีย์ตามชื่อ+ตำแหน่งห้อง กันเข้าเรียนซ้ำหลังโหลด
  - **อาการทั้งวัน (ป่วย/ไฟแรง)** ([PlayerEffects.cs](Assets/_Project/Scripts/Player/PlayerEffects.cs)) — เก็บตัวคูณ 3 ตัว
  - สถาปัตยกรรม: เก็บรวมที่ `SaveManager.Save()` · คืนค่าในแต่ละระบบเอง (gated `GameSession.IsContinue`) เลี่ยงปัญหาลำดับ Start() · เซฟเก่ายังโหลดได้ (ฟิลด์ใหม่มีค่า default)
- 🧹 **เก็บงานเล็ก**: ลบ ScriptableObject โค้ดตาย `ItemData.cs`/`CourseData.cs` (ไม่มี .asset/โค้ดใดอ้าง) · ลบฟิลด์ตาย `SaveData.currentDay`
- 🔊 **สไลเดอร์เสียงครบทุกตัว** — เพิ่ม `voiceSource` (สร้าง runtime) ใน [SFXManager.cs](Assets/_Project/Scripts/Core/SFXManager.cs) route เสียงตอบรับ UI (คลิก/แจ้งเตือน/สำเร็จ/ผิดพลาด/เปิดหน้า) ผ่านมัน → สไลเดอร์ Voice มีผลจริง · [SettingsController.cs](Assets/_Project/Scripts/UI/SettingsController.cs) ปรับทุกสไลเดอร์ให้มีผล live

### ตั้งค่าเสียงในเมนู Pause (2026-09-24)
- ⚙️ เพิ่มปุ่ม **"ตั้งค่าเสียง"** ในเมนู Pause (เดิม 3 ปุ่ม → 4 ปุ่ม) + แผงสไลเดอร์ 5 ตัว (Master/Music/SFX/Voice/Ambient) ในเกม — สร้างใน [M35PauseBuilder.cs](Assets/_Project/Scripts/Editor/M35PauseBuilder.cs) ใช้ `SettingsController` เดิม (apply live กับ SFXManager ในเกมได้แล้ว)
- [PauseMenu.cs](Assets/_Project/Scripts/UI/PauseMenu.cs): เปิด/ปิดแผงตั้งค่า · Esc ปิดแผงตั้งค่าก่อน (ไม่ออกจาก Pause) · Resume ปิดทั้งคู่
- ปิด gap "ปรับเสียงตอนอยู่ในเกมไม่ได้" — ต้อง re-bake (★ Rebuild All) 1 ครั้ง

### หน้าแต่งตัวละคร (พรีวิว 3D หมุนได้) (2026-09-24)
- 🧑‍🎨 หน้าจอแต่งตัวเป็นของตัวเองในเมนู (เดิมอยู่แค่แผง F3): เลือกแบบ/เพศ + สี + ชื่อ พร้อม **พรีวิว 3D หมุนได้**
- flow: ปุ่ม "เล่นคนเดียว" → หน้าแต่งตัว → เริ่มเล่น → (เลือกคณะ) → เข้าเกม · reuse `CharacterCatalog` + `GameSession` + `PlayerModelSwapper` เดิม (เข้าเกมใส่โมเดลให้อัตโนมัติ + sync MP ฟรี)
- [CharacterCreatorController.cs](Assets/_Project/Scripts/UI/CharacterCreatorController.cs): พรีวิวด้วย Camera→RenderTexture (สร้างรันไทม์) ส่องเวทีที่วางไกล (1000,0,1000) · instantiate โมเดลจากแคตตาล็อก + auto-scale วางเท้า + หมุนช้า ๆ · ทาสีด้วย `NetworkAvatar.ApplyColor` (เปิดเป็น public) ให้ตรงกับในเกม
- [M37CharacterCreator.cs](Assets/_Project/Scripts/Editor/M37CharacterCreator.cs): สร้างเวที (กล้อง/ไฟ point 2 ดวง/จุดวางโมเดล) + แผง UI (พรีวิว/ชื่อ/6 แบบ/8 สี/ยืนยัน-ย้อนกลับ) · อยู่ใน ★ Rebuild All (หลัง M4) · ต้องมี CharacterCatalog (Setup Multiplayer) ถึงจะมีแบบให้เลือก
- [MainMenuController.cs](Assets/_Project/Scripts/UI/MainMenuController.cs): OpenCustomize/ConfirmCustomize/CloseCustomize (MP ยังเข้าทาง F3 เหมือนเดิม)

### ของแต่ง: หมวก/แว่น/เป้/ของถือ (ติดกระดูก) (2026-09-24)
- 👒 ระบบ accessory ติดกับกระดูก humanoid — เพิ่มในหน้าแต่งตัว 4 ช่อง (หมวก/แว่นตา/กระเป๋าเป้/ของถือ) เลือกได้ + พรีวิว 3D + sync MP
- [AccessoryCatalog.cs](Assets/_Project/Scripts/Systems/AccessoryCatalog.cs): ScriptableObject (Resources) เก็บช่อง (bone + offset/หมุน/สเกล + options[] ให้ผู้ใช้ลาก prop ใส่เอง)
- [CharacterAccessories.cs](Assets/_Project/Scripts/Systems/CharacterAccessories.cs): `Apply(root, selections[])` หา bone ด้วย `Animator.GetBoneTransform` แล้ว instantiate prop เป็นลูกของกระดูก (ตามท่าเดิน) · Pack/Unpack เป็นสตริงสำหรับเครือข่าย · `AccessoryTag` กำกับไว้ถอด
- [GameSession](Assets/_Project/Scripts/SaveLoad/GameSession.cs) เพิ่ม `PlayerAccessories int[]` (0=ไม่ใส่) · [PlayerModelSwapper](Assets/_Project/Scripts/Player/PlayerModelSwapper.cs) ใส่ให้ตอนเข้าเกม/สลับแบบ · [NetworkAvatar](Assets/_Project/Scripts/Net/NetworkAvatar.cs) เพิ่ม `netAcc` sync ให้ผู้เล่นอื่นเห็น (apply ใหม่เมื่อเปลี่ยนโมเดลด้วย)
- [M37CharacterCreator](Assets/_Project/Scripts/Editor/M37CharacterCreator.cs): สร้าง `AccessoryCatalog` เริ่มต้น (4 ช่องว่าง, ไม่ทับถ้ามีแล้ว) + UI แถวเลือกของแต่ง · โมเดล full-body สลับเสื้อ/กางเกงแยกชิ้นไม่ได้ (ต้อง modular) จึงใช้ accessory ติดกระดูกแทน — ผู้ใช้ลาก prop 3D (Kenney/Quaternius) ใส่ options[]

---

## Version Control
- git init + .gitignore (Unity) · commit แรก 2026-07-24 (1,110 ไฟล์)
- ยังเป็น local — แนะนำ push ขึ้น GitHub เพื่อ backup + ทำงานร่วมกัน

---

## เหลือทำ (ตามขอบเขตปริญญานิพนธ์)
- **Phase B — เนื้อหาเกม**: ~~สอบ~~ ✅ ~~ฤดูกาล/เดือน~~ ✅ ~~ภารกิจ~~ ✅ ~~สุ่มเหตุการณ์~~ ✅ ~~งานพาร์ทไทม์~~ ✅ ~~แอคชันตัวละคร~~ ✅ ~~ระบบคณะ+ข้อสอบแยกคณะ~~ ✅
- **Phase C — UI/UX + จูน**: ~~กระโดด~~ ✅ ~~โทรศัพท์(TAB)~~ ✅ ~~Minimap~~ ✅ ~~ฟอนต์ Mitr~~ ✅ ~~Kenney UI~~ ✅ ~~ไอคอนสถานะ~~ ✅ ~~HUD ดำ-ทอง~~ ✅ ~~เวลาเดินสมจริง~~ ✅ ~~บันทึกคณะลงเซฟ~~ ✅ ~~จูนบาลานซ์~~ ✅
- **Phase C+ — เกมจริง/เสียง**: ~~เหตุการณ์เดินไปทำ (GoTo)~~ ✅ ~~ผลกระทบทั้งวัน (ป่วย/ไฟแรง)~~ ✅ ~~หมุด/ลูกศรนำทาง~~ ✅ ~~ระบบเสียง+เพลง~~ ✅
- ~~คลิปท่า Mixamo (เรียน/กิน/นอน สุ่มท่า)~~ ✅ · ~~เสียงจริง Kenney/Pixabay~~ ✅
- ~~ระบบกระเป๋า Inventory~~ ✅ · ~~Voice/Ambient Volume~~ ✅ · ~~เอกสารปริญญานิพนธ์ บท 1-5 + .docx~~ ✅ · ~~ปุ่มเล่นหลายคน (โชว์ "กำลังพัฒนา")~~ ✅
- ~~ธีมการ์ตูนพาสเทลทุกหน้า~~ ✅ · ~~ตัวช่วยแคปรูป (F9/F5-F8)~~ ✅ · ~~ปุ่มรวม ★ Rebuild All UI~~ ✅ · แคปรูปเอกสารแล้ว 7/10 หน้า (เหลือ 3 หน้าจบ F5/F6/F7)
- **เหลือ**: 🏗️ Build .exe + playtest · ต่อท่าเสริม (Cheering จบ / Coughing ป่วย) · ตึก 4 คณะ (มีแต่ IT) · เนื้อหาเฉพาะชั้นปี · คู่มือ
- ปรับแต่งตัวละคร (เพศ/ผม/ชุด) · **Multiplayer จริง** + Trading (Netcode/Photon — ยังไม่เริ่ม, มีแต่ปุ่มโชว์)
- แก้จุดที่เอกสารขัดกัน 7 จุด (Netcode vs Photon, บรรณานุกรม, E vs F ฯลฯ)

---

## เครื่องมือ (Editor Tools) ที่สร้างไว้
เมนู **Nisit →** : Build M1 Scene · Build M3 HUD · Make It Pretty · Add Depth & Mood ·
Build M5 Gameplay · Setup Character (M2) · Fix Character Materials · Fix Animation Loops ·
Build M4 Menu · Menu Background (3D) · **Polish Menu Layout** · **Epic Menu Background** ·
**Day Campus Background** · **Use 3D Background** · **Build Exam System** · **Build Season System** ·
**Build Quest System** · **Build Event System** · **Place Exam & Jobs at Buildings** ·
**Build Faculty Select** · **Build Phone (TAB)** · **Build Minimap** · **Build Stat Icons** ·
**Apply Mitr Font (All UI)** · **Apply Kenney UI Skin** · **Balance Tune** · **Build Audio** ·
**Clean Placeholder Stations** · **Setup Character Animations** · **Build Inventory** · **Build Cafeteria** ·
**★ Rebuild All UI (กดครั้งเดียว)** · **Add Screenshot Helper** ·
CampusBuilder · InteriorBuilder · ShopBuilder ฯลฯ
