using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Academics
{
    // ===== ตัวเชื่อมระบบลงทะเบียนกับเกม =====
    //   • ฟัง ProgressionManager (วันในปี) → เปิด/ปิดภาค, ประกาศเกรด, เลื่อนชั้นปี, จบการศึกษา
    //   • ActivitySpot (โต๊ะเรียน) แจ้งทุกชั่วโมงที่นั่งเรียน → นับเข้าเรียนตามตาราง
    //   • ExamController ส่งผลสอบ/ขาดสอบมาบันทึกรายวิชา
    //   • Save/Load ผ่าน SaveManager (SaveData.academic)
    //   ทำงานเฉพาะคณะที่หลักสูตรกำหนด (ค่าเริ่มต้น = คณะ index 0 สายคอมพิวเตอร์) คณะอื่นใช้ระบบเดิมทั้งหมด
    //   Multiplayer: เกมนี้จำลองสถานะผู้เล่นฝั่ง client เอง (ดู CoopBonus/NetworkAvatar) ข้อมูลการเรียนจึงแยกต่อผู้เล่นอยู่แล้ว
    //   ถูกเพิ่มลง GameManager อัตโนมัติโดย GameplayBootstrap (EnsureExists) — ไม่ต้องแก้ฉาก
    public class CourseRegistrar : MonoBehaviour
    {
        public static CourseRegistrar Instance { get; private set; }
        public static bool Active => Instance != null && Instance.IsActive;

        [Tooltip("ปล่อยว่าง = โหลด Resources/Curricula/CS_Curriculum")]
        public CurriculumDefinition curriculum;
        [Tooltip("> 0 = เพดานหน่วยกิตต่อภาค (แทนค่าในหลักสูตร)")]
        public int creditCapOverride = 0;
        [Tooltip("หน่วงก่อนขึ้นหน้าจบการศึกษา (วินาทีจริง) ให้ผู้เล่นเห็นประกาศผลก่อน")]
        public float graduationDelay = 2.5f;

        public bool IsActive { get; private set; }
        public RegistrationService Service { get; private set; }
        public AcademicRecord Record => Service != null ? Service.Record : null;
        public int ClassYear => Record != null ? Record.classYear : 1;

        public event Action OnChanged;

        ProgressionManager prog;
        GameClock clock;
        bool initialized;
        bool ending;
        readonly Dictionary<Transform, string> spotBuilding = new Dictionary<Transform, string>();

        public static CourseRegistrar EnsureExists()
        {
            if (Instance != null) return Instance;
            var host = GameObject.Find("GameManager");
            if (host == null) host = new GameObject("CourseRegistrar");
            var r = host.GetComponent<CourseRegistrar>();
            if (r == null) r = host.AddComponent<CourseRegistrar>();
            return r;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (curriculum == null) curriculum = CurriculumDefinition.LoadDefault();
            prog = UnityEngine.Object.FindFirstObjectByType<ProgressionManager>();
            clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
            if (prog != null) prog.OnDayInYearChangedEarly += HandleDay;
        }

        void Start()
        {
            if (!initialized) InitializeNew();
        }

        void OnDestroy()
        {
            if (prog != null) prog.OnDayInYearChangedEarly -= HandleDay;
            if (Instance == this) Instance = null;
        }

        void OnValidate()
        {
            if (Service != null) Service.CreditCapOverride = creditCapOverride;
        }

        // ---------- เริ่ม / โหลด ----------
        public void InitializeNew()
        {
            var rec = new AcademicRecord { classYear = prog != null ? prog.CurrentYear : 1 };
            Setup(rec);
            if (IsActive) SyncTerm(prog != null ? prog.DayInYear : 1, announce: true);
        }

        float MinGpa => prog != null ? prog.minGpa : (curriculum != null ? curriculum.minGraduationGpa : 2f);

        // ผู้เล่นกดตกลงในหน้าผลเทอม
        public void MarkReportSeen(int serial)
        {
            if (Record == null) return;
            if (serial > Record.lastReportedSerial) Record.lastReportedSerial = serial;
            if (Record.pendingReportSerial <= Record.lastReportedSerial) Record.pendingReportSerial = 0;
        }

        void Setup(AcademicRecord rec)
        {
            IsActive = curriculum != null && curriculum.AppliesToFaculty(GameSession.SelectedFacultyIndex);
            // ส่ง ClassroomCatalog → ตรวจห้องชน/ห้องไม่มีจริงตอนเริ่ม (วิชาที่ข้อมูลเสียจะลงทะเบียนไม่ได้ ไม่ทำให้เกมพัง)
            Service = new RegistrationService(curriculum, rec, ClassroomCatalog.LoadDefault())
            {
                CreditCapOverride = creditCapOverride,
                QualityModifier = ClassEventSystem.ModifyQuality,   // ผลเหตุการณ์ระหว่างเรียนต่อคุณภาพชั่วโมงถัดไป
            };
            initialized = true;
            ending = false;
            if (IsActive && prog != null) prog.SetClassYear(rec.classYear);
            // โหลดเซฟที่ยังไม่ได้กดตกลงหน้าผลเทอม → แสดงอีกครั้ง
            if (IsActive && rec.HasPendingReport)
                TermResultUI.Queue(Service.BuildTermReport(rec.pendingReportSerial, MinGpa, rec.pendingReportPromoted, rec.pendingReportExhausted, rec.pendingReportExtra));
            OnChanged?.Invoke();
        }

        // เรียกจาก SaveManager.ApplyIfPending ก่อน ProgressionManager.RestoreState
        public void RestoreFrom(SaveData d)
        {
            if (d == null) { InitializeNew(); return; }
            if (d.hasAcademicRecord && d.academic != null)
            {
                Setup(d.academic);
                return;
            }
            // เซฟเก่า (ก่อนมีระบบลงทะเบียน) → migration: คงชั้นปี/วันเดิม ภาคที่ผ่านมาแล้วเทียบโอน
            int year = Mathf.Max(1, d.currentYear);
            int cal = d.calendarYear > 0 ? d.calendarYear : year;
            int sem = AcademicCalendar.SemesterIndex(d.dayInYear);
            var rec = curriculum != null ? RegistrationService.MigrateLegacy(curriculum, year, sem) : new AcademicRecord { classYear = year };
            Setup(rec);
            if (IsActive)
            {
                Service.OpenTerm(cal, sem, lateRegistration: true);
                Debug.Log($"[Registrar] ย้ายเซฟเก่า: ชั้นปี {year} เทียบโอน {Service.EarnedCredits()} หน่วยกิต · ลงทะเบียนภาคนี้ได้ถึงสิ้นภาค");
            }
        }

        public void CollectSave(SaveData d)
        {
            if (Record == null) return;
            d.hasAcademicRecord = true;
            d.academic = Record;
        }

        // ---------- ปฏิทิน ----------
        public int SemDay => AcademicCalendar.SemesterDay(prog != null ? prog.DayInYear : 1);

        void HandleDay(int dayInYear, int daysPerYear)
        {
            if (!initialized || !IsActive) return;
            SyncTerm(dayInYear, announce: true);
        }

        void SyncTerm(int dayInYear, bool announce)
        {
            if (Service == null || prog == null) return;
            int sem = AcademicCalendar.SemesterIndex(dayInYear);
            int semDay = AcademicCalendar.SemesterDay(dayInYear);
            int cal = prog.CalendarYear;
            var cur = Record.Current;

            if (cur == null || cur.calendarYear != cal || cur.semIndex != sem)
            {
                var res = Service.CloseCurrentTerm();
                if (res != null) AnnounceClose(res);
                if (Record.graduated || ending) { OnChanged?.Invoke(); return; }
                var t = Service.OpenTerm(cal, sem);
                if (announce) AnnounceOpen(t);
            }
            else if (Service.CheckRegistrationDeadline(semDay, out var msg))
            {
                if (announce && msg != null) HUDController.Toast(msg);
            }
            prog.SetClassYear(Record.classYear);
            OnChanged?.Invoke();
        }

        void AnnounceOpen(TermState t)
        {
            if (t.isBreak) { HUDController.Toast("ปิดภาคฤดูร้อน — ไม่มีการเรียนการสอน พักผ่อน/ทำงานได้เต็มที่"); return; }
            string kind = t.isExtra ? "ภาคเรียนเพิ่มเติม" : $"ชั้นปี {t.classYear} ภาค {t.planSemester}";
            HUDController.Toast($"เปิดลงทะเบียน {kind} — กด TAB ▸ ลงทะเบียนเรียน (ภายในวันแรกของภาค)");
        }

        void AnnounceClose(TermCloseResult r)
        {
            if (r.term == null || r.term.isBreak) return;
            if (r.graded.Count > 0)
            {
                int pass = 0; foreach (var e in r.graded) if (e.passed) pass++;
                HUDController.Toast($"ประกาศผลภาค: ผ่าน {pass}/{r.graded.Count} วิชา · GPA ภาค {r.termGpa:0.00}");
                // หน้าแสดงผลตอนจบเทอม (ขึ้นหลังสรุปวัน · ครั้งเดียวต่อเทอม · ปิดเกมก่อนกดตกลง → โหลดแล้วขึ้นอีก)
                Record.pendingReportSerial = r.term.serial;
                Record.pendingReportPromoted = r.promoted;
                Record.pendingReportExhausted = r.exhausted;
                Record.pendingReportExtra = r.term.isExtra;
                TermResultUI.Queue(Service.BuildTermReport(r, MinGpa));
            }
            if (r.promoted) StartCoroutine(DelayedToast($"เลื่อนเป็นชั้นปี {r.newClassYear}! (หน่วยกิตสะสม {Service.EarnedCredits()})", 2.6f));
            else if (r.term.planSemester == 2 && !r.term.isExtra && r.term.classYear < 4)
                StartCoroutine(DelayedToast($"ยังไม่เลื่อนชั้นปี — ต้องมีหน่วยกิตสะสม ≥ {curriculum.PromotionThreshold(r.term.classYear)} (ตอนนี้ {Service.EarnedCredits()})", 2.6f));

            if (r.graduated)
            {
                ending = true;
                StartCoroutine(EndAfter(EndReason.Graduated, "ครบเงื่อนไขจบการศึกษา! ยินดีด้วย"));
            }
            else if (r.exhausted)
            {
                ending = true;
                StartCoroutine(EndAfter(EndReason.Flunked, $"เรียนครบ {curriculum.maxRegularTerms} ภาคแล้วยังไม่จบ — พ้นสภาพนิสิต"));
            }
            else if (r.term.classYear >= 4 && r.term.planSemester == 2 && Record.finishedPlan)
                StartCoroutine(DelayedToast("ยังไม่ครบเงื่อนไขจบ — เปิดภาคเรียนเพิ่มเติมให้เก็บวิชาค้าง (ดูแอปลงทะเบียน ▸ ผลการเรียน)", 5f));
        }

        IEnumerator DelayedToast(string msg, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            HUDController.Toast(msg);
        }

        IEnumerator EndAfter(EndReason reason, string msg)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            // รอให้ผู้เล่นอ่านผลเทอมก่อนขึ้นหน้าจบเกม (หน้าสรุปวัน → ผลเทอม → จบ)
            while (TermResultUI.Busy || DaySummaryUI.IsShowing) yield return null;
            HUDController.Toast(msg);
            yield return new WaitForSecondsRealtime(graduationDelay);
            GameManager.Instance?.EndGame(reason);
        }

        // เรียกจาก ProgressionManager ตอนครบปี (โหมดหลักสูตร) — คืน true ถ้าเกมจบแล้ว/กำลังจบ
        public bool HandleAcademicYearEnd(float minGpa, int gpaCheckFromCalendarYear, int calendarYear, out string probation)
        {
            probation = "";
            if (Service == null) return false;
            if (ending || Record.graduated) return true;
            if (Service.HasGpa && Service.Gpa() < minGpa)
            {
                if (calendarYear >= gpaCheckFromCalendarYear)
                {
                    GameManager.Instance?.EndGame(EndReason.RetiredGPA);
                    return true;
                }
                probation = $"\nติดโปร! GPA {Service.Gpa():0.00} ต่ำกว่า {minGpa:0.00} — ต้องดึงขึ้นก่อนสิ้นปีการศึกษาที่ {gpaCheckFromCalendarYear}";
            }
            return false;
        }

        // ---------- เข้าเรียน (เรียกจาก ActivitySpot / ClassStation) ----------
        // คืนข้อความเสริมสำหรับ toast (ว่าง = ไม่เกี่ยวกับรายวิชา)
        public static string NotifyStudyTick(Transform spot, float gainedKnowledge, float baseKnowledge)
        {
            if (!Active || spot == null) return "";
            return Instance.StudyTick(spot, gainedKnowledge, baseKnowledge);
        }

        string StudyTick(Transform spot, float gained, float baseK)
        {
            var t = Record.Current;
            if (t == null || t.isBreak || !t.confirmed || t.closed) return "";
            float q = baseK > 0f ? gained / baseK : 0f;
            float minute = clock != null ? clock.TotalMinutes : 0f;
            string building = BuildingOf(spot);
            // เล่นคนเดียว: นับตามห้อง (ClassroomZone ของที่นั่งต้องตรงกับ roomId ของคาบ) · multiplayer: เช็กตึกแบบเดิม
            // TODO(multiplayer): host validation ของห้อง/ชั่วโมงเรียนก่อนเปิดการนับตามห้องในโหมดหลายคน
            bool roomMode = ClassroomRules.IsSinglePlayer;
            string roomId = roomMode ? NisitSimulator.Interaction.ClassroomZone.RoomOfSpot(spot) : null;
            var r = Service.RecordStudyTick(SemDay, minute, building, roomId, q, roomMode);
            OnChanged?.Invoke();
            if (r.attended != null)
            {
                if (r.tickNo == 1)   // นับหนึ่งคาบต่อหนึ่งครั้งที่เข้า (สถิติ/ภารกิจ "เข้าเรียน")
                {
                    StatsTracker.Instance.Add("classes", 1);
                    GameplayEvents.Raise(GameplayEvents.Class);
                }
                return $"\nเข้าเรียน {r.attended.code} ชั่วโมงที่ {r.tickNo}/{r.tickMax} ({Mathf.RoundToInt(Service.StudyRatio(r.attended) * 100)}% ของวิชา)";
            }
            if (r.wrongRoomCode != null) return $"\nคาบ {r.wrongRoomCode} เรียนที่ {RoomText(r.wrongRoomTarget)} (ที่นี่ไม่นับเข้าเรียน)";
            if (r.wrongBuildingCode != null) return $"\nคาบ {r.wrongBuildingCode} เรียนที่ {r.wrongBuildingTarget} (ที่นี่ไม่นับเข้าเรียน)";
            if (r.capped) return "\nคาบนี้นับชั่วโมงเรียนครบแล้ว";
            if (r.selfStudy != null) return $"\nอ่านทบทวน → ชดเชย {r.selfStudy.code}";
            return "";
        }

        // หาตึกของจุดเรียน = Spawn_<ชื่อ> ที่ใกล้ที่สุดใน Interiors
        public string BuildingOf(Transform spot)
        {
            if (spotBuilding.TryGetValue(spot, out var b)) return b;
            // ที่นั่งในห้องเรียนจริง (ตึกเดินเข้าได้) → ใช้ชื่อตึกแบบเดิมของห้อง (multiplayer ยังเช็กตึกเหมือนเดิม)
            var zone = NisitSimulator.Interaction.ClassroomZone.ZoneOfSpot(spot);
            if (zone != null && !string.IsNullOrEmpty(zone.legacyBuilding)) { spotBuilding[spot] = zone.legacyBuilding; return zone.legacyBuilding; }
            string best = null; float bd = 20f;
            var root = GameObject.Find("Interiors");
            if (root != null)
                foreach (Transform c in root.transform)
                {
                    if (!c.name.StartsWith("Spawn_")) continue;
                    var p = c.position; var q = spot.position;
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                    if (d < bd) { bd = d; best = c.name.Substring(6); }
                }
            spotBuilding[spot] = best;   // null = ไม่อยู่ในตึกใด → ไม่เช็กตึก
            return best;
        }

        // คาบที่กำลังเรียนอยู่ ณ ตอนนี้ (ใช้โชว์ในโทรศัพท์/ClassStation)
        public bool TryGetOngoingSession(out Enrollment enrollment, out ClassSession session)
            => TryGetOngoingSession(out enrollment, out session, out _);

        public bool TryGetOngoingSession(out Enrollment enrollment, out ClassSession session, out int sessionIndex)
        {
            enrollment = null; session = null; sessionIndex = -1;
            if (!IsActive || Service == null) return false;
            float m = clock != null ? clock.TotalMinutes : 0f;
            int d = SemDay;
            foreach (var e in Service.CurrentEnrollments())
            {
                var ss = Service.SessionsFor(e);
                for (int i = 0; i < ss.Count; i++)
                {
                    var s = ss[i];
                    if (s.day == d && m >= s.startMinute && m < s.endMinute) { enrollment = e; session = s; sessionIndex = i; return true; }
                }
            }
            return false;
        }

        // คาบที่กำลังเรียนหรือคาบถัดไป (วันนี้ก่อน แล้ววันถัดไปของภาค) — ใช้กับปุ่มนำทาง/ตู้เข้าเรียน
        public bool TryGetNextSession(out Enrollment enrollment, out ClassSession session, out int day)
        {
            enrollment = null; session = null; day = 0;
            if (!IsActive || Service == null) return false;
            float m = clock != null ? clock.TotalMinutes : 0f;
            int today = SemDay;
            float best = float.MaxValue;
            foreach (var e in Service.CurrentEnrollments())
            {
                if (e.graded) continue;
                foreach (var s in Service.SessionsFor(e))
                {
                    if (s.day < today || (s.day == today && m >= s.endMinute)) continue;
                    float key = (s.day - today) * 1440f + s.startMinute;
                    if (key < best) { best = key; enrollment = e; session = s; day = s.day; }
                }
            }
            return session != null;
        }

        // "อาคาร IT · ชั้น 2 · IT-202" (ไม่มีห้องในทะเบียน = ชื่อตึกเดิม)
        public static string RoomText(ClassSession s)
        {
            if (s == null) return "";
            if (!s.HasRoom) return string.IsNullOrEmpty(s.room) ? s.building : $"{s.building} · {s.room}";
            return RoomText(s.roomId);
        }

        public static string RoomText(string roomId)
        {
            var c = ClassroomCatalog.LoadDefault().Get(roomId);
            return c != null ? c.LocationText : roomId;
        }

        // ---------- สอบ (เรียกจาก ExamController) ----------
        public bool HasExamEligibleCourses => IsActive && Service != null && Service.HasActiveEnrollments;

        public float RecordExam(bool final, float quiz, float bonus)
        {
            if (!IsActive) return 0f;
            float avg = Service.RecordExam(final, quiz, bonus, SemDay);
            OnChanged?.Invoke();
            return avg;
        }

        // บันทึกคะแนนสอบแบบเดิม "เฉพาะวิชาที่ระบุ" (วิชาที่ไม่ใช้มินิเกม) — คืนคะแนนเฉลี่ยของวิชาที่บันทึก
        public float RecordExam(bool final, float quiz, float bonus, ICollection<string> onlyCodes)
        {
            if (!IsActive) return 0f;
            float avg = Service.RecordExam(final, quiz, bonus, SemDay, onlyCodes);
            OnChanged?.Invoke();
            return avg;
        }

        // มินิเกมสอบรายวิชา: บันทึกคะแนนหนึ่งวิชาด้วยสูตรเดิม (ครั้งเดียวต่อรอบ) — คืน -1 ถ้าบันทึกไม่ได้
        public float RecordCourseExam(string code, bool final, float quiz, float bonus)
        {
            if (!IsActive || Service == null) return -1f;
            float s = Service.RecordCourseExam(code, final, quiz, bonus, SemDay);
            if (s >= 0f) OnChanged?.Invoke();
            return s;
        }

        // ขาดสอบ → คืนจำนวนวิชาที่ถูกบันทึกว่าขาด (0 = สอบครบแล้ว)
        public int RecordMissedExam(bool final)
        {
            if (!IsActive) return 0;
            int n = Service.RecordMissedExam(final);
            OnChanged?.Invoke();
            return n;
        }

        public void NotifyChanged() => OnChanged?.Invoke();

        // สรุปสั้นสำหรับแอปอื่นในโทรศัพท์
        public string ShortSummary()
        {
            if (!IsActive || Service == null) return "";
            var t = Record.Current;
            string term = t == null ? "-" : t.isBreak ? "ปิดภาคฤดูร้อน" : t.isExtra ? "ภาคเรียนเพิ่มเติม" : $"ภาค {t.planSemester}";
            string reg = t == null || t.isBreak ? "" : t.confirmed ? "ลงทะเบียนแล้ว" : Service.RegistrationWindowOpen(SemDay) ? "<color=#E0664F>ยังไม่ลงทะเบียน!</color>" : "ไม่ได้ลงทะเบียน";
            return $"ชั้นปี {ClassYear} · {term} · {reg}\nหน่วยกิตสะสม {Service.EarnedCredits()}/{curriculum.GraduationCredits}";
        }
    }
}
