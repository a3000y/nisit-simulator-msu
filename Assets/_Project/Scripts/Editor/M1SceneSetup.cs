#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using NisitSimulator.Player;
using NisitSimulator.Stats;
using NisitSimulator.CameraRig;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // สร้างฉากทดสอบ M1 ให้อัตโนมัติ: พื้น + ตัวละคร + กล้อง isometric + โต๊ะเรียน (โต้ตอบได้)
    // ใช้: เมนู  Nisit -> Setup M1 Scene  แล้วกดปุ่มเดียวจบ
    public static class M1SceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/01_Gameplay.unity";
        private const string InteractableLayerName = "Interactable";

        [MenuItem("Nisit/Setup M1 Scene", false, 0)]
        public static void SetupM1()
        {
            // 1) ทำให้แน่ใจว่ามี Layer "Interactable"
            int interactableLayer = EnsureLayer(InteractableLayerName);
            if (interactableLayer < 0)
            {
                EditorUtility.DisplayDialog("Nisit", "เพิ่ม Layer \"Interactable\" ไม่สำเร็จ (User Layer เต็ม 8-31)\nลบ layer ที่ไม่ใช้ออกก่อน แล้วลองใหม่", "OK");
                return;
            }

            // 2) สร้างฉากใหม่ (มี Main Camera + Directional Light ให้อยู่แล้ว)
            if (!EditorUtility.DisplayDialog("Nisit — Setup M1 Scene",
                "จะสร้างฉากใหม่ 01_Gameplay (พื้น + ตัวละคร + กล้อง + โต๊ะเรียน)\nถ้าฉากปัจจุบันยังไม่เซฟ Unity จะถามก่อน\n\nทำต่อไหม?",
                "สร้างเลย", "ยกเลิก"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 3) พื้น (Ground)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            // 4) ตัวละคร (Player)
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            // เอา CapsuleCollider เดิมออก เพราะจะใช้ CharacterController คุมการชนแทน
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            var cc = player.AddComponent<CharacterController>();
            cc.center = Vector3.zero;   // แคปซูลอยู่ที่ pivot (สูง 2) วางพอดีพื้น
            cc.height = 2f;
            cc.radius = 0.5f;

            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerStats>();
            var interaction = player.AddComponent<PlayerInteraction>();
            interaction.interactableLayer = 1 << interactableLayer;   // ตรวจจับเฉพาะเลเยอร์ Interactable

            // 5) กล้อง isometric
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            var rig = cam.GetComponent<IsometricCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<IsometricCameraRig>();
            rig.target = player.transform;

            // ต่อกล้องให้ระบบเดินโดยตรง (ไม่ต้องพึ่ง Camera.main tag)
            player.GetComponent<PlayerMovement>().cameraTransform = cam.transform;

            // 6) โต๊ะเรียน (จุดกิจกรรมทดสอบ)
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "โต๊ะเรียน";
            desk.transform.position = new Vector3(3f, 0.5f, 0f);
            desk.layer = interactableLayer;                 // ต้องอยู่เลเยอร์ Interactable ให้ผู้เล่นตรวจเจอ
            desk.AddComponent<ActivityStation>();           // BoxCollider ของ Cube ใช้ตรวจจับได้เลย

            // 7) เซฟฉาก
            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ScenePath));
            Debug.Log("<color=lime>[Nisit] ✅ สร้างฉาก M1 เสร็จ! กด Play แล้วลอง WASD เดิน / Shift วิ่ง / เดินหาโต๊ะแล้วกด E</color>");
        }

        // เพิ่ม Layer ตามชื่อ ถ้ายังไม่มี — คืน index (หรือ -1 ถ้าเต็ม)
        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)   // User Layer เริ่มที่ 8
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            return -1;
        }
    }
}
#endif
