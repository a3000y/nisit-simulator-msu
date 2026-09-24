namespace NisitSimulator.SaveLoad
{
    // สถานะที่ต้องส่งข้ามซีน (เมนู → เกม)
    public static class GameSession
    {
        public const string MenuScene = "Scene1";          // ฉากเมนูหลัก (ฉาก 1)
        public const string GameplayScene = "01_Gameplay"; // ฉากเล่นเกม

        public static bool PendingLoad = false;  // true = ให้โหลดเซฟตอนเข้าฉากเกม

        // คณะของผู้เล่น (0=IT, 1=บริหาร, 2=วิทย์, 3=นิเทศ) — เป็นค่าต่อผู้เล่น เผื่อ multiplayer ในอนาคต
        public static int SelectedFacultyIndex = 0;

        // ปรับแต่งตัวละคร (sync ใน multiplayer) — ตั้งจากแผงแต่งตัว
        public static string PlayerName = "";   // ว่าง = ใช้ "ผู้เล่น N"
        public static int PlayerColor = 0;       // index ในพาเลตต์สี (0 = สีจริงของโมเดล)
        public static int PlayerModel = 0;       // index ในแคตตาล็อกตัวละคร (แบบ/เพศ)
    }
}
