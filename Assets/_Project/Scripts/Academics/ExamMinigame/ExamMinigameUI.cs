using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;
using NisitSimulator.Systems;

namespace NisitSimulator.Academics.ExamMinigame
{
    // หน้าจอห้องสอบ/มินิเกมสอบ — สร้าง UI เองตอนรัน (แบบเดียวกับ RegistrationUI) ไม่ต้อง bake ฉาก
    //   หน้า: รายการวิชา → รายละเอียดก่อนเริ่ม → ทำข้อสอบ → ผลสอบ · ป้ายเล็ก (เมื่อซ่อนหน้าต่าง) · กล่องยืนยัน
    //   ทุกมินิเกมใช้ "คลิก" ได้ทั้งหมด (เรียงลำดับ/จับคู่ไม่ต้องลาก) · ระบบ Input เดิม (UnityEngine.Input + ปุ่ม UGUI)
    public class ExamMinigameUI : MonoBehaviour
    {
        static ExamMinigameUI _i;
        public static ExamMinigameUI EnsureExists(ExamMinigameController c)
        {
            if (_i == null) _i = new GameObject("ExamMinigameUI").AddComponent<ExamMinigameUI>();
            _i.ctl = c;
            return _i;
        }

        static readonly Color CardCol = new Color(0.955f, 0.93f, 0.985f, 1f);
        static readonly Color Ink = new Color(0.30f, 0.25f, 0.46f, 1f);
        static readonly Color Sub = new Color(0.44f, 0.41f, 0.57f, 1f);
        static readonly Color Accent = new Color(0.55f, 0.42f, 0.85f, 1f);
        static readonly Color Ok = new Color(0.22f, 0.60f, 0.40f, 1f);
        static readonly Color Bad = new Color(0.82f, 0.30f, 0.36f, 1f);
        static readonly Color Warn = new Color(0.88f, 0.52f, 0.16f, 1f);
        static readonly Color Muted = new Color(0.62f, 0.62f, 0.70f, 1f);
        static readonly Color RowBg = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color CodeBg = new Color(0.20f, 0.19f, 0.30f, 1f);
        static readonly Color CodeInk = new Color(0.93f, 0.92f, 1f, 1f);
        static readonly Color SelCol = new Color(0.55f, 0.42f, 0.85f, 1f);
        static readonly string[] Letters = { "A", "B", "C", "D", "E", "F", "G", "H" };

        enum Page { None, List, Brief, Exam, Result, Chip }

        ExamMinigameController ctl;
        TMP_FontAsset font;
        CanvasScaler scaler;
        bool built;
        Page page = Page.None;
        int qIndex;
        int selectedLeft = -1;
        string briefCode;
        string flash; Color flashCol;

        GameObject root, card, chip, confirmLayer;
        GameObject listPage, briefPage, examPage, resultPage;
        TMP_Text titleText, subText;
        RectTransform listContent;
        TMP_Text listMsg;
        TMP_Text briefText; Button briefStart;
        TMP_Text examHeader, timerText, progressText, hintCountText;
        RectTransform navRow, qContent; ScrollRect qScroll;
        Button prevBtn, nextBtn, hintBtn, submitBtn, quitBtn, hideBtn;
        TMP_Text resultScore, resultDetail; RectTransform reviewContent; ScrollRect reviewScroll;
        TMP_Text chipText;
        TMP_Text confirmText; Button confirmOk, confirmCancel; System.Action confirmAction;

        ExamSessionState S => ctl != null ? ctl.Session : null;
        public bool IsVisible => page != Page.None;
        public string PageName => page.ToString();

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
        }

        void OnDestroy() { if (_i == this) _i = null; }

        // ============================================================
        // เปลี่ยนหน้า
        // ============================================================
        public void HideAll()
        {
            if (!built) return;
            page = Page.None;
            root.SetActive(false); chip.SetActive(false); confirmLayer.SetActive(false);
        }

        void Show(Page p)
        {
            if (!built) Build();
            page = p;
            bool inCard = p == Page.List || p == Page.Brief || p == Page.Exam || p == Page.Result;
            root.SetActive(inCard);
            chip.SetActive(p == Page.Chip);
            confirmLayer.SetActive(false);
            listPage.SetActive(p == Page.List);
            briefPage.SetActive(p == Page.Brief);
            examPage.SetActive(p == Page.Exam);
            resultPage.SetActive(p == Page.Result);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }

        public void ShowCourseList()
        {
            Show(Page.List);
            string kind = ctl.RoundFinal ? "สอบปลายภาค" : "สอบกลางภาค";
            titleText.text = "ห้องสอบ — " + kind + " " + AcademicCalendar.SemesterName(ctl.RoundSemester);
            subText.text = "เลือกวิชาที่ลงทะเบียนเพื่อสอบทีละวิชา · สอบได้วิชาละ 1 ครั้งต่อรอบ · การเข้าเรียนตามปกติไม่มีมินิเกม";
            RefreshList();
        }

        public void ShowExam()
        {
            if (S == null || !S.InProgress) { ShowCourseList(); return; }
            Show(Page.Exam);
            titleText.text = (S.isFinal ? "สอบปลายภาค" : "สอบกลางภาค") + " — " + S.courseCode + " " + S.courseTitle;
            subText.text = "ไม่แสดงเฉลยจนกว่าจะส่ง · กลับไปแก้คำตอบได้ก่อนส่ง · Esc = ซ่อนหน้าต่าง (เวลายังเดิน)";
            qIndex = Mathf.Clamp(S.currentIndex, 0, Mathf.Max(0, S.questions.Count - 1));
            selectedLeft = -1; flash = null;
            RebuildQuestion(true);
        }

        public void ShowMiniChip()
        {
            Show(Page.Chip);
            UpdateTimer();
        }

        public void ShowResult()
        {
            if (S == null) { ShowCourseList(); return; }
            Show(Page.Result);
            titleText.text = "ผลสอบ — " + S.courseCode + " " + S.courseTitle;
            subText.text = (S.isFinal ? "สอบปลายภาค" : "สอบกลางภาค") + (S.autoSubmitted ? " · ส่งอัตโนมัติเมื่อหมดเวลา" : S.quit ? " · เลิกสอบ (ส่งคำตอบที่มี)" : "");
            BuildResult();
        }

        // ============================================================
        // Update: เวลา + ปุ่มลัด
        // ============================================================
        void Update()
        {
            if (!built || page == Page.None) return;
            FitToScreen();
            if (page == Page.Exam || page == Page.Chip) UpdateTimer();

            if (confirmLayer.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) confirmLayer.SetActive(false);
                return;
            }
            switch (page)
            {
                case Page.Exam: if (Input.GetKeyDown(KeyCode.Escape)) ctl.HideWindow(); break;
                case Page.Chip: if (Input.GetKeyDown(KeyCode.E)) ShowExam(); break;
                case Page.Brief: if (Input.GetKeyDown(KeyCode.Escape)) ShowCourseList(); break;
                case Page.Result: if (Input.GetKeyDown(KeyCode.Escape)) ShowCourseList(); break;
                case Page.List: if (Input.GetKeyDown(KeyCode.Escape)) ctl.LeaveExamRoom(); break;
            }
        }

        void UpdateTimer()
        {
            if (S == null) return;
            int sec = Mathf.Max(0, Mathf.CeilToInt(S.remainingSeconds));
            string t = $"{sec / 60:00}:{sec % 60:00}";
            var col = sec <= 30 ? Bad : Ink;
            if (timerText != null) { timerText.text = "เหลือ " + t; timerText.color = col; }
            if (chipText != null && page == Page.Chip)
                chipText.text = $"กำลังสอบ {S.courseCode} · เหลือเวลา <b>{t}</b> · ตอบแล้ว {ExamMinigameLogic.AnsweredCount(S)}/{S.questions.Count}";
        }

        void FitToScreen()
        {
            if (scaler == null || Screen.height <= 0) return;
            scaler.matchWidthOrHeight = Screen.width / (float)Screen.height > 1920f / 1080f ? 1f : 0f;
        }

        // ============================================================
        // หน้ารายการวิชา
        // ============================================================
        void RefreshList()
        {
            Clear(listContent);
            var entries = ctl.BuildEntries();
            if (entries.Count == 0) AddNote(listContent, "ภาคนี้ไม่มีวิชาที่ลงทะเบียน จึงไม่มีการสอบ", Sub);
            foreach (var en in entries) BuildEntryRow(en);
            listMsg.text = flash ?? "";
            listMsg.color = flash != null ? flashCol : Sub;
            flash = null;
        }

        void BuildEntryRow(ExamMinigameController.CourseEntry en)
        {
            var row = Panel(listContent, "Row_" + en.code, RowBg, 112f);
            var title = Text(row.transform, $"<b>{en.code}</b>  {Esc(en.title)}", 23, Ink, TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -50), new Vector2(-300, -8));
            title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;

            string info;
            if (en.status == ExamMinigameController.EntryStatus.Ready || en.status == ExamMinigameController.EntryStatus.InProgress)
                info = $"{en.questions} ข้อ · {Mathf.RoundToInt(en.timeLimit)} วินาที · คำใบ้ {ctl.HintsFor(en.code)} ครั้ง (ความรู้วิชานี้ {ctl.CourseKnowledge(en.code):0}/100)\n{en.reason}";
            else if (en.status == ExamMinigameController.EntryStatus.Submitted)
                info = $"{en.reason} · คะแนนสอบที่บันทึก {en.examScore * 100f:0}/100";
            else info = en.reason;
            Color infoCol = en.status == ExamMinigameController.EntryStatus.NotEligible ? Bad
                          : en.status == ExamMinigameController.EntryStatus.NoContent || en.status == ExamMinigameController.EntryStatus.LegacyAssessment ? Warn : Sub;
            var it = Text(row.transform, info, 18, infoCol, TextAlignmentOptions.TopLeft);
            Stretch(it.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(22, 6), new Vector2(-300, -48));

            string code = en.code;
            switch (en.status)
            {
                case ExamMinigameController.EntryStatus.Ready:
                    RowButton(row.transform, "ดูรายละเอียด", Accent, () => ShowBriefing(code)); break;
                case ExamMinigameController.EntryStatus.InProgress:
                    RowButton(row.transform, "ทำต่อ", Warn, () => ctl.ResumeWindow()); break;
                case ExamMinigameController.EntryStatus.NoContent:
                case ExamMinigameController.EntryStatus.LegacyAssessment:
                    RowButton(row.transform, "สอบแบบเดิม", Warn, () => Confirm(
                        $"สอบ {code} ด้วยข้อสอบแบบเดิมของระบบ (ไม่ใช่มินิเกม)?\nคะแนนจะบันทึกให้วิชานี้เท่านั้น และสอบได้ครั้งเดียวต่อรอบ", "เริ่มสอบแบบเดิม",
                        () => ctl.StartLegacyExam(new List<string> { code }))); break;
                case ExamMinigameController.EntryStatus.Submitted:
                    Chip(row.transform, "ส่งแล้ว", Ok); break;
                default:
                    Chip(row.transform, "สอบไม่ได้", Bad); break;
            }
        }

        public void ShowBriefing(string code)
        {
            if (!ctl.CanStart(code, out var why)) { flash = why; flashCol = Bad; ShowCourseList(); return; }
            briefCode = code;
            Show(Page.Brief);
            var svc = CourseRegistrar.Instance.Service;
            var def = svc.Curriculum.Get(code);
            var bank = ctl.Db.Get(code);
            int n = ctl.Db.QuestionsFor(bank);
            float k = ctl.CourseKnowledge(code);
            int hints = ExamMinigameLogic.HintAllowance(k, ctl.Db);
            titleText.text = "รายละเอียดการสอบ — " + code;
            subText.text = "ตรวจสอบรายละเอียด แล้วกดยืนยันเพื่อเริ่มสอบ (เวลาเริ่มนับหลังกดยืนยันเท่านั้น)";
            var sb = new StringBuilder();
            sb.Append($"<size=130%><b>{code} {Esc(def != null ? def.title : "")}</b></size>\n");
            sb.Append($"{(ctl.RoundFinal ? "สอบปลายภาค" : "สอบกลางภาค")} {AcademicCalendar.SemesterName(ctl.RoundSemester)}\n\n");
            sb.Append($"รูปแบบข้อสอบ: <b>{ExamMinigameLogic.FormatsOf(bank)}</b>\n");
            sb.Append($"จำนวนข้อ: <b>{n} ข้อ</b> (สุ่มจากคลัง {bank.questions.Count} ข้อ ไม่ซ้ำในรอบ)\n");
            sb.Append($"เวลาสอบ: <b>{Mathf.RoundToInt(ctl.Db.TimeFor(bank))} วินาที</b> — เริ่มจับเวลาเมื่อกดยืนยันเท่านั้น\n");
            sb.Append($"คำใบ้ที่ใช้ได้: <b>{hints} ครั้ง</b> (ความรู้วิชานี้ {k:0}/100 · ต่ำกว่า {ctl.Db.oneHintFrom:0} ไม่มี · {ctl.Db.oneHintFrom:0}–{ctl.Db.twoHintsFrom - 1:0} ได้ 1 · {ctl.Db.twoHintsFrom:0} ขึ้นไปได้ 2)\n\n");
            sb.Append("<b>กติกา</b>\n");
            sb.Append("• คิดคะแนนจากความถูกต้องเท่านั้น ไม่มีโบนัสความเร็ว · ข้อที่ไม่ตอบได้ 0\n");
            sb.Append("• เลือกคำตอบ/หาจุดผิด: ถูกได้เต็ม · จับคู่: ตามจำนวนคู่ที่ถูก · เรียงลำดับ: ตามสัดส่วนบล็อกที่อยู่ถูกตำแหน่ง\n");
            sb.Append("• คำใบ้ไม่หักคะแนน ใช้ได้ข้อละครั้ง · ไม่แสดงเฉลยจนกว่าจะส่ง\n");
            sb.Append($"• คะแนนสอบบันทึกเข้าสูตรเดิม: ตอบถูก × {svc.Curriculum.examQuizWeight * 100:0}% + ความพร้อมจากการเข้าเรียน × {(1f - svc.Curriculum.examQuizWeight) * 100:0}%\n");
            sb.Append($"  แล้วรวมเป็นเกรดรายวิชา: เรียน {svc.Curriculum.studyWeight * 100:0}% · กลางภาค {svc.Curriculum.midtermWeight * 100:0}% · ปลายภาค {svc.Curriculum.finalWeight * 100:0}% (ประกาศตอนสิ้นภาค)\n");
            sb.Append("• หมดเวลา = ส่งอัตโนมัติ · เลิกสอบ = ส่งคำตอบที่มี · สอบซ้ำในรอบเดียวกันไม่ได้");
            briefText.text = sb.ToString();
        }

        // ============================================================
        // หน้าทำข้อสอบ
        // ============================================================
        public void GoTo(int i)
        {
            if (S == null || S.questions.Count == 0) return;
            qIndex = Mathf.Clamp(i, 0, S.questions.Count - 1);
            S.currentIndex = qIndex;
            selectedLeft = -1; flash = null;
            RebuildQuestion(true);
        }

        void RebuildQuestion(bool scrollTop)
        {
            if (S == null || S.questions.Count == 0) return;
            var st = S.questions[qIndex];
            var q = ctl.QuestionAt(qIndex);

            examHeader.text = $"<b>{S.courseCode}</b> {Esc(S.courseTitle)}";
            int answered = ExamMinigameLogic.AnsweredCount(S);
            progressText.text = $"ข้อ {qIndex + 1}/{S.questions.Count} · ตอบแล้ว {answered}/{S.questions.Count}";
            hintCountText.text = $"คำใบ้คงเหลือ {Mathf.Max(0, S.hintsAllowed - S.hintsUsed)}/{S.hintsAllowed}";

            // ปุ่มเลขข้อ (สีเขียว = ตอบแล้ว)
            Clear(navRow);
            for (int i = 0; i < S.questions.Count; i++)
            {
                int k = i;
                bool ans = ExamMinigameLogic.IsAnswered(S.questions[i]);
                var col = i == qIndex ? SelCol : ans ? new Color(0.70f, 0.88f, 0.76f) : new Color(0.86f, 0.84f, 0.92f);
                var b = SimpleButton(navRow, (i + 1).ToString(), col, i == qIndex ? Color.white : Ink, 22);
                var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 60; le.minWidth = 60; le.preferredHeight = 48;
                b.onClick.AddListener(() => GoTo(k));
            }

            Clear(qContent);
            if (q == null)
            {
                AddNote(qContent, "ข้อนี้ถูกนำออกจากคลังข้อสอบหลังเริ่มสอบ — ไม่นับคะแนน", Warn);
            }
            else
            {
                var type = q.type;
                AddNote(qContent, $"<b>{ExamMinigameLogic.TypeName(type)}</b>  <size=85%>({st.maxPoints:0.#} คะแนน) · {ExamMinigameLogic.RuleText(type)}</size>", Accent);
                AddNote(qContent, $"<b>{Esc(q.prompt)}</b>", Ink, 25);
                if (!string.IsNullOrEmpty(q.code)) AddCodeBlock(qContent, q.code);
                switch (type)
                {
                    case ExamQuestionType.MultipleChoice: BuildChoices(q, st, false); break;
                    case ExamQuestionType.FindError: BuildChoices(q, st, true); break;
                    case ExamQuestionType.Ordering: BuildOrdering(q, st); break;
                    case ExamQuestionType.Matching: BuildMatching(q, st); break;
                }
                if (st.hintUsed) AddBox(qContent, "คำใบ้: " + Esc(ExamMinigameLogic.HintText(q, st)), new Color(1f, 0.95f, 0.80f), new Color(0.50f, 0.36f, 0.10f));
            }
            if (!string.IsNullOrEmpty(flash)) AddNote(qContent, flash, flashCol);

            prevBtn.interactable = qIndex > 0;
            nextBtn.interactable = qIndex < S.questions.Count - 1;
            hintBtn.interactable = q != null && ExamMinigameLogic.CanUseHint(S, st, out _);
            var hl = hintBtn.GetComponentInChildren<TMP_Text>();
            if (hl != null) hl.text = st.hintUsed ? "ใช้คำใบ้แล้ว" : "ใช้คำใบ้";

            if (scrollTop) { Canvas.ForceUpdateCanvases(); qScroll.verticalNormalizedPosition = 1f; }
        }

        void Changed() { flash = null; RebuildQuestion(false); }

        void BuildChoices(ExamQuestion q, ExamQuestionState st, bool findError)
        {
            AddNote(qContent, findError ? "คลิกบรรทัด/ส่วนที่ผิด (เลือกได้ 1 ตำแหน่ง)" : "คลิกเลือกคำตอบที่ถูกต้อง (เลือกได้ 1 ข้อ)", Sub, 18);
            for (int k = 0; k < st.displayOrder.Count; k++)
            {
                int orig = st.displayOrder[k];
                if (orig < 0 || orig >= q.items.Count) continue;
                bool sel = st.selected == orig;
                bool elim = st.eliminated.Contains(orig);
                string label = findError
                    ? $"<b>{orig + 1}</b>   {Esc(q.items[orig])}"
                    : $"<b>{Letters[k % Letters.Length]}.</b>  {Esc(q.items[orig])}";
                if (elim) label = $"<s>{label}</s>  <size=80%>(คำใบ้: ตัดออก)</size>";
                var b = FlowButton(qContent, label, elim ? new Color(0.88f, 0.88f, 0.90f) : sel ? SelCol : (findError ? new Color(0.93f, 0.93f, 0.98f) : RowBg),
                                   elim ? Muted : sel ? Color.white : Ink, 21);
                b.interactable = !elim;
                b.onClick.AddListener(() => { ExamMinigameLogic.Select(S, st, orig); Changed(); });
            }
        }

        void BuildOrdering(ExamQuestion q, ExamQuestionState st)
        {
            AddNote(qContent, "<b>ลำดับของคุณ</b>  <size=85%>(คลิกบล็อกเพื่อนำออก · ปุ่ม \"ขึ้น\" เพื่อสลับกับบล็อกก่อนหน้า)</size>", Accent, 20);
            if (st.order.Count == 0) AddNote(qContent, "— ยังไม่ได้วางบล็อก —", Muted, 18);
            for (int p = 0; p < st.order.Count; p++)
            {
                int pos = p; int blk = st.order[p];
                var row = HRow(qContent);
                var main = FlowButton(row, $"<b>{p + 1}.</b>  {Esc(q.items[blk])}", new Color(0.86f, 0.80f, 0.98f), Ink, 21);
                main.GetComponent<LayoutElement>().flexibleWidth = 1f;
                main.onClick.AddListener(() => { ExamMinigameLogic.RemoveOrderAt(S, st, pos); Changed(); });
                var up = SimpleButton(row, "ขึ้น", new Color(0.82f, 0.78f, 0.92f), Ink, 18);
                var ule = up.gameObject.AddComponent<LayoutElement>(); ule.preferredWidth = 90; ule.minWidth = 90; ule.minHeight = 52;
                up.interactable = p > 0;
                up.onClick.AddListener(() => { ExamMinigameLogic.MoveOrderUp(S, st, pos); Changed(); });
            }
            AddNote(qContent, "<b>บล็อกที่ยังไม่ได้วาง</b>  <size=85%>(คลิกเพื่อต่อท้ายลำดับ)</size>", Accent, 20);
            int left = 0;
            foreach (var blk in st.displayOrder)
            {
                if (st.order.Contains(blk) || blk < 0 || blk >= q.items.Count) continue;
                left++;
                int b = blk;
                var btn = FlowButton(qContent, Esc(q.items[blk]), RowBg, Ink, 21);
                btn.onClick.AddListener(() => { ExamMinigameLogic.AppendOrder(S, st, b); Changed(); });
            }
            if (left == 0) AddNote(qContent, "— วางครบทุกบล็อกแล้ว —", Muted, 18);
            if (st.order.Count > 0)
            {
                var clr = SimpleButton(qContent, "ล้างลำดับทั้งหมด", new Color(0.95f, 0.85f, 0.85f), Bad, 18);
                var le = clr.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 46; le.minHeight = 46;
                clr.onClick.AddListener(() => Confirm("ล้างลำดับที่วางไว้ทั้งหมดของข้อนี้?", "ล้าง", () => { ExamMinigameLogic.ClearOrder(S, st); Changed(); }));
            }
        }

        void BuildMatching(ExamQuestion q, ExamQuestionState st)
        {
            AddNote(qContent, "<b>ขั้นที่ 1: คลิกรายการฝั่งซ้าย</b>  <size=85%>แล้วคลิกคำตอบฝั่งขวาที่คู่กัน (คลิกฝั่งซ้ายที่จับแล้วเพื่อเปลี่ยนคู่)</size>", Accent, 20);
            int n = q.items.Count;
            for (int i = 0; i < n; i++)
            {
                int li = i;
                int m = i < st.match.Count ? st.match[i] : -1;
                string cur = m >= 0 && m < q.matches.Count ? $"<b>{RightLabel(st, m)}</b> {Esc(q.matches[m])}" : "<color=#9A96AE>— ยังไม่จับคู่ —</color>";
                bool sel = selectedLeft == i;
                var b = FlowButton(qContent, $"<b>{i + 1}.</b> {Esc(q.items[i])}\n      <size=90%>คู่กับ: {cur}</size>",
                                   sel ? SelCol : m >= 0 ? new Color(0.86f, 0.94f, 0.88f) : RowBg, sel ? Color.white : Ink, 21);
                b.onClick.AddListener(() => { selectedLeft = selectedLeft == li ? -1 : li; Changed(); });
            }
            AddNote(qContent, selectedLeft >= 0 ? $"<b>ขั้นที่ 2: เลือกคำตอบให้ข้อ {selectedLeft + 1}</b>" : "<b>ฝั่งขวา</b>  <size=85%>(เลือกฝั่งซ้ายก่อน)</size>", Accent, 20);
            for (int k = 0; k < st.displayOrder.Count; k++)
            {
                int r = st.displayOrder[k];
                if (r < 0 || r >= q.matches.Count) continue;
                int usedBy = st.match.IndexOf(r);
                string label = $"<b>{Letters[k % Letters.Length]}</b>  {Esc(q.matches[r])}" + (usedBy >= 0 ? $"  <size=80%>(คู่กับข้อ {usedBy + 1})</size>" : "");
                var b = FlowButton(qContent, label, usedBy >= 0 ? new Color(0.93f, 0.93f, 0.97f) : RowBg, usedBy >= 0 ? Sub : Ink, 21);
                b.onClick.AddListener(() => PickRight(st, r));
            }
            if (selectedLeft >= 0 && selectedLeft < st.match.Count && st.match[selectedLeft] >= 0)
            {
                int li = selectedLeft;
                var clr = SimpleButton(qContent, $"ยกเลิกคู่ของข้อ {li + 1}", new Color(0.95f, 0.85f, 0.85f), Bad, 18);
                var le = clr.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 46; le.minHeight = 46;
                clr.onClick.AddListener(() => { ExamMinigameLogic.SetMatch(S, st, li, -1); Changed(); });
            }
        }

        // จับคู่: เลือกฝั่งขวาให้ฝั่งซ้ายที่เลือกไว้ แล้วเลื่อนไปฝั่งซ้ายที่ยังว่างข้อถัดไป
        public void PickRight(ExamQuestionState st, int r)
        {
            if (selectedLeft < 0) { flash = "เลือกรายการฝั่งซ้ายก่อน แล้วค่อยคลิกคำตอบฝั่งขวา"; flashCol = Warn; RebuildQuestion(false); return; }
            ExamMinigameLogic.SetMatch(S, st, selectedLeft, r);
            int next = -1;
            for (int j = 1; j <= st.match.Count; j++) { int c = (selectedLeft + j) % st.match.Count; if (st.match[c] < 0) { next = c; break; } }
            selectedLeft = next;
            Changed();
        }

        public void SelectLeft(int i) { selectedLeft = i; Changed(); }

        static string RightLabel(ExamQuestionState st, int r)
        {
            int k = st.displayOrder.IndexOf(r);
            return k >= 0 ? Letters[k % Letters.Length] : "?";
        }

        public void DoHint()
        {
            if (S == null) return;
            if (!ctl.UseHint(qIndex, out var why)) { flash = why; flashCol = Warn; }
            else flash = null;
            RebuildQuestion(false);
        }

        public void AskSubmit()
        {
            if (S == null || !S.InProgress) return;
            int un = S.questions.Count - ExamMinigameLogic.AnsweredCount(S);
            string msg = un > 0
                ? $"ยังไม่ได้ตอบ <b><color=#D14D5C>{un} ข้อ</color></b> (ได้ 0 คะแนน)\nยืนยันส่งข้อสอบ {S.courseCode}? ส่งแล้วแก้ไม่ได้"
                : $"ตอบครบ {S.questions.Count} ข้อแล้ว\nยืนยันส่งข้อสอบ {S.courseCode}? ส่งแล้วแก้ไม่ได้";
            Confirm(msg, "ส่งข้อสอบ", () => ctl.Submit(false));
        }

        public void AskQuit()
        {
            if (S == null || !S.InProgress) return;
            Confirm($"เลิกสอบ {S.courseCode}?\nคำตอบที่มีตอนนี้ ({ExamMinigameLogic.AnsweredCount(S)}/{S.questions.Count} ข้อ) จะถูกส่งเป็นผลของรอบนี้ และสอบซ้ำไม่ได้",
                    "เลิกสอบและส่ง", () => ctl.QuitExam());
        }

        // สำหรับเทสต์/ปุ่มลัด: กดยืนยันในกล่องยืนยันที่เปิดอยู่
        public string ConfirmMessage => confirmLayer != null && confirmLayer.activeSelf ? confirmText.text : null;
        public void PressConfirm() { if (confirmLayer != null && confirmLayer.activeSelf) confirmOk.onClick.Invoke(); }

        // ============================================================
        // หน้าผลสอบ
        // ============================================================
        void BuildResult()
        {
            var s = S;
            resultScore.text = $"{s.score100}<size=45%>/100</size>";
            resultScore.color = s.score100 >= 70 ? Ok : s.score100 >= 50 ? Warn : Bad;
            var sb = new StringBuilder();
            sb.Append($"ได้ {s.earned:0.##}/{s.maxPoints:0.##} คะแนน จาก {s.questions.Count} ข้อ (คิดจากความถูกต้องเท่านั้น)\n");
            if (s.recorded && s.recordedExamScore >= 0f)
            {
                var cur = CourseRegistrar.Instance != null && CourseRegistrar.Instance.Service != null ? CourseRegistrar.Instance.Service.Curriculum : null;
                float w = cur != null ? cur.examQuizWeight : 0.6f;
                sb.Append($"<color=#389966><b>บันทึกคะแนนแล้ว</b></color> — คะแนน{(s.isFinal ? "ปลายภาค" : "กลางภาค")}ของ {s.courseCode} = <b>{s.recordedExamScore * 100f:0}</b> ");
                sb.Append($"<size=85%>(สูตรเดิม: ตอบถูก×{w * 100:0}% + ความพร้อมจากการเข้าเรียน×{(1f - w) * 100:0}%{(Perks.ExamBonus > 0f ? " + โบนัสเซียนสอบ" : "")})</size>\n");
                sb.Append("เกรดรายวิชาจะประกาศตอนสิ้นภาค · " + ctl.LastRewardText);
            }
            else sb.Append("<color=#D14D5C>" + (string.IsNullOrEmpty(ctl.LastRewardText) ? "ยังไม่ได้บันทึกคะแนน" : ctl.LastRewardText) + "</color>");
            resultDetail.text = sb.ToString();

            Clear(reviewContent);
            var bank = ctl.Db.Get(s.courseCode);
            for (int i = 0; i < s.questions.Count; i++)
            {
                var st = s.questions[i];
                var q = bank != null ? bank.Find(st.questionId) : null;
                var box = new StringBuilder();
                if (q == null || st.missingInBank) { box.Append($"<b>ข้อ {i + 1}</b> — ข้อนี้ถูกนำออกจากคลัง ไม่นับคะแนน"); AddBox(reviewContent, box.ToString(), RowBg, Ink); continue; }
                string mark = st.earned >= st.maxPoints - 1e-4f ? "<color=#389966>ถูก</color>" : st.earned > 0f ? "<color=#E0843A>ถูกบางส่วน</color>" : "<color=#D14D5C>ผิด/ไม่ตอบ</color>";
                box.Append($"<b>ข้อ {i + 1}</b> · {ExamMinigameLogic.TypeName(q.type)} · {mark} · {st.earned:0.##}/{st.maxPoints:0.##} คะแนน{(st.hintUsed ? " · ใช้คำใบ้" : "")}\n");
                box.Append($"<b>{Esc(q.prompt)}</b>\n");
                if (!string.IsNullOrEmpty(q.code)) box.Append($"<size=90%>{Esc(q.code)}</size>\n");
                box.Append($"\n<color=#6E5CA8><b>คำตอบของคุณ</b></color>\n{Esc(ExamMinigameLogic.PlayerAnswerText(q, st))}\n");
                box.Append($"\n<color=#389966><b>คำตอบที่ถูกต้อง</b></color>\n{Esc(ExamMinigameLogic.CorrectAnswerText(q))}\n");
                if (!string.IsNullOrEmpty(q.explanation)) box.Append($"\n<b>คำอธิบาย:</b> {Esc(q.explanation)}");
                AddBox(reviewContent, box.ToString(), RowBg, Ink);
            }
            Canvas.ForceUpdateCanvases();
            reviewScroll.verticalNormalizedPosition = 1f;
        }

        // ============================================================
        // กล่องยืนยัน
        // ============================================================
        void Confirm(string msg, string okLabel, System.Action ok)
        {
            if (!built) Build();
            confirmText.text = msg;
            var l = confirmOk.GetComponentInChildren<TMP_Text>(); if (l != null) l.text = okLabel;
            confirmAction = ok;
            confirmLayer.SetActive(true);
            confirmLayer.transform.SetAsLastSibling();
        }

        // ============================================================
        // สร้าง UI
        // ============================================================
        void Build()
        {
            built = true;
            font = FindThaiFont();

            var canGo = new GameObject("ExamMinigame Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 88;
            scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); NisitSimulator.UI.UIFit.Scaler(scaler);   // Expand: ทั้งหน้าอยู่ในจอทุกสัดส่วน
            FitToScreen();

            root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canGo.transform, false);
            Fill((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0.16f, 0.13f, 0.26f, 0.62f);

            card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1640f, 980f);
            UIStyle.Card(card, CardCol);

            titleText = Text(card.transform, "ห้องสอบ", 34, Accent, TextAlignmentOptions.TopLeft); titleText.fontStyle = FontStyles.Bold;
            // ฟอนต์ไทย (Mitr) บรรทัดสูง — กล่องต้องสูงพอ ไม่งั้น Ellipsis ซ่อนทั้งบรรทัด
            Stretch(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -80), new Vector2(-30, -6));
            titleText.textWrappingMode = TextWrappingModes.NoWrap; titleText.overflowMode = TextOverflowModes.Ellipsis;
            subText = Text(card.transform, "", 19, Sub, TextAlignmentOptions.TopLeft);
            Stretch(subText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -110), new Vector2(-30, -78));

            BuildListPage();
            BuildBriefPage();
            BuildExamPage();
            BuildResultPage();
            BuildConfirm(canGo.transform);
            BuildChip(canGo.transform);

            root.SetActive(false);
        }

        GameObject PageRoot(string name)
        {
            var p = new GameObject(name, typeof(RectTransform));
            p.transform.SetParent(card.transform, false);
            Stretch((RectTransform)p.transform, Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -112));
            return p;
        }

        void BuildListPage()
        {
            listPage = PageRoot("ListPage");
            var sc = Scroll(listPage.transform, "Courses", out listContent);
            Stretch((RectTransform)sc.transform, Vector2.zero, Vector2.one, new Vector2(0, 96), new Vector2(0, 0));
            listMsg = Text(listPage.transform, "", 20, Sub, TextAlignmentOptions.MidlineLeft);
            Stretch(listMsg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 8), new Vector2(-340, 84));
            var leave = Btn(listPage.transform, "ออกจากห้องสอบ", new Color(0.86f, 0.80f, 0.90f), Ink, 24);
            Stretch((RectTransform)leave.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 10), new Vector2(0, 80));
            leave.onClick.AddListener(() => ctl.LeaveExamRoom());
        }

        void BuildBriefPage()
        {
            briefPage = PageRoot("BriefPage");
            var bg = Panel(briefPage.transform, "Box", RowBg, 0f, false);
            Stretch((RectTransform)bg.transform, Vector2.zero, Vector2.one, new Vector2(0, 96), Vector2.zero);
            briefText = Text(bg.transform, "", 22, Ink, TextAlignmentOptions.TopLeft);
            Stretch(briefText.rectTransform, Vector2.zero, Vector2.one, new Vector2(26, 16), new Vector2(-26, -18));
            briefText.lineSpacing = 6f;
            var back = Btn(briefPage.transform, "กลับ", new Color(0.86f, 0.80f, 0.90f), Ink, 24);
            Stretch((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 10), new Vector2(240, 80));
            back.onClick.AddListener(ShowCourseList);
            briefStart = Btn(briefPage.transform, "ยืนยันเริ่มสอบ (เริ่มจับเวลา)", Accent, Color.white, 26);
            Stretch((RectTransform)briefStart.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-520, 10), new Vector2(0, 80));
            briefStart.onClick.AddListener(ConfirmStart);
        }

        public void ConfirmStart()
        {
            if (!ctl.StartExam(briefCode, out var why)) { flash = why; flashCol = Bad; ShowCourseList(); }
        }

        void BuildExamPage()
        {
            examPage = PageRoot("ExamPage");
            examHeader = Text(examPage.transform, "", 22, Ink, TextAlignmentOptions.TopLeft);
            Stretch(examHeader.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -44), new Vector2(-560, 6));
            examHeader.textWrappingMode = TextWrappingModes.NoWrap; examHeader.overflowMode = TextOverflowModes.Ellipsis;
            timerText = Text(examPage.transform, "เหลือ 03:00", 34, Ink, TextAlignmentOptions.TopRight); timerText.fontStyle = FontStyles.Bold;
            Stretch(timerText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-300, -48), new Vector2(0, 4));
            progressText = Text(examPage.transform, "", 20, Sub, TextAlignmentOptions.TopLeft);
            Stretch(progressText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -68), new Vector2(-560, -36));
            hintCountText = Text(examPage.transform, "", 20, Warn, TextAlignmentOptions.TopRight);
            Stretch(hintCountText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-560, -80), new Vector2(0, -48));

            var nav = new GameObject("Nav", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            nav.transform.SetParent(examPage.transform, false);
            navRow = (RectTransform)nav.transform;
            Stretch(navRow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -136), new Vector2(0, -84));
            var hl = nav.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            hl.childAlignment = TextAnchor.MiddleLeft;

            qScroll = Scroll(examPage.transform, "Question", out qContent);
            Stretch((RectTransform)qScroll.transform, Vector2.zero, Vector2.one, new Vector2(0, 96), new Vector2(0, -146));

            prevBtn = Btn(examPage.transform, "< ข้อก่อนหน้า", new Color(0.82f, 0.78f, 0.92f), Ink, 22);
            Stretch((RectTransform)prevBtn.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 10), new Vector2(210, 80));
            prevBtn.onClick.AddListener(() => GoTo(qIndex - 1));
            nextBtn = Btn(examPage.transform, "ข้อถัดไป >", new Color(0.82f, 0.78f, 0.92f), Ink, 22);
            Stretch((RectTransform)nextBtn.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(222, 10), new Vector2(432, 80));
            nextBtn.onClick.AddListener(() => GoTo(qIndex + 1));
            hintBtn = Btn(examPage.transform, "ใช้คำใบ้", new Color(1f, 0.86f, 0.55f), Ink, 22);
            Stretch((RectTransform)hintBtn.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(456, 10), new Vector2(676, 80));
            hintBtn.onClick.AddListener(DoHint);

            hideBtn = Btn(examPage.transform, "ซ่อนหน้าต่าง", new Color(0.86f, 0.84f, 0.92f), Sub, 20);
            Stretch((RectTransform)hideBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-770, 10), new Vector2(-580, 80));
            hideBtn.onClick.AddListener(() => ctl.HideWindow());
            quitBtn = Btn(examPage.transform, "เลิกสอบ", new Color(0.95f, 0.80f, 0.80f), Bad, 22);
            Stretch((RectTransform)quitBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-566, 10), new Vector2(-386, 80));
            quitBtn.onClick.AddListener(AskQuit);
            submitBtn = Btn(examPage.transform, "ส่งข้อสอบ", Ok, Color.white, 26);
            Stretch((RectTransform)submitBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-372, 10), new Vector2(0, 80));
            submitBtn.onClick.AddListener(AskSubmit);
        }

        void BuildResultPage()
        {
            resultPage = PageRoot("ResultPage");
            resultScore = Text(resultPage.transform, "0", 96, Ok, TextAlignmentOptions.Center); resultScore.fontStyle = FontStyles.Bold;
            Stretch(resultScore.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -150), new Vector2(300, 0));
            resultDetail = Text(resultPage.transform, "", 21, Ink, TextAlignmentOptions.TopLeft);
            Stretch(resultDetail.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(320, -160), new Vector2(0, -4));
            resultDetail.lineSpacing = 4f;
            reviewScroll = Scroll(resultPage.transform, "Review", out reviewContent);
            Stretch((RectTransform)reviewScroll.transform, Vector2.zero, Vector2.one, new Vector2(0, 96), new Vector2(0, -170));
            var back = Btn(resultPage.transform, "กลับไปห้องสอบ", Accent, Color.white, 24);
            Stretch((RectTransform)back.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-340, 10), new Vector2(0, 80));
            back.onClick.AddListener(ShowCourseList);
        }

        void BuildConfirm(Transform canvas)
        {
            confirmLayer = new GameObject("Confirm", typeof(RectTransform), typeof(Image));
            confirmLayer.transform.SetParent(canvas, false);
            Fill((RectTransform)confirmLayer.transform);
            confirmLayer.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.18f, 0.55f);
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(confirmLayer.transform, false);
            var brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(820, 380);
            UIStyle.Card(box, CardCol);
            confirmText = Text(box.transform, "", 25, Ink, TextAlignmentOptions.Center);
            Stretch(confirmText.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 110), new Vector2(-30, -24));
            confirmCancel = Btn(box.transform, "ยกเลิก", new Color(0.86f, 0.80f, 0.90f), Ink, 24);
            Stretch((RectTransform)confirmCancel.transform, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(30, 24), new Vector2(-12, 94));
            confirmCancel.onClick.AddListener(() => confirmLayer.SetActive(false));
            confirmOk = Btn(box.transform, "ยืนยัน", Accent, Color.white, 24);
            Stretch((RectTransform)confirmOk.transform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(12, 24), new Vector2(-30, 94));
            confirmOk.onClick.AddListener(() =>
            {
                confirmLayer.SetActive(false);
                var a = confirmAction; confirmAction = null;
                a?.Invoke();
            });
            confirmLayer.SetActive(false);
        }

        void BuildChip(Transform canvas)
        {
            chip = new GameObject("MiniChip", typeof(RectTransform), typeof(Image));
            chip.transform.SetParent(canvas, false);
            var rt = (RectTransform)chip.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 150); rt.sizeDelta = new Vector2(1060, 84);
            UIStyle.Card(chip, CardCol);
            chipText = Text(chip.transform, "", 22, Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(chipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 6), new Vector2(-330, -6));
            var b = Btn(chip.transform, "กลับไปทำข้อสอบ (E)", Accent, Color.white, 22);
            Stretch((RectTransform)b.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-316, 12), new Vector2(-14, -12));
            b.onClick.AddListener(ShowExam);
            chip.SetActive(false);
        }

        TMP_FontAsset FindThaiFont()
        {
            var phone = Object.FindFirstObjectByType<PhoneController>(FindObjectsInactive.Include);
            if (phone != null && phone.appBody != null && phone.appBody.font != null) return phone.appBody.font;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.font != null && t.font.HasCharacter('ก')) return t.font;
            return null;
        }

        // ============================================================
        // helpers
        // ============================================================
        // ข้อความจากคลัง/โค้ด → ห้าม TMP ตีความ < > เป็นแท็ก
        static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";

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

        void AddNote(RectTransform content, string s, Color col, float size = 20)
        {
            var t = Text(content, s, size, col, TextAlignmentOptions.TopLeft);
            t.margin = new Vector4(8, 4, 8, 2);
        }

        // กล่องข้อความสูงตามเนื้อหา (ใช้กับโค้ด/คำใบ้/เฉลย)
        void AddBox(RectTransform content, string s, Color bg, Color ink, float size = 20)
        {
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            box.transform.SetParent(content, false);
            var img = box.GetComponent<Image>();
            img.sprite = UIStyle.Rounded; img.type = Image.Type.Sliced; img.color = bg; img.pixelsPerUnitMultiplier = 2.2f;
            var vl = box.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(18, 18, 12, 12); vl.childControlHeight = true; vl.childControlWidth = true;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            var t = Text(box.transform, s, size, ink, TextAlignmentOptions.TopLeft);
            t.lineSpacing = 4f;
        }

        void AddCodeBlock(RectTransform content, string code)
        {
            var lines = code.Replace("\r", "").Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
                sb.Append("<color=#8E88B8>").Append((i + 1).ToString().PadLeft(2)).Append("</color>  ").Append(Esc(lines[i])).Append(i < lines.Length - 1 ? "\n" : "");
            AddBox(content, sb.ToString(), CodeBg, CodeInk, 21);
        }

        // ปุ่มที่สูงตามข้อความ (ตัวเลือก/บล็อกโค้ด) — ใช้ใน VerticalLayoutGroup
        Button FlowButton(Transform parent, string label, Color col, Color textCol, float size)
        {
            var go = new GameObject("Opt", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UIStyle.Rounded; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 2.2f;
            var ol = go.AddComponent<Outline>(); ol.effectColor = new Color(0.30f, 0.25f, 0.46f, 0.35f); ol.effectDistance = new Vector2(1.5f, -1.5f);
            var vl = go.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(18, 18, 10, 10); vl.childControlHeight = true; vl.childControlWidth = true;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childAlignment = TextAnchor.MiddleLeft;
            go.GetComponent<LayoutElement>().minHeight = 52;
            Text(go.transform, label, size, textCol, TextAlignmentOptions.MidlineLeft);
            var b = go.GetComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.06f); cb.pressedColor = Shift(col, -0.10f);
            cb.selectedColor = col; cb.disabledColor = col; cb.colorMultiplier = 1f; cb.fadeDuration = 0.06f; b.colors = cb;
            return b;
        }

        Transform HRow(Transform parent)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            return row.transform;
        }

        Button SimpleButton(Transform parent, string label, Color col, Color textCol, float size)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UIStyle.Rounded; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 2.2f;
            var t = Text(go.transform, label, size, textCol, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold;
            Fill(t.rectTransform);
            var b = go.GetComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.06f); cb.pressedColor = Shift(col, -0.10f);
            cb.selectedColor = col; cb.disabledColor = new Color(col.r, col.g, col.b, 0.45f); b.colors = cb;
            return b;
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
            var cb = b.colors; cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f); b.colors = cb;
            return b;
        }

        void RowButton(Transform row, string label, Color col, System.Action onClick)
        {
            var b = Btn(row, label, col, Color.white, 22);
            Stretch((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-280, -32), new Vector2(-16, 32));
            b.onClick.AddListener(() => onClick());
        }

        void Chip(Transform row, string label, Color col)
        {
            var c = Panel(row, "Chip", new Color(col.r, col.g, col.b, 0.16f), 0f, false);
            Stretch((RectTransform)c.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-280, -28), new Vector2(-16, 28));
            var t = Text(c.transform, label, 21, col, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold;
            Fill(t.rectTransform);
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
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var vl = c.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(12, 12, 12, 12); vl.spacing = 8f;
            vl.childControlHeight = true; vl.childControlWidth = true;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            c.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = (RectTransform)go.transform;
            sr.content = content;
            return sr;
        }

        static Color Shift(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);
    }
}
