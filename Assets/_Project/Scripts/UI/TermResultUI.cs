using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Academics;
using NisitSimulator.Systems;

namespace NisitSimulator.UI
{
    // ===== หน้าแสดงผลการเรียนตอนจบเทอม (สร้างตอนรัน · ฟอนต์ไทย + ธีมพาสเทลเดียวกับสรุปวัน) =====
    //   ข้อมูลมาจาก RegistrationService.BuildTermReport (ตรรกะล้วน) · CourseRegistrar ส่งเข้าคิวตอนปิดภาค/โหลดเซฟที่ยังไม่ได้ดู
    //   ลำดับ: ขึ้นวันใหม่ → สรุปวัน (DaySummaryUI) → หน้านี้ → (จบการศึกษา/พ้นสภาพ) หน้าจบเกม
    //   เปิดอยู่ = หยุดเวลา (timeScale 0) เหมือนหน้าต่างอื่น · กด "ตกลง" แล้วบันทึกว่าดูแล้ว (แสดงครั้งเดียวต่อเทอม)
    public class TermResultUI : MonoBehaviour
    {
        static TermResultUI _i;
        public static bool IsOpen => _i != null && _i.open;
        // มีหน้าที่รอแสดงหรือกำลังแสดง — ให้หน้าจบเกมรอ
        public static bool Busy => _i != null && (_i.open || _i.queue.Count > 0);
        public static TermReport Current => _i != null ? _i.current : null;

        readonly Queue<TermReport> queue = new Queue<TermReport>();
        TermReport current;
        bool open, waiting, pausedWorld;
        float prevTimeScale = 1f;

        GameObject root;
        TMP_Text title, subtitle, table, summary, status, advice;

        static readonly string[] SemNames = { "ภาคต้น", "ภาคปลาย", "ภาคฤดูร้อน" };

        public static TermResultUI EnsureExists()
        {
            if (_i == null) _i = new GameObject("TermResultUI").AddComponent<TermResultUI>();
            return _i;
        }

        public static void Queue(TermReport r)
        {
            if (r == null || r.Empty) return;
            var u = EnsureExists();
            foreach (var q in u.queue) if (q.serial == r.serial) return;   // กันเข้าคิวซ้ำ
            if (u.current != null && u.current.serial == r.serial && u.open) return;
            u.queue.Enqueue(r);
            if (!u.waiting) u.StartCoroutine(u.ShowWhenFree());
        }

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
        }

        void OnDestroy()
        {
            if (open && pausedWorld) Time.timeScale = prevTimeScale;
            if (_i == this) _i = null;
        }

        // รอให้หน้าต่างอื่นปิดก่อน (สรุปวัน/หน้าต่างที่หยุดเวลา/เหตุการณ์ในคาบ) ไม่ให้ซ้อนกัน
        IEnumerator ShowWhenFree()
        {
            waiting = true;
            yield return null; yield return null;   // สรุปวันขึ้นช้ากว่าการปิดภาค 1 เฟรม
            while (queue.Count > 0)
            {
                while (DaySummaryUI.IsShowing || ClassEventUI.IsOpen || NisitSimulator.Interaction.SleepController.IsSleeping
                       || (Time.timeScale == 0f && !open)) yield return null;
                if (!open) Show(queue.Dequeue());
                while (open) yield return null;
            }
            waiting = false;
        }

        void Show(TermReport r)
        {
            current = r;
            if (root == null) Build();
            Fill(r);
            open = true;
            root.SetActive(true);
            // เล่นคนเดียว = หยุดเวลา · multiplayer: ไม่หยุดเวลาโลก (เวลาเป็นของทุกคน)
            // TODO(multiplayer): แสดงแบบไม่บังเวลา/ให้ host รอทุกคนอ่านผลก่อนเปิดภาคถัดไป
            pausedWorld = ClassroomRules.IsSinglePlayer;
            if (pausedWorld)
            {
                prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            NisitSimulator.TimeSystem.GameClock.WarpMultiplier = 1f;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            NisitSimulator.Core.SFXManager.Whoosh();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            root.SetActive(false);
            if (pausedWorld) Time.timeScale = prevTimeScale;
            if (current != null) CourseRegistrar.Instance?.MarkReportSeen(current.serial);
        }

        // ปุ่ม "ดูผลการเรียนทั้งหมด" → ปิดหน้านี้ แล้วเปิดแท็บผลการเรียนในแอปลงทะเบียน
        public void OpenTranscript()
        {
            Close();
            var phone = Object.FindFirstObjectByType<PhoneController>(FindObjectsInactive.Include);
            if (phone != null && !phone.IsOpen) phone.Toggle();
            var reg = RegistrationUI.EnsureExists();
            reg.Open();
            reg.ShowTab(2);
        }

        void Update()
        {
            if (open && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))) Close();
        }

        // ---------- เนื้อหา ----------
        static string Pct(float v) => v < 0f ? "–" : Mathf.RoundToInt(v * 100f).ToString();
        static string GradeColor(string letter, bool passed)
        {
            if (!passed || letter == "F") return "#D14D5C";
            if (letter.StartsWith("A") || letter.StartsWith("B")) return "#389966";
            return "#E0843A";
        }

        public static string TableText(TermReport r)
        {
            var sb = new StringBuilder();
            sb.Append("<b><color=#7A7394>วิชา<pos=47%>นก.<pos=53%>เข้าเรียน<pos=63%>กลาง<pos=70%>ปลาย<pos=77%>พิเศษ<pos=84%>รวม<pos=92%>เกรด</color></b>\n");
            foreach (var row in r.rows)
            {
                string name = row.title.Length > 26 ? row.title.Substring(0, 25) + "…" : row.title;
                string mid = row.missedMidterm ? "<color=#D14D5C>ขาด</color>" : Pct(row.midterm);
                string fin = row.missedFinal ? "<color=#D14D5C>ขาด</color>" : Pct(row.final);
                float bonus = (row.bonusMid + row.bonusFinal) * 100f;
                string bon = bonus > 0.05f ? $"<color=#389966>+{bonus:0}</color>" : "–";
                string col = GradeColor(row.letter ?? "", row.passed);
                string tag = row.retakeSection ? " <size=75%><color=#E0843A>(ภาคค่ำ)</color></size>" : row.attempt > 1 ? $" <size=75%><color=#E0843A>(ครั้งที่ {row.attempt})</color></size>" : "";
                sb.Append($"<b>{row.code}</b> {name}{tag}<pos=47%>{row.credits}<pos=53%>{Pct(row.attendance)}%<pos=63%>{mid}<pos=70%>{fin}<pos=77%>{bon}<pos=84%>{row.score:0}<pos=92%><b><color={col}>{row.letter}</color></b>");
                if (!row.passed) sb.Append(" <size=80%><color=#D14D5C>ตก</color></size>");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static string StatusText(TermReport r)
        {
            if (r.graduated) return "<color=#389966><b>ครบเงื่อนไขจบการศึกษา! ยินดีด้วย</b></color>";
            if (r.exhausted) return "<color=#D14D5C><b>เรียนครบจำนวนภาคสูงสุดแล้วยังไม่จบ — พ้นสภาพนิสิต</b></color>";
            var parts = new List<string>();
            if (r.promoted) parts.Add($"<color=#389966><b>เลื่อนเป็นชั้นปี {r.newClassYear}!</b></color>");
            else if (r.promotionShortfall > 0) parts.Add($"<color=#E0843A><b>ยังไม่เลื่อนชั้นปี</b> — ขาดอีก {r.promotionShortfall} หน่วยกิต</color>");
            if (r.probation) parts.Add($"<color=#D14D5C><b>ติดโปร</b> — GPA สะสม {r.cumulativeGpa:0.00} ต่ำกว่า {r.minGpa:0.00}</color>");
            if (r.isExtra) parts.Add("<color=#7A7394>ภาคเรียนเพิ่มเติม</color>");
            if (parts.Count == 0) parts.Add("<color=#389966>ผ่านภาคนี้เรียบร้อย</color>");
            return string.Join("   ·   ", parts);
        }

        void Fill(TermReport r)
        {
            string sem = SemNames[Mathf.Clamp(r.semIndex, 0, 2)];
            title.text = $"ผลการเรียน {sem}";
            subtitle.text = $"ปีการศึกษาที่ {Mathf.Max(1, r.calendarYear)} · ชั้นปี {Mathf.Max(1, r.classYear)}";
            table.text = TableText(r);
            int pass = 0; foreach (var row in r.rows) if (row.passed) pass++;
            summary.text = $"ผ่าน <b>{pass}/{r.rows.Count}</b> วิชา   ·   GPA ภาค <b>{r.termGpa:0.00}</b>   ·   GPA สะสม <b>{(r.hasGpa ? r.cumulativeGpa.ToString("0.00") : "–")}</b>\n" +
                           $"หน่วยกิตภาคนี้ <b>{r.creditsEarnedTerm}/{r.creditsAttempted}</b>   ·   สะสม <b>{r.creditsEarnedTotal}</b>/{r.graduationCredits} (ที่ต้องใช้จบ)";
            status.text = StatusText(r);
            advice.text = r.nextStep;
        }

        void Build()
        {
            var c = GrowthUI.MakeCanvas(transform, "Term Result Canvas", 95);
            root = c.gameObject;
            GrowthUI.Dim(c.transform);
            var card = GrowthUI.Box(c.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 880f), GrowthUI.CardCol);
            GrowthUI.Box(card.transform, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(640f, 84f), GrowthUI.Strip, false);
            title = GrowthUI.Text(card.transform, "", new Vector2(0f, 380f), new Vector2(1200f, 60f), 40, GrowthUI.Title);
            subtitle = GrowthUI.Text(card.transform, "", new Vector2(0f, 322f), new Vector2(1200f, 40f), 24, GrowthUI.Soft);

            var tbox = GrowthUI.Box(card.transform, "Table", new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1240f, 410f), new Color(1f, 1f, 1f, 0.75f), false);
            table = GrowthUI.Text(tbox.transform, "", Vector2.zero, new Vector2(1200f, 390f), 22, GrowthUI.Ink, TextAlignmentOptions.TopLeft);
            table.lineSpacing = 8f;
            table.textWrappingMode = TextWrappingModes.NoWrap;
            table.overflowMode = TextOverflowModes.Ellipsis;

            summary = GrowthUI.Text(card.transform, "", new Vector2(0f, -170f), new Vector2(1240f, 80f), 24, GrowthUI.Ink);
            status = GrowthUI.Text(card.transform, "", new Vector2(0f, -232f), new Vector2(1240f, 40f), 24, GrowthUI.Ink);
            advice = GrowthUI.Text(card.transform, "", new Vector2(0f, -280f), new Vector2(1240f, 50f), 20, GrowthUI.Soft);
            advice.textWrappingMode = TextWrappingModes.Normal;

            var all = GrowthUI.Button(card.transform, "ดูผลการเรียนทั้งหมด", new Vector2(-170f, -370f), new Vector2(300f, 60f), new Color(0.82f, 0.78f, 0.94f), 24);
            all.onClick.AddListener(OpenTranscript);
            var ok = GrowthUI.Button(card.transform, "ตกลง", new Vector2(170f, -370f), new Vector2(300f, 60f), new Color(0.60f, 0.86f, 0.68f), 28);
            ok.onClick.AddListener(Close);
            root.SetActive(false);
        }
    }
}
