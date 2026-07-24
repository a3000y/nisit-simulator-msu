using System.IO;
using UnityEngine;

namespace NisitSimulator.SaveLoad
{
    // บันทึก/โหลดเกมเป็นไฟล์ JSON ในเครื่องผู้เล่น
    // เรียกใช้แบบ static: SaveSystem.Save(data) / SaveSystem.Load()
    public static class SaveSystem
    {
        // ที่อยู่ไฟล์เซฟ (Windows: C:/Users/<user>/AppData/LocalLow/<company>/<game>/)
        private static string SavePath => Path.Combine(Application.persistentDataPath, "nisit_save.json");

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

        public static void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
