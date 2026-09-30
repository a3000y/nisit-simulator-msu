using System.IO;
using UnityEngine;

namespace NisitSimulator.SaveLoad
{
    // บันทึก/โหลดเกมเป็นไฟล์ JSON ในเครื่องผู้เล่น
    // เรียกใช้แบบ static: SaveSystem.Save(data) / SaveSystem.Load()
    public static class SaveSystem
    {
        // ที่อยู่ไฟล์เซฟตามช่อง (Windows: C:/Users/<user>/AppData/LocalLow/<company>/<game>/)
        //   ช่องปัจจุบันมาจาก GameSession.SaveSlot (เลือกในเมนู)
        static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"nisit_save_{slot}.json");
        static string SavePath
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrEmpty(DevPathOverride)) return DevPathOverride;   // โปรไฟล์ทดสอบ (Dev Panel)
#endif
                return PathFor(GameSession.SaveSlot);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ===== Dev Testing Panel (มีเฉพาะ Editor / Development Build — Release ไม่มีโค้ดส่วนนี้) =====
        //   DevGuard = true      → กำลังอยู่ในโหมดทดสอบ: ห้ามเขียน/ลบไฟล์เซฟช่องจริงทุกกรณี (autosave/ออกเกม/จบเกม)
        //   DevPathOverride      → อ่าน+เขียนเซฟทดสอบแทนช่องจริง
        //   DevReadOverride      → อ่านอย่างเดียวจากไฟล์นี้ (ใช้ตอนออกจากโหมดทดสอบ: คืนสถานะเดิมจากสำเนา) · ล้างเองเมื่อมีการเซฟจริงครั้งถัดไป
        public static bool DevGuard;
        public static string DevPathOverride;
        public static string DevReadOverride;
        public static int DevBlockedRealWrites;   // จำนวนครั้งที่ระบบพยายามเขียน/ลบเซฟจริงระหว่างทดสอบ (ถูกกันไว้)
        public static string RealSlotPath(int slot) => PathFor(slot);

        static bool IsRealSlotPath(string p)
        {
            string full = Path.GetFullPath(p);
            for (int s = 0; s <= 2; s++) if (string.Equals(full, Path.GetFullPath(PathFor(s)), System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // true = ห้ามเขียน/ลบ path นี้ตอนนี้
        static bool DevBlocks(string p, string what)
        {
            if (!DevGuard || !IsRealSlotPath(p)) return false;
            DevBlockedRealWrites++;
            Debug.LogWarning($"[Save][DEV] กัน{what}เซฟจริงระหว่างทดสอบ: {Path.GetFileName(p)}");
            return true;
        }

        static string ReadPath
        {
            get
            {
                if (!string.IsNullOrEmpty(DevReadOverride) && File.Exists(DevReadOverride)) return DevReadOverride;
                return SavePath;
            }
        }
#else
        static string ReadPath => SavePath;
#endif

        public static void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevBlocks(SavePath, "เขียน")) return;
            if (!DevGuard && !string.IsNullOrEmpty(DevReadOverride)) DevReadOverride = null;   // เซฟจริงแล้ว → ไฟล์ช่องจริงเป็นสถานะล่าสุด เลิกอ่านจากสำเนา
#endif
            File.WriteAllText(SavePath, json);
            Debug.Log($"[Save] บันทึกเกมแล้วที่: {SavePath}");
        }

        public static SaveData Load()
        {
            string rp = ReadPath;
            if (!File.Exists(rp))
            {
                Debug.Log("[Save] ไม่พบไฟล์เซฟ — เริ่มเกมใหม่");
                return null;
            }
            string json = File.ReadAllText(rp);
            return JsonUtility.FromJson<SaveData>(json);
        }

        public static bool HasSave() => File.Exists(ReadPath);
        public static bool HasSave(int slot) => File.Exists(PathFor(slot));   // เช็คช่องอื่นได้ (ใช้ในเมนู)

        public static void DeleteSave()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevBlocks(SavePath, "ลบ")) return;
#endif
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }

        // อ่านข้อมูลเซฟของช่องที่ระบุ (ไว้โชว์ในหน้าโหลดเซฟแบบลิสต์) — ไม่มี/พัง= null
        public static SaveData LoadSlot(int slot)
        {
            if (!File.Exists(PathFor(slot))) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot))); }
            catch { return null; }
        }

        // ลบเซฟช่องที่ระบุ (ใช้ปุ่มลบในหน้าโหลดเซฟ)
        public static void DeleteSlot(int slot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevBlocks(PathFor(slot), "ลบ")) return;
#endif
            if (File.Exists(PathFor(slot))) File.Delete(PathFor(slot));
        }

        // สรุปสั้น ๆ ของช่อง (ชั้นปี + เงิน) ไว้โชว์ในปุ่มเลือกช่อง — ไม่มีเซฟ = null
        public static string SummaryFor(int slot)
        {
            if (!File.Exists(PathFor(slot))) return null;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
                return d != null ? $"ปี {d.currentYear} · {d.money}฿" : null;
            }
            catch { return null; }
        }
    }
}
