using System;
using System.Collections.Generic;

namespace NisitSimulator.Academics.ExamMinigame
{
    // ===== สถานะการสอบหนึ่งรอบ (ถูกเซฟลง SaveData.examSession เป็น JSON) =====
    //   เก็บชุดข้อ + ลำดับที่สุ่มไว้ตั้งแต่กดเริ่ม → เปิด UI ใหม่/โหลดเซฟแล้วไม่สุ่มใหม่
    //   คำตอบเก็บเป็น "index เดิมในคลัง" เสมอ → คะแนนไม่เปลี่ยนตามการสลับตัวเลือก

    [Serializable]
    public class ExamQuestionState
    {
        public string questionId;
        public int type;                                   // (int ของ ExamQuestionType) ไว้ตรวจว่าข้อในคลังยังเป็นชนิดเดิม
        public float maxPoints;                            // คะแนนเต็มตอนเริ่มสอบ (แก้คลังภายหลังไม่กระทบรอบนี้)
        public List<int> displayOrder = new List<int>();   // ลำดับแสดง: ตัวเลือก / บล็อกในกอง / ฝั่งขวาของจับคู่ (index เดิม)
        public int selected = -1;                          // เลือกคำตอบ/หาจุดผิด: index เดิม
        public List<int> order = new List<int>();          // เรียงลำดับ: index เดิมของบล็อกตามที่ผู้เล่นวาง
        public List<int> match = new List<int>();          // จับคู่: match[ซ้าย i] = index เดิมฝั่งขวา (-1 = ยังไม่จับ)
        public bool hintUsed;                              // ใช้คำใบ้ข้อนี้แล้ว (ไม่ให้ใช้ซ้ำ)
        public List<int> eliminated = new List<int>();     // ตัวเลือก/บรรทัดที่คำใบ้ตัดทิ้ง (index เดิม)
        public int revealed = -1;                          // เรียงลำดับ: บล็อกแรก · จับคู่: ซ้ายที่เปิดคู่ให้ (index เดิม)
        public float earned;                               // คะแนนที่ได้ (หลังส่ง)
        public bool missingInBank;                         // ข้อถูกลบออกจากคลังภายหลัง → ไม่นับคะแนนเต็ม
    }

    [Serializable]
    public class ExamSessionState
    {
        public int version = 1;
        public string playerId = "";
        public string courseCode = "";
        public string courseTitle = "";
        public int calendarYear;          // ปีการศึกษาที่เล่นจริง
        public int semIndex;              // 0 ต้น 1 ปลาย
        public int termSerial;            // ภาคเรียนลำดับที่ (ตรงกับ Enrollment.termSerial)
        public int attempt;               // ครั้งที่ลงเรียนวิชานี้
        public bool isFinal;              // false = กลางภาค · true = ปลายภาค
        public string roundKey = "";      // ปี-ภาค-ชนิด-วิชา-ภาคลำดับ (รอบสอบนี้)
        public int bankVersion;           // เวอร์ชันคลังข้อสอบที่ใช้
        public float timeLimit;
        public float remainingSeconds;
        public int hintsAllowed;
        public int hintsUsed;
        public float knowledgeAtStart;    // ความรู้ของวิชานี้ 0–100 ตอนเริ่ม (ไว้คำนวณสิทธิ์คำใบ้ — โหลดแล้วไม่คำนวณใหม่)
        public int currentIndex;
        public List<ExamQuestionState> questions = new List<ExamQuestionState>();

        public bool started;
        public bool submitted;
        public bool recorded;             // ส่งคะแนนเข้าระบบเกรดแล้ว (กันบันทึก/ให้หน่วยกิตซ้ำ)
        public bool autoSubmitted;        // หมดเวลา → ส่งอัตโนมัติ
        public bool quit;                 // เลิกสอบกลางคัน (ส่งคำตอบที่มี)

        public float earned;
        public float maxPoints;
        public int score100;              // คะแนนมินิเกม 0–100 (ความถูกต้องล้วน)
        public float recordedExamScore = -1f;   // คะแนนสอบที่บันทึกลงรายวิชาตามสูตรเดิม (0..1)

        public bool InProgress => started && !submitted;

        public static string MakeRoundKey(int calendarYear, int semIndex, bool final, string code, int termSerial) =>
            $"{calendarYear}-{semIndex}-{(final ? "final" : "mid")}-{code}-t{termSerial}";
    }
}
