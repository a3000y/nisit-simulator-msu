using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Academics;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.UI
{
    // 📱 แอป "MSG REG" (ระบบลงทะเบียนเรียน + ผลการเรียน/เกรด) ในโทรศัพท์ — สร้าง UI เองตอนรัน (แบบเดียวกับ AchievementsUI) ไม่ต้อง bake ฉาก
    //   • เพิ่มปุ่มแอปที่หน้าโฮมโทรศัพท์อัตโนมัติ (โคลนปุ่มเดิม → ได้สไตล์/เอฟเฟกต์เดียวกัน)
    //   • 3 แท็บ: ลงทะเบียน / ตารางเรียน / ผลการเรียน · ฟอนต์ไทยเดียวกับโทรศัพท์
    //   GameplayBootstrap เรียก EnsureExists() ตอนเข้าเกม
    public class RegistrationUI : MonoBehaviour
    {
        static RegistrationUI _i;
        public static bool IsOpen => _i != null && _i.open;
        public static RegistrationUI Instance => _i;
        public int CurrentTab => tab;
        public static RegistrationUI EnsureExists()
        {
            if (_i == null) _i = new GameObject("RegistrationUI").AddComponent<RegistrationUI>();
            return _i;
        }

        // สีพาสเทลให้เข้ากับ PastelTheme ของเกม
        static readonly Color CardCol = new Color(0.955f, 0.93f, 0.985f, 1f);
        static readonly Color Ink = new Color(0.30f, 0.25f, 0.46f, 1f);
        static readonly Color Sub = new Color(0.44f, 0.41f, 0.57f, 1f);
        static readonly Color Accent = new Color(0.55f, 0.42f, 0.85f, 1f);
        static readonly Color Ok = new Color(0.22f, 0.60f, 0.40f, 1f);
        static readonly Color Bad = new Color(0.82f, 0.30f, 0.36f, 1f);
        static readonly Color Warn = new Color(0.88f, 0.52f, 0.16f, 1f);
        static readonly Color Muted = new Color(0.60f, 0.60f, 0.68f, 1f);
        static readonly Color RowBg = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color PanelBg = new Color(0.90f, 0.87f, 0.96f, 1f);

        public const string PhoneButtonName = "ลงทะเบียนApp";   // ชื่อ GameObject ที่สร้างตอนรัน (คงเดิม)
        public const string AppName = "MSG REG";                  // ชื่อแอปที่ผู้เล่นเห็น

        TMP_FontAsset font;
        PhoneController phone;
        CourseRegistrar reg;
        GameClock clock;
        bool built, open, subscribed;
        int tab;
        float refreshTimer;
        bool dirty;

        GameObject root, regPage, textPage;
        CanvasScaler scaler;
        TMP_Text summaryText, programText, messageText, creditText, pageText, selTitle;
        RectTransform listContent, selContent, guideCard;
        ScrollRect listScroll, textScroll;
        Button confirmBtn; TMP_Text confirmLabel;
        Button navBtn;
        readonly List<Button> tabBtns = new List<Button>();
        string lastMessage; Color lastMessageColor;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
        }

        void Start()
        {
            phone = Object.FindFirstObjectByType<PhoneController>(FindObjectsInactive.Include);
            clock = Object.FindFirstObjectByType<GameClock>();
            font = phone != null && phone.appBody != null ? phone.appBody.font : null;
            if (font == null) { var any = Object.FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include); if (any != null) font = any.font; }
            InjectPhoneButton();
            Subscribe();
        }

        void OnDestroy()
        {
            if (reg != null) reg.OnChanged -= MarkDirty;
            if (_i == this) _i = null;
        }

        void Subscribe()
        {
            if (subscribed) return;
            reg = CourseRegistrar.Instance;
            if (reg != null) { reg.OnChanged += MarkDirty; subscribed = true; }
        }

        void MarkDirty() { dirty = true; }

        // ---------- ปุ่มแอปในหน้าโฮมโทรศัพท์ ----------
        void InjectPhoneButton()
        {
            if (phone == null || phone.homeView == null) return;
            if (phone.homeView.transform.Find(PhoneButtonName) != null) return;
            Button template = null;
            if (phone.appButtons != null)
                for (int i = phone.appButtons.Length - 1; i >= 0 && template == null; i--) template = phone.appButtons[i];
            if (template == null) return;

            // จำตำแหน่งช่องเดิม 6 ช่อง (2 คอลัมน์ × 3 แถว) ก่อนจัดใหม่
            var slots = new List<Vector2>();
            foreach (var b in phone.appButtons) if (b != null) slots.Add(((RectTransform)b.transform).anchoredPosition);

            var go = Instantiate(template.gameObject, template.transform.parent);
            go.name = PhoneButtonName;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(0f, -256f);   // สำรอง: ปุ่มเดิมไม่ครบ 6 ช่อง → แถวที่ 4 กึ่งกลาง

            // แอป "เกรด" รวมเข้า MSG REG แล้ว → ซ่อนปุ่ม (ไม่ลบ) แล้วเลื่อนแผนที่/เพื่อนขึ้น ให้ MSG REG เข้าช่องสุดท้ายพอดี 2×3
            //   สถานะ | ปฏิทิน / ภารกิจ | แผนที่ / เพื่อน | MSG REG
            int gi = (int)PhoneController.App.Grades, mi = (int)PhoneController.App.Map, fi = (int)PhoneController.App.Friends;
            if (slots.Count == 6 && phone.appButtons.Length == 6 && phone.appButtons[gi] != null)
            {
                phone.appButtons[gi].gameObject.SetActive(false);
                if (phone.appButtons[mi] != null) ((RectTransform)phone.appButtons[mi].transform).anchoredPosition = slots[3];
                if (phone.appButtons[fi] != null) ((RectTransform)phone.appButtons[fi].transform).anchoredPosition = slots[4];
                rt.anchoredPosition = slots[5];
            }
            // ชื่อแอปทุกปุ่ม: บรรทัดเดียว ย่อเองถ้ายาว (ไม่ตัดบรรทัดจนตัวอักษรหล่นใต้ปุ่ม)
            foreach (var b in phone.appButtons)
            {
                if (b == null) continue;
                var l = b.transform.Find("Label");
                if (l != null && l.TryGetComponent<TMP_Text>(out var t)) UIFit.OneLine(t, Mathf.Max(18f, t.fontSize), 18f);
            }
            var img = go.GetComponent<Image>();
            if (img != null) img.color = new Color(0.98f, 0.46f, 0.40f);
            var label = go.transform.Find("Label");
            if (label != null && label.TryGetComponent<TMP_Text>(out var lt)) { lt.text = AppName; UIFit.OneLine(lt, Mathf.Max(18f, lt.fontSize), 18f); }
            // ไอคอน: ยืมจากแอปภารกิจ (เช็กถูก) ถ้ามี
            var icon = go.transform.Find("Icon");
            if (icon != null && phone.appButtons.Length > 2 && phone.appButtons[2] != null)
            {
                var srcIcon = phone.appButtons[2].transform.Find("Icon");
                if (srcIcon != null && srcIcon.TryGetComponent<Image>(out var si) && icon.TryGetComponent<Image>(out var di)) di.sprite = si.sprite;
            }
            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(Open);
        }

        // ---------- เปิด/ปิด ----------
        public void Open()
        {
            Subscribe();
            if (!built) Build();
            open = true;
            root.SetActive(true);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            lastMessage = null;
            NisitSimulator.Core.SFXManager.Page();
            ShowTab(tab);
        }

        public void Close()
        {
            open = false;
            if (root != null) root.SetActive(false);
        }

// จอกว้างกว่า 16:9 → ยึดความสูง · แคบกว่า → ยึดความกว้าง (การ์ดไม่ล้นจอทุกสัดส่วน)
        void FitToScreen()
        {
            if (scaler == null || Screen.height <= 0) return;
            scaler.matchWidthOrHeight = Screen.width / (float)Screen.height > 1920f / 1080f ? 1f : 0f;
        }


        void Update()
        {
            if (!open) return;
            FitToScreen();
            if (guideCard != null)
            {
                bool guided = ArrivalIntroController.Active;
                guideCard.sizeDelta = new Vector2(1600, guided ? 770 : 940);
                guideCard.anchoredPosition = new Vector2(0, guided ? 135 : 0);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (phone != null && !phone.IsOpen) { Close(); return; }   // กด TAB ปิดโทรศัพท์ = ปิดแอปด้วย
            refreshTimer -= Time.unscaledDeltaTime;
            if (dirty || refreshTimer <= 0f)
            {
                refreshTimer = 1f;
                bool full = dirty; dirty = false;
                RefreshSummary();
                if (tab == 0) { if (full) RefreshRegister(); }
                else RefreshTextPage();
            }
        }

        public void ShowTab(int t)
        {
            tab = t;
            for (int i = 0; i < tabBtns.Count; i++)
            {
                var img = tabBtns[i].GetComponent<Image>();
                if (img != null) img.color = i == t ? Accent : new Color(0.82f, 0.78f, 0.92f);
                var lbl = tabBtns[i].GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.color = i == t ? Color.white : Ink;
            }
            regPage.SetActive(t == 0);
            textPage.SetActive(t != 0);
            if (navBtn != null) navBtn.gameObject.SetActive(t == 1 && Usable && ClassroomRules.IsSinglePlayer);
            RefreshSummary();
            if (t == 0) RefreshRegister(); else { RefreshTextPage(); textScroll.verticalNormalizedPosition = 1f; }
        }

        RegistrationService Svc => reg != null ? reg.Service : null;
        bool Usable => reg != null && reg.IsActive && Svc != null;

        // ---------- ส่วนหัว ----------
        void RefreshSummary()
        {
            if (summaryText == null) return;
            if (!Usable)
            {
                programText.text = "ระบบลงทะเบียนเรียน";
                summaryText.text = $"คณะ{FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex)}";
                return;
            }
            var svc = Svc; var t = svc.Record.Current;
            programText.text = svc.Curriculum.programName;
            string term = t == null ? "-" : t.isBreak ? "ปิดภาคฤดูร้อน" : t.isExtra ? "ภาคเรียนเพิ่มเติม" : $"ภาคเรียนที่ {t.planSemester}";
            string time = clock != null ? clock.GetTimeString() : "";
            string status;
            if (t == null || t.isBreak) status = "ปิดภาค — ยังไม่เปิดลงทะเบียน";
            else if (t.confirmed) status = svc.RegistrationWindowOpen(reg.SemDay)
                ? (svc.HasRegistrationChanges ? "<color=#E0843A>มีการแก้ไข — รอยืนยัน</color>" : "<color=#389966>ลงทะเบียนแล้ว (เพิ่ม/ถอนได้)</color>")
                : "<color=#389966>ลงทะเบียนแล้ว (หมดช่วงแก้ไข)</color>";
            else if (svc.RegistrationWindowOpen(reg.SemDay))
                status = t.lateRegistration ? "<color=#E0843A>เปิดลงทะเบียน (ย้ายเซฟเก่า: ได้ถึงสิ้นภาค)</color>"
                                            : $"<color=#E0843A>เปิดลงทะเบียนถึงสิ้นวันอาทิตย์แรกของภาค</color>";
            else status = "<color=#D14D5C>หมดช่วงลงทะเบียน</color>";
            string gpa = svc.HasGpa ? svc.Gpa().ToString("0.00") : "–";
            summaryText.text =
                $"ชั้นปี <b>{reg.ClassYear}</b>  ·  {term}  ·  ปีการศึกษาที่ {(t != null ? t.calendarYear : 1)}  ·  วันนี้: {AcademicCalendar.TermDayText(reg.SemDay)} {time}\n" +
                $"GPA <b>{gpa}</b>  ·  หน่วยกิตสะสม <b>{svc.EarnedCredits()}</b>/{svc.Curriculum.GraduationCredits}  ·  {status}";
        }

        // ---------- แท็บลงทะเบียน ----------
        void RefreshRegister()
        {
            if (!built) return;
            Clear(listContent); Clear(selContent);
            if (!Usable)
            {
                AddNote(listContent, $"หลักสูตรลงทะเบียนนี้ใช้กับคณะสายคอมพิวเตอร์ ({FacultyCatalog.NameOf(0)}) เท่านั้น\nคณะของคุณใช้ระบบการเรียนเดิม (เข้าเรียน/สอบตามปฏิทิน) — ไม่ต้องลงทะเบียน", Sub);
                creditText.text = ""; selTitle.text = "";
                SetMessage(null);
                SetConfirm(false, "ไม่ต้องลงทะเบียน");
                return;
            }
            var svc = Svc; var t = svc.Record.Current;
            var offered = svc.Offered();
            bool hasGroup = false; OfferGroup lastGroup = OfferGroup.Plan;
            foreach (var oc in offered)
            {
                if (!hasGroup || lastGroup != oc.group) { AddHeader(listContent, GroupTitle(oc.group)); lastGroup = oc.group; hasGroup = true; }
                BuildCourseRow(oc);
            }
            string explain = svc.ExplainNoOptions(reg.SemDay);
            if (offered.Count == 0 && explain == null) explain = "ไม่มีรายวิชาเปิดในภาคนี้";
            if (explain != null && offered.Count == 0) AddNote(listContent, explain, Sub);

            // รายการที่เลือก
            bool confirmed = t != null && t.confirmed;
            bool windowOpen = svc.RegistrationWindowOpen(reg.SemDay);
            bool changed = svc.HasRegistrationChanges;
            selTitle.text = changed ? "รายการรอยืนยัน" : confirmed ? "ลงทะเบียนแล้ว" : "รายการที่เลือก";
            var codes = new List<string>();
            if (confirmed && !windowOpen) foreach (var e in svc.CurrentEnrollments()) codes.Add(e.code);
            else if (t != null) codes.AddRange(t.selected);
            foreach (var code in codes) BuildSelectedRow(code, windowOpen);
            if (codes.Count == 0) AddNote(selContent, "ยังไม่มีรายวิชา — กด \"+ เพิ่ม\" ที่รายวิชาด้านซ้าย", Sub);

            int sel = CreditsOf(codes);
            creditText.text = $"รวม <b>{sel}</b> / {svc.CreditCap} หน่วยกิต";
            creditText.color = sel > svc.CreditCap ? Bad : Ink;

            if (lastMessage != null) SetMessage(lastMessage, lastMessageColor);
            else if (explain != null) SetMessage(explain, Warn);
            else SetMessage(changed ? "กดยืนยันการแก้ไขเพื่อใช้รายการใหม่ ตารางเรียนยังใช้รายการที่ยืนยันล่าสุด"
                : confirmed && windowOpen ? "เพิ่มหรือถอนวิชาได้ แล้วกดยืนยันการแก้ไขให้ทันก่อนปิดลงทะเบียน"
                : confirmed ? "ลงทะเบียนเรียบร้อย — ดูแท็บ \"ตารางเรียน\" แล้วไปนั่งเรียนที่ตึกตามเวลา"
                : "เลือกรายวิชาแล้วกดยืนยัน แก้ไขได้จนถึงสิ้นวันอาทิตย์แรกของภาค", Sub);

            bool canConfirm = t != null && windowOpen && (confirmed ? changed : t.selected.Count > 0);
            string label = t == null || t.isBreak ? "ปิดภาค" : !windowOpen ? "หมดช่วงลงทะเบียน"
                : changed ? "ยืนยันการแก้ไข" : confirmed ? "ยืนยันแล้ว" : "ยืนยันลงทะเบียน";
            SetConfirm(canConfirm, label);
        }

        // ความกว้างพื้นที่รายวิชา (ก่อน layout คำนวณเสร็จ ใช้ค่าตามแบบ 1000)
        float listContentWidth => listContent != null && listContent.rect.width > 10f ? listContent.rect.width : 1000f;

        int CreditsOf(List<string> codes)
        {
            int s = 0; foreach (var c in codes) { var d = Svc.Curriculum.Get(c); if (d != null) s += d.credits; } return s;
        }

        static string GroupTitle(OfferGroup g)
        {
            switch (g)
            {
                case OfferGroup.Plan: return "วิชาตามแผนของภาคนี้";
                case OfferGroup.Outstanding: return "วิชาค้าง / ลงเรียนซ้ำ";
                default: return "วิชาเลือกเฉพาะทาง (เลือกผ่าน 2 วิชาไม่ซ้ำกัน)";
            }
        }

        void BuildCourseRow(OfferedCourse oc)
        {
            var svc = Svc;
            var st = svc.StatusOf(oc, out var reason);
            var d = oc.def;
            var t = svc.Record.Current;
            bool confirmed = t != null && t.confirmed;

            var row = Panel(listContent, "Row_" + d.code, RowBg, 118f);
            var strip = Panel(row.transform, "Strip", oc.group == OfferGroup.Plan ? Accent : oc.group == OfferGroup.Outstanding ? Warn : Ok, 0f, false);
            Stretch((RectTransform)strip.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(6, 8), new Vector2(14, -8));

            var title = Text(row.transform, $"<b>{d.code}</b>  {d.title}", 21, Ink, TextAlignmentOptions.TopLeft);
            // ฟอนต์ไทย (Mitr) บรรทัดสูง — กล่องต้องสูงพอ ไม่งั้น Ellipsis ซ่อนทั้งบรรทัด
            Stretch(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -48), new Vector2(-250, -4));
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;

            var sb = new StringBuilder();
            sb.Append(d.credits).Append(" หน่วยกิต");
            int att = svc.Attempts(d.code).Count;
            if (att > 0 && st != CourseStatus.Passed && !(st == CourseStatus.Selected && confirmed)) sb.Append($" · เคยลง {att} ครั้ง");
            if (oc.retakeSection) sb.Append(" · ตอนเรียนซ้ำ (ภาคค่ำ)");
            sb.Append('\n');
            var parts = new List<string>();
            foreach (var s in oc.Sessions) parts.Add($"{AcademicCalendar.ShortTermDayText(s.day)} {s.TimeText} {(s.HasRoom ? s.roomId : s.building)}");
            // คาบละช่อง 2 คาบต่อบรรทัด (4 คาบ = 2 บรรทัดพอดี ไม่ตัดกลางคาบ)
            for (int i = 0; i < parts.Count; i++) sb.Append(parts[i]).Append(i == parts.Count - 1 ? "\n" : i % 2 == 1 ? "\n" : "   |   ");
            sb.Append("วิชาบังคับก่อน: ").Append(d.prerequisites.Count > 0 ? string.Join(", ", d.prerequisites) : "—");
            if (d.minEarnedCredits > 0) sb.Append($" + หน่วยกิตสะสม ≥ {d.minEarnedCredits}");
            var info = Text(row.transform, sb.ToString(), 18, Sub, TextAlignmentOptions.TopLeft);
            Stretch(info.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 8), new Vector2(-250, -46));
            info.lineSpacing = 2f;
            // แถวสูงตามข้อความจริง (เดิมสูงคงที่ 118 → ตารางคาบ 4 ครั้งถูกตัด/ทับปุ่ม)
            float infoW = Mathf.Max(300f, listContentWidth - 24f - 250f);
            float infoH = info.GetPreferredValues(info.text, infoW, 0f).y;
            var le = row.GetComponent<LayoutElement>();
            float rowH = Mathf.Max(118f, 46f + infoH + 12f);
            le.minHeight = rowH; le.preferredHeight = rowH;

            // ป้ายสถานะ
            string stText; Color stCol;
            switch (st)
            {
                case CourseStatus.Passed: stText = "ผ่านแล้ว"; stCol = Muted; break;
                case CourseStatus.Selected: stText = confirmed && svc.IsEnrolledThisTerm(d.code) ? "ลงทะเบียนแล้ว" : "เลือกแล้ว"; stCol = Accent; break;
                case CourseStatus.MissingPrerequisite: stText = "ขาดเงื่อนไข"; stCol = Bad; break;
                default: stText = "ลงได้"; stCol = Ok; break;
            }
            var chip = Panel(row.transform, "Status", new Color(stCol.r, stCol.g, stCol.b, 0.16f), 0f, false);
            Stretch((RectTransform)chip.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-236, -40), new Vector2(-14, -8));
            var ct = Text(chip.transform, stText, 18, stCol, TextAlignmentOptions.Center); ct.fontStyle = FontStyles.Bold;
            Fill(ct.rectTransform);

            if (!string.IsNullOrEmpty(reason) && st != CourseStatus.Passed)
            {
                var rt = Text(row.transform, reason, 18, st == CourseStatus.MissingPrerequisite ? Bad : Warn, TextAlignmentOptions.TopRight);
                UIFit.Wrap(rt, 18f, 18f);
                Stretch(rt.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-240, 50), new Vector2(-14, -44));
            }

            // ปุ่ม
            string code = d.code;
            if (st == CourseStatus.Selected && svc.RegistrationWindowOpen(reg.SemDay))
                RowButton(row.transform, "ถอน", Warn, () => DoRemove(code));
            else if (st == CourseStatus.CanRegister && svc.RegistrationWindowOpen(reg.SemDay))
                RowButton(row.transform, "+ เพิ่ม", Ok, () => DoAdd(code));
        }

        void RowButton(Transform row, string label, Color col, UnityEngine.Events.UnityAction act)
        {
            var b = Btn(row, label, col, Color.white, 20);
            Stretch((RectTransform)b.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-140, 10), new Vector2(-14, 50));
            b.onClick.AddListener(act);
        }

        void BuildSelectedRow(string code, bool canRemove)
        {
            var d = Svc.Curriculum.Get(code); if (d == null) return;
            var oc = Svc.FindOffered(code);
            var row = Panel(selContent, "Sel_" + code, RowBg, 96f);
            string when = "";
            if (oc != null) { var p = new List<string>(); foreach (var s in oc.Sessions) p.Add($"{AcademicCalendar.ShortTermDayText(s.day)} {s.TimeText}"); when = string.Join(" · ", p); }
            var tx = Text(row.transform, $"<b>{code}</b> {d.credits} นก.  <size=90%>{when}</size>\n<size=90%>{d.title}</size>", 20, Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(tx.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(canRemove ? -104 : -10, -2));
            tx.textWrappingMode = TextWrappingModes.NoWrap; tx.overflowMode = TextOverflowModes.Ellipsis;
            if (canRemove)
            {
                var b = Btn(row.transform, "ถอน", Warn, Color.white, 18);
                Stretch((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-94, -20), new Vector2(-10, 20));
                b.onClick.AddListener(() => DoRemove(code));
            }
        }

        // เปิด public ไว้ให้เทสต์/เครื่องมือเรียกได้ (ปุ่มใน UI เรียกเมธอดเดียวกัน)
        public void DoAdd(string code)
        {
            if (!Usable) return;
            bool ok = Svc.Add(code, out var msg);
            if (ok && Svc.HasRegistrationChanges) msg += " — กดยืนยันการแก้ไขเพื่อบันทึก";
            Feedback(ok, msg);
        }

        public void DoRemove(string code)
        {
            if (!Usable) return;
            bool ok = Svc.Remove(code, out var msg);
            if (ok && Svc.HasRegistrationChanges) msg += " — กดยืนยันการแก้ไขเพื่อบันทึก";
            Feedback(ok, msg);
        }

        // นำทางไปห้องของคาบถัดไป แล้วปิดโทรศัพท์ให้เดินได้ทันที
        public void DoNavigate()
        {
            string msg = ClassroomNavigator.NavigateToNextClass();
            HUDController.Toast(msg);
            if (ArrivalIntroController.Active) { ArrivalIntroController.Instance.NavigationDemonstrated(); return; }
            Close();
            if (phone != null && phone.IsOpen) phone.Toggle();
        }

        public void DoConfirm()
        {
            if (!Usable) return;
            if (confirmBtn != null) confirmBtn.interactable = false;   // กันกดรัว — ปุ่มจะกลับมาเองถ้ายืนยันไม่ผ่าน
            Svc.CheckRegistrationDeadline(reg.SemDay, out _);
            bool editing = Svc.Term != null && Svc.Term.confirmed;
            bool ok = Svc.Confirm(out var msg);
            Feedback(ok, msg);
            if (ok) HUDController.Toast(editing ? "แก้ไขการลงทะเบียนสำเร็จ! ตารางเรียนอัปเดตแล้ว"
                : "ลงทะเบียนสำเร็จ! ดูตารางเรียนในแอป แล้วไปนั่งเรียนที่ตึกตามเวลา");
        }

        void Feedback(bool ok, string msg)
        {
            lastMessage = msg; lastMessageColor = ok ? Ok : Bad;
            if (ok) NisitSimulator.Core.SFXManager.Success(); else NisitSimulator.Core.SFXManager.Error();
            reg.NotifyChanged();
            dirty = false;
            RefreshSummary();
            RefreshRegister();
        }

        void SetMessage(string msg, Color? col = null)
        {
            if (messageText == null) return;
            messageText.text = msg ?? "";
            messageText.color = col ?? Sub;
        }

        void SetConfirm(bool interactable, string label)
        {
            if (confirmBtn == null) return;
            confirmBtn.interactable = interactable;
            confirmLabel.text = label;
            var img = confirmBtn.GetComponent<Image>();
            if (img != null) img.color = interactable ? Accent : new Color(0.75f, 0.73f, 0.82f);
        }

        // ---------- แท็บตารางเรียน / ผลการเรียน ----------
        void RefreshTextPage()
        {
            if (!built) return;
            if (!Usable)
            {
                // คณะที่ใช้ระบบเกรดแบบเดิม: แท็บผลการเรียนแสดงเกรดแบบเดิม (เดิมอยู่ในแอป "เกรด" ที่รวมเข้ามาแล้ว)
                pageText.text = tab == 2 ? LegacyGradesText()
                    : "คณะของคุณไม่ใช้ระบบลงทะเบียนรายวิชา — ไม่มีตารางเรียนรายวิชา\nเข้าเรียน/สอบตามปฏิทินของคณะ · ดูเกรดได้ที่แท็บ \"ผลการเรียน\"";
                return;
            }
            pageText.text = tab == 1 ? ScheduleText() : GradeSummaryText() + TranscriptText();
        }

        public string ScheduleText()
        {
            var svc = Svc; var t = svc.Record.Current;
            var sb = new StringBuilder();
            if (t == null || t.isBreak) return "ปิดภาคฤดูร้อน — ไม่มีตารางเรียน\nรอเปิดลงทะเบียนวันอาทิตย์แรกของภาคต้นปีการศึกษาถัดไป";

            var items = new List<KeyValuePair<Enrollment, ClassSession>>();
            var idx = new Dictionary<ClassSession, int>();
            var ens = svc.CurrentEnrollments();
            bool preview = false;
            if (ens.Count == 0 && t.selected.Count > 0 && !t.confirmed)
            {
                preview = true;
                foreach (var code in t.selected)
                {
                    var oc = svc.FindOffered(code); if (oc == null) continue;
                    ens.Add(new Enrollment { code = code, retakeSection = oc.retakeSection });
                }
            }
            if (ens.Count == 0)
                return "ยังไม่มีตารางเรียนภาคนี้ — ลงทะเบียนและกดยืนยันก่อน\n" + (svc.ExplainNoOptions(reg.SemDay) ?? "");

            foreach (var e in ens)
            {
                var ss = svc.SessionsFor(e);
                for (int i = 0; i < ss.Count; i++) { items.Add(new KeyValuePair<Enrollment, ClassSession>(e, ss[i])); idx[ss[i]] = i; }
            }
            if (preview) sb.Append("<color=#E0843A><b>ตัวอย่างตาราง (ยังไม่ได้ยืนยัน — เข้าเรียนไม่ได้จนกว่าจะยืนยัน)</b></color>\n\n");

            float now = clock != null ? clock.TotalMinutes : 0f;
            int today = reg.SemDay;
            int days = Mathf.Max(3, AcademicCalendar.SemesterLen(t.semIndex));
            for (int d = 1; d <= days; d++)
            {
                sb.Append($"<b>{AcademicCalendar.TermDayText(d)}</b>");
                if (d == today) sb.Append("  <color=#8A5CD6>(วันนี้)</color>");
                // วันสอบตามปฏิทิน (กลางภาค = ครึ่งภาค, ปลายภาค = วันสุดท้าย) — ไม่เขียนตายตัว
                if (AcademicCalendar.HasMidterm(t.semIndex) && d == ExamController.MidtermDay(t.semIndex)) sb.Append("  <size=85%><color=#D14D5C>สอบกลางภาค</color></size>");
                if (d == ExamController.FinalDay(t.semIndex)) sb.Append("  <size=85%><color=#D14D5C>สอบปลายภาค</color></size>");
                sb.Append('\n');
                var dayItems = items.FindAll(p => p.Value.day == d);
                dayItems.Sort((a, b) => a.Value.startMinute.CompareTo(b.Value.startMinute));
                if (dayItems.Count == 0) sb.Append("   <color=#9A94B0>ไม่มีคาบเรียน</color>\n");
                foreach (var p in dayItems)
                {
                    var def = svc.Curriculum.Get(p.Key.code);
                    string mark = "";
                    if (!preview)
                    {
                        int ticks = p.Key.TicksFor(d + ":" + idx[p.Value]);
                        bool ongoing = d == today && now >= p.Value.startMinute && now < p.Value.endMinute;
                        bool past = d < today || (d == today && now >= p.Value.endMinute);
                        if (ongoing) mark = $"  <color=#E0843A><b>กำลังเรียน! ({ticks}/{p.Value.MaxTicks} ชม.)</b></color>";
                        else if (ticks > 0) mark = $"  <color=#389966>เข้าเรียนแล้ว {ticks}/{p.Value.MaxTicks} ชม.</color>";
                        else if (past) mark = "  <color=#D14D5C>ขาดเรียน</color>";
                    }
                    sb.Append($"   {p.Value.TimeText}   <b>{p.Key.code}</b> {(def != null ? def.title : "")}\n");
                    sb.Append($"   <size=85%><color=#7A7394>@ {CourseRegistrar.RoomText(p.Value)}</color></size>{mark}\n");
                }
                sb.Append('\n');
            }

            if (!preview)
            {
                sb.Append("<b>ความคืบหน้ารายวิชา</b>  <size=80%><color=#7A7394>(เข้าเรียน 30% · กลางภาค 30% · ปลายภาค 40%)</color></size>\n");
                foreach (var e in ens)
                {
                    float proj = svc.ProjectedScore(e);
                    var g = svc.Curriculum.GradeFor(proj);
                    string mid = e.midterm < 0 ? "–" : e.missedMidterm ? "ขาดสอบ" : Mathf.RoundToInt(e.midterm * 100).ToString();
                    string fin = e.final < 0 ? "–" : e.missedFinal ? "ขาดสอบ" : Mathf.RoundToInt(e.final * 100).ToString();
                    sb.Append($"   <b>{e.code}</b>  เรียน {Mathf.RoundToInt(svc.StudyRatio(e) * 100)}% · กลางภาค {mid} · ปลายภาค {fin} · คาดการณ์ {proj:0} ({g.letter})" +
                              (e.attempt > 1 ? $"  <color=#E0843A>[เรียนครั้งที่ {e.attempt}]</color>" : "") + "\n");
                }
                sb.Append(ClassroomRules.IsSinglePlayer
                    ? "\n<size=85%><color=#7A7394>เข้าเรียน = นั่งโต๊ะเรียนในห้องที่กำหนด (อาคาร · ชั้น · เลขห้อง) ระหว่างเวลาคาบ — เวลาจะเร่งจนเลิกคาบ (นับทุก 1 ชม. เกม) · ตู้เข้าเรียนในโถงพาไปห้องให้ · อ่านหนังสือนอกคาบช่วยชดเชยได้บางส่วน · สอบที่ห้องสอบคณะ</color></size>"
                    : "\n<size=85%><color=#7A7394>เข้าเรียน = นั่งโต๊ะเรียนในตึกที่กำหนดระหว่างเวลาคาบ (นับทุก 1 ชม. เกม) · อ่านหนังสือนอกคาบช่วยชดเชยได้บางส่วน · สอบที่ห้องสอบคณะ</color></size>");
            }
            return sb.ToString();
        }

        // กล่องสรุปบนแท็บผลการเรียน (แทนแอป "เกรด" เดิม)
        public string GradeSummaryText()
        {
            var svc = Svc;
            var cur = svc.Curriculum;
            string gpa = svc.HasGpa ? svc.Gpa().ToString("0.00") : "–";
            return $"<size=150%><b>GPA {gpa}</b></size>   <color=#7A7394>{reg.ShortSummary()}</color>\n" +
                   $"หน่วยกิตสะสม <b>{svc.EarnedCredits()}</b>/{cur.GraduationCredits}  ·  วิชาเลือกผ่าน <b>{svc.PassedElectiveCount()}</b>/{cur.electivesRequired}\n" +
                   "<color=#C9C2DD>----------------------------</color>\n\n";
        }

        // คณะที่ไม่ใช้หลักสูตรลงทะเบียน: GPA/จำนวนครั้งที่สอบจากระบบสอบแบบเดิม
        public static string LegacyGradesText()
        {
            var exam = Object.FindFirstObjectByType<ExamController>();
            float gpa = exam != null ? exam.GPA : 0f;
            int taken = exam != null ? exam.ExamsTaken : 0;
            var sb = new StringBuilder();
            sb.Append($"<size=150%><b>GPA {(taken > 0 ? gpa.ToString("0.00") : "–")}</b></size>\n");
            sb.Append($"สอบไปแล้ว <b>{taken}</b> ครั้ง  <size=85%><color=#7A7394>(สอบกลางภาค/ปลายภาคตามปฏิทิน ที่ห้องสอบของคณะ)</color></size>\n");
            if (exam != null && taken > 0)
            {
                var gps = exam.GetGradePoints();
                sb.Append("\n<b>เกรดแต่ละครั้ง</b>\n");
                for (int i = 0; i < gps.Count; i++) sb.Append($"   ครั้งที่ {i + 1}: <b>{gps[i]:0.0}</b>\n");
            }
            return sb.ToString();
        }

        public string TranscriptText()
        {
            var svc = Svc;
            var sb = new StringBuilder();
            var bySerial = new SortedDictionary<int, List<Enrollment>>();
            foreach (var e in svc.Record.enrollments)
            {
                if (!bySerial.TryGetValue(e.termSerial, out var l)) { l = new List<Enrollment>(); bySerial[e.termSerial] = l; }
                l.Add(e);
            }
            if (bySerial.Count == 0) sb.Append("<color=#9A94B0>ยังไม่มีผลการเรียน</color>\n\n");
            foreach (var kv in bySerial)
            {
                var first = kv.Value[0];
                bool allTransfer = kv.Value.TrueForAll(x => x.transfer);
                sb.Append($"<b>ภาคที่ {kv.Key}</b> — ชั้นปี {first.classYear} ภาค {first.semIndex + 1} (ปีการศึกษาที่ {first.calendarYear})");
                if (allTransfer) sb.Append("  <color=#7A7394>[เทียบโอนจากเซฟเก่า]</color>");
                sb.Append('\n');
                foreach (var e in kv.Value)
                {
                    var d = svc.Curriculum.Get(e.code);
                    string g = e.transfer ? "TR" : e.graded ? e.letter : "กำลังเรียน";
                    string col = e.transfer ? "#7A7394" : !e.graded ? "#E0843A" : e.passed ? "#389966" : "#D14D5C";
                    sb.Append($"   <b>{e.code}</b> {(d != null ? d.title : "")}  <size=85%>{(d != null ? d.credits : 0)} นก.</size>   <color={col}><b>{g}</b></color>");
                    if (e.graded && !e.transfer) sb.Append($" <size=80%>({e.score:0})</size>");
                    if (e.attempt > 1) sb.Append($"  <size=80%><color=#E0843A>ครั้งที่ {e.attempt}</color></size>");
                    sb.Append('\n');
                }
                if (!allTransfer && kv.Value.Exists(x => x.graded && !x.transfer))
                    sb.Append($"   <size=85%>GPA ภาค {svc.TermGpa(kv.Key):0.00}</size>\n");
                sb.Append('\n');
            }

            var cur = svc.Curriculum;
            sb.Append("<b>สรุป</b>\n");
            sb.Append($"   GPA สะสม {(svc.HasGpa ? svc.Gpa().ToString("0.00") : "– (ยังไม่มีเกรด)")}  <size=80%><color=#7A7394>(ถ่วงหน่วยกิต ใช้เกรดครั้งล่าสุดของแต่ละวิชา)</color></size>\n");
            sb.Append($"   หน่วยกิตสะสม {svc.EarnedCredits()}/{cur.GraduationCredits}  · วิชาบังคับ {svc.PassedRequiredCredits()}/{cur.RequiredCredits}  · วิชาเลือก {svc.PassedElectiveCount()}/{cur.electivesRequired} วิชา\n\n");
            var grad = svc.CheckGraduation();
            sb.Append("<b>เงื่อนไขจบการศึกษา</b>\n");
            if (grad.eligible) sb.Append("   <color=#389966><b>ครบทุกเงื่อนไขแล้ว</b></color>\n");
            else foreach (var r in grad.reasons) sb.Append("   <color=#D14D5C>- ").Append(r).Append("</color>\n");
            return sb.ToString();
        }

        // ============================================================
        // สร้าง UI
        // ============================================================
        void Build()
        {
            built = true;
            var canGo = new GameObject("Registration Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 78;
            scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); NisitSimulator.UI.UIFit.Scaler(scaler);   // Expand: ทั้งหน้าอยู่ในจอทุกสัดส่วน
            FitToScreen();

            root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canGo.transform, false);
            Fill((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0.16f, 0.13f, 0.26f, 0.55f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1600f, 940f);
            guideCard = crt;
            UIStyle.Card(card, CardCol);

            var title = Text(card.transform, AppName + "  <size=55%><color=#7A7394>ลงทะเบียนเรียน · ตารางเรียน · ผลการเรียน/เกรด</color></size>", 36, Accent, TextAlignmentOptions.TopLeft); title.fontStyle = FontStyles.Bold;
            UIFit.OneLine(title, 36f, 24f);
            Stretch(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -66), new Vector2(-420, -16));
            programText = Text(card.transform, "", 20, Sub, TextAlignmentOptions.TopRight);
            UIFit.OneLine(programText, 20f, 18f);
            Stretch(programText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(600, -54), new Vector2(-130, -22));
            summaryText = Text(card.transform, "", 21, Ink, TextAlignmentOptions.TopLeft);
            Stretch(summaryText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -140), new Vector2(-30, -70));
            summaryText.lineSpacing = 8f;

            var close = Btn(card.transform, "ปิด", new Color(0.86f, 0.80f, 0.90f), Ink, 22);
            Stretch((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -64), new Vector2(-24, -16));
            close.onClick.AddListener(Close);

            string[] tabs = { "ลงทะเบียน", "ตารางเรียน", "ผลการเรียน" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int k = i;
                var b = Btn(card.transform, tabs[i], new Color(0.82f, 0.78f, 0.92f), Ink, 22);
                Stretch((RectTransform)b.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30 + i * 236, -204), new Vector2(250 + i * 236, -150));
                b.onClick.AddListener(() => ShowTab(k));
                tabBtns.Add(b);
            }

            // --- หน้า ลงทะเบียน ---
            regPage = new GameObject("RegisterPage", typeof(RectTransform));
            regPage.transform.SetParent(card.transform, false);
            Stretch((RectTransform)regPage.transform, Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -218));

            listScroll = Scroll(regPage.transform, "CourseList", out listContent);
            Stretch((RectTransform)listScroll.transform, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0), new Vector2(1000, 0));

            var right = Panel(regPage.transform, "RightPanel", PanelBg, 0f, false);
            Stretch((RectTransform)right.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1016, 0), new Vector2(0, 0));
            selTitle = Text(right.transform, "รายการที่เลือก", 24, Accent, TextAlignmentOptions.TopLeft); selTitle.fontStyle = FontStyles.Bold;
            Stretch(selTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -50), new Vector2(-18, -12));
            creditText = Text(right.transform, "", 22, Ink, TextAlignmentOptions.TopRight);
            Stretch(creditText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -50), new Vector2(-18, -14));
            var selScroll = Scroll(right.transform, "Selected", out selContent);
            Stretch((RectTransform)selScroll.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 300), new Vector2(-12, -58));

            messageText = Text(right.transform, "", 19, Sub, TextAlignmentOptions.TopLeft);
            Stretch(messageText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 96), new Vector2(-18, 290));
            messageText.lineSpacing = 4f;

            confirmBtn = Btn(right.transform, "ยืนยันลงทะเบียน", Accent, Color.white, 26);
            Stretch((RectTransform)confirmBtn.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 82));
            confirmLabel = confirmBtn.GetComponentInChildren<TMP_Text>();
            confirmBtn.onClick.AddListener(DoConfirm);

            // --- หน้า ข้อความ (ตาราง/ผลการเรียน) ---
            textPage = new GameObject("TextPage", typeof(RectTransform));
            textPage.transform.SetParent(card.transform, false);
            Stretch((RectTransform)textPage.transform, Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -218));
            textScroll = Scroll(textPage.transform, "TextScroll", out var tc);
            Fill((RectTransform)textScroll.transform);
            pageText = Text(tc, "", 20, Ink, TextAlignmentOptions.TopLeft);
            pageText.lineSpacing = 6f;

            // ปุ่มนำทางไปห้องคาบถัดไป (แท็บตารางเรียน · เล่นคนเดียว)
            navBtn = Btn(card.transform, "นำทางไปห้องคาบถัดไป", Ok, Color.white, 20);
            Stretch((RectTransform)navBtn.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-420, -204), new Vector2(-24, -150));
            navBtn.onClick.AddListener(DoNavigate);
            navBtn.gameObject.SetActive(false);

            root.SetActive(false);
        }

        public RectTransform GuideTarget(string key)
        {
            if (key == "app") return phone != null && phone.homeView != null ? phone.homeView.transform.Find(PhoneButtonName) as RectTransform : null;
            if (!built) return null;
            if (key == "confirm") return confirmBtn != null ? (RectTransform)confirmBtn.transform : null;
            if (key == "navigate") return navBtn != null ? (RectTransform)navBtn.transform : null;
            if (key == "schedule" || key == "grades") return (RectTransform)tabBtns[key == "schedule" ? 1 : 2].transform;
            if (key == "add" && listContent != null)
                foreach (var b in listContent.GetComponentsInChildren<Button>()) if (b.interactable) return (RectTransform)b.transform;
            return null;
        }

        // ---------- helpers ----------
        static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static void Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        static void Clear(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var c = content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        TMP_Text Text(Transform parent, string s, float size, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = col; t.alignment = align;
            t.richText = true; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        GameObject Panel(Transform parent, string name, Color col, float height, bool layout = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UIStyle.Rounded; img.type = Image.Type.Sliced; img.color = col;
            img.pixelsPerUnitMultiplier = 2.2f;
            if (layout)
            {
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = height; le.preferredHeight = height;
            }
            return go;
        }

        void AddHeader(RectTransform content, string s)
        {
            var t = Text(content, $"<b>{s}</b>", 21, Accent, TextAlignmentOptions.BottomLeft);
            var le = t.gameObject.AddComponent<LayoutElement>(); le.minHeight = 40; le.preferredHeight = 40;
            t.margin = new Vector4(8, 0, 0, 0);
        }

        void AddNote(RectTransform content, string s, Color col)
        {
            var t = Text(content, s, 20, col, TextAlignmentOptions.TopLeft);
            t.margin = new Vector4(10, 10, 10, 10);
        }

        Button Btn(Transform parent, string label, Color col, Color textCol, float size)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            UIStyle.Button(go, col);
            var t = Text(go.transform, label, size, textCol, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            Fill(t.rectTransform);
            var b = go.GetComponent<Button>();
            var cb = b.colors; cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.7f); b.colors = cb;
            return b;
        }

        ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 40f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            var c = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            c.transform.SetParent(go.transform, false);
            content = (RectTransform)c.transform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0, 0); content.offsetMax = new Vector2(0, 0);
            var vl = c.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 10, 10); vl.spacing = 8f;
            vl.childControlHeight = true; vl.childControlWidth = true;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            c.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = (RectTransform)go.transform;
            sr.content = content;
            return sr;
        }
    }
}
