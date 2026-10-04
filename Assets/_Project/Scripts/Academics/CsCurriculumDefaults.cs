using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Academics
{
    // ค่าเริ่มต้นของหลักสูตรวิทยาการคอมพิวเตอร์ (สมมติ) 4 ปี 8 ภาค 120 หน่วยกิต
    //   ใช้สร้าง asset ใน Resources (Editor tool) และเป็นค่าสำรองถ้าไม่มี asset
    //
    // ตารางเรียน (ปฏิทินรุ่น 2: ภาคละ 10 วัน · คาบละ 2 ชม. · วิชาละ 4 ครั้ง/ภาค):
    //   วันที่ 1–4 เรียน · 5 สอบกลางภาค · 6–9 เรียน (ตารางเดียวกับ 1–4) · 10 สอบปลายภาค
    //   P1 09-11  P2 11-13  P3 13-15  P4 15-17  (ภาคค่ำสำหรับเรียนซ้ำ: 18-20, 20-22)
    //   ช่อง c1..c6 ใช้ 12 จาก 16 คาบของวันที่ 1–4 ไม่ทับกัน · เว้นว่างวันละ 1 คาบ (ว.1 15-17, ว.2 15-17, ว.3 09-11, ว.4 15-17)
    //   ปี 3 มี 4 วิชาบังคับ (c1..c4) → c5/c6 เว้นไว้เป็นช่องวิชาเลือก EA/EB
    //   ปี 4 ฝึกงาน (เช้า) กับโครงงาน (บ่าย) อยู่วันที่ 1/3 (+6/8) ไม่ชนช่องวิชาเลือก (วันที่ 2/4)
    public static class CsCurriculumDefaults
    {
        const string Hall = "อาคารเรียน";
        const string IT = "คณะ IT";
        const string Office = "อาคารบริหาร";
        const string OfficeRoom = "สำนักงานฝึกงาน/สหกิจ";

        // ช่องเวลาครึ่งแรกของภาค (วัน, เริ่ม, จบ) ×2 คาบ — ครึ่งหลังซ้ำ +HalfOffset วัน (วิชาละ 4 ครั้ง)
        public const int HalfOffset = 5;
        static readonly int[][] Slot =
        {
            new[] { 1,  9, 11,  3, 11, 13 },   // c1
            new[] { 1, 11, 13,  3, 13, 15 },   // c2
            new[] { 1, 13, 15,  3, 15, 17 },   // c3
            new[] { 2,  9, 11,  4,  9, 11 },   // c4
            new[] { 2, 11, 13,  4, 11, 13 },   // c5 / EA
            new[] { 2, 13, 15,  4, 13, 15 },   // c6 / EB
        };
        // ภาคค่ำ (เรียนซ้ำ/เก็บตก) 4 ช่องที่ไม่ชนกันเอง (ช่องละราว 9 วิชา)
        static readonly int[][] Evening =
        {
            new[] { 1, 18, 20,  3, 18, 20 },
            new[] { 2, 18, 20,  4, 18, 20 },
            new[] { 1, 20, 22,  3, 20, 22 },
            new[] { 2, 20, 22,  4, 20, 22 },
        };

        // คาบคู่หนึ่งในครึ่งแรก → 4 คาบทั้งภาค (ครึ่งแรก + ซ้ำครึ่งหลัง) เรียงตามวัน
        static List<ClassSession> FourMeetings(int dA, int sA, int eA, int dB, int sB, int eB, System.Func<int, int, int, ClassSession> make)
        {
            return new List<ClassSession>
            {
                make(dA, sA, eA), make(dB, sB, eB),
                make(dA + HalfOffset, sA, eA), make(dB + HalfOffset, sB, eB),
            };
        }

        public static CurriculumDefinition Create()
        {
            var c = ScriptableObject.CreateInstance<CurriculumDefinition>();
            c.name = "CS_Curriculum";
            Fill(c);
            return c;
        }

        public static void Fill(CurriculumDefinition c)
        {
            c.programName = "หลักสูตรวิทยาการคอมพิวเตอร์ (สมมติ)";
            c.facultyIndices = new[] { 0 };
            c.creditCapPerTerm = 18;
            c.registrationDays = 1;
            c.autoConfirmAtDeadline = true;
            c.electivesFromYear = 3;
            c.promotionCredits = new[] { 24, 54, 84 };
            c.electivesRequired = 2;
            c.minGraduationGpa = 2.00f;
            c.maxRegularTerms = 16;
            c.passPoint = 1f;
            // เกณฑ์เดิมของ ExamController (A≥85 B≥70 C≥55 D≥40) + แทรกขั้นบวกตรงกลาง
            c.gradeScale = new List<GradeStep>
            {
                new GradeStep("A", 85f, 4.0f), new GradeStep("B+", 78f, 3.5f),
                new GradeStep("B", 70f, 3.0f), new GradeStep("C+", 63f, 2.5f),
                new GradeStep("C", 55f, 2.0f), new GradeStep("D+", 48f, 1.5f),
                new GradeStep("D", 40f, 1.0f), new GradeStep("F", 0f, 0.0f),
            };
            c.studyWeight = 0.30f; c.midtermWeight = 0.30f; c.finalWeight = 0.40f;
            c.examQuizWeight = 0.60f;
            c.selfStudyShare = 0.5f; c.selfStudyCap = 0.4f;

            var list = new List<CourseDefinition>();

            // ---- ปี 1 ภาค 1 (18) ----
            list.Add(C("GE101", "ภาษาไทยเพื่อการสื่อสาร", 3, CourseCategory.GeneralEducation, 1, 1, 0, Hall));
            list.Add(C("GE102", "ภาษาอังกฤษพื้นฐาน", 3, CourseCategory.GeneralEducation, 1, 1, 1, Hall));
            list.Add(C("GE103", "ทักษะการเรียนรู้และการใช้ชีวิตในมหาวิทยาลัย", 3, CourseCategory.GeneralEducation, 1, 1, 2, Hall));
            list.Add(C("CS101", "ความรู้เบื้องต้นทางวิทยาการคอมพิวเตอร์", 3, CourseCategory.Core, 1, 1, 3, IT));
            list.Add(C("CS102", "การเขียนโปรแกรมเบื้องต้น", 3, CourseCategory.Core, 1, 1, 4, IT));
            list.Add(C("MA101", "คณิตศาสตร์สำหรับคอมพิวเตอร์", 3, CourseCategory.Core, 1, 1, 5, Hall));
            // ---- ปี 1 ภาค 2 (18) ----
            list.Add(C("GE104", "ภาษาอังกฤษเพื่อการสื่อสาร", 3, CourseCategory.GeneralEducation, 1, 2, 0, Hall, "GE102"));
            list.Add(C("GE105", "การคิดเชิงวิพากษ์และการแก้ปัญหา", 3, CourseCategory.GeneralEducation, 1, 2, 1, Hall));
            list.Add(C("GE106", "พลเมืองดิจิทัลและการรู้เท่าทันสื่อ", 3, CourseCategory.GeneralEducation, 1, 2, 2, Hall));
            list.Add(C("CS103", "การเขียนโปรแกรมเชิงวัตถุ", 3, CourseCategory.Core, 1, 2, 3, IT, "CS102"));
            list.Add(C("CS104", "ระบบดิจิทัลและองค์ประกอบคอมพิวเตอร์", 3, CourseCategory.Core, 1, 2, 4, IT, "CS101"));
            list.Add(C("MA102", "คณิตศาสตร์ไม่ต่อเนื่อง", 3, CourseCategory.Core, 1, 2, 5, Hall, "MA101"));
            // ---- ปี 2 ภาค 1 (18) ----
            list.Add(C("CS201", "โครงสร้างข้อมูลและขั้นตอนวิธี", 3, CourseCategory.Core, 2, 1, 0, IT, "CS103", "MA102"));
            list.Add(C("CS202", "ระบบฐานข้อมูล", 3, CourseCategory.Core, 2, 1, 1, IT, "CS102"));
            list.Add(C("CS203", "การพัฒนาเว็บไซต์เบื้องต้น", 3, CourseCategory.Core, 2, 1, 2, IT, "CS102"));
            list.Add(C("CS204", "สถาปัตยกรรมคอมพิวเตอร์", 3, CourseCategory.Core, 2, 1, 3, IT, "CS104"));
            list.Add(C("MA201", "ความน่าจะเป็นและสถิติสำหรับคอมพิวเตอร์", 3, CourseCategory.Core, 2, 1, 4, Hall, "MA101"));
            list.Add(C("GE201", "การสื่อสารและการทำงานเป็นทีม", 3, CourseCategory.GeneralEducation, 2, 1, 5, Hall));
            // ---- ปี 2 ภาค 2 (18) ----
            list.Add(C("CS205", "การวิเคราะห์และออกแบบระบบ", 3, CourseCategory.Core, 2, 2, 0, IT, "CS103", "CS202"));
            list.Add(C("CS206", "ระบบปฏิบัติการ", 3, CourseCategory.Core, 2, 2, 1, IT, "CS201", "CS204"));
            list.Add(C("CS207", "เครือข่ายคอมพิวเตอร์", 3, CourseCategory.Core, 2, 2, 2, IT, "CS104"));
            list.Add(C("CS208", "การพัฒนาเว็บแอปพลิเคชัน", 3, CourseCategory.Core, 2, 2, 3, IT, "CS202", "CS203"));
            list.Add(C("CS209", "การออกแบบประสบการณ์และส่วนติดต่อผู้ใช้", 3, CourseCategory.Core, 2, 2, 4, IT, "CS203"));
            list.Add(C("GE202", "ผู้ประกอบการและธุรกิจดิจิทัล", 3, CourseCategory.GeneralEducation, 2, 2, 5, Hall));
            // ---- ปี 3 ภาค 1 (12 + วิชาเลือก 3) ----
            list.Add(C("CS301", "วิศวกรรมซอฟต์แวร์", 3, CourseCategory.Core, 3, 1, 0, IT, "CS205"));
            list.Add(C("CS302", "ปัญญาประดิษฐ์เบื้องต้น", 3, CourseCategory.Core, 3, 1, 1, IT, "CS201", "MA201"));
            list.Add(C("CS303", "การพัฒนาแอปพลิเคชันบนอุปกรณ์เคลื่อนที่", 3, CourseCategory.Core, 3, 1, 2, IT, "CS103", "CS202"));
            list.Add(C("CS304", "ความมั่นคงปลอดภัยทางไซเบอร์", 3, CourseCategory.Core, 3, 1, 3, IT, "CS206", "CS207"));
            // ---- ปี 3 ภาค 2 (12 + วิชาเลือก 3) ----
            list.Add(C("CS305", "การทดสอบและประกันคุณภาพซอฟต์แวร์", 3, CourseCategory.Core, 3, 2, 0, IT, "CS301"));
            list.Add(C("CS306", "การประมวลผลแบบคลาวด์", 3, CourseCategory.Core, 3, 2, 1, IT, "CS206", "CS207"));
            list.Add(C("CS307", "ระเบียบวิธีวิจัยและการเสนอโครงงาน", 3, CourseCategory.Core, 3, 2, 2, IT, "CS301", "MA201"));
            list.Add(C("CS308", "จริยธรรมและกฎหมายสำหรับวิชาชีพคอมพิวเตอร์", 3, CourseCategory.Core, 3, 2, 3, Hall));
            // ---- ปี 4 ภาค 1 (9) — ฝึกงานช่วงเช้า / โครงงาน 1 ช่วงบ่าย ----
            var intern = C("CS401", "การฝึกงานทางวิทยาการคอมพิวเตอร์", 6, CourseCategory.Internship, 4, 1, -1, Office, "CS301", "CS308");
            intern.minEarnedCredits = 90;
            intern.sessions = FourMeetings(1, 9, 13, 3, 9, 13, (d, a, b) => S(d, a, b, Office, "CS401"));
            list.Add(intern);
            var p1 = C("CS402", "โครงงานวิทยาการคอมพิวเตอร์ 1", 3, CourseCategory.Project, 4, 1, -1, IT, "CS307");
            p1.sessions = FourMeetings(1, 13, 15, 3, 13, 15, (d, a, b) => S(d, a, b, IT, "CS402"));
            list.Add(p1);
            // ---- ปี 4 ภาค 2 (9) ----
            var p2 = C("CS403", "โครงงานวิทยาการคอมพิวเตอร์ 2", 6, CourseCategory.Project, 4, 2, -1, IT, "CS402");
            p2.sessions = FourMeetings(1, 13, 17, 3, 13, 17, (d, a, b) => S(d, a, b, IT, "CS403"));
            list.Add(p2);
            var sem = C("CS404", "สัมมนาและการเตรียมความพร้อมสู่อาชีพ", 3, CourseCategory.Core, 4, 2, -1, Hall, "CS401");
            sem.sessions = FourMeetings(1, 9, 11, 3, 9, 11, (d, a, b) => S(d, a, b, Hall, "CS404"));
            list.Add(sem);

            // ---- วิชาเลือกเฉพาะทาง (EA = ช่อง c5, EB = ช่อง c6) ----
            list.Add(C("EL301", "การพัฒนาเกมด้วย Unity", 3, CourseCategory.Elective, 0, 0, 4, IT, "CS103", "CS201"));
            list.Add(C("EL302", "คอมพิวเตอร์กราฟิกและแบบจำลองสามมิติ", 3, CourseCategory.Elective, 0, 0, 5, IT, "CS201", "MA101"));
            list.Add(C("EL303", "วิทยาการข้อมูลเบื้องต้น", 3, CourseCategory.Elective, 0, 0, 4, IT, "CS202", "MA201"));
            list.Add(C("EL304", "อินเทอร์เน็ตของสรรพสิ่ง", 3, CourseCategory.Elective, 0, 0, 5, IT, "CS102", "CS207"));
            list.Add(C("EL305", "การพัฒนาซอฟต์แวร์โอเพนซอร์ส", 3, CourseCategory.Elective, 0, 0, 4, IT, "CS103", "CS208"));
            list.Add(C("EL306", "การเรียนรู้ของเครื่อง", 3, CourseCategory.Elective, 0, 0, 5, IT, "CS302"));
            list.Add(C("EL307", "การออกแบบและพัฒนาเกมขั้นสูง", 3, CourseCategory.Elective, 0, 0, 5, IT, "EL301"));
            list.Add(C("EL308", "การออกแบบและพัฒนาระบบความเป็นจริงเสมือน", 3, CourseCategory.Elective, 0, 0, 4, IT, "EL302"));

            // ตอนเรียนซ้ำ (ภาคค่ำ) — วนช่อง 4 แบบตามลำดับวิชาบังคับ (วิชาละ 4 ครั้ง/ภาค เหมือนตอนปกติ)
            //   วิชาในช่องเดียวกันเรียนพร้อมกัน → ต้องคนละห้อง: ใช้ห้องเดิมของวิชาก่อน ถ้าไม่ว่างหยิบห้องว่างประเภทเดียวกันห้องถัดไป
            //   (ช่องค่ำ 4 ช่องไม่ทับกันเอง และไม่ทับคาบกลางวัน → ห้องหนึ่งใช้ได้ช่องละหนึ่งวิชา)
            var usedByLane = new List<HashSet<string>>();
            for (int k = 0; k < Evening.Length; k++) usedByLane.Add(new HashSet<string>());
            int lane = 0;
            foreach (var d in list)
            {
                if (!d.IsRequired) continue;
                int li = lane % Evening.Length;
                var e = Evening[li];
                string roomId = PickEveningRoom(d, usedByLane[li]);
                string building = d.sessions[0].building;
                string room = DisplayRoom(roomId) + " (ภาคค่ำ)";
                d.retakeSessions = FourMeetings(e[0], e[1], e[2], e[3], e[4], e[5], (dd, a, b) => new ClassSession(dd, a, b, building, room, roomId));
                lane++;
            }
            c.courses = list;
        }

        // ===== ห้องเรียนตอนปกติ (รหัสวิชา → ห้อง) =====
        //   ช่อง c1..c6 ไม่ทับกันเลย → ห้องหนึ่งรับได้ช่องละหนึ่งวิชา (ใช้ร่วมกันคนละเวลา) · ตรวจชนตามภาค (1/2) — วิชาเลือกเปิดทั้งสองภาค
        //   เขียนโปรแกรม → แล็บคอม · ทฤษฎี → ห้องบรรยาย (CS: ตึก IT / GE+MA: ตึก GE) · สัมมนา → ห้องสัมมนา · ฝึกงาน → อาคารบริหาร
        public static readonly Dictionary<string, string> RegularRoom = new Dictionary<string, string>
        {
            // ภาค 1
            { "GE101", "GE-101" }, { "GE102", "GE-101" }, { "GE103", "GE-101" }, { "MA201", "GE-101" }, { "MA101", "GE-101" }, { "GE201", "GE-102" },
            { "CS201", "IT-201" }, { "CS202", "IT-201" }, { "CS203", "IT-201" }, { "CS102", "IT-201" }, { "CS303", "IT-202" }, { "CS402", "IT-203" },
            { "CS301", "IT-301" }, { "CS302", "IT-301" }, { "CS304", "IT-301" }, { "CS101", "IT-101" }, { "CS204", "IT-102" },
            { "CS401", ClassroomDefaults.AdminOffice },
            // ภาค 2
            { "GE104", "GE-101" }, { "GE105", "GE-101" }, { "GE106", "GE-101" }, { "CS308", "GE-101" }, { "MA102", "GE-101" }, { "GE202", "GE-102" },
            { "CS305", "IT-201" }, { "CS306", "IT-201" }, { "CS103", "IT-201" }, { "CS208", "IT-202" }, { "CS403", "IT-203" },
            { "CS205", "IT-301" }, { "CS206", "IT-301" }, { "CS207", "IT-301" }, { "CS307", "IT-302" }, { "CS104", "IT-101" }, { "CS209", "IT-102" },
            { "CS404", "IT-405" },
            // วิชาเลือก (ช่อง c5/c6 ทั้งสองภาค)
            { "EL301", "IT-202" }, { "EL302", "IT-202" }, { "EL303", "IT-203" }, { "EL304", "IT-203" },
            { "EL305", "IT-204" }, { "EL306", "IT-204" }, { "EL308", "IT-205" }, { "EL307", "IT-205" },
        };

        public static ClassroomType TypeOfRoom(string roomId)
        {
            if (roomId == ClassroomDefaults.AdminOffice) return ClassroomType.Office;
            if (System.Array.IndexOf(ClassroomDefaults.ITLabs, roomId) >= 0) return ClassroomType.ComputerLab;
            if (System.Array.IndexOf(ClassroomDefaults.ITSeminars, roomId) >= 0) return ClassroomType.Seminar;
            return ClassroomType.Lecture;
        }

        static string DisplayRoom(string roomId)
        {
            switch (TypeOfRoom(roomId))
            {
                case ClassroomType.ComputerLab: return "แล็บคอม " + roomId;
                case ClassroomType.Seminar: return "ห้องสัมมนา " + roomId;
                case ClassroomType.Office: return OfficeRoom;
                default: return "ห้องบรรยาย " + roomId;
            }
        }

        // คาบเรียนพร้อมห้องของวิชา (ใช้กับตารางพิเศษปี 4)
        static ClassSession S(int day, int sh, int eh, string building, string code)
        {
            string rid = RegularRoom.TryGetValue(code, out var r) ? r : "";
            return new ClassSession(day, sh, eh, building, DisplayRoom(rid), rid);
        }

        // ห้องภาคค่ำ: ห้องเดิมก่อน → ห้องประเภทเดียวกัน → ห้องบรรยายสำรอง (ตึก IT → GE → สัมมนา)
        static string PickEveningRoom(CourseDefinition d, HashSet<string> used)
        {
            string home = RegularRoom.TryGetValue(d.code, out var r) ? r : "";
            var cand = new List<string>();
            if (!string.IsNullOrEmpty(home)) cand.Add(home);
            switch (TypeOfRoom(home))
            {
                case ClassroomType.ComputerLab: cand.AddRange(ClassroomDefaults.ITLabs); break;
                case ClassroomType.Office: cand.Add(ClassroomDefaults.AdminOffice); break;
                case ClassroomType.Seminar: cand.AddRange(ClassroomDefaults.ITSeminars); cand.AddRange(ClassroomDefaults.ITLectures); break;
                default:
                    if (home.StartsWith("GE-")) { cand.AddRange(ClassroomDefaults.GELectures); cand.AddRange(ClassroomDefaults.ITLectures); }
                    else { cand.AddRange(ClassroomDefaults.ITLectures); cand.AddRange(ClassroomDefaults.GELectures); }
                    cand.AddRange(ClassroomDefaults.ITSeminars);
                    break;
            }
            foreach (var id in cand) if (used.Add(id)) return id;
            Debug.LogError($"[Curriculum] ห้องภาคค่ำไม่พอสำหรับ {d.code} — ใช้ {home} (จะถูกรายงานว่าห้องชน)");
            return home;
        }

        static CourseDefinition C(string code, string title, int credits, CourseCategory cat, int year, int sem, int slot, string building, params string[] prereq)
        {
            var d = new CourseDefinition
            {
                code = code, title = title, credits = credits, category = cat,
                planYear = year, planSemester = sem,
                prerequisites = new List<string>(prereq),
            };
            if (slot >= 0)
            {
                var s = Slot[slot];
                d.sessions = FourMeetings(s[0], s[1], s[2], s[3], s[4], s[5], (dd, a, b) => S(dd, a, b, building, code));
            }
            return d;
        }
    }
}
