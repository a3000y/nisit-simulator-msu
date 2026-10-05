using System;
using System.Collections.Generic;

namespace NisitSimulator.Academics
{
    // ===== ข้อมูลการเรียนของผู้เล่น (ถูกเซฟลง SaveData เป็น JSON) =====
    //   อ้างอิงรายวิชาด้วยรหัสเท่านั้น → แก้ชื่อ/เวลาในหลักสูตรภายหลังได้โดยเซฟเดิมไม่พัง

    // การลงเรียนหนึ่งครั้ง (หนึ่งวิชา × หนึ่งภาค) — เก็บทุกครั้ง รวมครั้งที่ตก = ประวัติเรียนซ้ำ
    [Serializable]
    public class Enrollment
    {
        public string code;
        public int attempt = 1;          // ครั้งที่ลงเรียนวิชานี้
        public int termSerial;           // ภาคที่ลง (นับภาคปกติที่เล่นจริง 1,2,3...)
        public int calendarYear;         // ปีการศึกษาที่ (นับปีที่เล่นจริง)
        public int semIndex;             // 0 = ภาคต้น, 1 = ภาคปลาย
        public int classYear;            // ชั้นปีตอนลง
        public bool retakeSection;       // true = ตอนเรียนซ้ำ (ภาคค่ำ)

        // ความคืบหน้า (หน่วย = ชั่วโมงเรียนที่มีคุณภาพ) แยกตามคาบ
        public float progress;
        public float selfStudy;
        public List<string> meetingKeys = new List<string>();   // "วัน:ลำดับคาบ"
        public List<int> meetingTicks = new List<int>();        // ชั่วโมงที่นับแล้วของคาบนั้น

        // คะแนนสอบ 0..1 (-1 = ยังไม่สอบ)
        public float midterm = -1f;
        public float final = -1f;
        public bool missedMidterm;
        public bool missedFinal;

        // คะแนนพิเศษจากเหตุการณ์ระหว่างเรียน (0..1 บวกเข้าคะแนนสอบรอบนั้น มีเพดาน) — เซฟเก่าไม่มี = 0
        public float classBonusMid;
        public float classBonusFinal;
        // เหตุการณ์ระหว่างเรียนต่อคาบ "วัน:ลำดับคาบ=นาทีที่จะเกิด" (-1 = คาบนี้ไม่เกิด, done = เกิดไปแล้ว) — กันสุ่มซ้ำหลังลุก/โหลดเซฟ
        public List<string> classEventKeys = new List<string>();

        // ผลการเรียน (ประกาศตอนจบภาค)
        public bool graded;
        public float score;
        public string letter = "";
        public float point;
        public bool passed;
        public bool transfer;            // เทียบโอนจากเซฟเก่า (ไม่คิด GPA)

        public int TicksFor(string key)
        {
            int i = meetingKeys.IndexOf(key);
            return i >= 0 && i < meetingTicks.Count ? meetingTicks[i] : 0;
        }

        public void AddTick(string key)
        {
            int i = meetingKeys.IndexOf(key);
            if (i < 0) { meetingKeys.Add(key); meetingTicks.Add(1); }
            else
            {
                while (meetingTicks.Count <= i) meetingTicks.Add(0);
                meetingTicks[i]++;
            }
        }

        public int MeetingsAttended
        {
            get { int n = 0; foreach (var t in meetingTicks) if (t > 0) n++; return n; }
        }
    }

    // สถานะภาคเรียนปัจจุบัน
    [Serializable]
    public class TermState
    {
        public int serial;               // ลำดับภาคปกติ (ภาคฤดูร้อน = เลขเดียวกับภาคก่อนหน้า)
        public int calendarYear;
        public int semIndex;             // 0 ต้น 1 ปลาย 2 ฤดูร้อน
        public int classYear;            // ชั้นปีตอนเปิดภาค
        public int planSemester;         // 1 / 2 (0 = ปิดภาค)
        public bool isBreak;             // ภาคฤดูร้อน — ไม่เปิดสอนในหลักสูตรนี้
        public bool isExtra;             // ภาคเรียนเพิ่มเติม (เรียนครบแผน 4 ปีแล้วแต่ยังไม่จบ)
        public bool registrationOpen;
        public bool confirmed;
        public bool closed;
        public bool lateRegistration;    // ย้ายจากเซฟเก่า → ลงทะเบียนได้ถึงสิ้นภาค
        public List<string> selected = new List<string>();
    }

    [Serializable]
    public class AcademicRecord
    {
        public int version = 1;
        public int classYear = 1;              // ชั้นปี (แยกจากจำนวนภาค/ปีที่เล่นจริง)
        public int regularTermsStarted;        // จำนวนภาคปกติที่เปิดไปแล้ว
        public bool finishedPlan;              // ผ่านภาค 2 ของชั้นปี 4 มาแล้ว → ต่อไปเป็นภาคเพิ่มเติม
        public bool graduated;
        public bool migratedFromLegacy;
        public bool hasCurrent;
        public TermState current = new TermState();
        public List<Enrollment> enrollments = new List<Enrollment>();

        // ===== หน้าแสดงผลตอนจบเทอม (TermResultUI) — แสดงครั้งเดียวต่อเทอม =====
        //   pending > lastReported = ยังไม่กดตกลง (ปิดเกมก่อน → โหลดแล้วแสดงอีกครั้ง) · เซฟเก่าไม่มี = 0 (ไม่แสดง)
        public int pendingReportSerial;
        public int lastReportedSerial;
        public bool pendingReportPromoted;      // เลื่อนชั้นตอนปิดภาคนั้น
        public bool pendingReportExhausted;     // พ้นสภาพ (เรียนเกินจำนวนภาค)
        public bool pendingReportExtra;         // เป็นภาคเรียนเพิ่มเติม
        // ===== กังวลก่อนสอบ (ExamStress) — คีย์กันคิดซ้ำเมื่อโหลดเซฟวันเดียวกัน · เซฟเก่าไม่มี = ว่าง =====
        public List<string> stressKeys = new List<string>();

        public bool HasPendingReport => pendingReportSerial > 0 && pendingReportSerial > lastReportedSerial;

        public TermState Current => hasCurrent ? current : null;
    }
}
