#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
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

            // แผนประชากรสัตว์ — "ตัวเดียวมีได้หลายตัว" · targetH = ความสูงเป้าหมาย(เมตร) เทียบคน ~1.8m
            var plan = new (string keyword, string thai, float targetH)[]
            {
                ("shiba", "หมา", 0.6f),       // 🐕 หมา 3 ตัว เดินคนละมุม (สูง ~0.6m)
                ("husky", "หมา", 0.65f),
                ("shiba", "หมา", 0.6f),
                ("fox",   "จิ้งจอก", 0.5f),    // 🦊 (เล็ก)
                ("deer",  "กวาง", 1.3f),       // 🦌 (สูงกว่า)
            };

            int count = 0;
            for (int i = 0; i < plan.Length; i++)
            {
                var model = FindModel(models, plan[i].keyword) ?? models[i % models.Count];
                string nm = !string.IsNullOrEmpty(plan[i].thai) ? plan[i].thai : GuessName(model.name);

                var route = SectorRoute(i, plan.Length, 12f, 1.5f);   // กระจายกว้างขึ้น + ลาดตระเวนวงแคบ
                var wps = MakeWaypoints(root, route, "AnimalRoute" + i);

                var a = (GameObject)PrefabUtility.InstantiatePrefab(model);
                a.name = "Animal_" + nm + "_" + (i + 1);
                a.transform.SetParent(root);
                a.transform.position = wps[0].position;

                // วัดขนาดจริง แล้วย่อให้สูงตามเป้า (ทำงานกับโมเดลขนาดไหนก็ได้ — แก้ปัญหา base ใหญ่มาก)
                float h = ModelHeight(a);
                float s = h > 0.01f ? plan[i].targetH / h : 1f;
                a.transform.localScale = Vector3.one * s;

                var col = a.GetComponent<CapsuleCollider>();
                if (col == null) col = a.AddComponent<CapsuleCollider>();
                col.height = h * 0.9f; col.radius = h * 0.35f; col.center = new Vector3(0f, h * 0.45f, 0f);
                col.isTrigger = true;
                if (layer >= 0) a.layer = layer;

                // ต่อ Animator ให้สัตว์เดินได้ (Idle↔Walk ตามค่า Speed จาก MenuNPCWalker)
                var anim = a.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.applyRootMotion = false;   // ให้ MenuNPCWalker ขยับตำแหน่ง (กันเดินซ้อน/ไถล)
                    var ac = BuildAnimalController(AssetDatabase.GetAssetPath(model));
                    if (ac != null) anim.runtimeAnimatorController = ac;
                }

                var walker = a.AddComponent<MenuNPCWalker>();
                walker.waypoints = wps; walker.startIndex = 1 % wps.Length;
                walker.speed = Random.Range(0.5f, 0.9f);          // เดินช้าลงอีก
                walker.pauseTime = Random.Range(5f, 10f);         // ยืนพักนาน ๆ (เดินเป็นครั้งคราว ไม่วนถี่)

                var pet = a.AddComponent<PetAnimal>();
                pet.animalName = nm;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=lime>[Nisit] วางสัตว์ {count} ตัว (จาก {models.Count} โมเดล)</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"วางสัตว์ {count} ตัวแล้ว! 🐾\n\n🐕 หมา 3 ตัว (เดินคนละมุม) · 🦊 จิ้งจอก 1 · 🦌 กวาง 1\nกระจายเป็นวงรอบแมพ · เดินลาดตระเวน · กด E ลูบ (พอใจ +6)\n\n💡 ถ้าตัวใหญ่/เล็กไป ปรับ Scale ที่ออบเจกต์ Animal_ ในฉาก\n(อยากเพิ่ม/เปลี่ยนชนิด แก้ที่ plan ใน M27)", "เยี่ยม!");
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

        // วัดความสูงจริงของโมเดล (world) จาก Renderer bounds — ไว้ auto-scale
        static float ModelHeight(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0f;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.size.y;
        }

        // หาโมเดลที่ชื่อไฟล์มี keyword (เช่น "shiba") — ไม่เจอคืน null
        static GameObject FindModel(List<GameObject> models, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return null;
            string k = keyword.ToLower();
            foreach (var m in models) if (m.name.ToLower().Contains(k)) return m;
            return null;
        }

        // เส้นทางลาดตระเวนเล็ก ๆ รอบ "มุมที่ i" — กระจายสัตว์เป็นวงรอบแมพ (แต่ละตัวคนละมุม)
        static Vector3[] SectorRoute(int i, int total, float ringR, float patrol)
        {
            float ang = (i / (float)Mathf.Max(1, total)) * Mathf.PI * 2f + 0.6f;
            Vector3 c = new Vector3(Mathf.Cos(ang) * ringR, 0f, Mathf.Sin(ang) * ringR);
            return new[]
            {
                c + new Vector3(-patrol, 0f, -patrol),
                c + new Vector3( patrol, 0f, -patrol),
                c + new Vector3( patrol, 0f,  patrol),
                c + new Vector3(-patrol, 0f,  patrol),
            };
        }

        // สร้าง Animator Controller ให้สัตว์ (Idle default → Walk เมื่อ Speed>0.1) จากคลิปในไฟล์เอง
        static AnimatorController BuildAnimalController(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath)) return null;
            string baseName = Path.GetFileNameWithoutExtension(fbxPath);

            EnsureClipsLoop(fbxPath);   // ตั้งให้คลิปวนลูป (reimport) ก่อนโหลด

            AnimationClip idle = null, walk = null;
            var all = new List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (!(o is AnimationClip c) || c.name.StartsWith("__preview")) continue;
                all.Add(c);
                string n = c.name.ToLower();
                if (idle == null && n.Contains("idle") && !n.Contains("eat")) idle = c;
                if (walk == null && (n.Contains("walk") || n.Contains("trot"))) walk = c;
            }
            if (all.Count == 0) return null;
            // สำรอง: ไม่มี walk ใช้ gallop/run · ไม่เจอ idle ใช้คลิปแรก
            if (walk == null)
                foreach (var c in all) { var n = c.name.ToLower(); if (n.Contains("gallop") || n.Contains("run")) { walk = c; break; } }
            if (idle == null) idle = all[0];
            if (walk == null) walk = all.Count > 1 ? all[1] : all[0];

            const string baseDir = "Assets/_Project/Art/Models/Animals";
            const string ctrlDir = baseDir + "/Controllers";
            if (!AssetDatabase.IsValidFolder(ctrlDir)) AssetDatabase.CreateFolder(baseDir, "Controllers");
            string cpath = $"{ctrlDir}/AC_{baseName}.controller";

            var ac = AnimatorController.CreateAnimatorControllerAtPath(cpath);   // สร้างใหม่ทับของเดิม (idempotent)
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = ac.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            sm.defaultState = sIdle;

            var toWalk = sIdle.AddTransition(sWalk);
            toWalk.hasExitTime = false; toWalk.duration = 0.12f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var toIdle = sWalk.AddTransition(sIdle);
            toIdle.hasExitTime = false; toIdle.duration = 0.12f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ตั้งคลิปทั้งหมดในไฟล์ให้วนลูป (เดิน/ยืนจะได้ต่อเนื่อง)
        static void EnsureClipsLoop(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) return;
            var clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
                if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
        }

        // เดาชื่อไทยจากชื่อไฟล์ (รองรับชื่อ Quaternius: Husky/ShibaInu/Stag/Bull ฯลฯ)
        static string GuessName(string file)
        {
            string s = file.ToLower();
            if (s.Contains("cat")) return "แมว";
            if (s.Contains("husky") || s.Contains("shiba") || s.Contains("dog") || s.Contains("puppy")) return "หมา";
            if (s.Contains("wolf")) return "หมาป่า";
            if (s.Contains("deer") || s.Contains("stag")) return "กวาง";
            if (s.Contains("fox")) return "จิ้งจอก";
            if (s.Contains("rabbit") || s.Contains("bunny")) return "กระต่าย";
            if (s.Contains("bird")) return "นก";
            if (s.Contains("duck")) return "เป็ด";
            if (s.Contains("chicken") || s.Contains("hen")) return "ไก่";
            if (s.Contains("cow") || s.Contains("bull") || s.Contains("ox")) return "วัว";
            if (s.Contains("horse")) return "ม้า";
            if (s.Contains("donkey")) return "ลา";
            if (s.Contains("alpaca") || s.Contains("llama")) return "อัลปาก้า";
            if (s.Contains("sheep") || s.Contains("goat")) return "แกะ";
            if (s.Contains("pig")) return "หมู";
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
