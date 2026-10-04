using System;

namespace NisitSimulator.Systems
{
    // ศูนย์กลาง "ผู้เล่นทำกิจกรรมอะไร" — สถานี/ร้าน/NPC ยิงคีย์มาที่นี่ แล้วระบบภารกิจ/สรุปวันฟังต่อ
    //   แยกออกมาเพื่อไม่ให้ระบบภารกิจต้องผูกกับสถานีทุกตัวโดยตรง
    public static class GameplayEvents
    {
        public const string Class   = "class";    // เข้าเรียนจบคาบ
        public const string Eat     = "eat";      // กินอาหาร (โรงอาหาร/ของกินในกระเป๋า)
        public const string Work    = "work";     // ทำงานพาร์ทไทม์จบกะ
        public const string Talk    = "talk";     // คุยกับเพื่อน
        public const string Gift    = "gift";     // ให้ของขวัญ
        public const string Buy     = "buy";      // ซื้อของเข้ากระเป๋า
        public const string UseItem = "useItem";  // ใช้ไอเทมจากกระเป๋า
        public const string Study   = "study";    // อ่านหนังสือ/ติว (กิจกรรมที่ได้ความรู้)
        public const string Relax   = "relax";    // พักผ่อน/กิจกรรมคลายเครียด
        public const string Sleep   = "sleep";    // นอน
        public const string Exam    = "exam";     // สอบเสร็จ
        public const string ClassEvent = "classEvent";   // เหตุการณ์สุ่มระหว่างเรียน (เลือกแล้ว)

        public static event Action<string> OnAction;

        public static void Raise(string key)
        {
            if (!string.IsNullOrEmpty(key)) OnAction?.Invoke(key);
        }
    }
}
