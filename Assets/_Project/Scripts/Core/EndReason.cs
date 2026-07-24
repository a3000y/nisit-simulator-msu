namespace NisitSimulator.Core
{
    // สาเหตุที่เกมจบ — ใช้เลือกหน้าจอจบเกม (ฉาก 6/7/8)
    public enum EndReason
    {
        None,
        Died,       // พลังงาน/สุขภาพหมด → Game Over (ฉาก 6)
        Flunked,    // ความรู้ไม่ถึงเป้าตอนสิ้นปี → Flunked Out (ฉาก 8)
        Graduated   // ผ่านครบปี 4 → Graduation (ฉาก 7)
    }
}
