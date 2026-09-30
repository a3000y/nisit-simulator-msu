using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.Net;

namespace NisitSimulator.UI
{
    // หน้าจอแต่งตัวละคร (Synty POLYGON Generic + City) — เลือกเพศ → ชุด + ทรงผม + สีผม/สีผิว/โทนชุด + หมวก/แว่น/หนวด + ชื่อ
    //   ตัวเลือกกรองตามเพศ · ชุด City มีผม/หนวดติดมา (ปิดช่องทรงผม/สีผม/หนวด)
    //   เขียนค่าลง GameSession (PlayerName/PlayerColor/PlayerModel/PlayerAccessories) → เข้าเกมแล้ว PlayerModelSwapper ใส่ให้ + sync MP
    //   UI สร้างโดย Nisit -> Build Character Creator · ข้อมูลจาก Nisit -> Setup Synty Characters
    public class CharacterCreatorController : MonoBehaviour
    {
        [System.Serializable]
        public class AccessorySlotUI { public Button[] buttons; }   // buttons[0]=ไม่ใส่, [1..]=option ที่เข้ากับเพศ

        [Header("ต่อโดย Editor")]
        public TMP_InputField nameInput;
        public Button[] genderButtons;             // 0 = ชาย, 1 = หญิง
        public Button[] modelButtons;              // ชุด (กรองตามเพศ)
        public Button[] colorButtons;              // โทนสีชุด
        public Button[] hairColorButtons;          // สีผม
        public Button[] skinButtons;               // สีผิว
        public AccessorySlotUI[] accessorySlots;   // ทรงผม/หมวก/แว่น/หนวด เรียงตาม AccessoryCatalog
        public GameObject[] hairOnlyPanels;        // แผงที่ปิดเมื่อชุดมีผมติดมา (ทรงผม/สีผม/หนวด)
        public Button[] difficultyButtons;         // 0=ง่าย 1=ปกติ 2=ยาก
        public Button randomButton;
        public Camera previewCamera;
        public Transform previewRoot;
        public RawImage previewImage;

        [Header("ปรับได้")]
        public float rotateSpeed = 25f;
        public float targetHeight = 1.7f;

        private int model, gender;
        private GameObject current;
        private RenderTexture rt;
        private bool initialized;
        private readonly List<int> modelMap = new List<int>();          // ปุ่ม → index ชุด
        private readonly List<List<int>> accMap = new List<List<int>>(); // [ช่อง][ปุ่ม-1] → option
        private readonly int[] lastModelOf = { -1, -1 };                 // ชุดล่าสุดของแต่ละเพศ

        int Tone => CharacterCatalog.ToneOf(GameSession.PlayerColor);
        int HairCol => CharacterCatalog.HairOf(GameSession.PlayerColor);
        int Skin => CharacterCatalog.SkinOf(GameSession.PlayerColor);

        void Start()
        {
            var cat = CharacterCatalog.Load();
            int count = cat != null ? cat.Count : 0;
            model = Mathf.Clamp(GameSession.PlayerModel, 0, Mathf.Max(0, count - 1));
            GameSession.PlayerModel = model;
            gender = cat != null ? cat.Gender(model) : 0;

            if (previewCamera != null && previewImage != null)
            {
                rt = new RenderTexture(512, 640, 16) { name = "CharPreviewRT" };
                previewCamera.targetTexture = rt;
                previewImage.texture = rt;
            }

            if (nameInput != null)
            {
                nameInput.text = GameSession.PlayerName;
                nameInput.onValueChanged.AddListener(s => { GameSession.PlayerName = s; PushToNetwork(); });
            }

            // ผูกปุ่มครั้งเดียว — ความหมายของปุ่มเปลี่ยนตามเพศ/ชุดผ่าน map
            Bind(genderButtons, PickGender);
            Bind(modelButtons, b => { if (b < modelMap.Count) PickModel(modelMap[b]); });
            Bind(colorButtons, PickTone);
            Bind(hairColorButtons, PickHairColor);
            Bind(skinButtons, PickSkin);
            if (accessorySlots != null)
                for (int s = 0; s < accessorySlots.Length; s++)
                {
                    int slot = s;
                    if (accessorySlots[s] != null) Bind(accessorySlots[s].buttons, b => PickAccessoryButton(slot, b));
                }
            Bind(difficultyButtons, PickDifficulty);
            if (randomButton != null) randomButton.onClick.AddListener(Randomize);

            EnsureAccessoryArray();
            SanitizeForModel();
            RefreshAllUI();
            SpawnPreview(model);

            initialized = true;
            if (previewCamera != null) previewCamera.enabled = true;
        }

        void OnEnable()  { if (initialized && previewCamera != null) previewCamera.enabled = true; }
        void OnDisable() { if (previewCamera != null) previewCamera.enabled = false; }

        void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        void Update()
        {
            if (previewRoot != null && current != null)
                previewRoot.Rotate(0f, rotateSpeed * Time.unscaledDeltaTime, 0f);
        }

        static void Bind(Button[] buttons, System.Action<int> onPick)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int idx = i;
                buttons[i].onClick.AddListener(() => onPick(idx));
            }
        }

        // ---------- เลือก ----------
        void PickGender(int g)
        {
            var cat = CharacterCatalog.Load();
            if (cat == null || g == gender) return;
            lastModelOf[gender] = model;
            gender = g;
            int m = lastModelOf[g] >= 0 ? lastModelOf[g] : FirstUnlockedOfGender(g);
            SetModel(m);
        }

        void PickModel(int i) => SetModel(i);

        void SetModel(int i)
        {
            var cat = CharacterCatalog.Load();
            model = i; GameSession.PlayerModel = i;
            if (cat != null) gender = cat.Gender(i);
            SanitizeForModel();
            RefreshAllUI();
            SpawnPreview(i);
            PushToNetwork();
        }

        void PickTone(int i)      { SetColor(CharacterCatalog.PackColor(i, HairCol, Skin)); }
        void PickHairColor(int i) { SetColor(CharacterCatalog.PackColor(Tone, i, Skin)); }
        void PickSkin(int i)      { SetColor(CharacterCatalog.PackColor(Tone, HairCol, i)); }

        void SetColor(int packed)
        {
            GameSession.PlayerColor = packed;
            RefreshLook();
            HighlightAll();
            PushToNetwork();
        }

        void PickAccessoryButton(int slot, int button)
        {
            var acc = GameSession.PlayerAccessories;
            if (acc == null || slot >= acc.Length) return;
            if (button == 0) acc[slot] = 0;
            else
            {
                var map = slot < accMap.Count ? accMap[slot] : null;
                if (map == null || button - 1 >= map.Count) return;
                acc[slot] = map[button - 1] + 1;
            }
            if (current != null) CharacterAccessories.Apply(current.transform, acc);
            HighlightAll();
            PushToNetwork();
        }

        void PickDifficulty(int i)
        {
            GameSession.Difficulty = i;
            HighlightAll();
        }

        void RefreshLook()
        {
            if (current == null) return;
            NetworkAvatar.ApplyColor(current, GameSession.PlayerColor);
            CharacterAccessories.Apply(current.transform, GameSession.PlayerAccessories);
        }

        // sync การแต่งตัวขึ้นเครือข่าย (ใช้ในล็อบบี้ MP — ถ้าเล่นคนเดียวจะ no-op)
        void PushToNetwork()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.LocalClient == null) return;
            var po = nm.LocalClient.PlayerObject;
            var av = po != null ? po.GetComponent<NetworkAvatar>() : null;
            if (av != null) { av.SetIdentity(GameSession.PlayerName, GameSession.PlayerColor, GameSession.PlayerModel); av.SetAccessories(); }
        }

        // ---------- กติกา: ของแต่ง/สีต้องเข้ากับเพศ+ชุด ----------
        void EnsureAccessoryArray()
        {
            var acat = AccessoryCatalog.Load();
            int n = acat != null ? acat.SlotCount : 0;
            var sel = GameSession.PlayerAccessories;
            if (sel != null && sel.Length == n) return;
            var arr = new int[n];
            if (sel != null) for (int i = 0; i < Mathf.Min(n, sel.Length); i++) arr[i] = sel[i];
            GameSession.PlayerAccessories = arr;
        }

        void SanitizeForModel()
        {
            var cat = CharacterCatalog.Load();
            var acat = AccessoryCatalog.Load();
            var acc = GameSession.PlayerAccessories;
            // ชุดที่ยังล็อก (เช่นค่าจากรอบก่อน) → ใช้ชุดแรกที่ปลดแล้วของเพศนั้น
            if (cat != null && !ModelUnlocked(model))
            {
                model = FirstUnlockedOfGender(gender); GameSession.PlayerModel = model;
            }
            if (acat != null && acc != null)
            {
                int hair = SlotByName(acat, "ทรงผม");
                for (int s = 0; s < acc.Length && s < acat.SlotCount; s++)
                {
                    if (acc[s] > 0 && !acat.Fits(s, acc[s] - 1, gender)) acc[s] = 0;   // ของต่างเพศ → ถอด
                    else if (acc[s] > 0 && acc[s] - 1 < acat.OptionCount(s) && !OptionUnlocked(acat, s, acc[s] - 1)) acc[s] = 0;   // ยังล็อก → ถอด
                    else if (acc[s] > 0 && acc[s] - 1 < acat.OptionCount(s) && !CharacterAccessories.CanWear(cat != null ? cat.Model(model) : null, acat.GetSlot(s).options[acc[s] - 1]) && !acat.GetSlot(s).hairLike) acc[s] = 0;   // ใส่กับชุดนี้ไม่ได้ (ฮู้ดบนชุด City)
                }
                // ไม่ให้หัวโล้นโดยไม่ตั้งใจเวลาเปลี่ยนเพศ: ใส่ทรงแรกของเพศนั้น
                if (hair >= 0 && hair < acc.Length && acc[hair] == 0)
                    for (int o = 0; o < acat.OptionCount(hair); o++) if (acat.Fits(hair, o, gender) && OptionUnlocked(acat, hair, o)) { acc[hair] = o + 1; break; }
            }
            // สีให้อยู่ในช่วงของชุดสีของชุดนี้
            var set = cat != null ? cat.ToneSetOf(model) : null;
            if (set != null)
            {
                int t = Mathf.Clamp(Tone, 0, Mathf.Max(0, set.ToneCount - 1));
                int sk = Mathf.Clamp(Skin, 0, Mathf.Max(0, set.SkinCount - 1));
                GameSession.PlayerColor = CharacterCatalog.PackColor(t, HairCol, sk);
            }
        }

        static int SlotByName(AccessoryCatalog cat, string name)
        {
            if (cat == null) return -1;
            for (int i = 0; i < cat.SlotCount; i++) { var s = cat.GetSlot(i); if (s != null && s.slotName == name) return i; }
            return -1;
        }

        // ---------- วาด UI ตามเพศ/ชุดปัจจุบัน ----------
        void RefreshAllUI()
        {
            var cat = CharacterCatalog.Load();
            var acat = AccessoryCatalog.Load();

            // ชุด (กรองเพศ)
            modelMap.Clear();
            if (cat != null) for (int i = 0; i < cat.Count; i++) if (cat.Gender(i) == gender) modelMap.Add(i);
            if (modelButtons != null)
                for (int b = 0; b < modelButtons.Length; b++)
                {
                    if (modelButtons[b] == null) continue;
                    bool has = b < modelMap.Count;
                    modelButtons[b].gameObject.SetActive(has);
                    if (!has) continue;
                    SetCaption(modelButtons[b], cat.Label(modelMap[b]));
                    SetIcon(modelButtons[b], cat.Icon(modelMap[b]));
                    var mo = cat.Model(modelMap[b]);
                    SetLocked(modelButtons[b], !CosmeticUnlocks.IsUnlocked(mo), CosmeticUnlocks.LockHint(mo));
                }

            // สี (ตามชุดสีของชุด)
            var set = cat != null ? cat.ToneSetOf(model) : null;
            ShowSwatches(colorButtons, set != null ? set.toneSwatches : NetworkAvatar.Palette, set != null ? set.ToneCount : NetworkAvatar.Palette.Length);
            ShowSwatches(skinButtons, set != null ? set.skinSwatches : null, set != null ? set.SkinCount : 0);
            ShowSwatches(hairColorButtons, cat != null ? cat.hairColors : null, cat != null && cat.hairColors != null ? cat.hairColors.Length : 0);

            // ของแต่ง (กรองเพศ)
            bool preset = cat != null && cat.PresetHair(model);
            accMap.Clear();
            int hairSlot = SlotByName(acat, "ทรงผม");
            if (accessorySlots != null)
                for (int s = 0; s < accessorySlots.Length; s++)
                {
                    var map = new List<int>();
                    accMap.Add(map);
                    var ui = accessorySlots[s];
                    if (ui == null || ui.buttons == null) continue;
                    bool exists = acat != null && s < acat.SlotCount;
                    var mdl = cat != null ? cat.Model(model) : null;
                    if (exists) for (int o = 0; o < acat.OptionCount(s); o++)
                        if (acat.Fits(s, o, gender) && CharacterAccessories.CanWear(mdl, acat.GetSlot(s).options[o])) map.Add(o);
                    for (int b = 0; b < ui.buttons.Length; b++)
                    {
                        var btn = ui.buttons[b];
                        if (btn == null) continue;
                        bool has = exists && (b == 0 || b - 1 < map.Count);
                        btn.gameObject.SetActive(has);
                        if (!has) continue;
                        if (b == 0) { SetCaption(btn, s == hairSlot ? "หัวโล้น" : "ไม่ใส่"); SetIcon(btn, null); SetLocked(btn, false, null); }
                        else
                        {
                            SetCaption(btn, acat.OptionLabel(s, map[b - 1])); SetIcon(btn, acat.OptionIcon(s, map[b - 1]));
                            var opt = acat.GetSlot(s).options[map[b - 1]];
                            SetLocked(btn, !CosmeticUnlocks.IsUnlocked(opt), CosmeticUnlocks.LockHint(opt));
                        }
                    }
                }

            // ชุดที่มีผมติดมา / เพศหญิง(หนวด) → หรี่แผง
            if (hairOnlyPanels != null)
                for (int i = 0; i < hairOnlyPanels.Length; i++)
                {
                    var p = hairOnlyPanels[i];
                    if (p == null) continue;
                    var cg = p.GetComponent<CanvasGroup>();
                    if (cg == null) cg = p.AddComponent<CanvasGroup>();
                    bool isBeard = p.name.Contains("หนวด");
                    bool off = preset || (isBeard && gender == 1);
                    cg.alpha = off ? 0.35f : 1f;
                    cg.interactable = !off; cg.blocksRaycasts = !off;
                }

            HighlightAll();
        }

        void ShowSwatches(Button[] buttons, Color[] colors, int n)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                bool has = colors != null && i < n && i < colors.Length;
                buttons[i].gameObject.SetActive(has);
                if (!has) continue;
                var img = buttons[i].GetComponent<Image>();
                if (img != null) img.color = colors[i];
            }
        }

        // ล็อกปุ่ม (ยังไม่ได้ความสำเร็จที่ต้องใช้) — กดไม่ได้ + ไอคอนจาง + บอกชื่อความสำเร็จ
        static void SetLocked(Button b, bool locked, string hint)
        {
            b.interactable = !locked;
            var ic = b.transform.Find("Icon");
            var im = ic != null ? ic.GetComponent<Image>() : null;
            if (im != null) im.color = locked ? new Color(0.55f, 0.55f, 0.6f, 0.35f) : Color.white;
            if (locked) SetCaption(b, "<color=#9A8FB8>ล็อก: " + hint + "</color>");
        }

        bool ModelUnlocked(int i) { var c = CharacterCatalog.Load(); return c != null && CosmeticUnlocks.IsUnlocked(c.Model(i)); }

        int FirstUnlockedOfGender(int g)
        {
            var c = CharacterCatalog.Load();
            if (c == null) return 0;
            for (int i = 0; i < c.Count; i++) if (c.Gender(i) == g && ModelUnlocked(i)) return i;
            return c.FirstOfGender(g);
        }

        static bool OptionUnlocked(AccessoryCatalog acat, int slot, int opt)
            => CosmeticUnlocks.IsUnlocked(acat.GetSlot(slot).options[opt]);

        static void SetCaption(Button b, string s)
        {
            var lbl = b.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = s;
        }

        static void SetIcon(Button b, Sprite icon)
        {
            var ic = b.transform.Find("Icon");
            if (ic != null)
            {
                var im = ic.GetComponent<Image>();
                if (im != null) { im.sprite = icon; im.preserveAspect = true; im.color = Color.white; im.enabled = icon != null; }
            }
            else if (icon != null)
            {
                var img = b.GetComponent<Image>();
                if (img != null) { img.sprite = icon; img.type = Image.Type.Simple; img.preserveAspect = true; img.color = Color.white; }
            }
        }

        // ---------- สุ่ม (ภายในเพศที่เลือก) ----------
        public void Randomize()
        {
            var cat = CharacterCatalog.Load();
            var acat = AccessoryCatalog.Load();
            if (cat == null) return;
            var pool = new List<int>();
            for (int i = 0; i < cat.Count; i++) if (cat.Gender(i) == gender && ModelUnlocked(i)) pool.Add(i);
            if (pool.Count > 0) { model = pool[Random.Range(0, pool.Count)]; GameSession.PlayerModel = model; }

            var set = cat.ToneSetOf(model);
            int nt = set != null ? Mathf.Max(1, set.ToneCount) : NetworkAvatar.Palette.Length;
            int ns = set != null ? Mathf.Max(1, set.SkinCount) : 1;
            int nh = cat.hairColors != null ? Mathf.Max(1, cat.hairColors.Length) : 1;
            int hairPick = Random.value < 0.6f ? Random.Range(0, Mathf.Min(3, nh)) : Random.Range(0, nh);   // ส่วนใหญ่สีธรรมชาติ
            GameSession.PlayerColor = CharacterCatalog.PackColor(Random.Range(0, nt), hairPick, Random.Range(0, ns));

            var acc = GameSession.PlayerAccessories;
            if (acat != null && acc != null)
            {
                for (int s = 0; s < acc.Length; s++) acc[s] = 0;
                RandomSlot(acat, acc, SlotByName(acat, "ทรงผม"), 1f);
                RandomSlot(acat, acc, SlotByName(acat, "หมวก"), 0.3f);
                RandomSlot(acat, acc, SlotByName(acat, "แว่นตา"), 0.25f);
                RandomSlot(acat, acc, SlotByName(acat, "หนวด/เครา"), 0.3f);
            }

            SanitizeForModel();
            RefreshAllUI();
            SpawnPreview(model);
            PushToNetwork();
        }

        void RandomSlot(AccessoryCatalog acat, int[] acc, int slot, float chance)
        {
            if (slot < 0 || slot >= acc.Length || Random.value > chance) return;
            var opts = new List<int>();
            var mdl = CharacterCatalog.Load() != null ? CharacterCatalog.Load().Model(model) : null;
            for (int o = 0; o < acat.OptionCount(slot); o++) if (acat.Fits(slot, o, gender) && CharacterAccessories.CanWear(mdl, acat.GetSlot(slot).options[o]) && OptionUnlocked(acat, slot, o)) opts.Add(o);
            if (opts.Count > 0) acc[slot] = opts[Random.Range(0, opts.Count)] + 1;
        }

        // ---------- ไฮไลต์ ----------
        void HighlightAll()
        {
            Mark(genderButtons, gender, 1.08f);
            Mark(modelButtons, modelMap.IndexOf(model), 1.12f);
            Mark(colorButtons, Tone, 1.3f);
            Mark(hairColorButtons, HairCol, 1.3f);
            Mark(skinButtons, Skin, 1.3f);
            Mark(difficultyButtons, GameSession.Difficulty, 1.14f);

            if (accessorySlots == null) return;
            var sel = GameSession.PlayerAccessories;
            for (int s = 0; s < accessorySlots.Length; s++)
            {
                if (accessorySlots[s] == null) continue;
                int v = (sel != null && s < sel.Length) ? sel[s] : 0;
                int button = v == 0 ? 0 : (s < accMap.Count ? accMap[s].IndexOf(v - 1) + 1 : -1);
                Mark(accessorySlots[s].buttons, button, 1.12f);
            }
        }

        static void Mark(Button[] buttons, int chosen, float scale)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                bool on = i == chosen;
                buttons[i].transform.localScale = Vector3.one * (on ? scale : 1f);
                var ring = buttons[i].transform.Find("Selected");
                if (ring != null) ring.gameObject.SetActive(on);
            }
        }

        // ---------- พรีวิว ----------
        void SpawnPreview(int i)
        {
            if (previewRoot == null) return;
            if (current != null) Destroy(current);
            previewRoot.localRotation = Quaternion.identity;

            var cat = CharacterCatalog.Load();
            var prefab = cat != null ? cat.Model(i) : null;
            if (prefab == null) return;

            current = Instantiate(prefab, previewRoot);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            current.transform.localScale = Vector3.one;

            var anim = current.GetComponentInChildren<Animator>();
            if (anim != null && cat.ControllerFor(i) != null) anim.runtimeAnimatorController = cat.ControllerFor(i);

            AutoScaleToFeet(current);
            RefreshLook();
        }

        void AutoScaleToFeet(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.01f) go.transform.localScale *= targetHeight / b.size.y;

            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(0f, previewRoot.position.y - b.min.y, 0f);
        }
    }
}
