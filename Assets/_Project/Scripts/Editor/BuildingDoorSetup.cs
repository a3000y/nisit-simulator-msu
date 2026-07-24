#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // ติดตั้ง "ประตู" ให้ตึกในเมือง — เดินเข้าใกล้กด E เพื่อเข้าห้อง (ต้นแบบ: เข้าห้องเรียนเดียวกัน)
    // ใช้: เมนู  Nisit -> Setup Building Doors  (ต้อง Build Interior ก่อน)
    public static class BuildingDoorSetup
    {
        [MenuItem("Nisit/Setup Building Doors", false, 8)]
        public static void Setup()
        {
            var campus = GameObject.Find("Campus");
            if (campus == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ Campus — สร้างเมืองก่อน", "OK"); return; }

            if (GameObject.Find("Interiors") == null) { EditorUtility.DisplayDialog("Nisit", "ยังไม่มีห้องภายใน\nรัน Nisit -> Build Interiors ก่อน", "OK"); return; }

            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) { EditorUtility.DisplayDialog("Nisit", "ไม่มี Layer Interactable", "OK"); return; }

            if (Object.FindFirstObjectByType<InteriorManager>() == null)
                new GameObject("InteriorManager").AddComponent<InteriorManager>();

            // root เก็บประตู (ไม่ผูกใต้ตึกที่ถูกสเกล เพื่อให้ collider ขนาดตรง)
            var oldRoot = GameObject.Find("BuildingDoors");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            var doorsRoot = new GameObject("BuildingDoors").transform;
            Undo.RegisterCreatedObjectUndo(doorsRoot.gameObject, "Setup Building Doors");

            var placeNames = new HashSet<string>(CampusBuilder.Places);
            int count = 0;
            foreach (Transform child in campus.transform)
            {
                if (!placeNames.Contains(child.name)) continue;

                var b = WorldBounds(child.gameObject);
                var door = new GameObject("Door_" + child.name);
                door.transform.SetParent(doorsRoot);
                door.transform.position = new Vector3(b.center.x, 0f, b.center.z);
                door.layer = layer;

                var col = door.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                col.size = new Vector3(b.size.x * 1.15f, b.size.y, b.size.z * 1.15f);   // คลุมรอบตึก เดินใกล้ตรวจเจอ

                // เข้าห้องของตึกนั้นๆ (แต่ละตึกมีห้องของตัวเอง = Spawn_<ชื่อตึก>)
                var spawn = GameObject.Find("Spawn_" + child.name);
                if (spawn == null) continue;   // ตึกนี้ไม่มีห้อง (ข้าม)

                var bd = door.AddComponent<BuildingDoor>();
                bd.interiorSpawn = spawn.transform;
                bd.activityName = "เข้า" + child.name;
                count++;
            }

            Debug.Log($"<color=lime>[Nisit] ✅ ติดตั้งประตูให้ตึก {count} หลัง — เดินเข้าใกล้กด E เพื่อเข้าห้อง\n" +
                      "(ต้นแบบ: ทุกตึกเข้าห้องเรียนเดียวกัน) กด E ที่ประตูเขียวในห้องเพื่อออก</color>");
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
