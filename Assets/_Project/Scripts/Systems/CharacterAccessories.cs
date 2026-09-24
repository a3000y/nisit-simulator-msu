using UnityEngine;

namespace NisitSimulator.Systems
{
    // ป้ายกำกับของแต่งที่ถูก spawn ติดกระดูก (ไว้ค้นหา+ลบตอนเปลี่ยน)
    public class AccessoryTag : MonoBehaviour { public int slot; }

    // ติด/ถอด ของแต่ง (หมวก/แว่น/กระเป๋า/ของถือ) กับกระดูกของตัวละคร humanoid
    //   selections[i]: 0 = ไม่ใส่, k>=1 = options[k-1] ของช่อง i · ใช้ทั้ง player, avatar เครือข่าย, และพรีวิว
    public static class CharacterAccessories
    {
        public static void Apply(Transform root, int[] selections)
        {
            if (root == null) return;
            var cat = AccessoryCatalog.Load();
            var anim = root.GetComponentInChildren<Animator>();
            if (cat == null || anim == null || !anim.isHuman) { RemoveAll(root); return; }

            RemoveAll(root);   // ถอดของเก่าออกก่อนเสมอ

            for (int i = 0; i < cat.SlotCount; i++)
            {
                int sel = (selections != null && i < selections.Length) ? selections[i] : 0;
                if (sel <= 0) continue;

                var slot = cat.GetSlot(i);
                if (slot == null || slot.options == null || sel - 1 >= slot.options.Length) continue;
                var prefab = slot.options[sel - 1];
                if (prefab == null) continue;

                var bone = anim.GetBoneTransform(slot.bone);
                if (bone == null) continue;

                var go = Object.Instantiate(prefab, bone);
                go.transform.localPosition = slot.posOffset;
                go.transform.localRotation = Quaternion.Euler(slot.eulerOffset);
                go.transform.localScale = Vector3.one * (slot.scale <= 0f ? 1f : slot.scale);
                var tag = go.AddComponent<AccessoryTag>(); tag.slot = i;
            }
        }

        public static void RemoveAll(Transform root)
        {
            if (root == null) return;
            var tags = root.GetComponentsInChildren<AccessoryTag>(true);
            foreach (var t in tags) if (t != null) Object.Destroy(t.gameObject);
        }

        // ---------- ตัวช่วยแปลง selections <-> string (สำหรับ GameSession/เครือข่าย) ----------
        public static string Pack(int[] sel)
        {
            if (sel == null || sel.Length == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sel.Length; i++) { if (i > 0) sb.Append(','); sb.Append(sel[i]); }
            return sb.ToString();
        }

        public static int[] Unpack(string s)
        {
            if (string.IsNullOrEmpty(s)) return new int[0];
            var parts = s.Split(',');
            var r = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) int.TryParse(parts[i], out r[i]);
            return r;
        }
    }
}
