#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // 👥 วาง NPC คุยได้ในแมพเกมจริง — ยืนคุยหน้าตึก (A) + เดินไปมาคุยได้ (B)
    //   • ใช้โมเดลคนทุกตัวในโฟลเดอร์ Characters อัตโนมัติ (วาง Mixamo เพิ่ม = หลากหลายทันที)
    //   • ย้อมสีแต่ละตัวต่างกัน (แม้ใช้โมเดลเดียวก็ดูไม่ซ้ำ)
    //   ท่า Waving/Talking มาจาก Setup Character Animations · รันซ้ำได้ (ลบชุดเก่าก่อน)
    public static class M26TalkNPCs
    {
        public static bool SuppressDialog = false;

        const string CharDir = "Assets/_Project/Art/Characters";
        const string CharCtrl = CharDir + "/NisitCharacter.controller";   // มีท่า Waving/Talking + locomotion(Speed)
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string Root = "TalkNPCs";

        // โทนสีย้อม (ให้ NPC แต่ละตัวดูต่างกัน) — ขาว = ใช้สีจริงของโมเดล
        static readonly Color[] Tints =
        {
            Color.white,
            new Color(0.72f, 0.84f, 1.00f),   // ฟ้า
            new Color(1.00f, 0.78f, 0.82f),   // ชมพู
            new Color(0.80f, 1.00f, 0.84f),   // เขียวมิ้นต์
            new Color(1.00f, 0.94f, 0.72f),   // เหลือง
            new Color(0.88f, 0.80f, 1.00f),   // ม่วงลาเวนเดอร์
            new Color(1.00f, 0.84f, 0.68f),   // ส้มพีช
        };

        [MenuItem("Nisit/Build Talk NPCs (คุยได้)", false, 30)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            var models = LoadCharacterModels();
            if (models.Count == 0) { Warn("ไม่พบโมเดลตัวละครในโฟลเดอร์ Characters"); return; }
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CharCtrl);
            int layer = LayerMask.NameToLayer("Interactable");

            // ลบชุดเดิม (กันซ้ำเวลารันหลายรอบ)
            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(Root).transform;

            // scale ให้เท่าผู้เล่น
            float scale = 1f;
            var player = GameObject.Find("Player");
            if (player != null) scale = player.transform.localScale.x;

            int idx = 0;   // ตัวนับรวม (ใช้เลือกโมเดล + สีย้อม)

            // ===== A: NPC ยืนคุย — วางหน้าประตูตึกจริง (หาไม่เจอใช้ตำแหน่งสำรอง) =====
            var standers = new (string name, string door, Vector3 fallback, string[] lines)[]
            {
                ("รุ่นพี่ปี 4", "Door_อาคารเรียน", new Vector3(6, 0, 4), new[]{
                    "น้องปีอะไรเหรอ? สู้ ๆ นะ!", "อย่าลืมเข้าเรียนล่ะ เดี๋ยวเกรดตก", "โปรเจกต์จบโหดจริง เตรียมใจไว้เลย" }),
                ("เพื่อนร่วมคณะ", "Door_โรงอาหาร", new Vector3(-6, 0, 3), new[]{
                    "เฮ้ ไปกินข้าวโรงอาหารกันไหม?", "วันนี้มีสอบรึเปล่านะ...", "เลิกเรียนแล้วไปเล่นเกมกันนะ" }),
                ("อาจารย์ที่ปรึกษา", "Door_อาคารบริหาร", new Vector3(3, 0, -6), new[]{
                    "ตั้งใจเรียนนะ อนาคตอยู่ในมือเธอ", "มีปัญหาอะไรมาปรึกษาได้เสมอ", "อย่าลืมส่งงานตรงเวลาด้วยล่ะ" }),
                ("บรรณารักษ์", "Door_ห้องสมุด", new Vector3(-4, 0, -5), new[]{
                    "ห้องสมุดมีชีทข้อสอบเก่าเยอะเลยนะ", "เงียบ ๆ หน่อยน้า กำลังมีคนอ่านหนังสือ", "ยืมหนังสือได้ไม่จำกัดเลยจ้ะ" }),
            };
            foreach (var s in standers)
            {
                Vector3 pos = NearDoor(s.door, s.fallback);
                var npc = MakeBase(models[idx % models.Count], ctrl, layer, scale, root, s.name, pos, LookYToCenter(pos), Tints[idx % Tints.Length]);
                var t = npc.AddComponent<TalkNPC>();
                t.npcName = s.name; t.lines = s.lines;
                idx++;
            }

            // ===== B: NPC เดินไปมา คุยได้ =====
            Vector3[][] routes =
            {
                new[]{ new Vector3(8, 0, -2), new Vector3(8, 0, 8), new Vector3(0, 0, 8) },
                new[]{ new Vector3(-8, 0, -3), new Vector3(-8, 0, 6), new Vector3(-2, 0, 6) },
                new[]{ new Vector3(2, 0, 10), new Vector3(10, 0, 10), new Vector3(10, 0, 2) },
            };
            var walkers = new (string name, string[] lines)[]
            {
                ("นิสิตปี 1", new[]{ "หลงทางอ่ะ ตึกเรียนอยู่ไหนนะ", "ตื่นเต้นจัง วันแรกของการเรียน!", "สวัสดีครับ/ค่ะ!" }),
                ("นิสิตปี 3", new[]{ "ใกล้ฝึกงานแล้ว เครียดเลย", "งานกลุ่มเยอะมาก...", "สู้ ๆ นะทุกคน" }),
                ("รุ่นพี่ใกล้จบ", new[]{ "เดี๋ยวก็จบแล้วเรา", "ทำ ปนพ. เสร็จยังน้อง? 555", "ขอให้โชคดีกับการสอบนะ" }),
            };
            for (int i = 0; i < routes.Length; i++)
            {
                var wps = MakeWaypoints(root, routes[i], "Route" + i);
                var npc = MakeBase(models[idx % models.Count], ctrl, layer, scale, root, walkers[i].name, wps[0].position, 0, Tints[idx % Tints.Length]);
                var walker = npc.AddComponent<MenuNPCWalker>();
                walker.waypoints = wps;
                walker.startIndex = 1 % wps.Length;
                walker.speed = Random.Range(1.2f, 1.8f);
                var t = npc.AddComponent<TalkNPC>();
                t.npcName = walkers[i].name; t.lines = walkers[i].lines;
                idx++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=lime>[Nisit] วาง NPC คุยได้ {idx} ตัว (ยืน 4 + เดิน 3) จาก {models.Count} โมเดล</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"วาง NPC คุยได้ {idx} ตัวแล้ว! 👥\n\n• ใช้โมเดลคน {models.Count} แบบ + ย้อมสีต่างกัน\n• ยืนคุยหน้าตึก 4 · เดินไปมา 3\n\nเข้าใกล้ = โบกมือ · กด E = คุย (พอใจ +5)\n\n💡 วางไฟล์ตัวละคร Mixamo เพิ่มในโฟลเดอร์ Characters แล้วรันใหม่ = หลากหลายขึ้น\n⚠️ ท่าต้องมาจาก \"Setup Character Animations\"", "เยี่ยม!");
        }

        // โหลดโมเดลคนทั้งหมดในโฟลเดอร์ Characters (ข้ามไฟล์อนิเมชัน @ )
        static List<GameObject> LoadCharacterModels()
        {
            var list = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(CharDir)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { CharDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("@")) continue;                    // ไฟล์อนิเมชัน (Ch29@Walking) ข้าม
                if (!path.ToLower().EndsWith(".fbx")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) list.Add(go);
            }
            return list;
        }

        // สร้างตัว NPC พื้นฐาน (โมเดล + Animator + Collider + Layer + Scale + ย้อมสี)
        static GameObject MakeBase(GameObject model, UnityEditor.Animations.AnimatorController ctrl,
                                   int layer, float scale, Transform root, string name, Vector3 pos, float rotY, Color tint)
        {
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(model);
            npc.name = "NPC_" + name;
            npc.transform.SetParent(root);
            npc.transform.position = pos;
            npc.transform.rotation = Quaternion.Euler(0, rotY, 0);
            npc.transform.localScale = Vector3.one * scale;

            var anim = npc.GetComponentInChildren<Animator>();
            if (anim != null && ctrl != null) anim.runtimeAnimatorController = ctrl;

            // Collider สำหรับตรวจจับ E (trigger = เดินทะลุได้ ไม่ติด)
            var col = npc.GetComponent<CapsuleCollider>();
            if (col == null) col = npc.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f); col.height = 2f; col.radius = 0.4f; col.isTrigger = true;
            if (layer >= 0) npc.layer = layer;

            // ย้อมสีให้ต่างกัน
            npc.AddComponent<CharacterTint>().tint = tint;

            return npc;
        }

        // ตำแหน่งหน้าประตู (ขยับเข้าห่างจากประตู 2.2m ทางกลางแมพ) — หาไม่เจอคืน fallback
        static Vector3 NearDoor(string door, Vector3 fallback)
        {
            var d = GameObject.Find(door);
            if (d == null) return fallback;
            Vector3 p = d.transform.position;
            Vector3 toCenter = new Vector3(-p.x, 0f, -p.z);
            if (toCenter.sqrMagnitude > 0.01f) p += toCenter.normalized * 2.2f;
            p.y = 0f;
            return p;
        }

        static float LookYToCenter(Vector3 pos)
        {
            Vector3 to = new Vector3(-pos.x, 0f, -pos.z);
            return to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to).eulerAngles.y : 0f;
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
