using UnityEngine;

namespace NisitSimulator.Systems
{
    // แคตตาล็อกโมเดลตัวละครที่เลือกได้ (ชุด แยกเพศ) + ชุดสี — เก็บใน Resources ให้โหลดตอนรันได้
    //   Synty POLYGON Generic + City สร้าง+ใส่โดย Editor tool: Nisit -> Setup Synty Characters
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Nisit/Character Catalog")]
    public class CharacterCatalog : ScriptableObject
    {
        // ชุดสีของแพ็กหนึ่ง ๆ: materials เรียง [โทน0-ผิว0, โทน0-ผิว1, ..., โทน1-ผิว0, ...]
        [System.Serializable]
        public class ToneSet
        {
            public string name;
            public Material[] materials;
            public int skinVariants = 1;      // >1 = สีผิวอยู่ใน material (City: A/B/C)
            public bool hairSkinMasks;        // true = สีผม/ผิวผ่าน _Hair_Color/_Skin_Color (Generic)
            public Color[] toneSwatches;      // สีปุ่ม "โทนสีชุด"
            public Color[] skinSwatches;      // สีปุ่ม "สีผิว" (Generic = ค่าสีผิวจริงด้วย)
            public Color[] hairAlbedo;        // สีผมเดิมใน texture ของแต่ละ material
            public int ToneCount => materials != null && skinVariants > 0 ? materials.Length / skinVariants : 0;
            public int SkinCount => hairSkinMasks ? (skinSwatches != null ? skinSwatches.Length : 0) : Mathf.Max(1, skinVariants);
        }

        public GameObject[] models;                     // prefab ตัวละคร (Humanoid) = "ชุด"
        public string[] labels;                         // ชื่อโชว์
        public Sprite[] icons;                          // รูปตัวอย่าง
        public int[] modelGender;                       // 0 = ชาย, 1 = หญิง
        public int[] modelToneSet;                      // index ใน toneSets
        public bool[] modelPresetHair;                  // ผม/หนวดติดมากับชุด
        public RuntimeAnimatorController controller;    // NisitCharacter.controller (ท่าผู้ชาย — Kevin Iglesias Human Basic Motions)
        public RuntimeAnimatorController femaleController;   // NisitCharacter_Female (override ท่าผู้หญิง HumanF@) — ว่าง = ใช้ controller

        // เลือก controller ตามเพศของโมเดล (ท่าเดิน/ยืน ผู้หญิงใช้ชุด HumanF)
        public RuntimeAnimatorController ControllerFor(int i) => (Gender(i) == 1 && femaleController != null) ? femaleController : controller;

        [Header("สี — GameSession.PlayerColor = โทน | สีผม<<4 | สีผิว<<8")]
        public ToneSet[] toneSets;
        public Color[] hairColors;                                     // สีผม (ผ่าน _Hair_Color)
        public Color hairAlbedo = new Color(0.357f, 0.283f, 0.186f);   // สีผมเดิมใน texture (shader คูณสี) → ใช้ชดเชย

        static CharacterCatalog _cache;
        public static CharacterCatalog Load()
        {
            if (_cache == null) _cache = Resources.Load<CharacterCatalog>("CharacterCatalog");
            return _cache;
        }

        public GameObject Model(int i) => (models != null && i >= 0 && i < models.Length) ? models[i] : null;
        public string Label(int i) => (labels != null && i >= 0 && i < labels.Length) ? labels[i] : ("แบบ " + (i + 1));
        public Sprite Icon(int i) => (icons != null && i >= 0 && i < icons.Length) ? icons[i] : null;
        public int Count => models != null ? models.Length : 0;
        public int Gender(int i) => (modelGender != null && i >= 0 && i < modelGender.Length) ? modelGender[i] : 0;
        public bool PresetHair(int i) => modelPresetHair != null && i >= 0 && i < modelPresetHair.Length && modelPresetHair[i];
        public ToneSet ToneSetOf(int i)
        {
            if (toneSets == null || toneSets.Length == 0) return null;
            int s = (modelToneSet != null && i >= 0 && i < modelToneSet.Length) ? modelToneSet[i] : 0;
            return toneSets[Mathf.Clamp(s, 0, toneSets.Length - 1)];
        }
        public int FirstOfGender(int g) { for (int i = 0; i < Count; i++) if (Gender(i) == g) return i; return 0; }

        // ---------- แพ็ก/แกะค่าสี (ให้ sync ผ่าน int เดิมได้) ----------
        public static int ToneOf(int packed) => packed & 15;
        public static int HairOf(int packed) => (packed >> 4) & 15;
        public static int SkinOf(int packed) => (packed >> 8) & 15;
        public static int PackColor(int tone, int hair, int skin) => (tone & 15) | ((hair & 15) << 4) | ((skin & 15) << 8);

        public static bool IsSynty(Material m) => m != null && m.shader != null && m.shader.name.StartsWith("Synty/");

        // หา ToneSet ที่มี material นี้ (ใช้แยก Generic/City จาก material ที่ใส่อยู่)
        public ToneSet FindSet(Material m)
        {
            if (m == null || toneSets == null) return null;
            foreach (var s in toneSets)
                if (s != null && s.materials != null)
                    foreach (var x in s.materials) if (x == m) return s;
            return null;
        }

        // ลงสีทั้งตัว: Synty = สลับ material ตามโทน(+ผิว) + ตั้งสีผม/ผิว (ถ้ามี mask) · โมเดลอื่น = ย้อมสีตาม fallback
        public static void ApplyLook(GameObject go, int packed, Color[] fallbackPalette)
        {
            if (go == null) return;
            var cat = Load();
            int tone = ToneOf(packed), hair = HairOf(packed), skin = SkinOf(packed);
            var mpb = new MaterialPropertyBlock();
            var all = go.GetComponentsInChildren<Renderer>(true);
            bool anySynty = false;
            foreach (var r in all) if (IsSynty(r.sharedMaterial)) { anySynty = true; break; }
            foreach (var r in all)
            {
                if (r is ParticleSystemRenderer) continue;
                if (cat != null && IsSynty(r.sharedMaterial))
                {
                    var set = cat.FindSet(r.sharedMaterial);
                    if (set == null) continue;   // ชิ้น Synty อื่น (ไม่ใช่ตัวละคร)
                    int sv = Mathf.Max(1, set.skinVariants);
                    int t = Mathf.Clamp(tone, 0, Mathf.Max(0, set.ToneCount - 1));
                    int s = sv > 1 ? Mathf.Clamp(skin, 0, sv - 1) : 0;
                    int mi = Mathf.Clamp(t * sv + s, 0, set.materials.Length - 1);
                    var m = set.materials[mi];
                    if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;

                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", Color.white);
                    if (set.hairSkinMasks)
                    {
                        if (cat.hairColors != null && cat.hairColors.Length > 0)
                            mpb.SetColor("_Hair_Color", HairTint(cat.hairColors[Mathf.Clamp(hair, 0, cat.hairColors.Length - 1)], Pick(set.hairAlbedo, mi, cat.hairAlbedo)));
                        if (set.skinSwatches != null && set.skinSwatches.Length > 0)
                            mpb.SetColor("_Skin_Color", set.skinSwatches[Mathf.Clamp(skin, 0, set.skinSwatches.Length - 1)]);   // shader แทนสีผิวตรง ๆ (ไม่คูณ)
                    }
                    r.SetPropertyBlock(mpb);
                }
                else if (!anySynty)   // ตัว Synty: ไม่ย้อมชิ้นอื่น (ป้ายชื่อ/ของถือ)
                {
                    var col = (fallbackPalette != null && tone > 0 && tone < fallbackPalette.Length) ? fallbackPalette[tone] : Color.white;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", col);
                    mpb.SetColor("_Color", col);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        static Color Pick(Color[] arr, int i, Color fallback) => (arr != null && i >= 0 && i < arr.Length) ? arr[i] : fallback;

        // shader Synty คูณ _Hair_Color กับสีผมใน texture → หารด้วยสีเดิมก่อน (คิดใน linear) ให้ผลลัพธ์ = สีที่ต้องการ
        static Color HairTint(Color want, Color albedo)
        {
            var w = want.linear; var b = albedo.linear;
            var r = new Color(Mathf.Min(w.r / Mathf.Max(b.r, 0.01f), 16f), Mathf.Min(w.g / Mathf.Max(b.g, 0.01f), 16f), Mathf.Min(w.b / Mathf.Max(b.b, 0.01f), 16f), 1f);
            return r.gamma;
        }

        // สลับโมเดลลูกของ root เป็นแบบ index (คืน Animator ใหม่) — ใช้ทั้ง player และ avatar เครือข่าย
        //   ปรับสเกลให้สูงเท่าโมเดลเดิม (เทียบ humanScale ของ Avatar — ไม่ขึ้นกับท่าที่กำลังเล่น)
        public static Animator Apply(Transform root, int index)
        {
            var cat = Load();
            var prefab = cat != null ? cat.Model(index) : null;
            var oldAnim = root.GetComponentInChildren<Animator>();
            if (prefab == null) return oldAnim;   // ไม่มีแคตตาล็อก/index → คงเดิม

            Transform oldModel = oldAnim != null ? oldAnim.transform : null;
            var m = Instantiate(prefab, root);
            m.name = prefab.name;
            if (oldModel != null)
            {
                m.transform.localPosition = oldModel.localPosition;
                m.transform.localRotation = oldModel.localRotation;
                m.transform.localScale = oldModel.localScale;
            }
            else { m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity; }

            var newAnim = m.GetComponentInChildren<Animator>();
            if (newAnim != null && cat.ControllerFor(index) != null) newAnim.runtimeAnimatorController = cat.ControllerFor(index);

            if (oldAnim != null && newAnim != null && oldAnim.isHuman && newAnim.isHuman)
            {
                float a = oldAnim.humanScale, b = newAnim.humanScale;
                if (a > 0.01f && b > 0.01f)
                {
                    float k = Mathf.Clamp(a / b, 0.4f, 2.5f);
                    if (Mathf.Abs(k - 1f) > 0.01f) m.transform.localScale *= k;
                }
            }

            if (oldModel != null) { oldModel.gameObject.SetActive(false); Destroy(oldModel.gameObject); }
            return newAnim;
        }
    }
}
