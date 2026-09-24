namespace NisitSimulator.SaveLoad
{
    // สถานะที่ต้องส่งข้ามซีน (เมนู → เกม)
    public static class GameSession
    {
        public const string MenuScene = "Scene1";          // ฉากเมนูหลัก (ฉาก 1)
        public const string GameplayScene = "01_Gameplay"; // ฉากเล่นเกม

        public static bool PendingLoad = false;  // true = ให้โหลดเซฟตอนเข้าฉากเกม (ถูก "กิน" ตอนโหลดเสร็จ)
        public static bool IsContinue = false;   // true = เข้าเกมแบบ "เล่นต่อ" — ไม่ถูกกิน ใช้เช็คตอนเล่น
                                                 //   (เช่น DailyAllowance ไม่ให้เงินตั้งต้นซ้ำ) กันปัญหาลำดับ Start()

        public static bool OpenNetworkOnStart = false;  // true = เปิดแผง Multiplayer (F3) อัตโนมัติตอนเข้าเกม
                                                        //   (ตั้งจากปุ่ม "เล่นหลายคน" ในเมนู) NetworkUI อ่านครั้งเดียว

        // คณะของผู้เล่น (0=IT, 1=บริหาร, 2=วิทย์, 3=นิเทศ) — เป็นค่าต่อผู้เล่น เผื่อ multiplayer ในอนาคต
        public static int SelectedFacultyIndex = 0;

        // ปรับแต่งตัวละคร (sync ใน multiplayer) — ตั้งจากแผงแต่งตัว
        public static string PlayerName = "";   // ว่าง = ใช้ "ผู้เล่น N"
        public static int PlayerColor = 0;       // index ในพาเลตต์สี (0 = สีจริงของโมเดล)
        public static int PlayerModel = 0;       // index ในแคตตาล็อกตัวละคร (แบบ/เพศ)
        public static int[] PlayerAccessories = new int[0];   // ของแต่งต่อช่อง (0=ไม่ใส่, k=option k) — sync MP ได้
    }
}
