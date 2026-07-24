#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NisitSimulator.EditorTools
{
    // ใส่ BoxCollider ให้ตึกในเมืองที่วางไว้แล้ว (ไม่ต้องสร้างเมืองใหม่)
    // ตัวละครจะเดินชนตึก ไม่ทะลุ
    // ใช้: เมนู  Nisit -> Add Campus Colliders
    public static class BuildingColliders
    {
        [MenuItem("Nisit/Add Campus Colliders", false, 5)]
        public static void AddColliders()
        {
            var campus = GameObject.Find("Campus");
            if (campus == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบ \"Campus\"\n(สร้างเมืองด้วย Nisit -> Place Campus Buildings ก่อน)", "OK");
                return;
            }

            var placeNames = new HashSet<string>(CampusBuilder.Places);
            int buildings = 0, trees = 0, benches = 0;
            foreach (Transform child in campus.transform)
            {
                if (child.GetComponent<Collider>() != null) continue;
                Undo.RegisterCreatedObjectUndo(child.gameObject, "Add Colliders");

                if (placeNames.Contains(child.name)) { CampusBuilder.AddBoxCollider(child.gameObject); if (child.GetComponent<Collider>()) buildings++; }
                else if (child.name.StartsWith("Tree_")) { CampusBuilder.AddTrunkCollider(child.gameObject); if (child.GetComponent<Collider>()) trees++; }
                else if (child.name == "bench") { CampusBuilder.AddBoxCollider(child.gameObject); if (child.GetComponent<Collider>()) benches++; }
            }

            Debug.Log($"<color=lime>[Nisit] ✅ ใส่ collider แล้ว: ตึก {buildings} · ต้นไม้ {trees} · ม้านั่ง {benches}\n" +
                      "เดินชนไม่ทะลุ (ถนน/รถ/พุ่ม/หญ้า ยังเดินผ่านได้)</color>");
        }
    }
}
#endif
