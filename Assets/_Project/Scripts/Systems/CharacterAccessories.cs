using UnityEngine;

namespace NisitSimulator.Systems
{
    // ป้ายกำกับของแต่งที่ใส่อยู่ (ไว้ค้นหา+ถอดตอนเปลี่ยน)
    //   native = ชิ้นส่วนที่มีอยู่แล้วในโครงตัวละคร (เช่น ผม Synty แบบ skinned) → ถอด = ซ่อน ไม่ใช่ลบ
    public class AccessoryTag : MonoBehaviour { public int slot; public bool native; }

    // ติด/ถอด ของแต่ง (ทรงผม/หมวก/แว่น/หนวด/...) กับตัวละคร humanoid
    //   selections[i]: 0 = ไม่ใส่, k>=1 = options[k-1] ของช่อง i · ใช้ทั้ง player, avatar เครือข่าย, และพรีวิว
    //   ถ้าตัวละครมีลูกชื่อเดียวกับ option อยู่แล้ว (ผม skinned ของ Synty) → เปิดชิ้นนั้นแทนการ spawn ใหม่
    public static class CharacterAccessories
    {
        public static void Apply(Transform root, int[] selections)
        {
            if (root == null) return;
            var cat = AccessoryCatalog.Load();
            var anim = root.GetComponentInChildren<Animator>();
            if (cat == null || anim == null || !anim.isHuman) { RemoveAll(root); return; }

            RemoveAll(root);   // ถอดของเก่าออกก่อนเสมอ
            var model = anim.transform;
            var look = FindBodyRenderer(model);
            var info = model.GetComponent<CharacterLookInfo>();

            for (int i = 0; i < cat.SlotCount; i++)
            {
                int sel = (selections != null && i < selections.Length) ? selections[i] : 0;
                if (sel <= 0) continue;

                var slot = cat.GetSlot(i);
                if (slot == null || slot.options == null || sel - 1 >= slot.options.Length) continue;
                var prefab = slot.options[sel - 1];
                if (prefab == null) continue;
                if (info != null && info.presetHair && slot.hairLike) continue;          // ชุดนี้มีผม/หนวดติดมาแล้ว
                if (info != null && !cat.Fits(i, sel - 1, info.gender)) continue;        // ของต่างเพศ (กันค่าเก่าค้าง)

                // 1) ชิ้นส่วนในโครง (skinned) — เปิดใช้
                var native = FindNative(model, prefab.name);
                if (native != null)
                {
                    native.gameObject.SetActive(true);
                    var nt = native.gameObject.AddComponent<AccessoryTag>(); nt.slot = i; nt.native = true;
                    CopyLook(look, native.gameObject);
                    continue;
                }

                // ชิ้นที่ต้องอยู่ในโครงเดียวกัน (skinned) แต่ตัวนี้ไม่มี → ข้าม (spawn แยกจะผิดรูป)
                if (NeedsRig(prefab)) continue;

                // 2) prop ติดกระดูก
                var bone = anim.GetBoneTransform(slot.bone);
                if (bone == null) continue;

                var go = Object.Instantiate(prefab, bone);
                go.name = prefab.name;
                go.transform.localPosition = slot.posOffset;
                go.transform.localRotation = Quaternion.Euler(slot.eulerOffset);
                go.transform.localScale = Vector3.one * (slot.scale <= 0f ? 1f : slot.scale);
                var tag = go.AddComponent<AccessoryTag>(); tag.slot = i;
                CopyLook(look, go);
            }
        }

        public static void RemoveAll(Transform root)
        {
            if (root == null) return;
            var tags = root.GetComponentsInChildren<AccessoryTag>(true);
            foreach (var t in tags)
            {
                if (t == null) continue;
                if (t.native)
                {
                    t.gameObject.SetActive(false);
                    Kill(t);
                }
                else Kill(t.gameObject);
            }
        }

        // option ที่เป็นชิ้น skinned ของโครง Synty Generic (ผม/ฮู้ด) — ใช้ได้เฉพาะตัวที่มีชิ้นนั้นอยู่แล้ว
        public static bool NeedsRig(GameObject option) => option != null && option.GetComponent<SkinnedMeshRenderer>() != null;

        // ตัวละครนี้ใส่ option ได้ไหม (ใช้กรองปุ่มในหน้าแต่งตัว)
        public static bool CanWear(GameObject model, GameObject option)
        {
            if (!NeedsRig(option)) return true;
            return model != null && FindNative(model.transform, option.name) != null;
        }

        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        // ลูกตรงของโมเดลที่ชื่อตรงกันและเป็น SkinnedMesh (ผม/ฮู้ดของ Synty)
        static Transform FindNative(Transform model, string name)
        {
            for (int i = 0; i < model.childCount; i++)
            {
                var c = model.GetChild(i);
                if (c.name == name && c.GetComponent<SkinnedMeshRenderer>() != null) return c;
            }
            return null;
        }

        // ตัวเนื้อหลัก (skinned ที่เปิดอยู่และไม่ใช่ของแต่ง) — ใช้เป็นต้นแบบ material/สี
        static Renderer FindBodyRenderer(Transform model)
        {
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r.GetComponent<AccessoryTag>() == null) return r;
            return null;
        }

        // ให้ของแต่งใช้ material + สีผม/ผิวเดียวกับตัว (โทนสีตรงกันทั้งชุด)
        static void CopyLook(Renderer src, GameObject dst)
        {
            if (src == null || dst == null || !CharacterCatalog.IsSynty(src.sharedMaterial)) return;
            // ใช้ได้เฉพาะตัว Generic (atlas เดียวกับของแต่ง) — ตัว City ใช้ texture คนละแผ่น → คงสีเดิมของของแต่ง
            var cat = CharacterCatalog.Load();
            var set = cat != null ? cat.FindSet(src.sharedMaterial) : null;
            if (set == null || !set.hairSkinMasks) return;
            var mpb = new MaterialPropertyBlock();
            src.GetPropertyBlock(mpb);
            foreach (var r in dst.GetComponentsInChildren<Renderer>(true))
            {
                if (!CharacterCatalog.IsSynty(r.sharedMaterial)) continue;
                r.sharedMaterial = src.sharedMaterial;
                r.SetPropertyBlock(mpb);
            }
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
