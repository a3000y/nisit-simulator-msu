using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Academics
{
    // ค่าเริ่มต้นของหลักสูตรวิทยาการคอมพิวเตอร์ (สมมติ) 4 ปี 8 ภาค 120 หน่วยกิต
    //   ใช้สร้าง asset ใน Resources (Editor tool) และเป็นค่าสำรองถ้าไม่มี asset
    //
    // ตารางเรียน (ภาคละ 3 วัน · คาบละ 2 ชม. · วิชาละ 2 คาบ/ภาค):
    //   P1 09-11  P2 11-13  P3 13-15  P4 15-17  (ภาคค่ำสำหรับเรียนซ้ำ: 18-20, 20-22)
    //   ภาคที่มี 6 วิชา ใช้ช่อง c1..c6 ครบ 12 คาบพอดี ไม่มีวิชาไหนชนกัน
    //   ปี 3 มี 4 วิชาบังคับ (c1..c4) → c5/c6 เว้นไว้เป็นช่องวิชาเลือก EA/EB
    //   ปี 4 ฝึกงาน (เช้า) กับโครงงาน 1 (บ่าย) แยกเวลากัน และไม่ชนช่องวิชาเลือก
    public static class CsCurriculumDefaults
    {
        const string Hall = "อาคารเรียน";
        const string IT = "คณะ IT";
        const string Office = "อาคารบริหาร";
        const string HallRoom = "ห้องบรรยายรวม";
        const string LabRoom = "ห้องปฏิบัติการคอมพิวเตอร์";
        const string OfficeRoom = "สำนักงานฝึกงาน/สหกิจ";

        // ช่องเวลา (วัน, เริ่ม, จบ) ×2 คาบ
        static readonly int[][] Slot =
        {
            new[] { 1,  9, 11,  2, 13, 15 },   // c1
            new[] { 1, 11, 13,  2, 15, 17 },   // c2
            new[] { 1, 13, 15,  3,  9, 11 },   // c3
            new[] { 1, 15, 17,  3, 11, 13 },   // c4
            new[] { 2,  9, 11,  3, 13, 15 },   // c5 / EA
            new[] { 2, 11, 13,  3, 15, 17 },   // c6 / EB
        };
        // ภาคค่ำ (เรียนซ้ำ/เก็บตก) 3 ช่องที่ไม่ชนกันเอง
        static readonly int[][] Evening =
        {
            new[] { 1, 18, 20,  2, 18, 20 },
            new[] { 1, 20, 22,  3, 18, 20 },
            new[] { 2, 20, 22,  3, 20, 22 },
        };

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
            intern.sessions = new List<ClassSession> { new ClassSession(1, 9, 13, Office, OfficeRoom), new ClassSession(3, 9, 13, Office, OfficeRoom) };
            list.Add(intern);
            var p1 = C("CS402", "โครงงานวิทยาการคอมพิวเตอร์ 1", 3, CourseCategory.Project, 4, 1, -1, IT, "CS307");
            p1.sessions = new List<ClassSession> { new ClassSession(1, 13, 15, IT, LabRoom), new ClassSession(2, 13, 15, IT, LabRoom) };
            list.Add(p1);
            // ---- ปี 4 ภาค 2 (9) ----
            var p2 = C("CS403", "โครงงานวิทยาการคอมพิวเตอร์ 2", 6, CourseCategory.Project, 4, 2, -1, IT, "CS402");
            p2.sessions = new List<ClassSession> { new ClassSession(1, 13, 17, IT, LabRoom), new ClassSession(2, 13, 17, IT, LabRoom) };
            list.Add(p2);
            var sem = C("CS404", "สัมมนาและการเตรียมความพร้อมสู่อาชีพ", 3, CourseCategory.Core, 4, 2, -1, Hall, "CS401");
            sem.sessions = new List<ClassSession> { new ClassSession(1, 9, 11, Hall, HallRoom), new ClassSession(3, 9, 11, Hall, HallRoom) };
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

            // ตอนเรียนซ้ำ (ภาคค่ำ) — วนช่อง 3 แบบตามลำดับวิชาบังคับ
            int lane = 0;
            foreach (var d in list)
            {
                if (!d.IsRequired) continue;
                var e = Evening[lane % Evening.Length];
                string room = RoomFor(d.sessions[0].building);
                d.retakeSessions = new List<ClassSession>
                {
                    new ClassSession(e[0], e[1], e[2], d.sessions[0].building, room + " (ภาคค่ำ)"),
                    new ClassSession(e[3], e[4], e[5], d.sessions[0].building, room + " (ภาคค่ำ)"),
                };
                lane++;
            }
            c.courses = list;
        }

        static string RoomFor(string building) => building == Hall ? HallRoom : building == Office ? OfficeRoom : LabRoom;

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
                string room = RoomFor(building);
                d.sessions = new List<ClassSession>
                {
                    new ClassSession(s[0], s[1], s[2], building, room),
                    new ClassSession(s[3], s[4], s[5], building, room),
                };
            }
            return d;
        }
    }
}
