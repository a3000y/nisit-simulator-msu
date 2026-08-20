using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Systems;
using NisitSimulator.Player;

namespace NisitSimulator.UI
{
    // หน้าต่างกระเป๋า — กด I เปิด/ปิด, แสดงช่องไอเทม, คลิกช่องเพื่อใช้
    //   UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Inventory)
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI (เซ็ตโดย Editor)")]
        public GameObject panel;
        public Transform grid;          // คอนเทนเนอร์ช่องเก็บของ
        public GameObject slotTemplate; // ต้นแบบ 1 ช่อง (Image ไอคอน + Text จำนวน + Button)
        public TMP_Text emptyHint;      // ข้อความ "กระเป๋าว่าง"

        public bool IsOpen { get; private set; }

        private InventoryManager inv;
        private PlayerMovement move;
        private readonly List<GameObject> spawned = new List<GameObject>();

        void Start()
        {
            inv = InventoryManager.Instance ?? Object.FindFirstObjectByType<InventoryManager>();
            var player = GameObject.Find("Player");
            if (player != null) move = player.GetComponent<PlayerMovement>();

            if (slotTemplate != null) slotTemplate.SetActive(false);
            if (inv != null) inv.OnChanged += Refresh;
            if (panel != null) panel.SetActive(false);
            Refresh();
        }

        void OnDestroy() { if (inv != null) inv.OnChanged -= Refresh; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I)) Toggle();
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (panel == null) return;
            panel.SetActive(true); IsOpen = true;
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            NisitSimulator.Core.SFXManager.Whoosh();
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false); IsOpen = false;
            if (move != null) move.enabled = true;
        }

        // สร้างช่องใหม่ตามไอเทมในกระเป๋า
        void Refresh()
        {
            if (grid == null || slotTemplate == null) return;

            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();

            var stacks = inv != null ? inv.Stacks : null;
            int n = stacks != null ? stacks.Count : 0;
            if (emptyHint != null) emptyHint.gameObject.SetActive(n == 0);

            for (int i = 0; i < n; i++)
            {
                var s = stacks[i];
                var go = Object.Instantiate(slotTemplate, grid);
                go.SetActive(true);
                spawned.Add(go);

                var icon = go.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    if (s.item.icon != null) { icon.sprite = s.item.icon; icon.color = Color.white; }
                    else icon.color = IconColor(s.item);   // ไม่มีไอคอน bake → ใช้สีตามหมวด
                }

                var nameT = go.transform.Find("Name")?.GetComponent<TMP_Text>();
                if (nameT != null) nameT.text = s.item.name;

                var countT = go.transform.Find("Count")?.GetComponent<TMP_Text>();
                if (countT != null) countT.text = s.count > 1 ? "x" + s.count : "";

                int idx = i;
                var btn = go.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => { if (inv != null) inv.Use(idx); });
            }
        }

        static Color IconColor(ShopItem it)
        {
            if (it.knowledge > 0) return new Color(0.45f, 0.75f, 0.45f);
            if (it.health > 0)    return new Color(0.95f, 0.50f, 0.55f);
            if (it.hunger > 0)    return new Color(1.00f, 0.68f, 0.32f);
            if (it.energy > 0)    return new Color(0.42f, 0.70f, 1.00f);
            return new Color(0.72f, 0.72f, 0.78f);
        }
    }
}
