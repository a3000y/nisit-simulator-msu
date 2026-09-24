using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.Net;

namespace NisitSimulator.UI
{
    // หน้าจอแต่งตัวละคร — เลือกแบบ/เพศ + สี + ชื่อ พร้อมพรีวิว 3D หมุนได้
    //   เขียนค่าลง GameSession (PlayerName/PlayerColor/PlayerModel) → เข้าเกมแล้ว PlayerModelSwapper ใส่ให้ + sync MP
    //   UI + เวที (กล้อง/ไฟ/จุดวางโมเดล) สร้าง+ต่อโดย Editor tool (Nisit -> Build Character Creator)
    public class CharacterCreatorController : MonoBehaviour
    {
        [System.Serializable]
        public class AccessorySlotUI { public Button[] buttons; }   // buttons[0]=ไม่ใส่, [1..]=option

        [Header("ต่อโดย Editor")]
        public TMP_InputField nameInput;
        public Button[] modelButtons;
        public Button[] colorButtons;
        public AccessorySlotUI[] accessorySlots;   // 1 ช่องต่อชนิดของแต่ง (หมวก/แว่น/...)
        public Camera previewCamera;   // กล้องส่องเวที (render → RawImage)
        public Transform previewRoot;  // จุดวางโมเดลพรีวิว
        public RawImage previewImage;  // แสดงผลพรีวิว

        [Header("ปรับได้")]
        public float rotateSpeed = 25f;
        public float targetHeight = 1.7f;

        private int model, color;
        private GameObject current;
        private RenderTexture rt;
        private bool initialized;

        void Start()
        {
            model = GameSession.PlayerModel;
            color = GameSession.PlayerColor;

            // สร้าง RenderTexture รันไทม์ (ไม่ต้องมีไฟล์ asset)
            if (previewCamera != null && previewImage != null)
            {
                rt = new RenderTexture(512, 640, 16) { name = "CharPreviewRT" };
                previewCamera.targetTexture = rt;
                previewImage.texture = rt;
            }

            if (nameInput != null)
            {
                nameInput.text = GameSession.PlayerName;
                nameInput.onValueChanged.AddListener(s => GameSession.PlayerName = s);
            }

            var cat = CharacterCatalog.Load();
            int count = cat != null ? cat.Count : 0;
            if (modelButtons != null)
                for (int i = 0; i < modelButtons.Length; i++)
                {
                    if (modelButtons[i] == null) continue;
                    bool has = i < count;
                    modelButtons[i].gameObject.SetActive(has);
                    if (!has) continue;
                    int idx = i;
                    var lbl = modelButtons[i].GetComponentInChildren<TMP_Text>();
                    if (lbl != null) lbl.text = cat.Label(i);
                    modelButtons[i].onClick.AddListener(() => PickModel(idx));
                }

            if (colorButtons != null)
                for (int i = 0; i < colorButtons.Length; i++)
                {
                    if (colorButtons[i] == null) continue;
                    int idx = i;
                    var img = colorButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i > 0 && i < NetworkAvatar.Palette.Length) ? NetworkAvatar.Palette[i] : Color.white;
                    colorButtons[i].onClick.AddListener(() => PickColor(idx));
                }

            SetupAccessoryUI();

            SpawnPreview(model);
            HighlightModel();
            HighlightColor();
            HighlightAccessories();

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

        void PickModel(int i)
        {
            model = i; GameSession.PlayerModel = i;
            SpawnPreview(i);
            HighlightModel();
        }

        void PickColor(int i)
        {
            color = i; GameSession.PlayerColor = i;
            if (current != null) NetworkAvatar.ApplyColor(current, i);
            HighlightColor();
        }

        // ---------- ของแต่ง (หมวก/แว่น/...) ----------
        void SetupAccessoryUI()
        {
            var cat = AccessoryCatalog.Load();
            int slotCount = cat != null ? cat.SlotCount : 0;

            // ให้ GameSession.PlayerAccessories ยาวเท่าจำนวนช่อง (คงค่าที่เคยเลือกไว้)
            var sel = GameSession.PlayerAccessories;
            if (sel == null || sel.Length != slotCount)
            {
                var arr = new int[slotCount];
                if (sel != null) for (int i = 0; i < Mathf.Min(slotCount, sel.Length); i++) arr[i] = sel[i];
                GameSession.PlayerAccessories = arr;
            }

            if (accessorySlots == null) return;
            for (int s = 0; s < accessorySlots.Length; s++)
            {
                var slotUI = accessorySlots[s];
                if (slotUI == null || slotUI.buttons == null) continue;
                bool slotExists = s < slotCount;
                int optCount = slotExists ? cat.OptionCount(s) : 0;

                for (int b = 0; b < slotUI.buttons.Length; b++)
                {
                    var btn = slotUI.buttons[b];
                    if (btn == null) continue;
                    // ปุ่ม b=0 คือ "ไม่ใส่"; b>=1 คือ option b-1
                    bool has = slotExists && (b == 0 || (b - 1) < optCount);
                    btn.gameObject.SetActive(has);
                    if (!has) continue;

                    var lbl = btn.GetComponentInChildren<TMP_Text>();
                    if (lbl != null) lbl.text = (b == 0) ? "ไม่ใส่" : cat.OptionLabel(s, b - 1);

                    int slotIdx = s, val = b;
                    btn.onClick.AddListener(() => PickAccessory(slotIdx, val));
                }
            }
        }

        void PickAccessory(int slot, int value)
        {
            if (GameSession.PlayerAccessories == null || slot >= GameSession.PlayerAccessories.Length) return;
            GameSession.PlayerAccessories[slot] = value;
            if (current != null) CharacterAccessories.Apply(current, GameSession.PlayerAccessories);
            HighlightAccessories();
        }

        void HighlightAccessories()
        {
            if (accessorySlots == null) return;
            var sel = GameSession.PlayerAccessories;
            for (int s = 0; s < accessorySlots.Length; s++)
            {
                var slotUI = accessorySlots[s];
                if (slotUI == null || slotUI.buttons == null) continue;
                int chosen = (sel != null && s < sel.Length) ? sel[s] : 0;
                for (int b = 0; b < slotUI.buttons.Length; b++)
                    if (slotUI.buttons[b] != null)
                        slotUI.buttons[b].transform.localScale = Vector3.one * (b == chosen ? 1.12f : 1f);
            }
        }

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
            if (anim != null && cat.controller != null) anim.runtimeAnimatorController = cat.controller;

            AutoScaleToFeet(current);
            NetworkAvatar.ApplyColor(current, color);
            CharacterAccessories.Apply(current, GameSession.PlayerAccessories);   // ใส่ของแต่งกับโมเดลใหม่
        }

        // ปรับสเกลให้สูงเท่า targetHeight + วางเท้าที่ระดับ previewRoot (โมเดลต่างขนาดกัน)
        void AutoScaleToFeet(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.01f)
            {
                float s = targetHeight / b.size.y;
                go.transform.localScale *= s;
            }

            // คำนวณใหม่หลังสเกล แล้วยกให้เท้าอยู่ที่ระดับ previewRoot
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float footOffset = previewRoot.position.y - b.min.y;
            go.transform.position += new Vector3(0f, footOffset, 0f);
        }

        void HighlightModel()
        {
            if (modelButtons == null) return;
            for (int i = 0; i < modelButtons.Length; i++)
                if (modelButtons[i] != null)
                    modelButtons[i].transform.localScale = Vector3.one * (i == model ? 1.12f : 1f);
        }

        void HighlightColor()
        {
            if (colorButtons == null) return;
            for (int i = 0; i < colorButtons.Length; i++)
                if (colorButtons[i] != null)
                    colorButtons[i].transform.localScale = Vector3.one * (i == color ? 1.35f : 1f);
        }
    }
}
