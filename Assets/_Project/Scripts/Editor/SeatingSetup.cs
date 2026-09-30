#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // ทำให้ "นั่งได้" ทุกที่: ม้านั่งข้างนอก (Campus) + เก้าอี้ในห้อง (Interiors)
    //   สร้าง marker "Sittable" (ไม่มีสเกล) ที่ตำแหน่งที่นั่งแต่ละตัว
    // ใช้: เมนู  Nisit -> Setup Seating   (ต้องมี Player + Campus/Interiors + ท่านั่ง)
    public static class SeatingSetup
    {
        // ยกตัวตอนนั่ง (มากขึ้น=สูงขึ้น / น้อยลง=ต่ำลง) — แยกกันได้ แล้วกด Setup Seating ใหม่
        private const float SitLiftBench = 0.3f;    // ม้านั่งข้างนอก (Campus)
        private const float SitLiftChair = 0.15f;   // เก้าอี้ในห้อง (Interiors) — เตี้ยกว่า ยกน้อยกว่า

        [MenuItem("Nisit/Setup Seating", false, 10)]
        public static void Setup()
        {
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) { EditorUtility.DisplayDialog("Nisit", "ไม่มี Layer Interactable — รัน Setup M1 Scene ก่อน", "OK"); return; }

            // ขนาดตัวละคร (ใช้คำนวณระดับนั่ง)
            float unit = 1.3f;
            var player = GameObject.Find("Player");
            if (player != null) { var r = player.GetComponentInChildren<Renderer>(); if (r != null) unit = r.bounds.size.y; }

            // ลบของเก่า (กดซ้ำได้ ไม่ซ้อน)
            var oldRoot = GameObject.Find("Seating");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            foreach (var go in Object.FindObjectsByType<ActivitySpot>(FindObjectsSortMode.None))
                if (go.gameObject.name == "Sittable") Object.DestroyImmediate(go.gameObject);

            var root = new GameObject("Seating").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Setup Seating");

            int benches = 0, chairs = 0;

            // ม้านั่งข้างนอก (Campus)
            var campus = GameObject.Find("Campus");
            if (campus != null)
                foreach (Transform c in campus.transform)
                    if (c.name == "bench") { MakeSeat(root, c, unit, layer, SitLiftBench, "Bench"); benches++; }

            // เก้าอี้ในห้อง (Interiors) — ผลกิจกรรมตามตึก (หาตึกจาก Spawn ที่ใกล้สุด)
            var interiors = GameObject.Find("Interiors");
            if (interiors != null)
                foreach (Transform c in interiors.transform)
                    if (c.name.StartsWith("chair")) { MakeSeat(root, c, unit, layer, SitLiftChair, BuildingOfChair(interiors.transform, c.position)); chairs++; }

            Debug.Log($"<color=lime>[Nisit] ✅ ทำที่นั่งแล้ว: ม้านั่งข้างนอก {benches} · เก้าอี้ในห้อง {chairs}\n" +
                      "เดินเข้าใกล้ที่นั่งไหนก็ได้ กด E เพื่อนั่ง / กด E อีกทีลุก</color>");
        }

        // หาชื่อตึกของเก้าอี้ จาก Spawn_<ตึก> ที่ใกล้ที่สุด (ห้องอยู่ห่างกันมาก จึงแม่น)
        private static string BuildingOfChair(Transform interiors, Vector3 chairPos)
        {
            Transform best = null; float md = float.MaxValue;
            foreach (Transform c in interiors)
                if (c.name.StartsWith("Spawn_"))
                {
                    float d = (c.position - chairPos).sqrMagnitude;
                    if (d < md) { md = d; best = c; }
                }
            return best != null ? best.name.Substring("Spawn_".Length) : "";
        }

        // ตั้งผลกิจกรรม + ชื่อ ตามตึก (ฐานตามชนิดห้อง แล้ว override ชื่อ/ผลเฉพาะบางตึก)
        private static void EffectsFor(string building, ActivitySpot spot)
        {
            string room = InteriorBuilder.RoomOf.TryGetValue(building, out var r) ? r : (building == "Bench" ? "Bench" : "Classroom");

            // ---- ผล + ชื่อกิจกรรม ตามชนิดห้อง ----
            switch (room)
            {
                case "Classroom": spot.activityName = "เรียน";            spot.knowledgeChange = 8f; spot.energyChange = -4f; spot.stressChange = 3f; spot.satisfactionChange = 1f; spot.expReward = 5; break;
                case "ITLab":     spot.activityName = "เรียนคอมพิวเตอร์"; spot.knowledgeChange = 9f; spot.energyChange = -4f; spot.stressChange = 3f; spot.satisfactionChange = 2f; spot.expReward = 6; break;
                case "Library":   spot.activityName = "อ่านหนังสือ";       spot.knowledgeChange = 6f; spot.energyChange = -2f; spot.stressChange = 2f; spot.satisfactionChange = 2f; spot.expReward = 3; break;
                case "Office":    spot.activityName = "ทำงานเอกสาร";       spot.knowledgeChange = 3f; spot.energyChange = -2f; spot.stressChange = 1f; spot.satisfactionChange = 3f; spot.expReward = 2; break;
                case "ClubRoom":  spot.activityName = "ทำกิจกรรมชมรม";    spot.knowledgeChange = 2f; spot.energyChange = -2f; spot.stressChange = -5f; spot.satisfactionChange = 8f; spot.expReward = 3; break;
                case "Cafeteria": spot.activityName = "กินข้าว";           spot.hungerChange = 35f;   spot.energyChange = 8f;  spot.satisfactionChange = 5f; break;
                case "Shop":      spot.activityName = "นั่งพักในร้าน";      spot.hungerChange = 15f;   spot.satisfactionChange = 4f; break;
                case "Dorm":      spot.activityName = "อ่านหนังสือ";       spot.knowledgeChange = 4f; spot.energyChange = 2f;  spot.satisfactionChange = 3f; break;
                default:          spot.activityName = "นั่งพัก";            spot.energyChange = 5f;    spot.satisfactionChange = 3f; break;   // ม้านั่งข้างนอก
            }
        }

        private static void MakeSeat(Transform root, Transform seatObj, float unit, int layer, float lift, string building)
        {
            var wb = Bounds(seatObj);   // world bounds ของที่นั่ง

            // marker ไม่หมุน (แกนตรง) -> collider แนบขนาดจริง ไม่ทับตัวข้างๆ
            var marker = new GameObject("Sittable");
            marker.transform.SetParent(root);
            marker.transform.position = new Vector3(wb.center.x, 0f, wb.center.z);
            marker.layer = layer;

            var col = marker.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, wb.center.y, 0f);
            col.size = new Vector3(wb.size.x, wb.size.y, wb.size.z) + Vector3.one * (0.8f * unit);   // เผื่อเดินเข้าใกล้เล็กน้อย

            // จุดนั่ง = กลางที่นั่ง หันตามที่นั่ง
            var seat = new GameObject("Seat").transform;
            seat.SetParent(marker.transform);
            seat.position = new Vector3(wb.center.x, wb.min.y, wb.center.z);
            seat.rotation = seatObj.rotation;

            var spot = marker.AddComponent<ActivitySpot>();
            spot.seat = seat;
            spot.sitYOffset = lift * unit + 0.5f * wb.size.y;
            EffectsFor(building, spot);   // ชื่อ + ผลตามตึก
        }

        private static Bounds Bounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
        private static Bounds Bounds(Transform t) => Bounds(t.gameObject);
    }
}
#endif
