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
        public int currentDay = 1;
        public int facultyIndex = 0;   // คณะที่เลือก (0=IT,1=บริหาร,2=วิทย์,3=นิเทศ)
        public float posX, posY, posZ; // ตำแหน่งตัวละคร

        // ไอเทมในกระเป๋า (เก็บเป็น itemId)
        public List<string> inventoryItemIds = new List<string>();

        // ประวัติสอบ (เกรดแต่ละวิชา → คำนวณ GPA ต่อได้) + เวลาในเกม
        public List<float> gradePoints = new List<float>();
        public List<string> doneExams = new List<string>();   // การสอบที่ทำเสร็จแล้ว (กันสอบซ้ำเมื่อโหลดเซฟ)
        public int gameDay = 1;        // วันสะสมของ GameClock
        public float gameMinutes;      // เวลาในวัน (นาทีสะสม)
    }
}
