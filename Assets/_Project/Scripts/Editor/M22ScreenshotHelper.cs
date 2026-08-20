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
                "ใส่ตัวช่วยแคปภาพแล้ว! 📸\n\nกด Play แล้วใช้ปุ่ม:\n" +
                "• F9 = แคปหน้าจอ (เซฟที่โฟลเดอร์ Screenshots/)\n" +
                "• F5 = หน้าจบการศึกษา\n" +
                "• F6 = หน้าเสียชีวิต\n" +
                "• F7 = หน้าเรียนไม่ผ่าน\n" +
                "• F8 = หน้าสอบ\n\n" +
                "แถบช่วยจำจะไม่ติดในรูป · ไฟล์อยู่ที่  (โฟลเดอร์โปรเจกต์)/Screenshots/", "เยี่ยม!");
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
