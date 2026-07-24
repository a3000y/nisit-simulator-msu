namespace NisitSimulator.Core
{
    // สถานะหลักของเกม ใช้กับ State Machine ใน GameManager
    public enum GameState
    {
        MainMenu,   // อยู่หน้าเมนู (ฉาก 1)
        Playing,    // กำลังเล่น (ฉาก 3/4)
        Paused,     // หยุดชั่วคราว
        GameOver,   // ตาย/สอบตก (ฉาก 6/8)
        Win         // จบการศึกษา (ฉาก 7)
    }
}
