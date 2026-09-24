using System;
using System.Collections.Generic;

namespace NisitSimulator.SaveLoad
{
    // โครงข้อมูลที่จะถูกบันทึกลงไฟล์ (ต้อง [Serializable] เพื่อแปลงเป็น JSON ได้)
    [Serializable]
    public class SaveData
    {
        // สถานะตัวละคร
        public float energy;
        public float health;
        public float hunger;
        public float knowledge;
        public float satisfaction;
        public int money;
        public int exp;

        // ความคืบหน้า
        public int currentYear = 1;    // ชั้นปี 1-4
        public int dayInYear = 1;      // วันในปีการศึกษาปัจจุบัน
        public int facultyIndex = 0;   // คณะที่เลือก (0=IT,1=บริหาร,2=วิทย์,3=นิเทศ)
        public float posX, posY, posZ; // ตำแหน่งตัวละคร

        // ไอเทมในกระเป๋า (เก็บเป็น itemId)
        public List<string> inventoryItemIds = new List<string>();

        // ประวัติสอบ (เกรดแต่ละวิชา → คำนวณ GPA ต่อได้) + เวลาในเกม
        public List<float> gradePoints = new List<float>();
        public List<string> doneExams = new List<string>();   // การสอบที่ทำเสร็จแล้ว (กันสอบซ้ำเมื่อโหลดเซฟ)
        public int gameDay = 1;        // วันสะสมของ GameClock
        public float gameMinutes;      // เวลาในวัน (นาทีสะสม)

        // ===== ภารกิจรายวัน (เควส) — กันสุ่มใหม่/ความคืบหน้าหายตอนโหลด =====
        public bool hasQuestData = false;                     // false = เซฟเก่าไม่มีเควส → สุ่มใหม่
        public List<QuestSave> quests = new List<QuestSave>();
        public float questAccK, questAccX, questAccM, questAccSpent;  // ตัวสะสมของวันนี้
        public float questLastK; public int questLastM, questLastX;   // สแนปช็อตล่าสุด (ไว้คำนวณส่วนต่าง)

        // ===== เข้าเรียนของวันนี้ ("stationKey:session") — กันเข้าเรียนซ้ำหลังโหลด =====
        public List<string> classAttendance = new List<string>();

        // ===== ผลกระทบทั้งวัน (PlayerEffects: ป่วย/ไฟแรง) =====
        public float fxMove = 1f, fxDrain = 1f, fxKnow = 1f;

        // ===== ความสัมพันธ์กับ NPC (id -> คะแนนสนิท) =====
        public List<string> relIds = new List<string>();
        public List<int> relPoints = new List<int>();

        // ===== เป้าหมาย GoTo ที่ค้าง (เดินไปทำ) =====
        public bool hasObjective = false;
        public string objDoor = "";
        public string objText = "";
        public float objEnergy, objHealth, objHunger, objKnowledge, objSatisfaction;
        public int objMoney, objExp;
    }

    // หนึ่งภารกิจที่สุ่มได้ (เก็บพอให้สร้างใหม่ + สถานะสำเร็จ)
    [Serializable]
    public class QuestSave
    {
        public string desc;
        public int metric;      // (int ของ QuestSystem.Metric)
        public float target;
        public int rewardMoney;
        public int rewardExp;
        public float rewardSat;
        public bool done;
    }
}
