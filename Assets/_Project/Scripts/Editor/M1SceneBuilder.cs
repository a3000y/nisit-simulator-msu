#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.Player;
using NisitSimulator.Stats;
using NisitSimulator.CameraRig;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // เครื่องมือสร้างฉาก M1 อัตโนมัติ
    // ใช้: เมนูบน  Nisit -> Build M1 Scene
    // สร้าง: พื้น, ตัวละคร(+สคริปต์), กล้อง isometric, Layer Interactable, โต๊ะเรียนทดสอบ, GameManager
    public static class M1SceneBuilder
    {
        [MenuItem("Nisit/Build M1 Scene")]
        public static void BuildM1()
        {
            // 1) สร้าง Layer "Interactable" ถ้ายังไม่มี
            int interactableLayer = EnsureLayer("Interactable");

            // 2) ลบของเดิมที่ชื่อซ้ำ (กันสร้างซ้ำเวลากดหลายรอบ)
            DestroyIfExists("Ground");
            DestroyIfExists("Player");
            DestroyIfExists("โต๊ะเรียน");
            DestroyIfExists("GameManager");

            // 3) พื้น
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            // 4) ตัวละคร Player
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.tag = "Player";
            // ลบ CapsuleCollider เดิม (จะใช้ CharacterController แทน)
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);   // ยกให้ตรงกับแคปซูล ไม่จมพื้น
            cc.height = 2f;
            cc.radius = 0.5f;

            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerStats>();
            var interaction = player.AddComponent<PlayerInteraction>();
            interaction.interactableLayer = 1 << interactableLayer;  // ตั้ง Layer ให้อัตโนมัติ

            // 5) กล้อง isometric (ใช้ Main Camera เดิมถ้ามี ไม่มีก็สร้าง)
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;       // สำคัญ! ทำให้เป็น isometric แท้
            cam.orthographicSize = 7f;

            var rig = cam.GetComponent<IsometricCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<IsometricCameraRig>();
            rig.target = player.transform;

            // 6) โต๊ะเรียนทดสอบ (จุดกิจกรรม)
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "โต๊ะเรียน";
            desk.transform.position = new Vector3(3f, 0.5f, 0f);
            desk.layer = interactableLayer;
            desk.AddComponent<ActivityStation>();

            // 7) GameManager (ตัวคุมสถานะเกม)
            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();

            // 8) เลือก Player ให้เห็นใน Inspector + mark scene ว่าแก้แล้ว
            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=lime>[Nisit] สร้างฉาก M1 สำเร็จ! กด Ctrl+S เพื่อเซฟ แล้วกด Play ทดสอบ WASD + E</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างฉาก M1 เสร็จแล้ว!\n\n1. กด Ctrl+S เพื่อเซฟฉาก\n2. กด Play (▶)\n3. ลอง WASD เดิน, Shift วิ่ง, เดินไปกด E ที่โต๊ะเรียน\n\nดู Console จะเห็นข้อความตอนกด E", "เยี่ยม!");
        }

        // สร้าง Layer ใหม่ถ้ายังไม่มี แล้วคืนค่า index
        private static int EnsureLayer(string layerName)
        {
            // มีอยู่แล้ว?
            int existing = LayerMask.NameToLayer(layerName);
            if (existing != -1) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // หาช่องว่างในเลเยอร์ผู้ใช้ (8-31)
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[Nisit] สร้าง Layer '{layerName}' ที่ช่อง {i}");
                    return i;
                }
            }
            Debug.LogWarning("[Nisit] เลเยอร์เต็ม! ใช้ Default แทน");
            return 0;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
#endif
