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
        static string SavePath => PathFor(GameSession.SaveSlot);

        public static void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[Save] บันทึกเกมแล้วที่: {SavePath}");
        }

        public static SaveData Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[Save] ไม่พบไฟล์เซฟ — เริ่มเกมใหม่");
                return null;
            }
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveData>(json);
        }

        public static bool HasSave() => File.Exists(SavePath);
        public static bool HasSave(int slot) => File.Exists(PathFor(slot));   // เช็คช่องอื่นได้ (ใช้ในเมนู)

        public static void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
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
