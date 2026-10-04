using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.Interaction;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.Academics
{
    public enum ClassEventKind { CalledOn, PopQuiz, Drowsy, FriendNote, ExamHint, Outage }

    // ผลของเหตุการณ์ (ตรรกะล้วน) — ระบบเกมนำไปใช้ต่อผ่าน PlayerStats / RelationshipManager / RegistrationService
    public struct ClassEventOutcome
    {
        public float qualityAdd;        // ปรับ q ของชั่วโมงถัดไปของวิชานี้ (บวก/ลบ)
        public float qualityMul;        // คูณ q ของชั่วโมงถัดไป (1 = ไม่เปลี่ยน)
        public float knowledge, stress, energy;
        public int relation;            // คะแนนสนิทกับเพื่อนที่ส่งโน้ต
        public float bonus;             // คะแนนพิเศษสอบ (0..1)
        public bool bonusFinal;         // true = ปลายภาค, false = กลางภาค
        public bool endClass;           // เลิกเรียนทันที + นับชั่วโมงที่เหลือเต็ม
        public string summary;

        public static ClassEventOutcome None => new ClassEventOutcome { qualityMul = 1f, summary = "" };
    }

    // ===== ตรรกะล้วนของเหตุการณ์สุ่มระหว่างเรียน (ทดสอบด้วย EditMode test) =====
    public static class ClassEventLogic
    {
        public const string Done = "done";
        public const string NoEvent = "-1";

        [Serializable]
        public class Tuning
        {
            [Range(0f, 1f)] public float chance = 0.4f;
            [Range(0f, 1f)] public float windowStart = 0.25f;
            [Range(0f, 1f)] public float windowEnd = 0.75f;
            public float calledOnQuality = 0.3f;
            public float calledOnKnowledge = 5f;
            public float wrongStress = 5f;
            public float quizBonusPerCorrect = 0.02f;
            public float drowsyFightEnergy = -6f;
            public float napQualityMul = 0.5f;
            public float napEnergy = 4f;
            public int noteRelation = 5;
            public float noteQuality = -0.2f;
            public float hintBonus = 0.03f;
            public float outageQuality = 1f;   // คุณภาพชั่วโมงที่นับให้ตอนไฟดับ (อนุมัติ: นับเต็ม q = 1)
        }

        // ---------- แผนต่อคาบ (เก็บใน Enrollment.classEventKeys = "วัน:ลำดับ=ค่า") ----------
        public static string GetState(Enrollment e, string key)
        {
            if (e == null || e.classEventKeys == null) return null;
            string p = key + "=";
            foreach (var k in e.classEventKeys) if (k.StartsWith(p)) return k.Substring(p.Length);
            return null;
        }

        public static void SetState(Enrollment e, string key, string value)
        {
            if (e == null) return;
            if (e.classEventKeys == null) e.classEventKeys = new List<string>();
            string p = key + "=";
            for (int i = 0; i < e.classEventKeys.Count; i++)
                if (e.classEventKeys[i].StartsWith(p)) { e.classEventKeys[i] = p + value; return; }
            e.classEventKeys.Add(p + value);
        }

        // สุ่มครั้งเดียวต่อคาบ: คืนนาทีที่จะเกิด (-1 = คาบนี้ไม่มีเหตุการณ์) · เคยสุ่มแล้ว = ใช้ค่าเดิม (ไม่สุ่มซ้ำหลังลุก/โหลดเซฟ)
        //   เกิดช่วง windowStart–windowEnd ของคาบ · มาสายเลยช่วงไปแล้ว = ไม่เกิด
        public static float PlanOrRoll(Enrollment e, string key, ClassSession s, float nowMinute, System.Random rng, Tuning t)
        {
            string st = GetState(e, key);
            if (st != null)
            {
                if (st == Done) return -1f;
                return float.TryParse(st, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : -1f;
            }
            float minute = -1f;
            if (s != null && rng.NextDouble() < t.chance)
            {
                float len = s.endMinute - s.startMinute;
                float a = s.startMinute + len * t.windowStart, b = s.startMinute + len * t.windowEnd;
                a = Mathf.Max(a, nowMinute);
                if (a < b) minute = Mathf.Floor(a + (float)rng.NextDouble() * (b - a));
            }
            SetState(e, key, minute < 0f ? NoEvent : minute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return minute;
        }

        public static bool ShouldFire(Enrollment e, string key, float nowMinute)
        {
            string st = GetState(e, key);
            if (st == null || st == Done || st == NoEvent) return false;
            return float.TryParse(st, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) && m >= 0f && nowMinute >= m;
        }

        public static void MarkDone(Enrollment e, string key) => SetState(e, key, Done);

        // จำนวนเหตุการณ์ที่เกิดแล้วของคาบนี้ (0/1)
        public static int FiredCount(Enrollment e, string key) => GetState(e, key) == Done ? 1 : 0;

        // ---------- น้ำหนักการสุ่ม ----------
        public static float Weight(ClassEventKind k, float energy, ClassroomType room, bool hasQuestion, bool hasQuiz, bool hasFriend)
        {
            float w;
            switch (k)
            {
                case ClassEventKind.CalledOn: w = hasQuestion ? 20f : 0f; break;
                case ClassEventKind.PopQuiz: w = hasQuiz ? 15f : 0f; break;
                case ClassEventKind.Drowsy: w = 15f * (energy < 40f ? Mathf.Lerp(3f, 1f, Mathf.Clamp01(energy / 40f)) : 1f); break;
                case ClassEventKind.FriendNote: w = hasFriend ? 15f : 0f; break;
                case ClassEventKind.ExamHint: w = 10f; break;
                default: w = 5f; break;   // Outage
            }
            switch (room)
            {
                case ClassroomType.ComputerLab:
                    if (k == ClassEventKind.CalledOn) w *= 0.7f;
                    if (k == ClassEventKind.PopQuiz) w *= 1.2f;
                    if (k == ClassEventKind.Outage) w *= 1.5f;   // คอมค้าง
                    break;
                case ClassroomType.Seminar:
                    if (k == ClassEventKind.CalledOn) w *= 1.5f;
                    if (k == ClassEventKind.FriendNote) w *= 0.5f;
                    break;
                case ClassroomType.Lecture:
                    if (k == ClassEventKind.CalledOn) w *= 1.2f;
                    if (k == ClassEventKind.FriendNote) w *= 1.2f;
                    break;
            }
            return Mathf.Max(0f, w);
        }

        public static ClassEventKind Pick(System.Random rng, float energy, ClassroomType room, bool hasQuestion, bool hasQuiz, bool hasFriend)
        {
            var kinds = (ClassEventKind[])Enum.GetValues(typeof(ClassEventKind));
            float sum = 0f;
            foreach (var k in kinds) sum += Weight(k, energy, room, hasQuestion, hasQuiz, hasFriend);
            double r = rng.NextDouble() * sum;
            foreach (var k in kinds)
            {
                r -= Weight(k, energy, room, hasQuestion, hasQuiz, hasFriend);
                if (r < 0) return k;
            }
            return ClassEventKind.ExamHint;
        }

        // ---------- ผล ----------
        public static ClassEventOutcome CalledOn(bool correct, Tuning t)
        {
            var o = ClassEventOutcome.None;
            if (correct) { o.qualityAdd = t.calledOnQuality; o.knowledge = t.calledOnKnowledge; o.summary = $"ตอบถูก! ชั่วโมงนี้เรียนได้ดีขึ้น (+{t.calledOnQuality:0.0} คุณภาพ) ความรู้ +{t.calledOnKnowledge:0}"; }
            else { o.stress = t.wrongStress; o.summary = $"ตอบผิด... อายเพื่อนนิดหน่อย (เครียด +{t.wrongStress:0})"; }
            return o;
        }

        public static ClassEventOutcome PopQuiz(int correct, int total, bool nextIsFinal, Tuning t)
        {
            var o = ClassEventOutcome.None;
            o.bonus = Mathf.Max(0, correct) * t.quizBonusPerCorrect;
            o.bonusFinal = nextIsFinal;
            o.summary = $"ควิซย่อย ถูก {correct}/{total} ข้อ → คะแนนพิเศษสอบ{(nextIsFinal ? "ปลายภาค" : "กลางภาค")} +{o.bonus * 100f:0} คะแนน";
            return o;
        }

        public static ClassEventOutcome Drowsy(bool nap, Tuning t)
        {
            var o = ClassEventOutcome.None;
            if (nap) { o.qualityMul = t.napQualityMul; o.energy = t.napEnergy; o.summary = $"งีบไปแป๊บ... สดชื่นขึ้น (พลังงาน +{t.napEnergy:0}) แต่ชั่วโมงนี้ได้ความรู้น้อยลง"; }
            else { o.energy = t.drowsyFightEnergy; o.summary = $"ฝืนเรียนต่อ (พลังงาน {t.drowsyFightEnergy:0})"; }
            return o;
        }

        public static ClassEventOutcome FriendNote(bool chat, Tuning t)
        {
            var o = ClassEventOutcome.None;
            if (chat) { o.relation = t.noteRelation; o.qualityAdd = t.noteQuality; o.summary = $"คุยกับเพื่อนสนุกดี (สนิทขึ้น +{t.noteRelation}) แต่ฟังไม่ค่อยทัน"; }
            else o.summary = "ตั้งใจเรียนต่อ — เก็บโน้ตไว้คุยตอนพัก";
            return o;
        }

        public static ClassEventOutcome ExamHint(bool takeNote, Tuning t)
        {
            var o = ClassEventOutcome.None;
            if (takeNote) { o.bonus = t.hintBonus; o.bonusFinal = true; o.summary = $"จดไว้แล้ว! คะแนนพิเศษสอบปลายภาค +{t.hintBonus * 100f:0} คะแนน"; }
            else o.summary = "ปล่อยผ่านไป...";
            return o;
        }

        public static ClassEventOutcome Outage(ClassroomType room)
        {
            var o = ClassEventOutcome.None;
            o.endClass = true;
            o.summary = room == ClassroomType.ComputerLab ? "คอมค้างทั้งห้อง — อาจารย์ให้เลิกเรียน นับชั่วโมงที่เหลือของคาบให้เต็ม"
                                                           : "ไฟดับ แอร์เสีย — อาจารย์ให้เลิกเรียน นับชั่วโมงที่เหลือของคาบให้เต็ม";
            return o;
        }

        public static float ApplyQuality(float q, float mul, float add) => Mathf.Clamp01(q * mul + add);

        // คำถามที่ใช้ในเหตุการณ์ได้ (ตอบด้วยการเลือกข้อเดียว)
        public static List<ExamQuestion> UsableQuestions(CourseExamBank bank)
        {
            var l = new List<ExamQuestion>();
            if (bank == null || bank.questions == null) return l;
            foreach (var q in bank.questions)
                if (q != null && (q.type == ExamQuestionType.MultipleChoice || q.type == ExamQuestionType.FindError) && q.items != null && q.items.Count >= 2
                    && q.correctIndex >= 0 && q.correctIndex < q.items.Count)
                    l.Add(q);
            return l;
        }
    }

    // ===== ตัวจัดการเหตุการณ์สุ่มระหว่างเรียน (เล่นคนเดียว) — สร้างโดย GameplayBootstrap =====
    //   คาบละไม่เกิน 1 เหตุการณ์ · ตอนเกิด: หยุดเร่งและหยุดเวลา (หน้าต่าง) → เลือก → กลับมาเร่งต่อ
    public class ClassEventSystem : MonoBehaviour
    {
        public static ClassEventSystem Instance { get; private set; }

        public ClassEventLogic.Tuning tuning = new ClassEventLogic.Tuning();
        [Tooltip("จำนวนข้อของควิซย่อย (สุ่มระหว่างค่าต่ำสุด–สูงสุด)")]
        public int quizMin = 2, quizMax = 3;

        readonly Dictionary<string, (float mul, float add)> pendingQuality = new Dictionary<string, (float, float)>();
        System.Random rng = new System.Random(Environment.TickCount);

        public static ClassEventSystem EnsureExists()
        {
            if (Instance != null) return Instance;
            return new GameObject("ClassEventSystem").AddComponent<ClassEventSystem>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ใช้กับ RegistrationService.QualityModifier — ปรับ q ของชั่วโมงถัดไปที่นับให้วิชานั้น (ใช้แล้วหมด)
        public static float ModifyQuality(string code, float q)
        {
            var s = Instance;
            if (s == null || code == null || !s.pendingQuality.TryGetValue(code, out var m)) return q;
            s.pendingQuality.Remove(code);
            return ClassEventLogic.ApplyQuality(q, m.mul, m.add);
        }

        public void OnClassStarted(ClassWarpController c, float now)
        {
            if (c == null || c.Enrollment == null || !ClassroomRules.IsSinglePlayer) return;
            string key = RegistrationService.MeetingKey(c.SemDay, c.SessionIndex);
            float m = ClassEventLogic.PlanOrRoll(c.Enrollment, key, c.Session, now, rng, tuning);
            if (m >= 0f) Debug.Log($"[ClassEvent] {c.Enrollment.code} คาบ {key}: จะเกิดเหตุการณ์ราว {(int)m / 60:00}:{(int)m % 60:00}");
        }

        public void Tick(ClassWarpController c, float now)
        {
            if (c == null || !c.InClass || c.Enrollment == null || ClassEventUI.IsOpen) return;
            string key = RegistrationService.MeetingKey(c.SemDay, c.SessionIndex);
            if (!ClassEventLogic.ShouldFire(c.Enrollment, key, now)) return;
            ClassEventLogic.MarkDone(c.Enrollment, key);   // คาบละไม่เกิน 1 — บันทึกก่อนแสดง
            Fire(c);
        }

        // ---------- เลือก + แสดงเหตุการณ์ ----------
        public void Fire(ClassWarpController c, ClassEventKind? force = null)
        {
            var e = c.Enrollment;
            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            float energy = stats != null ? stats.Energy : 100f;
            var room = ClassroomCatalog.LoadDefault().Get(c.RoomId);
            var roomType = room != null ? room.type : ClassroomType.Lecture;
            var bank = ExamBankDatabase.LoadDefault() != null ? ExamBankDatabase.LoadDefault().Get(e.code) : null;
            var qs = ClassEventLogic.UsableQuestions(bank);
            // เพื่อนที่ส่งโน้ตได้ = NPC ที่รู้จักในระบบความสัมพันธ์ (ยกเว้นอาจารย์)
            var friends = new List<string>();
            if (RelationshipManager.Instance != null)
                foreach (var id in RelationshipManager.Instance.AllIds)
                    if (!RelationshipManager.Instance.DisplayName(id).Contains("อาจารย์")) friends.Add(id);
            bool hasFriend = friends.Count > 0;
            var kind = force ?? ClassEventLogic.Pick(rng, energy, roomType, qs.Count >= 1, qs.Count >= quizMin, hasFriend);
            GameClock_Stop();
            Debug.Log($"[ClassEvent] {e.code} @ {c.RoomId}: {kind}");

            switch (kind)
            {
                case ClassEventKind.CalledOn:
                {
                    var q = qs[rng.Next(qs.Count)];
                    AskQuestion($"อาจารย์สุ่มเรียกตอบ! ({e.code})", q, correct => Finish(c, ClassEventLogic.CalledOn(correct, tuning)));
                    break;
                }
                case ClassEventKind.PopQuiz:
                {
                    int n = Mathf.Clamp(rng.Next(quizMin, quizMax + 1), 1, qs.Count);
                    var pick = new List<ExamQuestion>(qs);
                    for (int i = pick.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = pick[i]; pick[i] = pick[j]; pick[j] = tmp; }
                    pick = pick.GetRange(0, n);
                    RunQuiz(c, e, pick, 0, 0);
                    break;
                }
                case ClassEventKind.Drowsy:
                    ClassEventUI.Show("ง่วงจนตาจะปิด...", $"เสียงอาจารย์เหมือนเพลงกล่อมนอน (พลังงาน {energy:0})\nจะทำยังไงดี?",
                        new[] { $"ฝืนเรียน (พลังงาน {tuning.drowsyFightEnergy:0})", $"งีบแป๊บ (ชั่วโมงนี้ได้ความรู้ ×{tuning.napQualityMul:0.#}, พลังงาน +{tuning.napEnergy:0})" },
                        i => Finish(c, ClassEventLogic.Drowsy(i == 1, tuning)));
                    break;
                case ClassEventKind.FriendNote:
                {
                    string id = friends[rng.Next(friends.Count)];
                    string name = RelationshipManager.Instance.DisplayName(id);
                    ClassEventUI.Show("เพื่อนส่งโน้ตมา", $"{name} ส่งกระดาษมา: \"เย็นนี้ไปไหนกัน?\"",
                        new[] { $"คุยด้วย (สนิทขึ้น +{tuning.noteRelation}, ฟังไม่ทัน)", "ไม่สนใจ ตั้งใจเรียน" },
                        i =>
                        {
                            var o = ClassEventLogic.FriendNote(i == 0, tuning);
                            if (o.relation != 0 && RelationshipManager.Instance != null) RelationshipManager.Instance.AddPoints(id, name, o.relation);
                            o.relation = 0;
                            Finish(c, o);
                        });
                    break;
                }
                case ClassEventKind.ExamHint:
                    ClassEventUI.Show("อาจารย์ใบ้ข้อสอบ!", $"\"ตรงนี้ออกสอบปลายภาคนะ ใครจดไว้ได้เปรียบ\" ({e.code})",
                        new[] { $"จดไว้ (สอบปลายภาค +{tuning.hintBonus * 100f:0} คะแนน)", "ไม่สนใจ" },
                        i => Finish(c, ClassEventLogic.ExamHint(i == 0, tuning)));
                    break;
                default:
                {
                    var o = ClassEventLogic.Outage(roomType);
                    ClassEventUI.Show(roomType == ClassroomType.ComputerLab ? "คอมค้างทั้งห้อง!" : "ไฟดับ แอร์เสีย!", o.summary, new[] { "ตกลง" }, _ => Finish(c, o, showResult: false));
                    break;
                }
            }
        }

        static readonly string[] Letters = { "ก", "ข", "ค", "ง", "จ", "ฉ" };

        void AskQuestion(string title, ExamQuestion q, Action<bool> done)
        {
            string body = q.prompt + (string.IsNullOrEmpty(q.code) ? "" : $"\n<size=80%><color=#5A5378>{q.code}</color></size>");
            if (q.type == ExamQuestionType.FindError) body += "\n<size=80%>(เลือกบรรทัด/ส่วนที่ผิด)</size>";
            var opts = new string[q.items.Count];
            for (int i = 0; i < opts.Length; i++)
            {
                string it = q.items[i] ?? "";
                if (it.Length > 70) it = it.Substring(0, 67) + "...";
                opts[i] = (q.type == ExamQuestionType.FindError ? $"{i + 1}) " : $"{Letters[i % Letters.Length]}. ") + it;
            }
            ClassEventUI.Show(title, body, opts, i => done(i == q.correctIndex));
        }

        void RunQuiz(ClassWarpController c, Enrollment e, List<ExamQuestion> qs, int index, int correct)
        {
            if (index >= qs.Count)
            {
                Finish(c, ClassEventLogic.PopQuiz(correct, qs.Count, RegistrationService.NextExamIsFinal(e), tuning));
                return;
            }
            AskQuestion($"ควิซย่อย ข้อ {index + 1}/{qs.Count} ({e.code})", qs[index], ok => RunQuiz(c, e, qs, index + 1, correct + (ok ? 1 : 0)));
        }

        // ใช้ผล → ระบบเดิม แล้วแสดงสรุป (กดตกลง = ปิดหน้าต่าง → ClassWarpController เร่งต่อ)
        void Finish(ClassWarpController c, ClassEventOutcome o, bool showResult = true)
        {
            Apply(c, o);
            if (showResult && !string.IsNullOrEmpty(o.summary)) ClassEventUI.Show("ผลลัพธ์", o.summary, new[] { "ตกลง" }, _ => ClassEventUI.Close());
            else ClassEventUI.Close();
            if (o.endClass && c != null && c.Enrollment != null)
            {
                var reg = CourseRegistrar.Instance;
                int added = reg != null && reg.Service != null ? reg.Service.CreditRemainingMeeting(c.Enrollment, c.SemDay, c.SessionIndex, tuning.outageQuality) : 0;
                int t = c.Enrollment.TicksFor(RegistrationService.MeetingKey(c.SemDay, c.SessionIndex));
                string code = c.Enrollment.code; int max = c.Session != null ? c.Session.MaxTicks : t;
                reg?.NotifyChanged();
                c.EndClassEarly($"เลิกเรียนก่อนเวลา — {code} นับให้ครบ {t}/{max} ชม. (+{added})");
            }
        }

        public void Apply(ClassWarpController c, ClassEventOutcome o)
        {
            var e = c != null ? c.Enrollment : null;
            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null)
            {
                if (o.knowledge != 0f) stats.ChangeKnowledge(o.knowledge);
                if (o.stress != 0f) stats.ChangeStress(o.stress);
                if (o.energy != 0f) stats.ChangeEnergy(o.energy);
            }
            if (e != null && (o.qualityAdd != 0f || Mathf.Abs(o.qualityMul - 1f) > 1e-4f))
                pendingQuality[e.code] = (o.qualityMul, o.qualityAdd);
            if (e != null && o.bonus > 0f) RegistrationService.AddClassBonus(e, o.bonusFinal, o.bonus);
            StatsTracker.Instance.Add("classEvents", 1);
            GameplayEvents.Raise(GameplayEvents.ClassEvent);
            CourseRegistrar.Instance?.NotifyChanged();
        }

        // ให้ PendingQuality ของวิชา (เทสต์/Dev)
        public bool HasPendingQuality(string code) => code != null && pendingQuality.ContainsKey(code);

        static void GameClock_Stop() { NisitSimulator.TimeSystem.GameClock.WarpMultiplier = 1f; }
    }

    // ===== หน้าต่างเหตุการณ์ระหว่างเรียน (สร้างตอนรัน ฟอนต์ไทย + ธีมพาสเทลเดียวกับสรุปวัน) =====
    //   เปิด = หยุดเวลา (timeScale 0) เหมือนหน้าต่างอื่นของเกม · ปิด = คืนค่าเดิม
    public class ClassEventUI : MonoBehaviour
    {
        static ClassEventUI _i;
        public static bool IsOpen => _i != null && _i.open;

        GameObject root; TMP_Text title, body;
        RectTransform optionsRoot;
        readonly List<Button> buttons = new List<Button>();
        bool open; float prevTimeScale = 1f;

        static ClassEventUI Get()
        {
            if (_i == null) _i = new GameObject("ClassEventUI").AddComponent<ClassEventUI>();
            if (_i.root == null) _i.Build();
            return _i;
        }

        void OnDestroy()
        {
            if (open) Time.timeScale = prevTimeScale;
            if (_i == this) _i = null;
        }

        public static void Show(string title, string body, string[] options, Action<int> onPick)
        {
            var u = Get();
            if (!u.open)
            {
                u.prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                u.open = true;
            }
            u.root.SetActive(true);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            u.title.text = title;
            u.body.text = body;
            foreach (var b in u.buttons) Destroy(b.gameObject);
            u.buttons.Clear();
            int n = options != null ? options.Length : 0;
            float h = 58f, gap = 10f;
            float top = (n - 1) * (h + gap) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                var b = GrowthUI.Button(u.optionsRoot, options[i], new Vector2(0f, top - i * (h + gap)), new Vector2(760f, h), i == 0 ? new Color(0.78f, 0.90f, 0.98f) : new Color(0.93f, 0.86f, 0.98f), 22);
                b.onClick.AddListener(() => { NisitSimulator.Core.SFXManager.Page(); onPick?.Invoke(k); });
                u.buttons.Add(b);
            }
            NisitSimulator.Core.SFXManager.Notify();
        }

        public static void Close()
        {
            if (_i == null || !_i.open) return;
            _i.open = false;
            _i.root.SetActive(false);
            Time.timeScale = _i.prevTimeScale;
        }

        void Build()
        {
            var c = GrowthUI.MakeCanvas(transform, "Class Event Canvas", 96);
            root = c.gameObject;
            GrowthUI.Dim(c.transform);
            var card = GrowthUI.Box(c.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f), GrowthUI.CardCol);
            GrowthUI.Box(card.transform, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(560f, 80f), GrowthUI.Strip, false);
            title = GrowthUI.Text(card.transform, "", new Vector2(0f, 264f), new Vector2(820f, 56f), 36, GrowthUI.Title);
            body = GrowthUI.Text(card.transform, "", new Vector2(0f, 120f), new Vector2(800f, 200f), 24, GrowthUI.Ink, TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.Normal;
            var opt = new GameObject("Options", typeof(RectTransform));
            opt.transform.SetParent(card.transform, false);
            optionsRoot = (RectTransform)opt.transform;
            optionsRoot.anchorMin = optionsRoot.anchorMax = optionsRoot.pivot = new Vector2(0.5f, 0.5f);
            optionsRoot.anchoredPosition = new Vector2(0f, -140f);
            optionsRoot.sizeDelta = new Vector2(780f, 330f);
            root.SetActive(false);
        }
    }
}
