using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.SaveLoad;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // เลเวลนิสิต — EXP สะสม → เลเวล 1–30 · เลเวลอัปแต่ละครั้งได้เลือกความสามารถ 1 จาก 3 (Perks)
    //   ป้ายเลเวล + แถบ EXP มุมขวาบน · กด L (หรือคลิกป้าย) เปิดหน้าเลือกถ้ามีแต้มค้าง
    //   สร้างอัตโนมัติโดย GameplayBootstrap (EnsureExists) หลังโหลดเซฟแล้ว
    public class LevelSystem : MonoBehaviour
    {
        static LevelSystem _i;
        public static LevelSystem Instance => _i;
        public static void EnsureExists() { if (_i == null) new GameObject("LevelSystem").AddComponent<LevelSystem>(); }

        public const int MaxLevel = 30;
        public static int XpToNext(int lvl) => 80 + 40 * (lvl - 1);                 // Lv1→2 = 80, Lv2→3 = 120, ...
        public static int XpAtLevel(int lvl) { int s = 0; for (int l = 1; l < lvl; l++) s += XpToNext(l); return s; }
        public static int LevelFromExp(int exp) { int l = 1; while (l < MaxLevel && exp >= XpAtLevel(l + 1)) l++; return l; }

        public int Level { get; private set; } = 1;
        public int PendingPicks => Mathf.Max(0, (Level - 1) - picksMade);
        public bool PickerOpen => pickerOpen;

        int picksMade;
        PlayerStats stats;
        PlayerMovement move;

        // UI
        GameObject pickerRoot;
        TMP_Text pickerTitle, pickerSub;
        readonly Button[] cardBtn = new Button[3];
        readonly TMP_Text[] cardName = new TMP_Text[3], cardDesc = new TMP_Text[3], cardRank = new TMP_Text[3];
        readonly string[] offer = new string[3];
        TMP_Text badgeText;
        RectTransform badgeFill;
        Image badgeBg;
        bool pickerOpen, wantOpen;
        float prevTimeScale = 1f;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
        }

        void OnDestroy()
        {
            if (stats != null) stats.OnExpChanged -= OnExp;
            if (pickerOpen) Time.timeScale = prevTimeScale;
            if (_i == this) _i = null;
        }

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            var p = GameObject.Find("Player");
            if (p != null) move = p.GetComponent<PlayerMovement>();

            // เกมใหม่ = เริ่มจากศูนย์ · เล่นต่อ = คืนความสามารถจากเซฟ
            Perks.Reset(); picksMade = 0;
            if (GameSession.IsContinue)
            {
                var d = SaveSystem.Load();
                if (d != null) { Perks.Load(d.perks); picksMade = d.perkPicks; }
            }
            Level = stats != null ? LevelFromExp(stats.Exp) : 1;
            if (!GameSession.IsContinue) picksMade = Level - 1;
            StatsTracker.Instance.Set("level", Level);

            Build();
            if (stats != null) stats.OnExpChanged += OnExp;
            UpdateBadge();
            if (PendingPicks > 0) HUDController.Toast($"มีแต้มความสามารถ {PendingPicks} แต้ม — กด L เพื่อเลือก");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                if (pickerOpen) ClosePicker();
                else if (PendingPicks > 0) wantOpen = true;
                else HUDController.Toast("ยังไม่มีแต้มความสามารถ — เก็บ EXP ให้เลเวลอัปก่อน");
            }
            if (wantOpen && !pickerOpen && CanOpenNow()) { wantOpen = false; OpenPicker(); }
        }

        bool CanOpenNow()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return false;
            if (DaySummaryUI.IsShowing) return false;
            return true;
        }

        void OnExp(int exp)
        {
            int lvl = LevelFromExp(exp);
            if (lvl > Level)
            {
                Level = lvl;
                StatsTracker.Instance.Set("level", Level);
                HUDController.Toast($"เลเวลอัป! ตอนนี้ Lv.{Level} — เลือกความสามารถใหม่ได้");
                SFXManager.Success();
                wantOpen = true;
            }
            UpdateBadge();
        }

        // ---------- เลือกความสามารถ ----------
        void OpenPicker()
        {
            if (PendingPicks <= 0) return;
            RollOffer();
            pickerOpen = true;
            pickerRoot.SetActive(true);
            prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            SFXManager.Whoosh();
        }

        void ClosePicker()
        {
            pickerOpen = false;
            pickerRoot.SetActive(false);
            Time.timeScale = prevTimeScale;
            if (move != null) move.enabled = true;
            UpdateBadge();
        }

        void RollOffer()
        {
            var pool = new List<Perks.Def>();
            foreach (var d in Perks.All) if (!Perks.IsMaxed(d)) pool.Add(d);
            for (int i = 0; i < 3; i++)
            {
                if (pool.Count == 0) { offer[i] = null; continue; }
                int r = Random.Range(0, pool.Count);
                offer[i] = pool[r].id; pool.RemoveAt(r);
            }
            pickerTitle.text = $"เลเวลอัป! Lv.{Level}";
            pickerSub.text = PendingPicks > 1 ? $"เลือกความสามารถ 1 อย่าง  (เหลือ {PendingPicks} แต้ม)" : "เลือกความสามารถ 1 อย่าง";
            for (int i = 0; i < 3; i++)
            {
                var d = offer[i] != null ? Perks.Find(offer[i]) : null;
                cardBtn[i].gameObject.SetActive(d != null);
                if (d == null) continue;
                int r = Perks.Rank(d.id);
                cardName[i].text = d.name;
                cardDesc[i].text = d.desc;
                cardRank[i].text = Stars(r + 1, d.maxRank) + $"\n<size=80%>ระดับ {r + 1}/{d.maxRank}</size>";
            }
            // ทุกอย่างเต็มแล้ว → ปิดแต้มค้างทิ้ง
            if (offer[0] == null) { picksMade = Level - 1; ClosePicker(); HUDController.Toast("อัปความสามารถครบทุกอย่างแล้ว!"); }
        }

        static string Stars(int filled, int max)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < max; i++) sb.Append(i < filled ? "<color=#F2B640>◆</color>" : "<color=#C9C2DD>◆</color>");
            return sb.ToString();
        }

        void Pick(int i)
        {
            var d = offer[i] != null ? Perks.Find(offer[i]) : null;
            if (d == null) return;
            Perks.Add(d.id);
            picksMade++;
            HUDController.Toast($"ได้ความสามารถ: {d.name} ระดับ {Perks.Rank(d.id)} — {d.desc}");
            SFXManager.Success();
            if (PendingPicks > 0) RollOffer();
            else ClosePicker();
        }

        // ---------- เซฟ ----------
        public void CollectSave(SaveData d)
        {
            d.perks = Perks.Serialize();
            d.perkPicks = picksMade;
        }

        // ---------- ข้อความให้ระบบอื่น ----------
        public string ProgressText()
        {
            if (Level >= MaxLevel) return $"Lv.{Level} (สูงสุด)";
            int exp = stats != null ? stats.Exp : 0;
            int cur = exp - XpAtLevel(Level), need = XpToNext(Level);
            return $"Lv.{Level}  ({cur}/{need} EXP)";
        }

        public string PerkListText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var d in Perks.All)
            {
                int r = Perks.Rank(d.id);
                if (r > 0) sb.AppendLine($"{d.name} {Stars(r, d.maxRank)}  <size=80%><color=#8A93AB>{d.desc}</color></size>");
            }
            return sb.Length > 0 ? sb.ToString() : "<color=#8A93AB>ยังไม่มี — เลเวลอัปแล้วกด L เพื่อเลือก</color>";
        }

        // ---------- UI ----------
        void UpdateBadge()
        {
            if (badgeText == null) return;
            int exp = stats != null ? stats.Exp : 0;
            float t = Level >= MaxLevel ? 1f : Mathf.Clamp01((exp - XpAtLevel(Level)) / (float)XpToNext(Level));
            badgeFill.anchorMax = new Vector2(t, 1f);
            if (PendingPicks > 0)
            {
                badgeText.text = $"<b>Lv.{Level}</b>  เลือกความสามารถ! (L)";
                badgeBg.color = new Color(1f, 0.90f, 0.62f, 0.98f);
            }
            else
            {
                badgeText.text = Level >= MaxLevel ? $"<b>Lv.{Level}</b>  สูงสุด" : $"<b>Lv.{Level}</b>  {exp - XpAtLevel(Level)}/{XpToNext(Level)} EXP";
                badgeBg.color = GrowthUI.CardCol;
            }
        }

        void Build()
        {
            var canvas = GrowthUI.MakeCanvas(transform, "Level Canvas", 60);

            // ป้ายเลเวลมุมขวาบน (ใต้การ์ดวัน/เงิน/ปี)
            badgeBg = GrowthUI.Box(canvas.transform, "LevelBadge", new Vector2(1f, 1f), new Vector2(-19f, -318f), new Vector2(300f, 50f), GrowthUI.CardCol);
            var bar = GrowthUI.Box(badgeBg.transform, "Bar", new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(270f, 8f), new Color(0.84f, 0.80f, 0.93f), false);
            var fill = GrowthUI.Box(bar.transform, "Fill", new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.56f, 0.44f, 0.86f), false);
            badgeFill = fill.rectTransform;
            badgeFill.anchorMin = Vector2.zero; badgeFill.anchorMax = new Vector2(0f, 1f); badgeFill.offsetMin = badgeFill.offsetMax = Vector2.zero;
            badgeText = GrowthUI.Text(badgeBg.transform, "", new Vector2(0f, 5f), new Vector2(280f, 36f), 21, GrowthUI.Ink);
            var badgeButton = badgeBg.gameObject.AddComponent<Button>();
            badgeButton.onClick.AddListener(() => { if (PendingPicks > 0) wantOpen = true; });

            // หน้าเลือกความสามารถ
            var pc = GrowthUI.MakeCanvas(transform, "Perk Canvas", 95);
            pickerRoot = pc.gameObject;
            GrowthUI.Dim(pc.transform);
            var card = GrowthUI.Box(pc.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 560f), GrowthUI.CardCol);
            GrowthUI.Box(card.transform, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(620f, 84f), GrowthUI.Strip, false);
            pickerTitle = GrowthUI.Text(card.transform, "เลเวลอัป!", new Vector2(0f, 222f), new Vector2(900f, 60f), 44, GrowthUI.Title);
            pickerSub = GrowthUI.Text(card.transform, "", new Vector2(0f, 160f), new Vector2(900f, 40f), 24, GrowthUI.Soft);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = GrowthUI.Button(card.transform, "", new Vector2((i - 1) * 330f, -40f), new Vector2(300f, 300f), Color.white, 20);
                cardBtn[i] = b;
                cardName[i] = GrowthUI.Text(b.transform, "", new Vector2(0f, 96f), new Vector2(270f, 50f), 32, GrowthUI.Title);
                cardDesc[i] = GrowthUI.Text(b.transform, "", new Vector2(0f, 10f), new Vector2(260f, 90f), 22, GrowthUI.Ink);
                cardDesc[i].enableWordWrapping = true;
                cardRank[i] = GrowthUI.Text(b.transform, "", new Vector2(0f, -96f), new Vector2(260f, 70f), 30, GrowthUI.Soft);
                b.onClick.AddListener(() => Pick(idx));
            }
            GrowthUI.Text(card.transform, "เลือกทีหลังได้: กด L", new Vector2(0f, -240f), new Vector2(600f, 30f), 18, GrowthUI.Soft);
            var later = GrowthUI.Button(card.transform, "ไว้ทีหลัง", new Vector2(390f, -236f), new Vector2(170f, 46f), new Color(0.86f, 0.80f, 0.88f), 20);
            later.onClick.AddListener(ClosePicker);
            pickerRoot.SetActive(false);
        }
    }
}
