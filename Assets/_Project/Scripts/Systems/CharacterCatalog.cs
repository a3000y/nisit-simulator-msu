using UnityEngine;

namespace NisitSimulator.Systems
{
    // แคตตาล็อกโมเดลตัวละครที่เลือกได้ (แต่งตัว/เลือกเพศ) — เก็บใน Resources ให้โหลดตอนรันได้
    //   สร้าง+ใส่โมเดลโดย Editor tool: Nisit -> Setup Character Select
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Nisit/Character Catalog")]
    public class CharacterCatalog : ScriptableObject
    {
        public GameObject[] models;                     // FBX โมเดล (Humanoid) ที่เลือกได้
        public string[] labels;                         // ชื่อโชว์ (เช่น "หญิง 1", "ชาย 1")
        public RuntimeAnimatorController controller;     // NisitCharacter.controller (ใช้ร่วมกันทุกแบบ)

        static CharacterCatalog _cache;
        public static CharacterCatalog Load()
        {
            if (_cache == null) _cache = Resources.Load<CharacterCatalog>("CharacterCatalog");
            return _cache;
        }

        public GameObject Model(int i) => (models != null && i >= 0 && i < models.Length) ? models[i] : null;
        public string Label(int i) => (labels != null && i >= 0 && i < labels.Length) ? labels[i] : ("แบบ " + (i + 1));
        public int Count => models != null ? models.Length : 0;

        // สลับโมเดลลูกของ root เป็นแบบ index (คืน Animator ใหม่) — ใช้ทั้ง player และ avatar เครือข่าย
        public static Animator Apply(Transform root, int index)
        {
            var cat = Load();
            var prefab = cat != null ? cat.Model(index) : null;
            var oldAnim = root.GetComponentInChildren<Animator>();
            if (prefab == null) return oldAnim;   // ไม่มีแคตตาล็อก/index → คงเดิม

            Transform oldModel = oldAnim != null ? oldAnim.transform : null;
            var m = Instantiate(prefab, root);
            if (oldModel != null)
            {
                m.transform.localPosition = oldModel.localPosition;
                m.transform.localRotation = oldModel.localRotation;
                m.transform.localScale = oldModel.localScale;
            }
            else { m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity; }

            var newAnim = m.GetComponentInChildren<Animator>();
            if (newAnim != null && cat.controller != null) newAnim.runtimeAnimatorController = cat.controller;

            if (oldModel != null) Destroy(oldModel.gameObject);
            return newAnim;
        }
    }
}
