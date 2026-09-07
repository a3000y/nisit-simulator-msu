#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // 🌟 กดครั้งเดียว → ทำทุกอย่าง: แปลงโมเดล Humanoid + ท่าตัวละคร + UI ทั้งเกม + NPC + สัตว์
    //   (เมนู + HUD + สอบ + กระเป๋า + หน้าจบ + คณะ + พื้นหลัง + NPC คุยได้ + สัตว์)
    //   ปิด popup ระหว่างทาง → เด้ง dialog สรุปครั้งเดียวตอนจบ · ไม่ต้องไล่กดเมนูเอง
    // ใช้: เมนู  Nisit -> ★ Rebuild All UI
    public static class M23RebuildAll
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/★ Rebuild All UI (one click)", false, 0)]
        public static void RebuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ปิด popup ทุก tool
            M28SetupNPCModels.SuppressDialog = true;
            M20CharacterAnims.SuppressDialog = true;
            M3HudBuilder.SuppressDialog = true;
            M6ExamBuilder.SuppressDialog = true;
            M21InventoryBuilder.SuppressDialog = true;
            M24TutorialBuilder.SuppressDialog = true;
            M25FacultyBuildings.SuppressDialog = true;
            M26TalkNPCs.SuppressDialog = true;
            M27AnimalBuilder.SuppressDialog = true;
            M4MenuBuilder.SuppressDialog = true;
            MenuPolish.SuppressDialog = true;
            M11FacultyBuilder.SuppressDialog = true;

            var log = new System.Text.StringBuilder();
            try
            {
                // ===== ตัวละคร: แปลงโมเดล Humanoid + ตั้งค่าท่า (ทำก่อนวาง NPC) =====
                Step(log, "แปลงโมเดลเป็น Humanoid", () => M28SetupNPCModels.Setup());   // โมเดล Mixamo → Humanoid
                Step(log, "ตั้งค่าท่าตัวละคร (Waving/Talking ฯลฯ)", () => M20CharacterAnims.Setup());

                // ===== ฉากเกม: HUD + หน้าจบ + สอบ + กระเป๋า =====
                EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
                Step(log, "HUD + หน้าจบเกม", () =>
                {
                    M3HudBuilder.BuildHud();                                   // ทำบนฉากปัจจุบัน (ไม่เซฟเอง)
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                });
                Step(log, "ระบบสอบ", () => M6ExamBuilder.Build());             // เปิด/เซฟฉากเกมเอง
                Step(log, "ระบบกระเป๋า", () => M21InventoryBuilder.BuildInventory()); // ทำบนฉากเกม + เซฟ
                Step(log, "คู่มือในเกม", () => M24TutorialBuilder.Build());       // เปิด/เซฟฉากเกมเอง
                Step(log, "ป้ายคณะ 4 ตึก", () => M25FacultyBuildings.Build());     // เปิด/เซฟฉากเกมเอง
                Step(log, "NPC คุยได้ (ยืน+เดิน)", () => M26TalkNPCs.Build());      // เปิด/เซฟฉากเกมเอง
                Step(log, "สัตว์ในมหาลัย", () => M27AnimalBuilder.Build());          // ข้ามถ้ายังไม่มีโมเดลสัตว์

                // ===== ฉากเมนู (auto-chain: Polish + พื้นหลัง + คณะ) =====
                Step(log, "เมนู + คณะ + พื้นหลัง + เสียง", () => M4MenuBuilder.Build());
            }
            finally
            {
                M28SetupNPCModels.SuppressDialog = false;
                M20CharacterAnims.SuppressDialog = false;
                M3HudBuilder.SuppressDialog = false;
                M6ExamBuilder.SuppressDialog = false;
                M21InventoryBuilder.SuppressDialog = false;
                M24TutorialBuilder.SuppressDialog = false;
                M25FacultyBuildings.SuppressDialog = false;
                M26TalkNPCs.SuppressDialog = false;
                M27AnimalBuilder.SuppressDialog = false;
                M4MenuBuilder.SuppressDialog = false;
                MenuPolish.SuppressDialog = false;
                M11FacultyBuilder.SuppressDialog = false;
            }

            Debug.Log("<color=lime>[Nisit] ★ Rebuild All เสร็จ!</color>\n" + log);
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ทำทุกอย่างในคลิกเดียวเสร็จแล้ว! 🌟\n\n" + log +
                "\nโมเดล Humanoid + ท่า + UI + NPC + สัตว์ ครบ\nทุกหน้าสไตล์การ์ตูนพาสเทลเข้าชุดกัน\n\nเปิด Scene1 → Play ได้เลย", "เยี่ยม!");
        }

        static void Step(System.Text.StringBuilder log, string name, System.Action act)
        {
            try { act(); log.AppendLine("✓ " + name); }
            catch (System.Exception e) { log.AppendLine("✗ " + name + " — " + e.Message); Debug.LogWarning("[Rebuild] " + name + ": " + e); }
        }
    }
}
#endif
