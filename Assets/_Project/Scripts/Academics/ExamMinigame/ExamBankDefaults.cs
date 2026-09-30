using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Academics.ExamMinigame
{
    // คลังข้อสอบตั้งต้น — ผูกกับรหัสวิชาเดิมใน CsCurriculumDefaults:
    //   CS102 การเขียนโปรแกรมเบื้องต้น · CS202 ระบบฐานข้อมูล · CS207 เครือข่ายคอมพิวเตอร์ · GE102 ภาษาอังกฤษพื้นฐาน
    //   ใช้สร้าง asset (Nisit ▸ Build Exam Minigame Banks) และเป็นค่าสำรองเมื่อไม่มี asset
    //   ตัวอย่างโค้ดในข้อสอบเป็น "ข้อความ" (ภาษา Python) ไม่ถูกประมวลผล
    public static class ExamBankDefaults
    {
        public static ExamBankDatabase Create()
        {
            var db = ScriptableObject.CreateInstance<ExamBankDatabase>();
            db.name = "ExamBankDatabase";
            Fill(db);
            return db;
        }

        public static void Fill(ExamBankDatabase db)
        {
            db.defaultQuestionsPerExam = 5;
            db.defaultTimeLimitSeconds = 180f;
            db.oneHintFrom = 40f;
            db.twoHintsFrom = 70f;
            db.minStudyRatioToSit = 0f;
            db.legacyCategories = new List<CourseCategory> { CourseCategory.Project, CourseCategory.Internship };
            db.banks = new List<CourseExamBank> { Programming(), Database(), Network(), English() };
        }

        // ---------- helpers ----------
        static ExamQuestion MC(string id, string prompt, string code, string[] choices, int correct, string hint, string expl) =>
            new ExamQuestion { id = id, type = ExamQuestionType.MultipleChoice, prompt = prompt, code = code ?? "", items = new List<string>(choices), correctIndex = correct, hint = hint, explanation = expl };

        static ExamQuestion Order(string id, string prompt, string[] blocksInCorrectOrder, string hint, string expl, params string[] alternate) =>
            new ExamQuestion { id = id, type = ExamQuestionType.Ordering, prompt = prompt, items = new List<string>(blocksInCorrectOrder), alternateOrders = new List<string>(alternate), hint = hint, explanation = expl };

        static ExamQuestion Match(string id, string prompt, string[] left, string[] right, string hint, string expl) =>
            new ExamQuestion { id = id, type = ExamQuestionType.Matching, prompt = prompt, items = new List<string>(left), matches = new List<string>(right), hint = hint, explanation = expl };

        static ExamQuestion Err(string id, string prompt, string[] lines, int wrongIndex, string hint, string expl) =>
            new ExamQuestion { id = id, type = ExamQuestionType.FindError, prompt = prompt, items = new List<string>(lines), correctIndex = wrongIndex, hint = hint, explanation = expl };

        // =====================================================================
        // CS102 การเขียนโปรแกรมเบื้องต้น — เรียงโค้ด + หาจุดผิด
        // =====================================================================
        static CourseExamBank Programming()
        {
            var b = new CourseExamBank { courseCode = "CS102", version = 1 };
            var q = b.questions;
            q.Add(Order("CS102-O01", "เรียงโค้ดเพื่อสลับค่าตัวแปร a กับ b แล้วพิมพ์ผลลัพธ์ \"5 3\"",
                new[] { "a = 3", "b = 5", "temp = a", "a = b", "b = temp", "print(a, b)" },
                "ต้องมีที่พักค่าชั่วคราวก่อนเขียนทับตัวแปร",
                "ต้องกำหนดค่าเริ่มต้นก่อน แล้วเก็บ a ไว้ใน temp ก่อนเขียนทับ a ด้วย b จากนั้นนำ temp ไปใส่ b (บรรทัด a = 3 และ b = 5 สลับกันได้)",
                "1,0,2,3,4,5"));
            q.Add(Order("CS102-O02", "เรียงโค้ดเพื่อหาผลรวมของเลข 1 ถึง 10 แล้วพิมพ์ผล",
                new[] { "total = 0", "for i in range(1, 11):", "    total = total + i", "print(total)" },
                "ตัวสะสมต้องถูกตั้งค่าก่อนเริ่มวนซ้ำ และพิมพ์หลังวนครบ",
                "ตั้ง total = 0 ก่อน วนซ้ำ i ตั้งแต่ 1–10 (range(1, 11)) บวกสะสม แล้วพิมพ์นอกลูป (ไม่ย่อหน้า)"));
            q.Add(Order("CS102-O03", "เรียงโค้ดฟังก์ชันหาค่ามากที่สุดในลิสต์",
                new[] { "def find_max(nums):", "    best = nums[0]", "    for n in nums:", "        if n > best:", "            best = n", "    return best" },
                "ดูระดับการย่อหน้า — บรรทัดที่ย่อหน้ามากกว่าอยู่ข้างในบรรทัดก่อนหน้า",
                "ประกาศฟังก์ชัน → สมมติตัวแรกเป็นค่ามากสุด → วนเทียบทีละตัว → ถ้ามากกว่าให้แทนค่า → คืนค่าหลังวนครบ"));
            q.Add(Order("CS102-O04", "เรียงโค้ดรับอายุแล้วแสดงว่าบรรลุนิติภาวะ (อายุ 20 ปีขึ้นไป) หรือไม่",
                new[] { "age = int(input(\"อายุ: \"))", "if age >= 20:", "    print(\"บรรลุนิติภาวะ\")", "else:", "    print(\"ยังไม่บรรลุนิติภาวะ\")" },
                "ต้องได้ข้อมูลก่อนตัดสินใจ และ else ต้องตามหลังคำสั่งภายใต้ if",
                "รับค่าและแปลงเป็นจำนวนเต็มก่อน แล้วตรวจเงื่อนไข if ส่วน else เป็นทางเลือกเมื่อเงื่อนไขเป็นเท็จ"));
            q.Add(Order("CS102-O05", "เรียงขั้นตอนการพัฒนาโปรแกรมให้ถูกต้อง",
                new[] { "วิเคราะห์ปัญหาและความต้องการ", "ออกแบบขั้นตอนวิธี (อัลกอริทึม/ผังงาน)", "เขียนโปรแกรมตามที่ออกแบบ", "ทดสอบและแก้ไขข้อผิดพลาด", "จัดทำเอกสารและบำรุงรักษา" },
                "ต้องเข้าใจโจทย์ก่อนลงมือออกแบบ",
                "วงจรพื้นฐาน: วิเคราะห์ → ออกแบบ → เขียนโค้ด → ทดสอบ/ดีบัก → จัดทำเอกสาร/บำรุงรักษา"));
            q.Add(Err("CS102-E01", "โปรแกรมนี้ต้องการรวมค่าในลิสต์ แต่รันไม่ได้ บรรทัดใดผิดไวยากรณ์?",
                new[] { "numbers = [4, 8, 15]", "total = 0", "for n in numbers", "    total += n", "print(total)" }, 2,
                "คำสั่งที่ขึ้นบล็อกใหม่ใน Python ต้องจบด้วยสัญลักษณ์บางอย่าง",
                "บรรทัด for ต้องจบด้วยเครื่องหมาย : → for n in numbers:"));
            q.Add(Err("CS102-E02", "โปรแกรมหาค่าเฉลี่ยของคะแนน 3 วิชา ให้ผลลัพธ์ผิด บรรทัดใดผิด?",
                new[] { "scores = [70, 80, 90]", "total = sum(scores)", "avg = total / 2", "print(\"เฉลี่ย\", avg)" }, 2,
                "ค่าเฉลี่ย = ผลรวม ÷ จำนวนข้อมูล",
                "มีข้อมูล 3 ตัวแต่หารด้วย 2 — ควรเป็น avg = total / len(scores)"));
            q.Add(Err("CS102-E03", "บรรทัดใดทำให้โปรแกรมนี้เกิด SyntaxError?",
                new[] { "x = 10", "if x = 10:", "    print(\"สิบ\")" }, 1,
                "การกำหนดค่ากับการเปรียบเทียบใช้สัญลักษณ์ต่างกัน",
                "การเปรียบเทียบต้องใช้ == → if x == 10:  ส่วน = ใช้กำหนดค่าเท่านั้น"));
            q.Add(Err("CS102-E04", "ต้องการพิมพ์เลข 1 ถึง 5 แต่ได้แค่ 1 ถึง 4 บรรทัดใดผิด?",
                new[] { "i = 1", "while i < 5:", "    print(i)", "    i = i + 1" }, 1,
                "ลองไล่ค่า i รอบสุดท้ายว่าเงื่อนไขยังเป็นจริงหรือไม่",
                "เงื่อนไข i < 5 หยุดก่อนพิมพ์ 5 — ควรเป็น while i <= 5: (ข้อผิดพลาดแบบ off-by-one)"));
            q.Add(Err("CS102-E05", "บรรทัดใดทำให้เกิด TypeError ตอนรัน?",
                new[] { "name = \"ต้น\"", "age = 19", "print(\"ชื่อ \" + name)", "print(\"อายุ \" + age)" }, 3,
                "ต่อข้อความด้วย + ได้เฉพาะข้อมูลชนิดเดียวกัน",
                "age เป็น int ต่อกับ str ด้วย + ไม่ได้ — ใช้ print(\"อายุ \" + str(age)) หรือ print(\"อายุ\", age)"));
            q.Add(MC("CS102-M01", "ผลลัพธ์ของโค้ดนี้คือข้อใด?", "print(7 // 2)",
                new[] { "3", "3.5", "4", "1" }, 0,
                "// คือการหารแบบปัดเศษทิ้ง (floor division)",
                "7 // 2 = 3 (หารเอาส่วนจำนวนเต็ม) ถ้าใช้ / จะได้ 3.5 และ % จะได้เศษ 1"));
            q.Add(MC("CS102-M02", "ผลลัพธ์ของโค้ดนี้คือข้อใด?", "word = \"hello\"\nprint(len(word))",
                new[] { "5", "4", "6", "hello" }, 0,
                "len() นับจำนวนตัวอักษรทั้งหมดในสตริง",
                "\"hello\" มี 5 ตัวอักษร len() จึงคืน 5 (ดัชนีเริ่ม 0 ถึง 4 แต่ความยาวคือ 5)"));
            return b;
        }

        // =====================================================================
        // CS202 ระบบฐานข้อมูล — จับคู่ความสัมพันธ์ + เลือก SQL ที่ถูกต้อง
        // =====================================================================
        static CourseExamBank Database()
        {
            var b = new CourseExamBank { courseCode = "CS202", version = 1 };
            var q = b.questions;
            q.Add(Match("CS202-P01", "จับคู่สถานการณ์กับประเภทความสัมพันธ์ (Cardinality)",
                new[] { "นิสิต 1 คน มีบัตรนิสิตได้ 1 ใบ", "อาจารย์ที่ปรึกษา 1 คน ดูแลนิสิตได้หลายคน", "นิสิตหลายคนลงทะเบียนได้หลายวิชา" },
                new[] { "หนึ่งต่อหนึ่ง (1:1)", "หนึ่งต่อกลุ่ม (1:N)", "กลุ่มต่อกลุ่ม (M:N)" },
                "ดูว่าแต่ละฝั่งมีได้ \"หนึ่ง\" หรือ \"หลาย\"",
                "บัตร 1 ใบต่อ 1 คน = 1:1 · อาจารย์ 1 คน : นิสิตหลายคน = 1:N · นิสิตหลายคน : วิชาหลายวิชา = M:N (ต้องมีตารางเชื่อม เช่น enroll)"));
            q.Add(Match("CS202-P02", "จับคู่คำศัพท์ฐานข้อมูลเชิงสัมพันธ์กับความหมาย",
                new[] { "Primary Key", "Foreign Key", "Tuple (Row)", "Attribute (Column)" },
                new[] { "ค่าที่ระบุแต่ละแถวได้ไม่ซ้ำกัน", "ค่าที่อ้างอิงคีย์หลักของอีกตาราง", "ระเบียนข้อมูล 1 แถว", "คุณสมบัติหนึ่งของข้อมูล (1 คอลัมน์)" },
                "คีย์หลักอยู่ในตารางตัวเอง ส่วนคีย์นอกชี้ไปตารางอื่น",
                "Primary Key ระบุแถวไม่ซ้ำ · Foreign Key อ้างอิงคีย์หลักตารางอื่น · Tuple = แถว · Attribute = คอลัมน์"));
            q.Add(Match("CS202-P03", "จับคู่คำสั่ง SQL กับหน้าที่",
                new[] { "SELECT", "INSERT", "UPDATE", "DELETE" },
                new[] { "ดึง/ค้นหาข้อมูล", "เพิ่มแถวใหม่", "แก้ไขค่าในแถวที่มีอยู่", "ลบแถว" },
                "ทั้งสี่คำสั่งอยู่ในกลุ่ม DML (จัดการข้อมูล)",
                "SELECT อ่าน · INSERT เพิ่ม · UPDATE แก้ไข · DELETE ลบ (CRUD)"));
            q.Add(Match("CS202-P04", "จับคู่รูปแบบบรรทัดฐาน (Normal Form) กับเงื่อนไข",
                new[] { "1NF", "2NF", "3NF" },
                new[] { "ทุกช่องเก็บค่าเดี่ยว (atomic) ไม่มีกลุ่มข้อมูลซ้ำ", "ไม่มีแอตทริบิวต์ที่ขึ้นกับบางส่วนของคีย์หลัก", "ไม่มีการขึ้นต่อกันแบบถ่ายทอด (transitive)" },
                "แต่ละระดับต้องผ่านระดับก่อนหน้าก่อน",
                "1NF ค่าเดี่ยว · 2NF ตัด partial dependency · 3NF ตัด transitive dependency"));
            q.Add(Match("CS202-P05", "จับคู่ชนิดการ JOIN กับผลลัพธ์",
                new[] { "INNER JOIN", "LEFT JOIN", "CROSS JOIN" },
                new[] { "เฉพาะแถวที่เงื่อนไขตรงกันทั้งสองตาราง", "ทุกแถวจากตารางซ้าย + แถวที่ตรงกันจากขวา", "ทุกคู่ผสมของสองตาราง (Cartesian product)" },
                "ดูว่าแถวที่หาคู่ไม่เจอจะถูกเก็บไว้หรือไม่",
                "INNER เก็บเฉพาะที่ตรงกัน · LEFT เก็บฝั่งซ้ายครบ (ฝั่งขวาไม่มีคู่เป็น NULL) · CROSS ผสมทุกคู่"));
            q.Add(MC("CS202-S01", "ต้องการดูนิสิตทุกคนที่มี gpa มากกว่า 3.00 จากตาราง students ข้อใดถูกต้อง?", "",
                new[] { "SELECT * FROM students WHERE gpa > 3.00;", "SELECT students WHERE gpa > 3.00;", "SELECT * WHERE gpa > 3.00 FROM students;", "GET * FROM students WHERE gpa > 3.00;" }, 0,
                "ลำดับคำสั่งพื้นฐานคือ SELECT … FROM … WHERE …",
                "SELECT คอลัมน์ FROM ตาราง WHERE เงื่อนไข — ข้ออื่นขาด FROM, สลับลำดับ หรือใช้คำสั่งที่ไม่มีใน SQL"));
            q.Add(MC("CS202-S02", "ต้องการนับจำนวนนิสิตของแต่ละคณะ ข้อใดถูกต้อง?", "",
                new[] { "SELECT faculty, COUNT(*) FROM students GROUP BY faculty;", "SELECT COUNT(faculty) FROM students;", "SELECT faculty, COUNT(*) FROM students ORDER BY faculty;", "SELECT faculty, SUM(*) FROM students GROUP BY faculty;" }, 0,
                "การสรุปผล \"แยกตามกลุ่ม\" ต้องใช้คำสั่งจัดกลุ่ม",
                "ใช้ GROUP BY faculty คู่กับ COUNT(*) · ข้อที่ไม่มี GROUP BY จะนับรวมทั้งตาราง ส่วน SUM(*) ไม่มีใน SQL"));
            q.Add(MC("CS202-S03", "ต้องการเพิ่มรายวิชา CS202 ลงตาราง courses ข้อใดถูกต้อง?", "",
                new[] { "INSERT INTO courses (code, title) VALUES ('CS202', 'ระบบฐานข้อมูล');", "INSERT courses SET code = 'CS202';", "ADD INTO courses VALUES ('CS202', 'ระบบฐานข้อมูล');", "UPDATE courses VALUES ('CS202', 'ระบบฐานข้อมูล');" }, 0,
                "รูปแบบคือ INSERT INTO ตาราง (คอลัมน์) VALUES (ค่า)",
                "INSERT INTO … VALUES … ใช้เพิ่มแถว · UPDATE ใช้แก้ไขแถวเดิม · ADD INTO ไม่มีใน SQL"));
            q.Add(MC("CS202-S04", "ต้องการแก้เกรดวิชา CS202 ของนิสิตรหัส 6501 เป็น A โดยไม่กระทบแถวอื่น ข้อใดถูกต้อง?", "",
                new[] { "UPDATE enroll SET grade = 'A' WHERE student_id = 6501 AND code = 'CS202';", "UPDATE enroll SET grade = 'A';", "ALTER TABLE enroll SET grade = 'A' WHERE student_id = 6501;", "MODIFY enroll grade = 'A' WHERE student_id = 6501;" }, 0,
                "ระวังคำสั่งที่ไม่มี WHERE",
                "UPDATE … SET … WHERE … เจาะจงแถว · ถ้าไม่มี WHERE จะแก้ทุกแถว · ALTER TABLE ใช้แก้โครงสร้างตาราง ไม่ใช่ข้อมูล"));
            q.Add(MC("CS202-S05", "ต้องการแสดงชื่อนิสิตเรียงตามตัวอักษรจากน้อยไปมาก ข้อใดถูกต้อง?", "",
                new[] { "SELECT name FROM students ORDER BY name ASC;", "SELECT name FROM students SORT BY name;", "SELECT name FROM students GROUP BY name ASC;", "SELECT name FROM students ORDER name;" }, 0,
                "การเรียงผลลัพธ์ใช้คำสั่งสองคำ",
                "ORDER BY คอลัมน์ ASC/DESC ใช้เรียงผลลัพธ์ · SORT BY ไม่มีใน SQL มาตรฐาน · GROUP BY ใช้จัดกลุ่ม"));
            q.Add(MC("CS202-S06", "ข้อจำกัด (constraint) ใดใช้ป้องกันไม่ให้อีเมลของนิสิตซ้ำกัน?", "",
                new[] { "UNIQUE", "NOT NULL", "DEFAULT", "FOREIGN KEY" }, 0,
                "นึกถึงคำที่แปลว่า \"ไม่ซ้ำ\"",
                "UNIQUE บังคับค่าไม่ซ้ำ · NOT NULL บังคับห้ามว่าง · DEFAULT ใส่ค่าเริ่มต้น · FOREIGN KEY อ้างอิงตารางอื่น"));
            return b;
        }

        // =====================================================================
        // CS207 เครือข่ายคอมพิวเตอร์ — จับคู่อุปกรณ์กับหน้าที่ + เรียงขั้นตอนการเชื่อมต่อ
        // =====================================================================
        static CourseExamBank Network()
        {
            var b = new CourseExamBank { courseCode = "CS207", version = 1 };
            var q = b.questions;
            q.Add(Match("CS207-P01", "จับคู่อุปกรณ์เครือข่ายกับหน้าที่",
                new[] { "Switch", "Router", "Access Point", "Firewall" },
                new[] { "ส่งเฟรมไปยังพอร์ตปลายทางตาม MAC address ภายใน LAN", "เลือกเส้นทางส่งแพ็กเก็ตข้ามเครือข่ายตาม IP address", "ให้อุปกรณ์ไร้สายเชื่อมเข้าสู่ LAN", "กรองการรับส่งข้อมูลตามกฎความปลอดภัย" },
                "Switch ทำงานชั้น 2 ส่วน Router ทำงานชั้น 3",
                "Switch ใช้ MAC ภายใน LAN · Router ใช้ IP เชื่อมหลายเครือข่าย · AP ให้บริการ Wi-Fi · Firewall กรองทราฟฟิก"));
            q.Add(Match("CS207-P02", "จับคู่โปรโตคอลกับหน้าที่",
                new[] { "DNS", "DHCP", "HTTP", "SMTP" },
                new[] { "แปลงชื่อโดเมนเป็น IP address", "แจก IP address ให้อุปกรณ์อัตโนมัติ", "รับส่งหน้าเว็บ", "ส่งอีเมล" },
                "ชื่อย่อของแต่ละโปรโตคอลบอกหน้าที่อยู่แล้ว",
                "DNS = Domain Name System · DHCP = Dynamic Host Configuration · HTTP = HyperText Transfer · SMTP = Simple Mail Transfer"));
            q.Add(Match("CS207-P03", "จับคู่ชั้นของแบบจำลอง OSI กับหน้าที่",
                new[] { "Physical", "Data Link", "Network", "Transport" },
                new[] { "ส่งสัญญาณบิตผ่านสื่อกลาง", "จัดการเฟรมและ MAC address ในลิงก์เดียวกัน", "กำหนดที่อยู่ IP และหาเส้นทาง", "ควบคุมการส่งระหว่างโปรเซส (พอร์ต, TCP/UDP)" },
                "ไล่จากล่างขึ้นบน: สัญญาณ → เฟรม → แพ็กเก็ต → เซกเมนต์",
                "ชั้น 1 Physical บิต · ชั้น 2 Data Link เฟรม/MAC · ชั้น 3 Network IP/เส้นทาง · ชั้น 4 Transport พอร์ต/TCP/UDP"));
            q.Add(Match("CS207-P04", "จับคู่บริการกับหมายเลขพอร์ตมาตรฐาน",
                new[] { "HTTP", "HTTPS", "DNS", "SSH" },
                new[] { "80", "443", "53", "22" },
                "พอร์ตของเว็บที่เข้ารหัสมีเลขสามหลักที่ขึ้นต้นด้วย 4",
                "HTTP 80 · HTTPS 443 · DNS 53 · SSH 22"));
            q.Add(Match("CS207-P05", "จับคู่สื่อกลางกับลักษณะเด่น",
                new[] { "สาย UTP", "สายใยแก้วนำแสง", "Wi-Fi" },
                new[] { "สายคู่บิดเกลียว หัว RJ-45 นิยมใน LAN", "ส่งด้วยแสง ระยะไกล ไม่ถูกรบกวนจากคลื่นแม่เหล็กไฟฟ้า", "ส่งด้วยคลื่นวิทยุ ตามมาตรฐาน IEEE 802.11" },
                "ดูว่าแต่ละแบบส่งข้อมูลด้วยอะไร: ไฟฟ้า แสง หรือคลื่นวิทยุ",
                "UTP ใช้สัญญาณไฟฟ้าในสายทองแดง · ไฟเบอร์ใช้แสง · Wi-Fi ใช้คลื่นวิทยุ 802.11"));
            q.Add(Order("CS207-O01", "เรียงขั้นตอนการเปิดการเชื่อมต่อแบบ TCP (Three-way handshake)",
                new[] { "Client ส่ง SYN", "Server ตอบ SYN-ACK", "Client ส่ง ACK", "เริ่มรับส่งข้อมูล" },
                "ฝั่งที่ขอเชื่อมต่อเป็นผู้เริ่มก่อนเสมอ",
                "SYN → SYN-ACK → ACK แล้วจึงเริ่มรับส่งข้อมูล"));
            q.Add(Order("CS207-O02", "เรียงขั้นตอนการขอ IP address จาก DHCP Server",
                new[] { "DHCP Discover — เครื่องลูกข่ายประกาศหา DHCP Server", "DHCP Offer — เซิร์ฟเวอร์เสนอ IP", "DHCP Request — เครื่องลูกข่ายขอใช้ IP ที่เสนอ", "DHCP Acknowledge — เซิร์ฟเวอร์ยืนยัน" },
                "จำคำย่อ D-O-R-A",
                "ขั้นตอน DORA: Discover → Offer → Request → Acknowledge"));
            q.Add(Order("CS207-O03", "เรียงขั้นตอนเมื่อพิมพ์ http://www.msu.ac.th เพื่อเปิดเว็บไซต์",
                new[] { "พิมพ์ URL ในเบราว์เซอร์", "ถาม DNS เพื่อแปลงชื่อโดเมนเป็น IP", "สร้างการเชื่อมต่อ TCP กับเซิร์ฟเวอร์", "ส่งคำขอ HTTP GET", "เซิร์ฟเวอร์ตอบกลับ แล้วเบราว์เซอร์แสดงผล" },
                "ต้องรู้ IP ของปลายทางก่อนจะเชื่อมต่อได้",
                "เบราว์เซอร์ต้องได้ IP จาก DNS ก่อน → เชื่อม TCP → ส่ง HTTP request → รับ response มาแสดง"));
            q.Add(Order("CS207-O04", "เรียงขั้นตอนเชื่อมคอมพิวเตอร์เครื่องใหม่เข้าวง LAN และตรวจสอบการเชื่อมต่อ",
                new[] { "เสียบสาย LAN จากคอมพิวเตอร์เข้าพอร์ตสวิตช์", "คอมพิวเตอร์ขอ IP address จาก DHCP", "ตรวจค่า IP ด้วยคำสั่ง ipconfig", "ping ไปยัง default gateway", "ทดสอบเปิดเว็บไซต์ภายนอก" },
                "ตรวจจากใกล้ตัวไปไกลตัว",
                "เชื่อมต่อทางกายภาพ → ได้ IP → ตรวจค่า IP → ทดสอบถึงเกตเวย์ในวง → ทดสอบออกอินเทอร์เน็ต"));
            q.Add(Order("CS207-O05", "เรียงกระบวนการห่อหุ้มข้อมูล (Encapsulation) ฝั่งผู้ส่ง",
                new[] { "Application สร้างข้อมูล (Data)", "Transport เพิ่ม TCP/UDP header → Segment", "Network เพิ่ม IP header → Packet", "Data Link เพิ่ม header/trailer → Frame", "Physical แปลงเป็นบิตส่งออกสื่อ" },
                "ฝั่งผู้ส่งไล่จากชั้นบนลงชั้นล่าง",
                "Data → Segment → Packet → Frame → Bits (ฝั่งผู้รับทำย้อนกลับ)"));
            q.Add(MC("CS207-M01", "เครือข่าย 192.168.1.0/24 มี IP ที่กำหนดให้เครื่อง (host) ได้กี่หมายเลข?", "",
                new[] { "254", "256", "255", "24" }, 0,
                "ต้องหักหมายเลขเครือข่ายและหมายเลข broadcast ออก",
                "/24 มีบิตโฮสต์ 8 บิต = 256 ค่า หัก network (.0) และ broadcast (.255) เหลือ 254"));
            return b;
        }

        // =====================================================================
        // GE102 ภาษาอังกฤษพื้นฐาน — เลือกคำตอบ + จับคู่ความหมาย
        // =====================================================================
        static CourseExamBank English()
        {
            var b = new CourseExamBank { courseCode = "GE102", version = 1 };
            var q = b.questions;
            q.Add(MC("GE102-M01", "เลือกคำที่ถูกต้อง", "She ___ to school every day.",
                new[] { "goes", "go", "going", "gone" }, 0,
                "ประธานเอกพจน์บุรุษที่ 3 ใน Present Simple",
                "She เป็นประธานเอกพจน์ Present Simple จึงเติม -es → goes"));
            q.Add(MC("GE102-M02", "เลือกคำที่ถูกต้อง", "I have lived here ___ 2020.",
                new[] { "since", "for", "at", "on" }, 0,
                "ตามด้วย \"จุดเวลา\" หรือ \"ช่วงเวลา\"?",
                "since + จุดเริ่มต้นของเวลา (2020) · for + ช่วงเวลา เช่น for five years"));
            q.Add(MC("GE102-M03", "เลือกคำที่ถูกต้อง", "There ___ many students in the library.",
                new[] { "are", "is", "be", "am" }, 0,
                "ดูคำนามที่ตามหลังว่าเป็นเอกพจน์หรือพหูพจน์",
                "many students เป็นพหูพจน์ → There are"));
            q.Add(MC("GE102-M04", "เลือกคำที่ถูกต้อง", "Yesterday we ___ a movie together.",
                new[] { "watched", "watch", "watches", "watching" }, 0,
                "Yesterday บอกเวลาในอดีต",
                "เหตุการณ์ในอดีตที่จบแล้วใช้ Past Simple → watched"));
            q.Add(MC("GE102-M05", "เลือกคำที่ถูกต้อง", "This test is ___ than the last one.",
                new[] { "easier", "more easy", "easiest", "easy" }, 0,
                "มีคำว่า than แปลว่ากำลังเปรียบเทียบสองสิ่ง",
                "คำคุณศัพท์ลงท้าย -y สองพยางค์ เปลี่ยน y เป็น i แล้วเติม -er → easier than"));
            q.Add(MC("GE102-M06", "เพื่อนถามว่า \"How are you?\" คำตอบใดเหมาะสมที่สุด?", "",
                new[] { "I'm fine, thank you.", "I'm nineteen.", "I'm a student.", "At the library." }, 0,
                "คำถามนี้ถามถึงสภาพความเป็นอยู่/สุขภาพ",
                "How are you? ถามว่าสบายดีไหม → I'm fine, thank you. (อายุตอบ How old…? อาชีพตอบ What do you do?)"));
            q.Add(MC("GE102-M07", "เลือกคำบุพบทที่ถูกต้อง", "The final exam is ___ Monday.",
                new[] { "on", "in", "at", "by" }, 0,
                "วันในสัปดาห์ใช้บุพบทตัวเดียวกับวันที่",
                "on + วัน/วันที่ (on Monday) · in + เดือน/ปี · at + เวลา"));
            q.Add(Match("GE102-P01", "จับคู่คำศัพท์ในชั้นเรียนกับความหมาย",
                new[] { "assignment", "deadline", "attendance", "semester" },
                new[] { "งานที่ได้รับมอบหมาย", "กำหนดส่ง", "การเข้าเรียน", "ภาคเรียน" },
                "นึกถึงคำที่อาจารย์ใช้ตอนสั่งงาน",
                "assignment งาน · deadline กำหนดส่ง · attendance การเข้าเรียน · semester ภาคเรียน"));
            q.Add(Match("GE102-P02", "จับคู่สถานที่ในมหาวิทยาลัยกับความหมาย",
                new[] { "library", "cafeteria", "dormitory", "auditorium" },
                new[] { "ห้องสมุด", "โรงอาหาร", "หอพัก", "หอประชุม" },
                "dorm- เกี่ยวกับการนอน · audi- เกี่ยวกับการฟัง",
                "library ห้องสมุด · cafeteria โรงอาหาร · dormitory หอพัก · auditorium หอประชุม"));
            q.Add(Match("GE102-P03", "จับคู่คำกริยาเกี่ยวกับการยืมหนังสือกับความหมาย",
                new[] { "borrow", "lend", "return", "renew" },
                new[] { "ยืม (รับมา)", "ให้ยืม", "คืน", "ต่ออายุการยืม" },
                "borrow กับ lend มีทิศทางตรงข้ามกัน",
                "borrow = ยืมจากคนอื่น · lend = ให้คนอื่นยืม · return = คืน · renew = ต่ออายุ"));
            q.Add(Match("GE102-P04", "จับคู่ประโยคที่ใช้ในห้องเรียนกับความหมาย",
                new[] { "Could you repeat that, please?", "May I come in?", "I have a question.", "See you next class." },
                new[] { "ช่วยพูดอีกครั้งได้ไหมคะ/ครับ", "ขออนุญาตเข้าห้องได้ไหม", "ฉันมีคำถาม", "แล้วเจอกันคาบหน้า" },
                "repeat = ทำซ้ำ · come in = เข้ามา",
                "ประโยคสุภาพที่ใช้บ่อยในชั้นเรียน — Could/May ช่วยให้คำขอสุภาพขึ้น"));
            return b;
        }
    }
}
