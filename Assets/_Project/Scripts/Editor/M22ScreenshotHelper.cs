#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Core;

namespace NisitSimulator.EditorTools
{
    // แปะ ScreenshotHelper เข้าทั้งฉากเมนูและฉากเกม (สำหรับแคปภาพทำเอกสาร)
    // ใช้: เมนู  Nisit -> Add Screenshot Helper
    public static class M22ScreenshotHelper
    {
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Add Screenshot Helper")]
        public static void Add()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AddToScene(GameplayPath);
            AddToScene(MenuPath);

            Debug.Log("<color=lime>[Nisit] ใส่ตัวช่วยแคปทั้ง 2 ฉากแล้ว</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ใส่ตัวช่วยแคปภาพแล้ว! 📸\n\nกด Play แล้วใช้ปุ่มลัด:\n" +
                "• F9 = แคปหน้าจอ (เซฟที่ Screenshots/)\n" +
                "• F1 = ซ่อน/แสดง แถบคำแนะนำปุ่มลัด\n" +
                "• F2 = ท่าทางตัวละคร (ภาพ 4.9)\n" +
                "• F3 = เปิดโทรศัพท์ (ภาพ 4.7)\n" +
                "• F4 = เสาสัญญาณเหตุการณ์+ลูกศร (ภาพ 4.6)\n" +
                "• F5 = จบการศึกษา (ภาพ 4.8)\n" +
                "• F6 = เสียชีวิต/Game Over (ภาพ 4.8)\n" +
                "• F7 = รีไทร์/เรียนไม่ผ่าน (ภาพ 4.8)\n" +
                "• F8 = หน้าสอบ (ภาพ 4.5)\n\n" +
                "แถบช่วยจำจะไม่ติดในรูปภาพ · ความละเอียดคมชัด", "เยี่ยม!");
        }

        static void AddToScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var go = GameObject.Find("ScreenshotHelper");
            if (go == null) go = new GameObject("ScreenshotHelper");
            if (go.GetComponent<ScreenshotHelper>() == null) go.AddComponent<ScreenshotHelper>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
