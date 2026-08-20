#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // 🧹 เก็บกวาดกล่องสถานีเก่า (ห้องเรียน/โรงอาหาร/หอพัก) ที่ลอยกลางแมป
    //   → ย้ายไปหน้าตึกจริง (ถ้าเจอ) + ซ่อนกล่อง (ปิด MeshRenderer) + ทำ collider เป็น trigger (เดินทะลุได้)
    //   จุดโต้ตอบยังอยู่ครบ (เข้าเรียน/กิน/นอนได้เหมือนเดิม)
    // ใช้: เมนู  Nisit -> Clean Placeholder Stations
    public static class M19CleanStations
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Clean Placeholder Stations")]
        public static void Clean()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            var log = new StringBuilder();
            Fix("ห้องเรียน", "Door_อาคารเรียน", log);
            Fix("โรงอาหาร", "Door_โรงอาหาร", log);
            Fix("หอพัก", "Door_หอพัก", log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] เก็บกล่องสถานีเก่าเสร็จ</color>\n" + log);
            EditorUtility.DisplayDialog("Nisit Simulator",
                "เก็บกวาดกล่องสถานีเก่าแล้ว! 🧹\n\n" + (log.Length == 0 ? "ไม่พบกล่องสถานี (อาจเก็บไปแล้ว)" : log.ToString()) +
                "\nกล่องหายไป แต่ยังเดินไปเข้าเรียน/กิน/นอนได้เหมือนเดิม", "เยี่ยม!");
        }

        static void Fix(string name, string doorName, StringBuilder log)
        {
            var go = GameObject.Find(name);
            if (go == null) return;

            // ย้ายไปหน้าตึกจริง (ถ้าเจอประตู)
            var door = GameObject.Find(doorName);
            if (door != null)
            {
                go.transform.position = door.transform.position + door.transform.forward * 2.4f;
                log.AppendLine($"✓ {name} → {doorName}");
            }
            else log.AppendLine($"• {name} — ไม่พบ {doorName}, ซ่อนอยู่ที่เดิม");

            // ซ่อนกล่อง
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            // เดินทะลุได้ (trigger) แต่ยังโต้ตอบได้
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
#endif
