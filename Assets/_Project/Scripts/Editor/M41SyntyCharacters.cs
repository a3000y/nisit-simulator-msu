#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NisitSimulator.Systems;

namespace NisitSimulator.EditorTools
{
    // 🧍 ตั้งตัวละคร Synty (POLYGON Generic + City) ให้หน้าแต่งตัว — แยกเพศ
    //   • prefab ชุด → Art/Characters/Synty (Generic: ตัวเปล่า + ผม skinned ซ่อนไว้ · City: ผม/หนวดติดมา)
    //   • ชุดสี: Generic 12 โทน (+ mask ผม/ผิว) · City 4 โทน × 3 สีผิว
    //   • CharacterCatalog (ชุด+เพศ+ชุดสี) · AccessoryCatalog (ทรงผม/หมวก/แว่น/หนวด + เพศของแต่ละชิ้น)
    //   • รูปตัวอย่าง
    //   ใช้: เมนู Nisit -> Setup Synty Characters  (แล้ว Nisit -> Build Character Creator)
    public static class M41SyntyCharacters
    {
        public static bool SuppressDialog = false;

        const string GenChars = "Assets/Synty/PolygonGeneric/Prefabs/Characters";
        const string GenAttach = "Assets/Synty/PolygonGeneric/Prefabs/Characters/Attachments";
        const string GenFbx = "Assets/Synty/PolygonGeneric/Models/Generic_Characters.fbx";
        const string GenAlts = "Assets/Synty/PolygonGeneric/Materials/Alts";
        const string CityChars = "Assets/Synty/PolygonCity/Prefabs/Characters";
        const string CityAlts = "Assets/Synty/PolygonCity/Materials/Alts";
        const string SkinMask = "Assets/Synty/PolygonGeneric/Textures/SkinMask.png";
        const string HairMask = "Assets/Synty/PolygonGeneric/Textures/HairMask.png";

        const string OutDir = "Assets/_Project/Art/Characters/Synty";
        const string MatDir = OutDir + "/Materials";
        const string ThumbDir = OutDir + "/Thumbs";
        const int ThumbSize = 192;
        const int M = 0, F = 1, B = 2;   // เพศ: ชาย / หญิง / ทั้งคู่

        // ชุด: (แพ็ก G=Generic C=City, ชื่อ prefab, ชื่อโชว์, เพศ)
        static readonly object[,] Outfits =
        {
            { "G", "Street_Male_01",   "ลำลอง 1",   M },
            { "G", "Street_Male_02",   "ลำลอง 2",   M },
            { "G", "Street_Male_03",   "ลำลอง 3",   M },
            { "G", "Street_Male_04",   "ลำลอง 4",   M },
            { "G", "Business_Male_01", "ทางการ",    M },
            { "C", "BusinessMan_Shirt", "เชิ้ต",     M },
            { "C", "BusinessMan_Suit",  "สูท",       M },
            { "C", "Male_Hoodie",       "ฮู้ดดี้",    M },
            { "C", "Male_Jacket",       "แจ็กเก็ต",  M },
            { "G", "Street_Female_01", "ลำลอง 1",   F },
            { "G", "Street_Female_02", "ลำลอง 2",   F },
            { "G", "Street_Female_03", "ลำลอง 3",   F },
            { "G", "Street_Female_04", "ลำลอง 4",   F },
            { "G", "Business_Female_01", "ทางการ",  F },
            { "C", "BusinessWoman",     "ชุดทำงาน",  F },
            { "C", "Female_Coat",       "โค้ท",      F },
            { "C", "Female_Jacket",     "แจ็กเก็ต",  F },
        };

        // ทรงผม (แยกเพศตามที่ Synty ใช้ในตัวละครตัวอย่าง) · "+" = ประกอบหลายชิ้น
        static readonly object[,] Hairs =
        {
            { "Hair_09", "สั้น 1", M }, { "Hair_09_alt", "สั้น 2", M }, { "Hair_10", "สั้น 3", M }, { "Hair_11", "สั้น 4", M },
            { "Hair_01", "ยาว 1", M }, { "Hair_01_alt", "ยาว 2", M },
            { "Hair_07", "กลาง 1", B }, { "Hair_08", "กลาง 2", B },
            { "Hair_02", "ยาว 1", F }, { "Hair_03", "ยาว 2", F }, { "Hair_04", "ยาว 3", F }, { "Hair_05", "ยาว 4", F },
            { "Hair_06", "บ๊อบ", F }, { "Hair_06+Ponytail_01", "หางม้า", F }, { "Hair_06+Bun_01", "มวย", F },
        };
        static readonly string[] NativeHair = { "Hair_01", "Hair_01_alt", "Hair_02", "Hair_03", "Hood_01" };

        static readonly object[,] Hats =
        {
            { "Beanie_01", "บีนนี่", B }, { "Hat_01", "หมวก 1", B }, { "Hat_02", "หมวก 2", B }, { "Hood_01", "ฮู้ด", B },
            { "Headset_01", "หูฟัง 1", B }, { "Headset_01_alt", "หูฟัง 2", B }, { "Headset_02", "หูฟัง 3", B },
        };
        static readonly object[,] Glasses = { { "Sunglasses_01", "แว่นกันแดด", B } };
        static readonly object[,] Facial =
        {
            { "Beard_01", "เครา 1", M }, { "Beard_02", "เครา 2", M }, { "Moustache_01", "หนวด", M }, { "Chops_01", "จอน", M },
        };

        static readonly Color[] HairColors =
        {
            new Color(0.09f, 0.08f, 0.08f), new Color(0.25f, 0.16f, 0.10f), new Color(0.48f, 0.31f, 0.18f), new Color(0.86f, 0.70f, 0.42f),
            new Color(0.62f, 0.20f, 0.12f), new Color(0.95f, 0.55f, 0.72f), new Color(0.36f, 0.56f, 0.92f), new Color(0.80f, 0.80f, 0.84f),
        };
        static readonly Color[] GenSkin =
        {
            new Color(1.00f, 0.84f, 0.72f), new Color(0.96f, 0.76f, 0.60f), new Color(0.85f, 0.63f, 0.46f),
            new Color(0.66f, 0.46f, 0.31f), new Color(0.45f, 0.30f, 0.20f),
        };
        // สีปุ่มโทน (ดูจากสีเสื้อแจ็กเก็ตจริงของแต่ละ alt)
        static readonly string[] GenToneHex = { "E0615A", "4A90E2", "5CCB5F", "F2D13C", "F2A33A", "9B6BE0", "E062C8", "9ED9B8", "9CCBF0", "F2B8E0", "F4F4F4", "6E6E74" };
        static readonly string[] CityToneHex = { "D9534F", "5B86D6", "4B5A9E", "9A4E86" };
        static readonly string[] CitySkinHex = { "F6D1B8", "D9A07A", "8A5A3C" };

        [MenuItem("Nisit/Setup Synty Characters", false, 35)]
        public static void Setup()
        {
            EnsureFolder("Assets/_Project/Art", "Characters");
            EnsureFolder("Assets/_Project/Art/Characters", "Synty");
            EnsureFolder(OutDir, "Materials");
            EnsureFolder(OutDir, "Thumbs");

            // ---- 1) ชุดสี ----
            var genMats = new List<Material>();
            var skinMask = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinMask);
            var hairMask = AssetDatabase.LoadAssetAtPath<Texture2D>(HairMask);
            foreach (var path in SortedAssets("t:Material", GenAlts))
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (src == null || !src.name.StartsWith("Generic_")) continue;
                string dst = MatDir + "/Nisit_" + src.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(dst);
                if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, dst); }
                else m.CopyPropertiesFromMaterial(src);
                if (skinMask != null) m.SetTexture("_Skin_Mask", skinMask);
                if (hairMask != null) m.SetTexture("_Hair_Mask", hairMask);
                m.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(m);
                genMats.Add(m);
            }
            UnifySkinAndHair(genMats);   // ผิว/ผมในทุกโทน = แบบ A → เปลี่ยนโทนชุดแล้วผิวไม่เปลี่ยน

            var cityMats = new List<Material>();
            foreach (var path in SortedAssets("t:Material", CityAlts))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && m.name.StartsWith("PolygonCity_")) cityMats.Add(m);
            }
            if (genMats.Count == 0) { Fail("ไม่พบ material ใน " + GenAlts + " — import POLYGON Generic ก่อน"); return; }

            var sets = new List<CharacterCatalog.ToneSet>
            {
                new CharacterCatalog.ToneSet { name = "Generic", materials = genMats.ToArray(), skinVariants = 1, hairSkinMasks = true,
                    toneSwatches = Hex(GenToneHex, genMats.Count), skinSwatches = (Color[])GenSkin.Clone(),
                    hairAlbedo = MaskAverages(genMats, hairMask) },
            };
            bool hasCity = cityMats.Count >= 3;
            if (hasCity)
                sets.Add(new CharacterCatalog.ToneSet { name = "City", materials = cityMats.ToArray(), skinVariants = 3, hairSkinMasks = false,
                    toneSwatches = Hex(CityToneHex, cityMats.Count / 3), skinSwatches = Hex(CitySkinHex, 3) });

            // ---- 2) prefab ชุด ----
            var models = new List<GameObject>(); var labels = new List<string>();
            var genders = new List<int>(); var toneSet = new List<int>(); var preset = new List<bool>();
            for (int i = 0; i < Outfits.GetLength(0); i++)
            {
                bool city = (string)Outfits[i, 0] == "C";
                if (city && !hasCity) continue;
                string key = (string)Outfits[i, 1]; int g = (int)Outfits[i, 3];
                var p = city ? BuildCityOutfit(key, g) : BuildGenericOutfit(key, g, genMats[0]);
                if (p == null) continue;
                models.Add(p); labels.Add((string)Outfits[i, 2]); genders.Add(g);
                toneSet.Add(city ? 1 : 0); preset.Add(city);
            }
            if (models.Count == 0) { Fail("สร้าง prefab ชุดไม่ได้"); return; }

            // ---- 3) CharacterCatalog ----
            var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/_Project/Resources/CharacterCatalog.asset");
            if (cat == null)
            {
                EnsureFolder("Assets/_Project", "Resources");
                cat = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(cat, "Assets/_Project/Resources/CharacterCatalog.asset");
            }
            if (cat.controller == null)
                cat.controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/NisitCharacter.controller");
            cat.models = models.ToArray(); cat.labels = labels.ToArray();
            cat.modelGender = genders.ToArray(); cat.modelToneSet = toneSet.ToArray(); cat.modelPresetHair = preset.ToArray();
            cat.toneSets = sets.ToArray();
            cat.hairColors = (Color[])HairColors.Clone();
            EditorUtility.SetDirty(cat);

            // ---- 4) AccessoryCatalog ----
            var acc = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>("Assets/_Project/Resources/AccessoryCatalog.asset");
            if (acc == null)
            {
                acc = ScriptableObject.CreateInstance<AccessoryCatalog>();
                AssetDatabase.CreateAsset(acc, "Assets/_Project/Resources/AccessoryCatalog.asset");
            }
            acc.slots = new[]
            {
                MakeSlot("ทรงผม", Hairs, true),
                MakeSlot("หมวก", Hats, false),
                MakeSlot("แว่นตา", Glasses, false),
                MakeSlot("หนวด/เครา", Facial, true),
            };
            EditorUtility.SetDirty(acc);
            AssetDatabase.SaveAssets();

            // ---- 5) รูปตัวอย่าง ----
            GenerateThumbnails(cat, acc);
            AssetDatabase.SaveAssets();

            int nm = 0, nf = 0; foreach (var g in genders) if (g == M) nm++; else nf++;
            Debug.Log($"<color=lime>[Nisit] ตั้งตัวละคร Synty แล้ว: ชุดชาย {nm} · ชุดหญิง {nf} · ทรงผม {acc.slots[0].options.Length}</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"ตั้งตัวละคร Synty เสร็จ! 🧍\n\n• ชุดชาย {nm} · ชุดหญิง {nf} (รวม POLYGON City)\n• ทรงผม/หนวดแยกเพศ\n\nต่อไป: Nisit ▸ Build Character Creator", "เยี่ยม!");
        }

        // ---------- prefab ชุด Generic: ตัวเนื้อ 1 ชิ้น + ผม/ฮู้ด skinned (ซ่อน) · ถอด prop ที่หัว ----------
        static GameObject BuildGenericOutfit(string key, int gender, Material baseMat)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(GenChars + "/SM_Gen_Chr_" + key + ".prefab");
            if (src == null) { Debug.LogWarning("[Nisit] ไม่พบ Synty prefab: " + key); return null; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Nisit_" + key;
            string bodyName = "SM_Gen_Chr_" + key;

            var toKill = new List<GameObject>();
            for (int i = 0; i < inst.transform.childCount; i++)
            {
                var c = inst.transform.GetChild(i);
                if (c.GetComponent<SkinnedMeshRenderer>() == null) continue;
                if (c.name == bodyName) { c.gameObject.SetActive(true); continue; }
                bool keepHair = false;
                foreach (var h in NativeHair) if (c.name == "SM_Gen_Chr_Attach_" + h) keepHair = true;
                if (keepHair) c.gameObject.SetActive(false); else toKill.Add(c.gameObject);
            }
            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true)) toKill.Add(mr.gameObject);
            foreach (var g in toKill) if (g != null) Object.DestroyImmediate(g);

            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (CharacterCatalog.IsSynty(r.sharedMaterial)) r.sharedMaterial = baseMat;
            return SaveOutfit(inst, gender, false, OutDir + "/Nisit_" + key + ".prefab");
        }

        // ---------- prefab ชุด City: ตัวเนื้อที่เปิดอยู่ชิ้นเดียว (ผม/หนวดอยู่ในเนื้อแล้ว) ----------
        static GameObject BuildCityOutfit(string key, int gender)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(CityChars + "/Character_" + key + ".prefab");
            if (src == null) { Debug.LogWarning("[Nisit] ไม่พบ City prefab: " + key); return null; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Nisit_City_" + key;
            var toKill = new List<GameObject>();
            for (int i = 0; i < inst.transform.childCount; i++)
            {
                var c = inst.transform.GetChild(i);
                if (c.GetComponent<SkinnedMeshRenderer>() == null) continue;
                if (!c.gameObject.activeSelf) toKill.Add(c.gameObject);
            }
            foreach (var g in toKill) Object.DestroyImmediate(g);
            return SaveOutfit(inst, gender, true, OutDir + "/Nisit_City_" + key + ".prefab");
        }

        static GameObject SaveOutfit(GameObject inst, int gender, bool presetHair, string path)
        {
            var anim = inst.GetComponent<Animator>();
            if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            var info = inst.GetComponent<CharacterLookInfo>();
            if (info == null) info = inst.AddComponent<CharacterLookInfo>();
            info.gender = gender; info.presetHair = presetHair;
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
            return prefab;
        }

        static AccessoryCatalog.Slot MakeSlot(string name, object[,] list, bool hairLike)
        {
            var opts = new List<GameObject>(); var labels = new List<string>(); var genders = new List<int>();
            for (int i = 0; i < list.GetLength(0); i++)
            {
                string key = (string)list[i, 0];
                var go = key.Contains("+") ? Combo(key) : FindAttachment(key);
                if (go == null) { Debug.LogWarning("[Nisit] ไม่พบชิ้นส่วน Synty: " + key); continue; }
                opts.Add(go); labels.Add((string)list[i, 1]); genders.Add((int)list[i, 2]);
            }
            return new AccessoryCatalog.Slot
            {
                slotName = name, bone = HumanBodyBones.Head,
                posOffset = Vector3.zero, eulerOffset = Vector3.zero, scale = 1f,
                options = opts.ToArray(), labels = labels.ToArray(), icons = new Sprite[opts.Count],
                genders = genders.ToArray(), hairLike = hairLike,
            };
        }

        // prefab รวมหลายชิ้น (เช่น ผมบ๊อบ + หางม้า) ติดกระดูกหัวพร้อมกัน
        static GameObject Combo(string key)
        {
            var root = new GameObject("Nisit_Hair_" + key.Replace("+", "_"));
            foreach (var part in key.Split('+'))
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(GenAttach + "/SM_Gen_Chr_Attach_" + part + ".prefab");
                if (src == null) { Object.DestroyImmediate(root); return null; }
                var c = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(c, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                c.transform.SetParent(root.transform, false);
            }
            var p = PrefabUtility.SaveAsPrefabAsset(root, OutDir + "/" + root.name + ".prefab");
            Object.DestroyImmediate(root);
            return p;
        }

        static GameObject FindAttachment(string key)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(GenAttach + "/SM_Gen_Chr_Attach_" + key + ".prefab");
            if (p != null) return p;
            string want = "SM_Gen_Chr_Attach_" + key;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(GenFbx))
                if (o is GameObject g && g.name == want) return g;
            return null;
        }

        // ---------- รูปตัวอย่าง ----------
        static void GenerateThumbnails(CharacterCatalog cat, AccessoryCatalog acc)
        {
            var far = new Vector3(15000f, 15000f, 15000f);
            var camGo = new GameObject("__ThumbCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 100f;
            cam.forceIntoRenderTexture = true;

            var keyGo = new GameObject("__ThumbKey"); var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.3f; key.color = new Color(1f, 0.98f, 0.94f);
            keyGo.transform.rotation = Quaternion.Euler(28f, 160f, 0f);
            var fillGo = new GameObject("__ThumbFill"); var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.7f; fill.color = new Color(0.86f, 0.9f, 1f);
            fillGo.transform.rotation = Quaternion.Euler(15f, -35f, 0f);

            var rt = new RenderTexture(ThumbSize, ThumbSize, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var written = new List<string>();

            int hairSlot = 0;
            int maleHair = FirstFit(acc, hairSlot, M) + 1, femaleHair = FirstFit(acc, hairSlot, F) + 1;
            int baseMale = cat.FirstOfGender(M), baseFemale = cat.FirstOfGender(F);

            var modelIcons = new string[cat.Count];
            for (int i = 0; i < cat.Count; i++)
            {
                var sel = new int[acc.slots.Length];
                sel[hairSlot] = cat.Gender(i) == F ? femaleHair : maleHair;
                var inst = Spawn(cat.Model(i), far, sel);
                if (inst == null) continue;
                cam.fieldOfView = 24f;
                FrameBody(cam, inst);
                modelIcons[i] = Shoot(cam, rt, "outfit_" + i);
                written.Add(modelIcons[i]);
                Object.DestroyImmediate(inst);
            }

            var slotIcons = new string[acc.slots.Length][];
            for (int s = 0; s < acc.slots.Length; s++)
            {
                var slot = acc.slots[s];
                slotIcons[s] = new string[slot.options.Length];
                for (int o = 0; o < slot.options.Length; o++)
                {
                    int g = acc.OptionGender(s, o) == F ? F : M;
                    var sel = new int[acc.slots.Length];
                    sel[s] = o + 1;
                    if (s != hairSlot) sel[hairSlot] = g == F ? femaleHair : maleHair;
                    var inst = Spawn(cat.Model(g == F ? baseFemale : baseMale), far, sel);
                    if (inst == null) continue;
                    cam.fieldOfView = 20f;
                    FrameHead(cam, inst);
                    slotIcons[s][o] = Shoot(cam, rt, "acc_" + s + "_" + o);
                    written.Add(slotIcons[s][o]);
                    Object.DestroyImmediate(inst);
                }
            }

            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(keyGo); Object.DestroyImmediate(fillGo);

            AssetDatabase.Refresh();
            foreach (var p in written) MakeSprite(p);

            var mi = new Sprite[cat.Count];
            for (int i = 0; i < cat.Count; i++) mi[i] = modelIcons[i] != null ? AssetDatabase.LoadAssetAtPath<Sprite>(modelIcons[i]) : null;
            cat.icons = mi;
            for (int s = 0; s < acc.slots.Length; s++)
            {
                var arr = new Sprite[slotIcons[s].Length];
                for (int o = 0; o < arr.Length; o++) arr[o] = slotIcons[s][o] != null ? AssetDatabase.LoadAssetAtPath<Sprite>(slotIcons[s][o]) : null;
                acc.slots[s].icons = arr;
            }
            EditorUtility.SetDirty(cat); EditorUtility.SetDirty(acc);
        }

        static int FirstFit(AccessoryCatalog acc, int slot, int g)
        {
            for (int o = 0; o < acc.OptionCount(slot); o++) if (acc.Fits(slot, o, g)) return o;
            return 0;
        }

        static GameObject Spawn(GameObject prefab, Vector3 pos, int[] sel)
        {
            if (prefab == null) return null;
            var inst = Object.Instantiate(prefab);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
            CharacterAccessories.Apply(inst.transform, sel);
            return inst;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void FrameBody(Camera cam, GameObject go)
        {
            var b = WorldBounds(go);
            float h = Mathf.Max(b.size.y, 0.2f);
            float dist = (h * 0.58f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.position = b.center + new Vector3(0f, h * 0.05f, dist);
            cam.transform.LookAt(b.center);
        }

        static void FrameHead(Camera cam, GameObject go)
        {
            var anim = go.GetComponent<Animator>();
            var head = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 c = head != null ? head.position + new Vector3(0f, 0.12f, 0f) : WorldBounds(go).center;
            float dist = 0.30f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.position = c + new Vector3(0.10f, 0.04f, dist);
            cam.transform.LookAt(c);
        }

        static string Shoot(Camera cam, RenderTexture rt, string name)
        {
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(ThumbSize, ThumbSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, ThumbSize, ThumbSize), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            string path = ThumbDir + "/" + name + ".png";
            File.WriteAllBytes(Application.dataPath + path.Substring("Assets".Length), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        static void MakeSprite(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }

        // ---------- helpers ----------
        // alt ของ Synty Generic: 01–04 = ชุดสี, A/B/C = ชุดสี + สีผิว/ผมคนละแบบ
        //   หา pixel ผิว/ผม = ค่าคงที่ข้าม 01–04 ในแต่ละ A/B/C แต่ต่างกันระหว่าง A/B/C
        //   แล้วเขียน texture ใหม่ที่ pixel ส่วนนั้นใช้ของ 01_A ทุกโทน (สีผิว/ผมเลือกเองผ่าน mask)
        static void UnifySkinAndHair(List<Material> mats)
        {
            if (mats.Count != 12) { Debug.LogWarning("[Nisit] ไม่ใช่ alt 12 แบบ — ข้ามการปรับสีผิว"); return; }
            var srcTex = new Texture2D[12];
            for (int i = 0; i < 12; i++) srcTex[i] = mats[i].GetTexture("_Albedo_Map") as Texture2D;
            if (srcTex[0] == null) return;
            int W = srcTex[0].width, H = srcTex[0].height;

            var P = new Color32[12][];
            for (int i = 0; i < 12; i++) P[i] = ReadPixels32(srcTex[i], W, H);

            int n = W * H, region = 0;
            var isSkin = new bool[n];
            for (int k = 0; k < n; k++)
            {
                bool constant = true;
                for (int v = 0; v < 3 && constant; v++)
                    for (int pal = 1; pal < 4; pal++)
                        if (!Same(P[v][k], P[pal * 3 + v][k])) { constant = false; break; }
                if (constant && (!Same(P[0][k], P[1][k]) || !Same(P[0][k], P[2][k]))) { isSkin[k] = true; region++; }
            }

            EnsureFolder(MatDir, "Textures");
            var srcImp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(srcTex[0])) as TextureImporter;
            for (int i = 0; i < 12; i++)
            {
                var outPx = (Color32[])P[i].Clone();
                if (i != 0) for (int k = 0; k < n; k++) if (isSkin[k]) outPx[k] = P[0][k];
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                t.SetPixels32(outPx); t.Apply();
                string path = MatDir + "/Textures/Nisit_" + mats[i].name.Replace("Nisit_", "") + ".png";
                File.WriteAllBytes(Application.dataPath + path.Substring("Assets".Length), t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.alphaIsTransparency = false;
                    imp.maxTextureSize = srcImp != null ? srcImp.maxTextureSize : 4096;
                    imp.textureCompression = srcImp != null ? srcImp.textureCompression : TextureImporterCompression.CompressedHQ;
                    imp.filterMode = srcTex[0].filterMode;
                    imp.SaveAndReimport();
                }
                mats[i].SetTexture("_Albedo_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                EditorUtility.SetDirty(mats[i]);
            }
            Debug.Log($"[Nisit] ปรับผิว/ผมให้เท่ากันทุกโทนแล้ว ({region} px)");
        }

        static bool Same(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 12;

        static Color32[] ReadPixels32(Texture tex, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels32();
            Object.DestroyImmediate(t);
            return px;
        }

        // สีเฉลี่ยของ albedo ในพื้นที่ mask (ผิว/ผม) ของแต่ละ material — ใช้ชดเชยการคูณสีของ shader
        //   (alt A/B/C ของ Synty มีสีผิว/ผมอบมาต่างกัน → ถ้าไม่ชดเชย เปลี่ยนโทนชุดแล้วผิวเปลี่ยนตาม)
        static Color[] MaskAverages(List<Material> mats, Texture2D mask)
        {
            var r = new Color[mats.Count];
            if (mask == null) return r;
            const int N = 512;
            var mp = ReadPixels(mask, N);
            for (int i = 0; i < mats.Count; i++)
            {
                var tex = mats[i].GetTexture("_Albedo_Map");
                if (tex == null) continue;
                var px = ReadPixels(tex, N);
                Color sum = Color.black; int n = 0;
                for (int k = 0; k < px.Length; k++) if (mp[k].r > 0.5f) { sum += px[k]; n++; }
                if (n > 0) { var c = sum / n; c.a = 1f; r[i] = c; }
            }
            return r;
        }

        static Color[] ReadPixels(Texture tex, int n)
        {
            var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, n, n), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels();
            Object.DestroyImmediate(t);
            return px;
        }

        static Color[] Hex(string[] hex, int n)
        {
            var r = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color c = Color.gray;
                if (i < hex.Length) ColorUtility.TryParseHtmlString("#" + hex[i], out c);
                r[i] = c;
            }
            return r;
        }

        static List<string> SortedAssets(string filter, string folder)
        {
            var list = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (var g in AssetDatabase.FindAssets(filter, new[] { folder })) list.Add(AssetDatabase.GUIDToAssetPath(g));
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static void Fail(string msg)
        {
            Debug.LogError("[Nisit] " + msg);
            if (!SuppressDialog) EditorUtility.DisplayDialog("Nisit Simulator", msg, "OK");
        }
    }
}
#endif
