#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // แปะ "จุดสอบ" และ "จุดงาน" ไว้ที่อาคารจริงที่มีอยู่แล้วในฉาก (จุดโต้ตอบล่องหนหน้าอาคาร)
    //   สอบ → อาคารเรียน · งานร้านกาแฟ → โรงอาหาร · งานผู้ช่วยห้องสมุด → ห้องสมุด
    // ใช้: เมนู  Nisit -> Place Exam & Jobs at Buildings
    public static class M10JobBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Place Exam & Jobs at Buildings")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            int layer = LayerMask.NameToLayer("Interactable");

            // ลบของเก่า
            foreach (var n in new[] { "ExamDesk", "ExamPoint", "Exam_IT", "Exam_Business", "Exam_Science", "Exam_Comm", "Job_Cafe", "Job_Library" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            var log = new StringBuilder();

            // 📝 สอบตึกใครตึกมัน — จุดสอบประจำคณะ ที่ตึกจริง (สอบได้เฉพาะคณะตัวเอง)
            AddExam("Door_คณะ IT",      "Exam_IT",       0, layer, log);
            AddExam("Door_อาคารบริหาร", "Exam_Business", 1, layer, log);
            AddExam("Door_อาคารเรียน",  "Exam_Science",  2, layer, log);
            AddExam("Door_อาคารชมรม",   "Exam_Comm",     3, layer, log);

            // ☕ งานร้านกาแฟ → โรงอาหาร
            var cafe = PointAtDoor("Door_โรงอาหาร", "Job_Cafe", layer, log);
            if (cafe != null)
            {
                var w = cafe.AddComponent<WorkStation>();
                w.jobName = "ร้านกาแฟ"; w.wage = 60; w.energyCost = 25f; w.workSeconds = 3f;
                w.shiftsPerDay = 2; w.satisfactionChange = -2f; w.expReward = 10;
            }

            // 📚 งานผู้ช่วยห้องสมุด → ห้องสมุด
            var lib = PointAtDoor("Door_ห้องสมุด", "Job_Library", layer, log);
            if (lib != null)
            {
                var w = lib.AddComponent<WorkStation>();
                w.jobName = "ผู้ช่วยห้องสมุด"; w.wage = 45; w.energyCost = 15f; w.workSeconds = 3f;
                w.shiftsPerDay = 2; w.knowledgeBonus = 8f; w.satisfactionChange = 0f; w.expReward = 15;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] แปะจุดสอบ/งานที่อาคารเสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "แปะจุดสอบ (แยกคณะ) + งาน ที่อาคารจริงแล้ว! 🏫\n\n📝 สอบตึกใครตึกมัน (สอบได้เฉพาะคณะตัวเอง):\n • IT → คณะ IT\n • บริหาร → อาคารบริหาร\n • วิทย์ → อาคารเรียน\n • นิเทศ → อาคารชมรม\n\n💼 งาน: กาแฟ → โรงอาหาร · ห้องสมุด → ห้องสมุด\n\n" + log.ToString(), "เยี่ยม!");
        }

        // จุดสอบประจำคณะ ที่ตึกจริง
        static void AddExam(string doorName, string pointName, int facultyIndex, int layer, StringBuilder log)
        {
            var go = PointAtDoor(doorName, pointName, layer, log);
            var es = go.AddComponent<ExamStation>();
            es.facultyIndex = facultyIndex;
        }

        // สร้างจุดโต้ตอบล่องหน (trigger) ไว้ "หน้า" ประตูอาคารที่ระบุ
        static GameObject PointAtDoor(string doorName, string pointName, int layer, StringBuilder log)
        {
            var go = new GameObject(pointName);
            if (layer >= 0) go.layer = layer;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(3f, 2.5f, 3f);

            var door = GameObject.Find(doorName);
            if (door != null)
            {
                go.transform.position = door.transform.position + door.transform.forward * 2.2f + Vector3.up * 0f;
                log.AppendLine($"✓ {pointName} → {doorName}");
            }
            else
            {
                go.transform.position = new Vector3(0f, 0f, 4f);
                log.AppendLine($"⚠ ไม่พบ {doorName} — วาง {pointName} ที่ (0,4) ลากเองได้");
            }
            return go;
        }
    }
}
#endif
