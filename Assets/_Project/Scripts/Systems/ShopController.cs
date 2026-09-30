using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // ไอเทมในร้าน 1 ชิ้น (แก้ได้ใน Inspector ของ ShopController)
    [System.Serializable]
    public class ShopItem
    {
        public string name = "กาแฟ";
        public int price = 20;
        public float energy, hunger, health, knowledge, satisfaction;
        public string modelName;   // โมเดล KayKit สำหรับ bake ไอคอน (เว้นว่าง = ใช้วงกลมสี)
        public Sprite icon;        // ไอคอนที่ bake แล้ว (เซ็ตโดย Build Shop)
    }

    // ระบบร้านค้า — เปิด UI, ซื้อของ (จ่ายเงิน→ได้ผลสถานะ)
    //   UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Shop)
    public class ShopController : MonoBehaviour
    {
        [Header("UI (เซ็ตโดย Editor)")]
        public GameObject panel;
        public TMP_Text moneyText;
        public Transform content;
        public GameObject rowTemplate;
        public Button closeButton;
        public Button confirmButton;
        public Button clearButton;
        public TMP_Text totalText;

        [Header("สินค้าในร้าน")]
        public List<ShopItem> catalog = new List<ShopItem>();

        [Header("โหมด")]
        [Tooltip("true = ซื้อแล้วเก็บเข้ากระเป๋า (ร้านค้า) · false = ใช้ผลทันที (โรงอาหาร)")]
        public bool storeToInventory = false;

        public bool IsOpen { get; private set; }

        private PlayerStats stats;
        private PlayerMovement move;
        private readonly List<ShopRow> rows = new List<ShopRow>();
        private int[] qty;   // จำนวนในตะกร้าต่อสินค้า

        void Awake() { if (catalog.Count == 0) FillDefaults(); }

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            var player = GameObject.Find("Player");
            if (player != null) move = player.GetComponent<PlayerMovement>();
            if (stats != null) stats.OnMoneyChanged += OnMoney;

            BuildRows();
            if (closeButton != null)   closeButton.onClick.AddListener(Close);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (clearButton != null)   clearButton.onClick.AddListener(ClearCart);
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy() { if (stats != null) stats.OnMoneyChanged -= OnMoney; }

        void Update() { if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close(); }

        // ---------- เปิด/ปิด ----------
        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (panel == null) return;
            panel.SetActive(true); IsOpen = true;
            if (move != null) move.enabled = false;                 // หยุดเดินระหว่างเลือกซื้อ
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            Refresh();
        }

        public void Close()
        {
            ClearCart();                                            // ปิด = ยกเลิกตะกร้า
            if (panel != null) panel.SetActive(false); IsOpen = false;
            if (move != null) move.enabled = true;
        }

        // ---------- สร้างการ์ดสินค้าจาก catalog ----------
        void BuildRows()
        {
            if (rowTemplate == null || content == null) return;
            rowTemplate.SetActive(false);
            qty = new int[catalog.Count];
            for (int i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                var go = Object.Instantiate(rowTemplate, content);
                go.SetActive(true);
                var row = go.GetComponent<ShopRow>();
                if (row == null) continue;
                if (row.iconBg) row.iconBg.color = IconColor(item);
                if (row.icon)
                {
                    bool has = item.icon != null;
                    row.icon.enabled = has;
                    if (has) { row.icon.sprite = item.icon; row.icon.color = Color.white; }
                }
                if (row.nameText)   row.nameText.text = item.name;
                if (row.effectText) row.effectText.text = EffectText(item);
                if (row.priceText)  row.priceText.text = Perks.Price(item.price) < item.price ? $"<s><size=70%>{item.price}</size></s> {Perks.Price(item.price)}฿" : $"{item.price}฿";
                if (row.qtyBadge)   row.qtyBadge.SetActive(false);
                int idx = i;
                if (row.buyButton)  row.buyButton.onClick.AddListener(() => AddOne(idx));
                rows.Add(row);
            }
        }

        // ---------- ตะกร้า ----------
        void AddOne(int i)
        {
            if (qty == null || i < 0 || i >= qty.Length) return;
            qty[i]++;
            Refresh();
        }

        public void Confirm()
        {
            if (stats == null || qty == null) return;
            int total = 0;
            for (int i = 0; i < catalog.Count; i++) total += qty[i] * Perks.Price(catalog[i].price);   // ราคาหลังส่วนลด "นักต่อรอง"
            if (total <= 0) return;
            if (!stats.TrySpendMoney(total)) { HUDController.Toast("เงินไม่พอ!"); return; }

            // หา InventoryManager แบบกันพลาด (Instance → หาในฉากรวม inactive → สร้างใหม่ถ้าไม่มี)
            InventoryManager inv = null;
            if (storeToInventory)
            {
                inv = InventoryManager.Instance
                      ?? Object.FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
                if (inv == null)
                {
                    var gmObj = GameObject.Find("GameManager");
                    inv = (gmObj != null ? gmObj : new GameObject("InventoryManager")).AddComponent<InventoryManager>();
                }
            }
            for (int i = 0; i < catalog.Count; i++)
                if (qty[i] > 0)
                {
                    var it = catalog[i]; int q = qty[i];
                    if (inv != null)
                    {
                        inv.Add(it, q);                       // ร้านค้า: เก็บเข้ากระเป๋า (กดใช้ทีหลัง)
                    }
                    else
                    {
                        stats.ChangeEnergy(it.energy * q);    // โรงอาหาร: กินทันที
                        stats.ChangeHunger(it.hunger * q);
                        stats.ChangeHealth(it.health * q);
                        stats.ChangeKnowledge(it.knowledge * q);
                        stats.ChangeSatisfaction(it.satisfaction * q);
                    }
                }
            HUDController.Toast(inv != null ? $"ซื้อเข้ากระเป๋าแล้ว!  -{total}฿" : $"ซื้อสำเร็จ!  -{total}฿");
            GameplayEvents.Raise(inv != null ? GameplayEvents.Buy : GameplayEvents.Eat);
            if (inv == null) NisitSimulator.Core.SFXManager.Eat();   // โรงอาหาร = กินทันที → เสียงกิน
            ClearCart();
        }

        public void ClearCart()
        {
            if (qty != null) for (int i = 0; i < qty.Length; i++) qty[i] = 0;
            Refresh();
        }

        void OnMoney(int m) => Refresh();

        void RefreshMoney() { if (moneyText != null && stats != null) moneyText.text = $"เงิน: {stats.Money}฿"; }

        void Refresh()
        {
            int total = 0;
            for (int i = 0; i < rows.Count && i < catalog.Count; i++)
            {
                int q = qty != null ? qty[i] : 0;
                total += q * Perks.Price(catalog[i].price);
                if (rows[i].qtyBadge) rows[i].qtyBadge.SetActive(q > 0);
                if (rows[i].qtyText)  rows[i].qtyText.text = "x" + q;
            }
            if (totalText != null) totalText.text = $"รวม: {total}฿";
            if (confirmButton != null) confirmButton.interactable = total > 0 && stats != null && stats.Money >= total;
            if (clearButton != null)   clearButton.interactable = total > 0;
            RefreshMoney();
        }

        // สีไอคอนตามหมวด (หนังสือ/ยา/อาหาร/เครื่องดื่ม)
        Color IconColor(ShopItem it)
        {
            if (it.knowledge > 0) return new Color(0.45f, 0.75f, 0.45f);   // เขียว = หนังสือ
            if (it.health > 0)    return new Color(0.95f, 0.50f, 0.55f);   // ชมพู = ยา
            if (it.hunger > 0)    return new Color(1.00f, 0.68f, 0.32f);   // ส้ม = อาหาร
            if (it.energy > 0)    return new Color(0.42f, 0.70f, 1.00f);   // ฟ้า = เครื่องดื่ม
            return new Color(0.72f, 0.72f, 0.78f);
        }

        string EffectText(ShopItem it)
        {
            var p = new List<string>();
            if (it.energy != 0)       p.Add($"พลังงาน {it.energy:+0;-0}");
            if (it.hunger != 0)       p.Add($"อิ่ม {it.hunger:+0;-0}");
            if (it.health != 0)       p.Add($"สุขภาพ {it.health:+0;-0}");
            if (it.knowledge != 0)    p.Add($"ความรู้ {it.knowledge:+0;-0}");
            if (it.satisfaction != 0) p.Add($"พอใจ {it.satisfaction:+0;-0}");
            return string.Join("  ", p);
        }

        void FillDefaults() { catalog = DefaultCatalog(); }

        // แคตตาล็อกกลาง (ทั้ง runtime และ Build Shop ใช้ร่วมกัน) — modelName = โมเดล bake ไอคอน
        public static List<ShopItem> DefaultCatalog() => new List<ShopItem>
        {
            new ShopItem { name = "น้ำเปล่า",         price = 8,  energy = 5,  health = 2,        modelName = "jar_C_medium" },
            new ShopItem { name = "ขนมปัง",          price = 15, hunger = 25,                    modelName = "food_ingredient_bun" },
            new ShopItem { name = "กาแฟ",            price = 20, energy = 25, satisfaction = 2, modelName = "jar_D_medium" },
            new ShopItem { name = "เอเนอร์จี้ดริ๊ง",   price = 30, energy = 45, health = -3,       modelName = "jar_A_medium" },
            new ShopItem { name = "ข้าวกล่อง",        price = 35, hunger = 45, energy = 5,        modelName = "food_dinner" },
            new ShopItem { name = "ยาบำรุง",         price = 40, health = 25,                    modelName = "jar_B_small" },
            new ShopItem { name = "หนังสือติว",       price = 50, knowledge = 15,                 modelName = "book_set" },
        };

        // เมนูโรงอาหาร (อาหารจานจริง อิ่มเยอะ) — คนละชุดกับร้านค้า
        public static List<ShopItem> CafeteriaCatalog() => new List<ShopItem>
        {
            new ShopItem { name = "ข้าวราดแกง",  price = 30, hunger = 50, energy = 8,               modelName = "food_dinner" },
            new ShopItem { name = "ก๋วยเตี๋ยว",   price = 25, hunger = 40, energy = 6,               modelName = "food_stew" },
            new ShopItem { name = "ต้มยำ",       price = 35, hunger = 45, energy = 10, health = 3, modelName = "pot_A_stew" },
            new ShopItem { name = "เบอร์เกอร์",   price = 40, hunger = 55, energy = 5,               modelName = "food_burger" },
            new ShopItem { name = "สลัดผัก",     price = 28, hunger = 30, health = 8, satisfaction = 3, modelName = "food_vegetableburger" },
            new ShopItem { name = "น้ำผลไม้",    price = 15, energy = 12, health = 3,               modelName = "jar_A_medium" },
            new ShopItem { name = "ของหวาน",     price = 20, hunger = 15, satisfaction = 8,         modelName = "bowl" },
        };
    }
}
