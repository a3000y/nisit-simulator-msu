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
        public float posX, posY, posZ; // ตำแหน่งตัวละคร

        // ไอเทมในกระเป๋า (เก็บเป็น itemId)
        public List<string> inventoryItemIds = new List<string>();

        // TODO (M5): เพิ่มรายวิชาที่ลงทะเบียน, เกรด, ภารกิจที่ทำแล้ว
    }
}
