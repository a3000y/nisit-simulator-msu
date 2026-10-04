# รายงานย้ายจุดเกิดเข้าห้องพัก + เตียงนอนได้ — 4 ตุลาคม 2026

✅ ติดตั้งครบ 4 ช่องในห้องพักชั้น 1 หอใหม่ ทดสอบ SP และ Host + Client 3 ตัวผ่านการเกิด/นอน/ตื่นจริงใน Development Build เงิน +60฿ คนละครั้งเดียวหลังทุกคนพร้อม ยกเลิกแล้วไม่ข้ามวัน และเซฟจริง 4 ไฟล์ไม่เปลี่ยน MD5

โปรเจกต์ที่ใช้งานจริง: [My project](<C:/Users/Pakwan/Downloads/My project/My project>) · Unity 6000.5.1f1 · ฉาก 01_Gameplay · Build_MPTest/localhost. อาคารในไฟล์ปัจจุบันอยู่ที่ **(-58, 0, 127.15168), scale 0.75** ไม่ตรงกับ (45,0,78) ในโจทย์ จึงรักษาตำแหน่งฉากปัจจุบัน จุดทั้งหมดเป็นลูกของห้องและตามอาคารเมื่อย้าย; EditMode test ตรวจการย้ายไป (45,0,78) แล้ว

สำรองก่อนแก้: [01_Gameplay_BeforeDormSpawn_2026-10-04.unity](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scenes/Backups/01_Gameplay_BeforeDormSpawn_2026-10-04.unity>) (SHA256 ณ สำรอง: `7991FBB849BDDDEA19F80987F49761ED2407B107760261BA13A357ECAC48980D`) ขั้นสำรวจและรายงานก่อนลงมืออยู่ใน [Step0_Report.md](<C:/Users/Pakwan/Downloads/nisitsimo/dorm-room/Step0_Report.md>).

## ห้องและตำแหน่ง

ชื่อ n01/n02 ในผังตรงกับ Room_101/Room_102 ใน hierarchy: `KR_Campus/Zone_DormNW/Dorm_Building/Floor_1/Rooms/…` เลือก **4 เตียงที่มีเจ้าของ** เพื่อไม่ให้ตื่นซ้อน/สลับเตียง อีก 44 เตียงยังเป็นของตกแต่ง การทำทั้ง 48 เตียงเพิ่มระบบเปลี่ยน/จองเตียงที่ยังไม่อยู่ในขอบเขตนี้

| ช่อง | ผู้เล่น | ห้อง/เตียง | Marker / Wake | local ต่อห้อง | local ต่ออาคาร | world (พื้น) |
|---|---|---|---|---|---|---|
| 0 | SP / Host | 101 / Bed_A | DormRoomSpawn_1 / DormBedWake_1 | (-0.600,0,4.000) | (-8.700,0.300,2.600) | (-64.525,0.225,129.102) |
| 1 | C1 | 101 / Bed_B | DormRoomSpawn_2 / DormBedWake_2 | (0.600,0,4.000) | (-9.900,0.300,2.600) | (-65.425,0.225,129.102) |
| 2 | C2 | 102 / Bed_A | DormRoomSpawn_3 / DormBedWake_3 | (-0.600,0,4.000) | (-4.900,0.300,2.600) | (-61.675,0.225,129.102) |
| 3 | C3 | 102 / Bed_B | DormRoomSpawn_4 / DormBedWake_4 | (0.600,0,4.000) | (-6.100,0.300,2.600) | (-62.575,0.225,129.102) |

ทุกจุดหัน world yaw≈0° ออกจากเตียงไปทางประตู/ทางเดิน Wake เป็น Transform แยกกัน 4 ตัว อยู่ข้างเตียงเดียวกับ spawn แต่ละช่อง ไม่ใช้ wake รวมแบบ B12 Alias DormSpawnPoint / DormSpawnSlot_2–4 ยังอยู่ใต้ DormSpawn ของอาคารเพื่อรองรับชื่อเดิม

Raycast พบพื้นจริง Plinth ชั้น 1 y=0.225 ไม่ใช่พื้นตกแต่ง/เฟอร์นิเจอร์ CharacterController.radius=0.5 × player scale 0.66 = **0.33 ม.** ระยะช่องคู่ในห้อง = **0.9 ม.** มากกว่าเส้นผ่านศูนย์กลาง 0.66 ม. ตรวจ full capsule ว่างครบ 4 จุด ทั้ง Editor และ Runtime ตำแหน่ง pivot ผู้เล่นยืนจริง y≈0.965 เนื่องจากครึ่งความสูงและ skin width ไม่ใช่ตำแหน่งลอยจากพื้น

ตั้งค่าห้องได้ใน Inspector ของ DormSpawnPoint: `roomSlots` → roomPath / bedPath / roomId / roomLocalPoint แล้วรัน M46 setup เพื่อจัด markers; DormBuildingGenerator เก็บ settings นี้ก่อน rebuild ลำดับผู้เล่นยังใช้ LobbyState.SlotIndex และ fallback sorted ClientId เดิม

## ไฟล์ที่แก้

| ไฟล์ | เหตุผล |
|---|---|
| [Scenes/01_Gameplay.unity](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scenes/01_Gameplay.unity>) | ย้าย 4 spawn/wake เข้าห้อง 101/102 ผูก 4 เตียง และเปิด room cutaway; หอเดิมยังอยู่ |
| [Prefabs/Dorm/Dorm_Building.prefab](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Prefabs/Dorm/Dorm_Building.prefab>) | เก็บ room slots, SleepInteract และ WakePoint ของ 4 เตียงไว้ใน prefab; ประตู 53 บานเริ่มปิด |
| [Scripts/Interaction/DormSpawnPoint.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/Interaction/DormSpawnPoint.cs>) | เพิ่ม Inspector configuration ต่อช่อง: ห้อง เตียง จุดเกิด จุดตื่น; ใช้ marker ใต้ห้องเป็นจุดหลัก |
| [Scripts/SaveLoad/PlayerSpawnSystem.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/SaveLoad/PlayerSpawnSystem.cs>) | เลือกช่องตาม Lobby SlotIndex เดิมของ B11; save room ID; migrate เซฟ warp dorm; fallback/กล้อง/cutaway |
| [Scripts/Interaction/SleepStation.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/Interaction/SleepStation.cs>) | IInteractable เดิมพร้อมสิทธิ์เจ้าของเตียงและ wake เฉพาะเตียง |
| [Scripts/Interaction/SleepController.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/Interaction/SleepController.cs>) | ตรวจเตียงของเจ้าของและใช้ WakePoint ของเตียงโดยตรง; คง flow นอน/พร้อม/ยกเลิก/ฟื้นสถานะเดิม |
| [Scripts/Editor/M46DayNightDormSetup.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/Editor/M46DayNightDormSetup.cs>) | raycast พื้นจริง ตรวจแคปซูล สร้าง marker/trigger แบบรันซ้ำได้ และรักษา prefab file IDs |
| [Scripts/Editor/DormBuildingGenerator.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/Editor/DormBuildingGenerator.cs>) | เก็บ room settings ก่อน rebuild แล้วเรียก setup ห้อง/เตียงใหม่ให้ผลคงอยู่ |
| [Scripts/GEBuilding/GEBuildingCutaway.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/GEBuilding/GEBuildingCutaway.cs>) | เฉพาะหอ: ซ่อนผนัง/ต้นไม้ในแนวกล้องขณะอยู่ในห้องที่ตั้งค่า; คืนเมื่อออก; ไม่เปลี่ยน Collider |
| [Scripts/DevTools/DevTimeTools.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/DevTools/DevTimeTools.cs>) | ปุ่มวาร์ปและทดสอบนอนใช้ช่อง/เตียงของผู้เล่นปัจจุบัน |
| [Scripts/DevTools/DevPanel.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/DevTools/DevPanel.cs>) | ข้อความปุ่มวาร์ปชี้ห้องพักของตัวเอง |
| [Scripts/SaveLoad/SaveData.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/SaveLoad/SaveData.cs>) | อธิบาย dormRoomId รูปแบบใหม่; schema เซฟคงเดิม |
| [Scripts/SaveLoad/SaveSystem.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/SaveLoad/SaveSystem.cs>) | จับ JSON เสีย/อ่านไฟล์ไม่ได้ คืน null พร้อมเหตุผล โดยไม่ลบไฟล์ |
| [Scripts/SaveLoad/SaveManager.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/SaveLoad/SaveManager.cs>) | ปลดสถานะ Continue เมื่ออ่านเซฟไม่สำเร็จ ให้ bootstrap ไปจุด fallback ต่อได้ |
| [Scripts/DevTools/MPTestAgent.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/DevTools/MPTestAgent.cs>) | เชื่อมคำสั่งและ snapshot สำหรับตรวจห้อง/เตียงใน Development Build |
| [Scripts/DevTools/MPTestAgent.Rooms.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/DevTools/MPTestAgent.Rooms.cs>) | ใหม่: harness ใช้ PlayerInteraction, SleepStation, SleepController และ CharacterController จริง; safe save scenarios |
| [Scripts/Editor/Tests/DormRoomSpawnTests.cs](<C:/Users/Pakwan/Downloads/My project/My project/Assets/_Project/Scripts/Editor/Tests/DormRoomSpawnTests.cs>) | ใหม่: 12 tests/cases สำหรับ bounds, spacing, wake, prefab/scene duplicate, config และ legacy/corrupt save |

มี .meta ใหม่ของ MPTestAgent.Rooms.cs และ DormRoomSpawnTests.cs รวมทั้ง scene backup ไม่แก้ DM_Bed.prefab หรือ DM_DormRoom.prefab: SleepInteract ของ 4 เตียงเก็บเป็น override ใน Dorm_Building.prefab และ Generator เรียก setup เดียวกัน

## ผลทดสอบ

สถานะ: ✅ วัดผ่านจริง · ❌ วัดไม่ผ่าน · 🔧 พบบั๊กและแก้แล้วพร้อมตรวจซ้ำ · ⏳ ยังไม่วัดจริง · ⛔ ติดข้อจำกัด

| กรณี | สถานะ | ผลที่วัด/หลักฐาน |
|---|---|---|
| SP เกมใหม่ | ✅ | snapshot แรก D1 07:00, ช่อง 0 ห้อง 101/Bed_A, grounded=true, capsuleClear=true; ภาพ spawn เป็น 07:05 หลังเวลาเดินไปแล้ว |
| outside / แสง / กล้อง / cutaway | ✅ | ทุก instance insideInterior=false, interiorLighting=false, camera target=Player อยู่กลาง viewport, cutFloor=0; ภาพเห็นตัวผู้เล่นในห้อง |
| HUD เตียงและ flow เดิม | ✅ | PlayerInteraction.DetectNearest พบ SleepStation ของตัวเองทั้ง SP/H/C1/C2/C3; HUD “กด E เพื่อนอน (ตื่น 07:00 น.)”; Interact เข้า Confirming |
| SP นอน + กันยืนยันซ้ำ | ✅ | ยืนยัน 2 ครั้ง: completedSleeps=1, D1→D2 07:00, เงิน 100→160, energy≈100, wakeDistance=0 |
| MP ช่องเกิด 4 คน | ✅ | slot 0/1 ห้อง 101, slot 2/3 ห้อง 102; grounded/clear ครบ 4, ระยะใกล้สุด 0.9 ม., ไม่ซ้อน |
| MP พร้อมเพียง 3/4 | ✅ | ยัง D1, completedSleeps=0; C3 ยัง Idle |
| MP คนหนึ่งยกเลิก | ✅ | C2 CancelWaiting กลับ Idle; C3 พร้อมแทนแต่ยัง 3/4, ไม่ข้ามวัน/ไม่เพิ่มเงิน |
| MP ครบทุกคน + กันยืนยันซ้ำ | ✅ | C2 พร้อมและยืนยัน 2 ครั้ง: ทุกคน D2 07:00, completedSleeps=1, เงิน 100→160, energy 99.995–99.998; wakeDistance=0 ทุกเครื่อง |
| NetworkAvatar หลังตื่น | ✅ | ทุก instance มี owner 0–3 ครบ; x/z เท่าตำแหน่งเจ้าของ (error 0 ม.); y ต่างจากระดับพื้นไม่เกิน 0.08 ม. |
| ประตูหอเริ่มปิด | ✅ | Dorm GEDoor 53/53 open=false และ angle≈0 ทั้ง 4 instance; เดิน SP เปิดประตูห้อง 101 และทางเข้าซ้ายผ่าน flow จริงได้ |
| Continue ในห้อง | ✅ | เซฟตำแหน่งขยับออกจาก marker แล้วโหลด x/z คืนตรงค่าเดิม: (-64.7894,0.965,129.1017), lastSpawn=restored |
| Continue เซฟ old warp dorm | ✅ | insideInterior=true, Spawn_หอพัก, x≈1316.8 → migrated_legacy_dorm, ห้อง 101, outside, wakeDistance=0 |
| Continue ตำแหน่งนอกแผนที่ | ✅ | safe profile x=99999 → fallback_dorm, ห้องของตัวเอง ไม่เสียความคืบหน้าที่โหลดได้ |
| Continue JSON เสีย | 🔧 | เดิม JsonUtility exception ทำ bootstrap หยุด; แก้จับข้อผิดพลาดแล้ว spawn fallback ห้องใหม่ D1 พร้อม SleepController, ไม่ลบไฟล์ |
| เดินห้อง→ทางเดิน→ทางเข้า→ถนน→กลับ | ✅ | SPW ใช้ CharacterController.Move ต่อเนื่อง ไม่ teleport: ถนน (-58.450,0.740,124.941); กลับ (-64.524,0.965,129.196), grounded/clear=true, ห่าง wake 0.095 ม. |
| Dev warp และ Dev sleep | ✅ | WarpToDorm ใน SP เรียกได้จริง ใช้ assigned slot; TestSleep เปลี่ยนไปใช้ station ของช่อง (เส้นทางคำสั่งไม่ได้เปิด UI DevPanel ด้วยเมาส์) |
| M46 setup ซ้ำ / prefab reload | ✅ | ทำ setup ซ้ำและโหลด scene/prefab ใหม่: 4 SleepStations, 4 wake, 53 doors; ไม่มี duplicate station |
| Generator เก็บ config | ✅ / ⏳ | unit test CopyRoomSettings ผ่านและ Build เรียก setup ด้วย config เดิม; ยังไม่รัน full Rebuild menu ซึ่งเขียน prefab เฟอร์นิเจอร์ทั้งหมด |
| EditMode tests ทั้งหมด | ✅ | **178 ผ่าน / 0 fail / 0 skip**, 16.876 วินาที; เพิ่ม 12 cases จากฐาน 166 |
| Development Build | ✅ | Succeeded, 0 errors, 232 warnings; ไม่ถือว่า warnings เป็นศูนย์ |
| Console / runtime errors รอบสุดท้าย | ✅ | Editor console 0 errors ณ ตรวจครั้งสุดท้ายหลัง refresh; SP/SPW/H/C1/C2/C3 ไม่มี Unity Error/Exception ใน logs; มีคำเตือน font emoji เดิม |
| Real saves / DevGuard | ✅ | MD5 ตรงกัน 4/4 ไม่มีไฟล์เพิ่ม/หาย; DevGuard=true ทุก snapshot, guardViolations=0, save override เป็น test folder ทุกครั้ง |
| กดปุ่ม E/Enter ด้วยคีย์บอร์ดจริง | ⏳ | harness เรียก DetectNearest → SleepStation.Interact และ Confirm ของระบบจริง ไม่ได้ฉีด keyboard input |
| หมดแรงแล้วพัก / กันนอนระหว่างสอบแบบ runtime | ⏳ | คงกฎและ flow เดิม; regression tests ผ่าน แต่ไม่ได้สร้างสถานการณ์หมดแรง/สอบใหม่ในรอบ runtime นี้ |
| LAN หลายเครื่อง / Relay / latency | ⏳ | ทดสอบ localhost เครื่องเดียว 4 processes เท่านั้น |

รอบ run14 SP เคยแสดง ❌ ของเส้นทางใน SP-results.json เพราะอ่าน periodic snapshot ก่อน coroutine เดินจบ แก้ขั้นตรวจให้ขอ state หลัง COMPLETE และวัดซ้ำด้วย SPW ผ่านทั้งสองทาง หลักฐานเดิมเก็บไว้ ไม่ลบหรือแทนผลล้มเหลว

## บั๊กที่แก้และข้อจำกัด

| อาการ | สาเหตุและการแก้ | หลักฐานตรวจซ้ำ |
|---|---|---|
| เตียงมี HUD แต่ harness ไม่พบ station ที่ assign | setup เดิมทำลาย/สร้าง SleepInteract แล้ว prefab refresh ทำ scene override ซ้ำ 8 ตัว; รักษา file IDs, ใช้ Child เดิม และล้าง duplicate ชื่อเดียว | scene และทุก instance เหลือ 4 stations; own=True ครบทุกคน, นอนสำเร็จ |
| โหลด JSON เสียแล้วระบบเกิด/นอนหาย | JsonUtility.FromJson throw ก่อน bootstrap สร้าง SleepController; SaveSystem จับ ArgumentException/IOException และ SaveManager ปลด Continue เมื่อไม่มี data | run14 corrupt fallback ไม่มี runtime error, grounded/wakeDistance=0 |
| ตัวผู้เล่นถูกบังในห้อง 101 | ต้นไม้ SM_Env_Tree_03 หน้าอาคารบังกล้อง; ไม่ใช่ Snap หรือชั้นบน; เพิ่ม bounds-based room occlusion ที่คง Collider | ภาพ SP/MP เห็นผู้เล่น, SPW นอกห้อง hidden=0 แล้วกลับ hidden=1 |

ไฟล์ JSON ที่อ่านไม่ได้ไม่มีข้อมูลความคืบหน้าให้กู้ การทดสอบนี้ fallback ไปห้องใหม่/D1 โดยไม่ลบไฟล์ ใน snapshot safe profile corrupt เงินเป็น 0 (ไม่มีข้อมูลคืน) ไม่อ้างว่าได้คืนเงิน/ความคืบหน้าจากไฟล์เสีย

## สิ่งที่ยังอ้างหอเดิม

- Door_หอพัก ยังมี BuildingDoor ที่ enabled และ reference `Interiors/Spawn_หอพัก` world (1316.800,0.132,-3.564) เหมือน backup; interior และ geometry เดิมยังอยู่ ตรวจ reference จริงแล้ว ไม่ได้เดินเข้า warp ด้วยปุ่มจริงในรอบนี้
- InteriorManager, BuildingDoor และ SaveData.insideInterior/interiorName ยังรองรับ interior warp; หอใหม่ใช้ outside กับ GEBuildingCutaway
- PlayerSpawnSystem ยังรู้จักชื่อ Spawn_หอพัก / หอพัก / Door_หอพัก สำหรับ migration และรองรับ warp interior อื่น รวมทั้ง DefaultRoomId=dorm_1 เมื่อไม่มี DormSpawnPoint
- Editor SleepSetup / M19CleanStations / KRCampusLayout ยังมี setup สำหรับหอเก่า ส่วน KRCampusLayout กับ KRLayoutWalkTest อ้าง path `DormSpawn/DormExteriorExit` เดิมที่ไม่มีในฉากปัจจุบัน ไม่ได้รันเมนูเหล่านี้ในงานนี้
- บททดสอบ legacy save และ dev harness ใช้ชื่อ/พิกัดหอเดิมเป็นข้อมูลจำลองเท่านั้น จุดเกิดจริงและปุ่มวาร์ปหอใหม่ใช้ room markers

## หลักฐานและความปลอดภัยเซฟ

หลักฐาน runtime เต็ม: [MPTestRuns/run14](<C:/Users/Pakwan/Downloads/nisitsimo/MPTestRuns/run14>) — `<id>.log`, `<id>.player.log`, `<id>.state.json`, `<id>.states.jsonl` และ `*-process.json` บันทึก arguments ทุก process มี -mptest โฟลเดอร์นี้และ -mptest-id; snapshot ตามรอบ nominal 0.5 วินาที (อาจช้าลงช่วงโหลดฉาก)

| Instance | จำนวน snapshot | guardFrames | guardViolations | errors |
|---|---:|---:|---:|---:|
| SP | 158 | 1713 | 0 | 0 |
| SPW | 91 | 1080 | 0 | 0 |
| H | 356 | 4838 | 0 | 0 |
| C1 | 344 | 4705 | 0 | 0 |
| C2 | 354 | 4789 | 0 | 0 |
| C3 | 348 | 4791 | 0 | 0 |

เปรียบเทียบ MD5 หลังทุก process ปิด: **4 ก่อน / 4 หลัง / identical=true** ที่ 2026-10-04 14:50:25 Bangkok ไม่มี real save ถูกเขียน/ลบ ทดสอบผ่าน safe profile เท่านั้น รวม corrupt file ที่สร้างใน test folder ดู [real_saves_before.txt](<C:/Users/Pakwan/Downloads/nisitsimo/dorm-room/real_saves_before.txt>) · [real_saves_after.txt](<C:/Users/Pakwan/Downloads/nisitsimo/dorm-room/real_saves_after.txt>) · [real_saves_comparison.json](<C:/Users/Pakwan/Downloads/nisitsimo/dorm-room/real_saves_comparison.json>).

รายละเอียด [178 tests](<C:/Users/Pakwan/Downloads/nisitsimo/dorm-room/editmode-details-final.json>) · [SP results (รวม false-negative ของ harness เดิม)](<C:/Users/Pakwan/Downloads/nisitsimo/MPTestRuns/run14/SP-results.json>) · [SP walk ตรวจซ้ำ](<C:/Users/Pakwan/Downloads/nisitsimo/MPTestRuns/run14/SP-walk-verified.json>) · [MP results](<C:/Users/Pakwan/Downloads/nisitsimo/MPTestRuns/run14/MP-results.json>) · [NetworkAvatar validation](<C:/Users/Pakwan/Downloads/nisitsimo/MPTestRuns/run14/MP-avatar-validation.json>) · [Guard/Console summary](<C:/Users/Pakwan/Downloads/nisitsimo/MPTestRuns/run14/guard-and-console-summary.json>).

## ภาพจาก Build จริง

ภาพเกิดถ่ายหลัง spawn แล้ว จึงอาจเห็นนาฬิกา 07:05/07:13 แทน 07:00; snapshot แรกใช้ตรวจเวลาเริ่ม ภาพตื่นทั้งหมดเป็น D2 07:00

| Instance | เกิดในห้อง | HUD เตียง 18:00 | ตื่น D2 07:00 |
|---|---|---|---|
| SP | [SP_sp_spawn.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/SP_sp_spawn.png>) | [SP_sp_bed_prompt.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/SP_sp_bed_prompt.png>) | [SP_sp_wake.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/SP_sp_wake.png>) |
| H | [H_mp_spawn.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/H_mp_spawn.png>) | [H_mp_bed_prompt.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/H_mp_bed_prompt.png>) | [H_mp_wake.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/H_mp_wake.png>) |
| C1 | [C1_mp_spawn.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C1_mp_spawn.png>) | [C1_mp_bed_prompt.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C1_mp_bed_prompt.png>) | [C1_mp_wake.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C1_mp_wake.png>) |
| C2 | [C2_mp_spawn.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C2_mp_spawn.png>) | [C2_mp_bed_prompt.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C2_mp_bed_prompt.png>) | [C2_mp_wake.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C2_mp_wake.png>) |
| C3 | [C3_mp_spawn.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C3_mp_spawn.png>) | [C3_mp_bed_prompt.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C3_mp_bed_prompt.png>) | [C3_mp_wake.png](<C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C3_mp_wake.png>) |

SP เกิดในห้อง

![SP เกิดในห้อง](C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/SP_sp_spawn.png)

SP HUD เตียง

![SP HUD เตียง](C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/SP_sp_bed_prompt.png)

SP ตื่นข้างเตียง

![SP ตื่นข้างเตียง](C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/SP_sp_wake.png)

Host HUD เตียง

![Host HUD เตียง](C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/H_mp_bed_prompt.png)

Client 1 เกิดคนละเตียงในห้อง 101

![Client 1 เกิดคนละเตียงในห้อง 101](C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C1_mp_spawn.png)

Client 3 ตื่นในห้อง 102

![Client 3 ตื่นในห้อง 102](C:/Users/Pakwan/Downloads/My project/My project/Docs/Reports/dorm-room-run14/C3_mp_wake.png)

รายงานนี้ไม่สรุป full Rebuild, keyboard input จริง, exhaustion/exam runtime, LAN/Relay/latency ว่าผ่าน ยังไม่มี commit/push สำหรับงานรอบนี้
