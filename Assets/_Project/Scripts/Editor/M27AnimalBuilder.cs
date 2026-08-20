#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // 🐾 วางสัตว์ในมหาลัย — เดินไปมา + ลูบได้ (กด E → พอใจ +6)
    //   อ่านโมเดลสัตว์จากโฟลเดอร์ Animals/ (วางไฟล์ Quaternius/KayKit ที่นั่น)
    //   ยังไม่มีโมเดล = แจ้งให้ไปวางก่อน (ไม่พัง) · รันซ้ำได้ (ลบชุดเก่าก่อน)
    public static class M27AnimalBuilder
    {
        public static bool SuppressDialog = false;

        const string AnimalDir = "Assets/_Project/Art/Models/Animals";
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string Root = "CampusAnimals";

        [MenuItem("Nisit/Build Animals (สัตว์เดิน+ลูบได้)", false, 31)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            int layer = LayerMask.NameToLayer("Interactable");

            var models = LoadModels();
            if (models.Count == 0)
            {
                Warn($"ยังไม่มีโมเดลสัตว์!\n\nวางไฟล์สัตว์ (.fbx/.glb/.gltf) ไว้ที่:\n{AnimalDir}/\n\nแนะนำฟรี: Quaternius Animated Animals · KayKit Animals\nแล้วรันเมนูนี้อีกครั้ง");
                return;
            }

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(Root).transform;

            // เส้นทางเดินของสัตว์ (กระจายรอบแมพ)
            Vector3[][] routes =
            {
                new[]{ new Vector3(5, 0, 5),  new Vector3(9, 0, 5),  new Vector3(9, 0, 9), new Vector3(5, 0, 9) },
                new[]{ new Vector3(-6, 0, -4), new Vector3(-9, 0, -4), new Vector3(-9, 0, 2) },
                new[]{ new Vector3(0, 0, -8),  new Vector3(6, 0, -8),  new Vector3(6, 0, -3) },
                new[]{ new Vector3(-3, 0, 7),  new Vector3(-8, 0, 7),  new Vector3(-8, 0, 11) },
            };

            int count = 0;
            for (int i = 0; i < routes.Length; i++)
            {
                var model = models[i % models.Count];
                string nm = GuessName(model.name);
                var wps = MakeWaypoints(root, routes[i], "AnimalRoute" + i);

                var a = (GameObject)PrefabUtility.InstantiatePrefab(model);
                a.name = "Animal_" + nm;
                a.transform.SetParent(root);
                a.transform.position = wps[0].position;

                var col = a.GetComponent<CapsuleCollider>();
                if (col == null) col = a.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.4f, 0f); col.height = 0.9f; col.radius = 0.5f; col.isTrigger = true;
                if (layer >= 0) a.layer = layer;

                var walker = a.AddComponent<MenuNPCWalker>();
                walker.waypoints = wps; walker.startIndex = 1 % wps.Length; walker.speed = Random.Range(1.0f, 2.2f);

                var pet = a.AddComponent<PetAnimal>();
                pet.animalName = nm;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=lime>[Nisit] วางสัตว์ {count} ตัว (จาก {models.Count} โมเดล)</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"วางสัตว์ {count} ตัวแล้ว! 🐾\n\nเดินไปมาในแมพ · กด E ลูบ (พอใจ +6)\n\n💡 ถ้าตัวใหญ่/เล็กไป ปรับ Scale ที่ออบเจกต์ Animal_ ในฉาก\n(สัตว์บางตัวอาจต้องปรับ y ให้พ้นพื้น)", "เยี่ยม!");
        }

        // โหลดโมเดลสัตว์จากโฟลเดอร์ Animals
        static List<GameObject> LoadModels()
        {
            var list = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(AnimalDir)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { AnimalDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var low = path.ToLower();
                if (!(low.EndsWith(".fbx") || low.EndsWith(".glb") || low.EndsWith(".gltf"))) continue;
                if (path.Contains("@")) continue;   // ไฟล์อนิเมชันแยก ข้าม
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) list.Add(go);
            }
            return list;
        }

        // เดาชื่อไทยจากชื่อไฟล์
        static string GuessName(string file)
        {
            string s = file.ToLower();
            if (s.Contains("cat")) return "แมว";
            if (s.Contains("dog")) return "หมา";
            if (s.Contains("deer")) return "กวาง";
            if (s.Contains("bird")) return "นก";
            if (s.Contains("rabbit") || s.Contains("bunny")) return "กระต่าย";
            if (s.Contains("fox")) return "จิ้งจอก";
            if (s.Contains("duck")) return "เป็ด";
            if (s.Contains("chicken") || s.Contains("hen")) return "ไก่";
            if (s.Contains("horse")) return "ม้า";
            if (s.Contains("sheep")) return "แกะ";
            return "สัตว์";
        }

        static Transform[] MakeWaypoints(Transform parent, Vector3[] pts, string holderName)
        {
            var holder = new GameObject(holderName).transform;
            holder.SetParent(parent);
            var arr = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var wp = new GameObject("WP" + i).transform;
                wp.SetParent(holder); wp.position = pts[i];
                arr[i] = wp;
            }
            return arr;
        }

        static void Warn(string msg)
        {
            Debug.LogWarning("[Nisit] " + msg);
            if (!SuppressDialog) EditorUtility.DisplayDialog("Nisit", msg, "OK");
        }
    }
}
#endif
