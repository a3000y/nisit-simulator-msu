#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.Core;
using NisitSimulator.Interaction;
using NisitSimulator.SaveLoad;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.DevTools
{
    // ===== Dev Testing Panel (F8) — มีเฉพาะ Unity Editor / Development Build =====
    //   Release Build: ไฟล์ทั้งหมดในโฟลเดอร์ DevTools อยู่ใน #if UNITY_EDITOR || DEVELOPMENT_BUILD → ไม่มีโค้ด/ปุ่มลัด/คำสั่งถูกคอมไพล์เข้าเกม
    //   สร้างตัวเองอัตโนมัติ (RuntimeInitializeOnLoadMethod) + DontDestroyOnLoad — ไม่ต้องแก้ฉาก
    //   เปิด Panel: ปิดการรับ input ของเกม (เดิน/โต้ตอบ/ปุ่มลัดแผงอื่น/กล้อง/คลิก UI) แล้วคืนค่าเดิมเมื่อปิด
    [DefaultExecutionOrder(-200)]
    public class DevPanel : MonoBehaviour
    {
        public const string KeyPref = "nisit_dev_panel_key";
        public static DevPanel Instance { get; private set; }
        public static KeyCode ToggleKey { get; private set; } = KeyCode.F8;
        public static bool IsOpen => Instance != null && Instance.open;
        public static int? LastPreviewSeed;
        public static DevScenarioResult LastScenarioResult;
        public static bool BypassUsed;

        static readonly List<string> logLines = new List<string>();
        public static void Log(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            logLines.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            if (logLines.Count > 80) logLines.RemoveAt(logLines.Count - 1);
            Debug.Log("[DEV] " + s);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!Debug.isDebugBuild) return;   // กันซ้ำ: Release ไม่มีคลาสนี้อยู่แล้ว
            if (Instance != null) return;
            var go = new GameObject("[DevPanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<DevPanel>();
        }

        bool open;
        int tab;
        Vector2 scroll, logScroll;
        string message = "";
        bool rebinding;
        bool bypass;
        bool confirmReset;
        readonly Dictionary<string, string> fields = new Dictionary<string, string>();
        readonly List<string> lastChange = new List<string>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            int k = PlayerPrefs.GetInt(KeyPref, (int)KeyCode.F8);
            ToggleKey = Enum.IsDefined(typeof(KeyCode), k) && ConflictOf((KeyCode)k) == null ? (KeyCode)k : KeyCode.F8;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; if (Instance == this) Instance = null; }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single) return;
            if (open) { disabled.Clear(); eventSystems.Clear(); open = false; }
            DevTimeTools.ResetSpeedState();
            fields.Clear();
            if (pendingScenario != null) StartCoroutine(VerifyScenarioAfterLoad(pendingScenario));
            pendingScenario = null;
            if (pendingExamFp != null) StartCoroutine(VerifyManualSaveLoad(pendingExamFp, pendingExamRemaining));
            pendingExamFp = null;
        }

        // ============================================================
        // ปุ่มเปิด/ปิด + ตรวจปุ่มชน
        // ============================================================
        static readonly Dictionary<KeyCode, string> Reserved = new Dictionary<KeyCode, string>
        {
            { KeyCode.E, "โต้ตอบ (PlayerInteraction)" }, { KeyCode.Tab, "โทรศัพท์ (PhoneController)" }, { KeyCode.I, "กระเป๋า" },
            { KeyCode.J, "ความสำเร็จ" }, { KeyCode.L, "เลเวล/ความสามารถ" }, { KeyCode.M, "แผนที่ย่อ" }, { KeyCode.H, "ของขวัญ" },
            { KeyCode.G, "แลกของ (Multiplayer)" }, { KeyCode.B, "อีโมต" }, { KeyCode.Y, "แชท" }, { KeyCode.Q, "กล้อง" }, { KeyCode.R, "กล้อง" },
            { KeyCode.V, "กล้อง" }, { KeyCode.W, "เดิน" }, { KeyCode.A, "เดิน" }, { KeyCode.S, "เดิน" }, { KeyCode.D, "เดิน" },
            { KeyCode.UpArrow, "เดิน" }, { KeyCode.DownArrow, "เดิน" }, { KeyCode.LeftArrow, "เดิน" }, { KeyCode.RightArrow, "เดิน" },
            { KeyCode.Escape, "หยุดเกม/ปิดหน้าต่าง" }, { KeyCode.Space, "กระโดด/ปิดสรุปวัน" }, { KeyCode.Return, "ปิดสรุปวัน" },
            { KeyCode.LeftShift, "วิ่ง" }, { KeyCode.RightShift, "วิ่ง" }, { KeyCode.F1, "คู่มือ (Tutorial)" }, { KeyCode.F2, "รายชื่อผู้เล่น (Multiplayer)" },
            { KeyCode.F11, "แผง Multiplayer (NetworkUI)" }, { KeyCode.F9, "บันทึกภาพหน้าจอ (ScreenshotHelper)" },
        };

        // null = ใช้ได้ · ไม่ null = ชื่อระบบที่ใช้ปุ่มนี้อยู่แล้ว
        public static string ConflictOf(KeyCode k)
        {
            if (k == KeyCode.None) return "ไม่มีปุ่ม";
            if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) return "ปุ่มเมาส์";
            if ((k >= KeyCode.A && k <= KeyCode.Z) || (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) || (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9))
                return "ตัวอักษร/ตัวเลข (ใช้พิมพ์ในช่องกรอกของ Panel)";
            if (Reserved.TryGetValue(k, out var who)) return who;
            var phone = UnityEngine.Object.FindFirstObjectByType<NisitSimulator.UI.PhoneController>();
            if (phone != null && phone.toggleKey == k) return "โทรศัพท์ (PhoneController)";
            var pi = UnityEngine.Object.FindFirstObjectByType<NisitSimulator.Player.PlayerInteraction>();
            if (pi != null && pi.interactKey == k) return "โต้ตอบ (PlayerInteraction)";
            var sh = UnityEngine.Object.FindFirstObjectByType<ScreenshotHelper>();
            if (sh != null && sh.isActiveAndEnabled && k >= KeyCode.F1 && k <= KeyCode.F7) return "ScreenshotHelper (เปิดอยู่ในฉาก)";
            return null;
        }

        void Update()
        {
            if (rebinding) return;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(ToggleKey) && !shift) { if (open) Close(); else Open(); }
            else if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void LateUpdate()
        {
            if (!open) return;
            // แผงอื่น (สรุปวัน/ห้องสอบ) อาจคืน move.enabled ระหว่าง Panel เปิด → ล็อกซ้ำทุกเฟรม
            foreach (var b in disabled) if (b.Key != null && b.Key.enabled) b.Key.enabled = false;
            foreach (var es in eventSystems) if (es != null && es.enabled) es.enabled = false;
        }

        // ============================================================
        // ล็อก/คืน input
        // ============================================================
        static readonly HashSet<string> BlockTypes = new HashSet<string>
        {
            "PlayerMovement", "PlayerInteraction", "PhoneController", "InventoryUI", "AchievementsUI", "MinimapToggle", "GiftUI", "TradeUI",
            "EmoteWheel", "ChatUI", "PlayerListUI", "TutorialController", "NetworkUI", "IsometricCameraRig", "PauseMenu", "LevelSystem",
            "ScreenshotHelper", "RegistrationUI", "ExamMinigameUI",
        };
        readonly List<KeyValuePair<Behaviour, bool>> disabled = new List<KeyValuePair<Behaviour, bool>>();
        readonly List<EventSystem> eventSystems = new List<EventSystem>();
        bool prevCursorVisible; CursorLockMode prevLock;

        public void Open()
        {
            if (open) return;
            open = true; rebinding = false; confirmReset = false;
            disabled.Clear(); eventSystems.Clear();
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb != null && mb.enabled && BlockTypes.Contains(mb.GetType().Name)) { disabled.Add(new KeyValuePair<Behaviour, bool>(mb, true)); mb.enabled = false; }
            foreach (var es in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (es.enabled) { eventSystems.Add(es); es.enabled = false; }
            prevCursorVisible = Cursor.visible; prevLock = Cursor.lockState;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            NisitSimulator.UI.HUDController.Prompt("");
            fields.Clear();
        }

        public void Close()
        {
            if (!open) return;
            open = false; rebinding = false; confirmReset = false;
            bool examLocks = ExamMinigameController.BlocksWorld;
            foreach (var kv in disabled)
            {
                if (kv.Key == null) continue;
                string n = kv.Key.GetType().Name;
                if (examLocks && (n == "PlayerMovement")) continue;   // ห้องสอบยังล็อกการเดินอยู่ — ให้ตัวคุมสอบจัดการ
                kv.Key.enabled = kv.Value;
            }
            foreach (var es in eventSystems) if (es != null) es.enabled = true;
            disabled.Clear(); eventSystems.Clear();
            Cursor.visible = prevCursorVisible; Cursor.lockState = prevLock;
            if (ExamMinigameController.BlocksWorld) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
        }

        // ============================================================
        // สิทธิ์คำสั่งเปลี่ยนสถานะ
        // ============================================================
        static bool CanMutate(out string why)
        {
            why = null;
            if (DevProfile.InNetworkSession) { why = "ปิดคำสั่งเปลี่ยนสถานะระหว่างเล่นหลายคน — เกมนี้จำลองสถานะฝั่งผู้เล่นแต่ละเครื่องและไม่มีห้อง Dev ที่ Host ตรวจสิทธิ์ได้ (ไม่มีคำสั่ง Dev ผ่านเครือข่าย)"; return false; }
            if (!DevProfile.Active) { why = "คำสั่งเปลี่ยนสถานะใช้ได้เฉพาะ DEV TEST PROFILE — ไปแท็บ \"เซฟทดสอบ\" ▸ เริ่มโปรไฟล์ทดสอบ"; return false; }
            if (!DevProfile.InGameplayScene) { why = "ใช้ได้ในฉากเล่นเกมเท่านั้น"; return false; }
            return true;
        }

        void Say(bool ok, string msg) { message = (ok ? "✔ " : "✘ ") + msg; Log(message); }

        // ============================================================
        // IMGUI
        // ============================================================
        GUIStyle sTitle, sBody, sSmall, sBtn, sTab, sTabOn, sBox, sBadge, sField, sRed, sGreen, sYellow, sHead, sToggle;
        Font font;
        float scale = 1f;

        void EnsureStyles()
        {
            if (sBody != null) return;
            try { font = Font.CreateDynamicFontFromOSFont(new[] { "Leelawadee UI", "Tahoma", "Segoe UI", "Arial" }, 14); } catch { font = null; }
            GUIStyle Mk(GUIStyle b, int size, Color c, FontStyle fs = FontStyle.Normal)
            {
                var s = new GUIStyle(b) { fontSize = size, fontStyle = fs, richText = true, wordWrap = true };
                if (font != null) s.font = font;
                s.normal.textColor = c; s.hover.textColor = c; s.active.textColor = c; s.focused.textColor = c;
                return s;
            }
            var white = new Color(0.95f, 0.95f, 0.97f);
            sTitle = Mk(GUI.skin.label, 17, new Color(1f, 0.85f, 0.45f), FontStyle.Bold);
            sHead = Mk(GUI.skin.label, 14, new Color(0.60f, 0.85f, 1f), FontStyle.Bold);
            sBody = Mk(GUI.skin.label, 13, white);
            sSmall = Mk(GUI.skin.label, 11, new Color(0.75f, 0.78f, 0.85f));
            sRed = Mk(GUI.skin.label, 13, new Color(1f, 0.45f, 0.45f), FontStyle.Bold);
            sGreen = Mk(GUI.skin.label, 13, new Color(0.45f, 0.95f, 0.55f), FontStyle.Bold);
            sYellow = Mk(GUI.skin.label, 13, new Color(1f, 0.85f, 0.35f), FontStyle.Bold);
            sBtn = Mk(GUI.skin.button, 13, white); sBtn.wordWrap = false;
            sTab = Mk(GUI.skin.button, 13, white); sTab.wordWrap = false;
            sTabOn = Mk(GUI.skin.button, 13, new Color(1f, 0.85f, 0.45f), FontStyle.Bold); sTabOn.wordWrap = false;
            sBox = Mk(GUI.skin.box, 13, white); sBox.alignment = TextAnchor.UpperLeft;
            sField = Mk(GUI.skin.textField, 13, white); sField.wordWrap = false;
            sBadge = Mk(GUI.skin.box, 15, Color.white, FontStyle.Bold); sBadge.alignment = TextAnchor.MiddleCenter;
            sToggle = Mk(GUI.skin.toggle, 12, white); sToggle.wordWrap = false;
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
        Texture2D texDim, texWin, texBadge;

        void OnGUI()
        {
            EnsureStyles();
            if (texDim == null) { texDim = Tex(new Color(0, 0, 0, 0.55f)); texWin = Tex(new Color(0.10f, 0.11f, 0.15f, 0.97f)); texBadge = Tex(new Color(0.75f, 0.12f, 0.15f, 0.92f)); }
            scale = Mathf.Clamp(Screen.height / 820f, 0.8f, 2f);
            var prevM = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float W = Screen.width / scale, H = Screen.height / scale;
            GUI.depth = -1000;

            if (DevProfile.Active)
            {
                GUI.DrawTexture(new Rect(W / 2 - 230, 4, 460, 26), texBadge);
                GUI.Label(new Rect(W / 2 - 230, 4, 460, 26), "DEV TEST PROFILE — เซฟแยก · ไม่เขียนทับเซฟจริง", sBadge);
            }
            string status = DevTimeTools.Status;
            if (DevRuntimeChecks.Running) status = "กำลังตรวจอัตโนมัติ: " + DevRuntimeChecks.Progress + (string.IsNullOrEmpty(status) ? "" : "\n" + status);
            if (!string.IsNullOrEmpty(status))
            {
                GUI.DrawTexture(new Rect(W / 2 - 300, 34, 600, 44), texDim);
                GUI.Label(new Rect(W / 2 - 292, 36, 584, 42), status, sSmall);
            }

            if (open)
            {
                GUI.DrawTexture(new Rect(0, 0, W, H), texDim);
                float w = Mathf.Min(1060, W - 30), h = Mathf.Min(760, H - 30);
                var r = new Rect((W - w) / 2, (H - h) / 2, w, h);
                GUI.DrawTexture(r, texWin);
                GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
                DrawPanel(r.width - 24, r.height - 20);
                GUILayout.EndArea();
                // กล่องดักคลิกเต็มจอ วาดหลังสุด (IMGUI ส่ง event ให้ปุ่มใน Panel ก่อน) → คลิกนอก Panel ไม่ทะลุไป UI/โลกเกม
                GUI.Button(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none);
                if (Event.current != null && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.ScrollWheel)) Event.current.Use();
            }
            if (rebinding) HandleRebind();
            GUI.matrix = prevM;
        }

        void HandleRebind()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            var k = e.keyCode;
            e.Use();
            rebinding = false;
            if (k == KeyCode.Escape) { Say(true, "ยกเลิกการเปลี่ยนปุ่ม"); return; }
            string c = k == ToggleKey ? null : ConflictOf(k);
            if (c != null) { Say(false, $"ใช้ {k} ไม่ได้ — ชนกับ: {c}"); return; }
            ToggleKey = k;
            PlayerPrefs.SetInt(KeyPref, (int)k); PlayerPrefs.Save();
            Say(true, $"ตั้งปุ่มเปิด Dev Panel เป็น {k}");
        }

        static readonly string[] Tabs = { "ผู้เล่น", "เวลา", "การเรียน", "การสอบ", "เซฟทดสอบ", "ผลทดสอบ" };

        void DrawPanel(float w, float h)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"DEV TESTING PANEL  <size=12>(Editor / Development Build เท่านั้น)</size>", sTitle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(rebinding ? "กดปุ่มใหม่… (Esc ยกเลิก)" : $"ปุ่มเปิด/ปิด: {ToggleKey} ▸ เปลี่ยน", sBtn, GUILayout.Width(230))) rebinding = !rebinding;
            if (GUILayout.Button("ปิด ✕", sBtn, GUILayout.Width(70))) Close();
            GUILayout.EndHorizontal();

            GUILayout.Label(HeaderInfo(), sSmall);
            if (DevProfile.Active) GUILayout.Label("โปรไฟล์: <b>DEV TEST PROFILE</b> — คำสั่งทั้งหมดมีผลกับเซฟทดสอบเท่านั้น", sGreen);
            else GUILayout.Label("โปรไฟล์: เกมปกติ — คำสั่งเปลี่ยนสถานะปิดอยู่ (แท็บ \"เซฟทดสอบ\" ▸ เริ่มโปรไฟล์ทดสอบ)", sYellow);
            if (DevProfile.InNetworkSession) GUILayout.Label("กำลังเชื่อมต่อ Multiplayer — ปิดคำสั่งเปลี่ยนสถานะทั้งหมด", sRed);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++) if (GUILayout.Button(Tabs[i], i == tab ? sTabOn : sTab, GUILayout.Height(28))) { tab = i; scroll = Vector2.zero; confirmReset = false; }
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(h - 250));
            try
            {
                switch (tab)
                {
                    case 0: TabPlayer(); break;
                    case 1: TabTime(); break;
                    case 2: TabAcademics(); break;
                    case 3: TabExam(); break;
                    case 4: TabSave(); break;
                    case 5: TabResults(); break;
                }
            }
            catch (Exception e) { GUILayout.Label("ข้อผิดพลาดในแท็บ: " + e.Message, sRed); }
            GUILayout.EndScrollView();

            GUILayout.Label(message, message.StartsWith("✘") ? sRed : sBody);
            logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(80));
            foreach (var l in logLines) GUILayout.Label(l, sSmall);
            GUILayout.EndScrollView();
        }

        string HeaderInfo()
        {
            var reg = CourseRegistrar.Instance; var prog = DevTimeTools.Prog; var clock = DevTimeTools.Clock;
            string name = string.IsNullOrEmpty(GameSession.PlayerName) ? "(ไม่ได้ตั้งชื่อ)" : GameSession.PlayerName;
            string fac = FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex);
            string year = reg != null && reg.IsActive ? reg.ClassYear.ToString() : prog != null ? prog.CurrentYear.ToString() : "-";
            string term = "-";
            if (reg != null && reg.IsActive && reg.Record != null && reg.Record.Current != null)
            {
                var t = reg.Record.Current;
                term = t.isBreak ? "ปิดภาคฤดูร้อน" : (t.isExtra ? "ภาคเรียนเพิ่มเติม" : "ภาค " + t.planSemester) + (t.confirmed ? " (ลงทะเบียนแล้ว)" : t.registrationOpen ? " (เปิดลงทะเบียน)" : "");
            }
            else if (prog != null) term = AcademicCalendar.SemesterName(AcademicCalendar.SemesterIndex(prog.DayInYear));
            string cal = prog != null ? $"ปีการศึกษา {prog.CalendarYear} วัน {prog.DayInYear}/{prog.daysPerYear} (วันที่ {AcademicCalendar.SemesterDay(prog.DayInYear)} ของ{AcademicCalendar.SemesterName(AcademicCalendar.SemesterIndex(prog.DayInYear))})" : "";
            string time = clock != null ? $"{clock.GetTimeString()} วันเกม {clock.Day}" : "";
            return $"ผู้เล่น: <b>{name}</b> · คณะ: {fac} · ชั้นปี {year} · {term} · {cal} · {time}\nScene: {SceneManager.GetActiveScene().name} · สถานะสอบ: {ExamStatus()}";
        }

        static string ExamStatus()
        {
            var exam = DevExamTools.Exam; var ctl = ExamMinigameController.Instance;
            var sb = new StringBuilder();
            if (exam != null && exam.HasPendingExam) sb.Append($"วันนี้มีสอบ{(exam.PendingIsFinal ? "ปลายภาค" : "กลางภาค")} {AcademicCalendar.SemesterName(exam.PendingSemester)}");
            else sb.Append("ไม่มีสอบวันนี้");
            if (ctl != null)
            {
                if (ctl.IsPreview && ctl.Session != null) sb.Append($" · PREVIEW {ctl.Session.courseCode}{(ctl.Session.submitted ? " (ส่งแล้ว)" : "")}");
                else if (ctl.HasSessionInProgress) sb.Append($" · กำลังสอบ {ctl.Session.courseCode} เหลือ {ctl.Session.remainingSeconds:0} วิ");
                else if (ctl.IsRoomOpen) sb.Append(" · อยู่ในห้องสอบ");
            }
            return sb.ToString();
        }

        // ---------- helpers ----------
        string F(string key, string def, float width = 70)
        {
            if (!fields.TryGetValue(key, out var v)) v = def;
            v = GUILayout.TextField(v ?? "", sField, GUILayout.Width(width));
            fields[key] = v;
            return v;
        }
        string Fget(string key, string def) => fields.TryGetValue(key, out var v) ? v : def;
        static float PF(string s, float d) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : d;
        static int PI(string s, int d) => int.TryParse(s, out var i) ? i : d;
        bool B(string label, float width = 0, bool enabled = true)
        {
            bool prev = GUI.enabled; GUI.enabled = prev && enabled;
            bool r = width > 0 ? GUILayout.Button(label, sBtn, GUILayout.Width(width)) : GUILayout.Button(label, sBtn);
            GUI.enabled = prev;
            return r;
        }
        void Head(string s) { GUILayout.Space(6); GUILayout.Label(s, sHead); }
        bool Gate()
        {
            if (CanMutate(out var why)) return true;
            GUILayout.Label("🔒 " + why, sYellow);
            return false;
        }

        // ============================================================
        // แท็บ: ผู้เล่น
        // ============================================================
        void TabPlayer()
        {
            var st = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            if (st == null) { GUILayout.Label("ไม่พบ PlayerStats (อยู่ในฉากเล่นเกมหรือยัง?)", sRed); return; }
            bool can = Gate();
            Head("ค่าสถานะ (มีจริงในเกม) — กรอกค่าใหม่แล้วกด \"ใช้ค่า\" · ขอบเขตตามระบบเดิม");
            var rows = new (string key, string label, float cur, float min, float max, bool integer)[]
            {
                ("money", "เงิน (฿)", st.Money, 0, 999999, true),
                ("energy", "พลังงาน", st.Energy, 0, st.maxEnergy, false),
                ("hunger", "ความอิ่ม", st.Hunger, 0, st.maxHunger, false),
                ("health", "สุขภาพ", st.Health, 0, st.maxHealth, false),
                ("stress", "ความเครียด", st.Stress, 0, st.maxStress, false),
                ("knowledge", "ความรู้ (รวม)", st.Knowledge, 0, 99999, false),
                ("sat", "ความพึงพอใจ", st.Satisfaction, 0, 100, false),
                ("exp", "EXP", st.Exp, 0, 9999999, true),
            };
            foreach (var r in rows)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(r.label, sBody, GUILayout.Width(140));
                GUILayout.Label(r.integer ? ((int)r.cur).ToString() : r.cur.ToString("0.#"), sBody, GUILayout.Width(80));
                F("st_" + r.key, r.integer ? ((int)r.cur).ToString() : r.cur.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), 90);
                GUILayout.Label($"ช่วง {r.min:0}–{(r.max >= 99999 ? "∞" : r.max.ToString("0"))}", sSmall, GUILayout.Width(120));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (B("ใช้ค่า (PlayerStats.LoadState)", 250, can))
            {
                float Val(string k, float cur, float min, float max) => Mathf.Clamp(PF(Fget("st_" + k, cur.ToString(System.Globalization.CultureInfo.InvariantCulture)), cur), min, max);
                var before = Snap(st);
                st.LoadState(Val("energy", st.Energy, 0, st.maxEnergy), Val("health", st.Health, 0, st.maxHealth), Val("hunger", st.Hunger, 0, st.maxHunger),
                             Val("knowledge", st.Knowledge, 0, 99999), Val("sat", st.Satisfaction, 0, 100), Mathf.RoundToInt(Val("money", st.Money, 0, 999999)),
                             Mathf.RoundToInt(Val("exp", st.Exp, 0, 9999999)), Val("stress", st.Stress, 0, st.maxStress));
                Diff(before, Snap(st));
                fields.Clear();
                Say(true, "ปรับค่าสถานะแล้ว (HUD อัปเดตผ่าน event ของ PlayerStats)");
            }
            if (B("ฟื้นฟูสถานะ (พลังงาน/อิ่ม/สุขภาพเต็ม · เครียด 0)", 330, can))
            {
                var before = Snap(st);
                st.LoadState(st.maxEnergy, st.maxHealth, st.maxHunger, st.Knowledge, st.Satisfaction, st.Money, st.Exp, 0f);
                Diff(before, Snap(st)); fields.Clear();
                Say(true, "ฟื้นฟูสถานะแล้ว");
            }
            if (B("รีเฟรชช่องกรอก", 130)) fields.Clear();
            GUILayout.EndHorizontal();
            if (lastChange.Count > 0)
            {
                Head("ค่าก่อน → หลังแก้ไขล่าสุด");
                foreach (var l in lastChange) GUILayout.Label(l, sBody);
            }

            Head("ความรู้รายวิชา (= สัดส่วนการเรียนรายวิชา ใช้คิดสิทธิ์คำใบ้ตอนสอบ)");
            var reg = CourseRegistrar.Instance;
            if (reg == null || !reg.IsActive || reg.Service == null) { GUILayout.Label("ระบบลงทะเบียนไม่ได้ใช้กับคณะนี้", sSmall); return; }
            var svc = reg.Service;
            var list = svc.CurrentEnrollments();
            if (list.Count == 0) { GUILayout.Label("ภาคนี้ยังไม่มีวิชาที่ลงทะเบียน (ยืนยันแล้ว)", sSmall); return; }
            GUILayout.Label("\"จำลองการเข้าเรียน\" บันทึกชั่วโมงเรียนผ่าน RegistrationService.RecordStudyTick แบบเดียวกับนั่งเรียนจริง เฉพาะคาบที่ผ่านมาแล้วตามเวลาในเกม", sSmall);
            GUILayout.BeginHorizontal(); GUILayout.Label("คุณภาพการเรียน q (0–1):", sBody, GUILayout.Width(180)); F("q_att", "0.8", 60); GUILayout.EndHorizontal();
            var clock = DevTimeTools.Clock; int semDay = reg.SemDay; float now = clock != null ? clock.TotalMinutes : 0f;
            foreach (var e in list)
            {
                float k = svc.StudyRatio(e, semDay) * 100f;
                int hints = ExamMinigameLogic.HintAllowance(k, DevExamTools.Db);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{e.code}  เรียนแล้ว {k:0}% · คำใบ้ {hints} · ชั่วโมง {e.progress:0.#}/{svc.ExpectedHours(e):0} · อ่านเอง {e.selfStudy:0.#}", sBody, GUILayout.Width(560));
                if (B("จำลองการเข้าเรียนคาบที่ผ่านมา", 250, can))
                {
                    float q = Mathf.Clamp01(PF(Fget("q_att", "0.8"), 0.8f));
                    int ticks = 0; var ss = svc.SessionsFor(e);
                    foreach (var s in ss)
                    {
                        if (s.day > semDay || (s.day == semDay && s.endMinute > now)) continue;
                        for (int i = 1; i <= s.MaxTicks; i++) { var rr = svc.RecordStudyTick(s.day, Mathf.Min(s.startMinute + 60 * i, s.endMinute), s.building, q); if (rr.attended == e) ticks++; }
                    }
                    reg.NotifyChanged();
                    Say(ticks > 0, ticks > 0 ? $"{e.code}: บันทึกเข้าเรียน {ticks} ชั่วโมง (q={q:0.##}) → เรียนแล้ว {svc.StudyRatio(e, semDay) * 100:0}%" : $"{e.code}: ไม่มีคาบที่ผ่านมาแล้วที่ยังไม่นับ");
                }
                GUILayout.EndHorizontal();
            }
        }

        static Dictionary<string, float> Snap(PlayerStats s) => new Dictionary<string, float>
        {
            { "เงิน", s.Money }, { "พลังงาน", s.Energy }, { "ความอิ่ม", s.Hunger }, { "สุขภาพ", s.Health }, { "ความเครียด", s.Stress },
            { "ความรู้", s.Knowledge }, { "ความพึงพอใจ", s.Satisfaction }, { "EXP", s.Exp },
        };

        void Diff(Dictionary<string, float> a, Dictionary<string, float> b)
        {
            lastChange.Clear();
            foreach (var kv in a)
            {
                float nb = b[kv.Key];
                lastChange.Add($"{kv.Key}: {kv.Value:0.#} → {nb:0.#}{(Mathf.Abs(nb - kv.Value) > 0.001f ? "   ●" : "")}");
            }
        }

        // ============================================================
        // แท็บ: เวลา
        // ============================================================
        void TabTime()
        {
            var clock = DevTimeTools.Clock; var prog = DevTimeTools.Prog;
            if (clock == null || prog == null) { GUILayout.Label("ไม่พบ GameClock/ProgressionManager", sRed); return; }
            bool can = Gate();
            int sem = AcademicCalendar.SemesterIndex(prog.DayInYear), semDay = AcademicCalendar.SemesterDay(prog.DayInYear), len = AcademicCalendar.SemesterLen(sem);
            GUILayout.Label($"ตอนนี้: วันเกม {clock.Day} · {clock.GetTimeString()} · ปีการศึกษา {prog.CalendarYear} · วันในปี {prog.DayInYear}/{prog.daysPerYear} · {AcademicCalendar.SemesterName(sem)} วันที่ {semDay}/{len} · ความเร็ว x{DevTimeTools.SpeedMult} ({clock.gameMinutesPerRealSecond:0.##} นาทีเกม/วิ)", sBody);

            Head("A) เปลี่ยนเวลาโดยตรง (Direct set)");
            GUILayout.Label("ใช้ RestoreClock / RestoreState (เส้นทางเดียวกับโหลดเซฟ): ไม่ยิง \"ข้ามวัน\" → ไม่มีค่าขนม ไม่ autosave ไม่ตัดสินขาดสอบ ไม่ลดสถานะตามเวลาที่ข้าม · เปลี่ยนวันในปีแล้วระบบลงทะเบียน/ตารางสอบจะ sync ตามวันใหม่ (ข้ามภาค = ปิดภาค/ประกาศเกรดทันที)", sSmall);
            GUILayout.BeginHorizontal();
            GUILayout.Label("เวลา ชม.", sBody, GUILayout.Width(70)); F("t_h", clock.Hour.ToString(), 45); GUILayout.Label("นาที", sBody, GUILayout.Width(40)); F("t_m", clock.Minute.ToString(), 45);
            if (B("ตั้งเวลา", 110, can)) { bool ok = DevTimeTools.SetTimeOfDay(PI(Fget("t_h", "8"), 8), PI(Fget("t_m", "0"), 0), out var m); Say(ok, m); }
            GUILayout.Space(20);
            GUILayout.Label("ปีการศึกษา", sBody, GUILayout.Width(80)); F("t_cal", prog.CalendarYear.ToString(), 45); GUILayout.Label("วันในปี", sBody, GUILayout.Width(60)); F("t_diy", prog.DayInYear.ToString(), 45);
            if (B("ตั้งวัน", 90, can)) { bool ok = DevTimeTools.SetAcademicDay(PI(Fget("t_cal", "1"), 1), PI(Fget("t_diy", "1"), 1), out var m); Say(ok, m); fields.Clear(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (B("ช่วงลงทะเบียน: วันแรกของภาคนี้ 08:00", 0, can)) { bool ok = DevTimeTools.JumpInSemester(1, 8, out var m); Say(ok, m); }
            if (B("วันเรียน: วันถัดไปในภาค 08:00", 0, can && semDay < len)) { bool ok = DevTimeTools.JumpInSemester(semDay + 1, 8, out var m); Say(ok, m); }
            if (B($"วันสอบกลางภาค (วันที่ {ExamController.MidtermDay(sem)}) 08:00", 0, can && AcademicCalendar.HasMidterm(sem) && sem < 2)) { bool ok = DevTimeTools.JumpInSemester(ExamController.MidtermDay(sem), 8, out var m); Say(ok, m); }
            if (B($"วันสอบปลายภาค (วันที่ {len}) 08:00", 0, can && sem < 2)) { bool ok = DevTimeTools.JumpInSemester(len, 8, out var m); Say(ok, m); }
            GUILayout.EndHorizontal();

            Head("B) จำลองเวลาเดินผ่าน (Simulate)");
            GUILayout.Label("เดินนาฬิกาทีละ 1 นาทีเกมผ่าน GameClock.AdvanceMinutes + StatDecay.Tick (ขั้นตอนเดียวกับเวลาเดินปกติ) · ข้ามวันยิงเหตุการณ์จริงครั้งเดียวต่อวัน: ค่าขนม, autosave (ลงเซฟทดสอบ), ขาดสอบ, ปิดภาค/ประกาศเกรด · หยุดรอเมื่อหน้าสรุปวัน/เหตุการณ์เปิด · Panel จะปิดเพื่อให้เห็นเกม", sSmall);
            GUILayout.BeginHorizontal();
            bool simOk = can && !DevTimeTools.Simulating;
            if (B("+1 ชม.", 80, simOk)) Sim(60f, "+1 ชั่วโมง", null);
            if (B("+6 ชม.", 80, simOk)) Sim(360f, "+6 ชั่วโมง", null);
            if (B("ถึง 07:00 วันถัดไป", 0, simOk)) Sim(DevTimeTools.MinutesUntil(420, true), "ถึง 07:00 วันถัดไป", null);
            if (B("ถึงวันสอบถัดไป (07:00)", 0, simOk)) SimToExam();
            if (B("ถึงเปิดภาคถัดไป (ช่วงลงทะเบียน 08:00)", 0, simOk)) SimToNextTerm();
            if (B("หยุด", 60, DevTimeTools.Simulating)) DevTimeTools.Cancel();
            GUILayout.EndHorizontal();

            Head("ความเร็วเวลาโลก");
            GUILayout.BeginHorizontal();
            foreach (var k in new[] { 1, 5, 10 })
                if (B((DevTimeTools.SpeedMult == k ? "● " : "") + "x" + k, 80, can)) { bool ok = DevTimeTools.SetSpeed(k, out var m); Say(ok, m); }
            GUILayout.Label("เปลี่ยนเฉพาะ gameMinutesPerRealSecond — ไม่แตะ Time.timeScale · ตัวจับเวลาสอบใช้ unscaled time และนาฬิกาโลกหยุดระหว่างอยู่ห้องสอบ", sSmall);
            GUILayout.EndHorizontal();

            Head("กลางวัน–กลางคืน / หอพัก");
            var dn = DayNightCycle.Instance;
            GUILayout.Label($"วันเกม {clock.Day} · {clock.Hour:00}:{clock.Minute:00} น. · ช่วง: {(dn != null ? dn.PhaseName : "ไม่มี DayNightCycle")} · แสงภายในอาคาร: {(dn != null && dn.IndoorLighting ? "ใช่" : "ไม่")} · ไฟถนน {(dn != null ? (dn.OutdoorLightsFactor * 100f).ToString("0") : "-")}% · การเกิดล่าสุด: {PlayerSpawnSystem.LastSpawnReason}", sBody);
            GUILayout.BeginHorizontal();
            foreach (var hh in new[] { 6, 12, 18, 0 })
                if (B($"ตั้งเวลา {hh:00}:00", 120, can)) { bool ok = DevTimeTools.SetTimeOfDay(hh, 0, out var m); Say(ok, m); }
            if (B("เร่งเวลา x10", 120, can)) { bool ok = DevTimeTools.SetSpeed(10, out var m); Say(ok, m); }
            if (B("คืนความเร็วเดิม", 140, can && DevTimeTools.SpeedMult != 1)) { bool ok = DevTimeTools.SetSpeed(1, out var m); Say(ok, m); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (B("วาร์ปไปจุดเกิดหอพัก", 0, can)) { bool ok = DevTimeTools.WarpToDorm(out var m); Say(ok, m); }
            if (B("ทดสอบนอนจนถึง 07:00", 0, can && !SleepController.IsSleeping)) { bool ok = DevTimeTools.TestSleep(out var m); Say(ok, m); if (ok) Close(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("ตั้งเวลา = เส้นทางเดียวกับโหลดเซฟ (ไม่ยิงเหตุการณ์ข้ามวัน) · แสงอัปเดตทันที · ทดสอบนอน = ขั้นตอนนอนจริง (ข้ามวันได้ครั้งเดียว บันทึกลงโปรไฟล์ทดสอบ)", sSmall);

            Head("วาร์ป (สถานที่ที่มีจริงในฉาก)");
            var doors = UnityEngine.Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None);
            Array.Sort(doors, (a, b) => string.CompareOrdinal(a.name, b.name));
            GUILayout.BeginHorizontal();
            int col = 0;
            foreach (var d in doors)
            {
                if (B(d.activityName.Replace("เข้า", ""), 140, can)) { bool ok = DevTimeTools.WarpToDoor(d, out var m); Say(ok, m); }
                if (++col % 6 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var es = UnityEngine.Object.FindFirstObjectByType<ExamStation>();
            if (B("จุดสอบ (ห้องสอบ)", 160, can && es != null)) { bool ok = DevTimeTools.WarpOutdoor(es.transform.position + new Vector3(0f, 0.2f, 1.6f), "จุดสอบ (ExamPoint)", out var m); Say(ok, m); }
            string nextB = NextClassBuilding(out var nextTxt);
            if (B("จุดเรียนของคาบถัดไป" + (nextB != null ? $" ({nextB})" : ""), 0, can && nextB != null)) { var d = DevTimeTools.DoorFor(nextB); bool ok = DevTimeTools.WarpToDoor(d, out var m); Say(ok, m + " · " + nextTxt); }
            GUILayout.EndHorizontal();
            if (nextB == null) GUILayout.Label("คาบถัดไป: " + nextTxt, sSmall);
        }

        void Sim(float minutes, string label, Func<bool> stop)
        {
            bool ok = DevTimeTools.StartSimulation(this, minutes, label, stop, out var m);
            Say(ok, m);
            if (ok) Close();
        }

        void SimToExam()
        {
            var exam = DevExamTools.Exam; var clock = DevTimeTools.Clock;
            if (exam == null) { Say(false, "ไม่พบระบบสอบ"); return; }
            int startDay = clock.Day;
            Sim(10 * 24 * 60f, "ถึงวันสอบถัดไป", () => clock.Day > startDay && exam.HasPendingExam && clock.Hour >= 7);
        }

        void SimToNextTerm()
        {
            var reg = CourseRegistrar.Instance; var clock = DevTimeTools.Clock;
            if (reg == null || !reg.IsActive) { Say(false, "ระบบลงทะเบียนไม่ได้ใช้กับคณะนี้"); return; }
            var t0 = reg.Record.Current;
            Sim(14 * 24 * 60f, "ถึงเปิดภาคถัดไป", () => { var t = reg.Record != null ? reg.Record.Current : null; return t != null && t != t0 && !t.isBreak && clock.Hour >= 8; });
        }

        string NextClassBuilding(out string text)
        {
            text = "ไม่มีคาบเรียน (ยังไม่ได้ลงทะเบียน/ภาคปิด)";
            var reg = CourseRegistrar.Instance; var clock = DevTimeTools.Clock;
            if (reg == null || !reg.IsActive || reg.Service == null || clock == null) return null;
            int d = reg.SemDay; float now = clock.TotalMinutes;
            ClassSession best = null; string code = null;
            foreach (var e in reg.Service.CurrentEnrollments())
                foreach (var s in reg.Service.SessionsFor(e))
                    if (s.day == d && s.endMinute > now && (best == null || s.startMinute < best.startMinute)) { best = s; code = e.code; }
            if (best == null) { text = "วันนี้ไม่มีคาบเรียนที่เหลือ"; return null; }
            text = $"{code} {best.TimeText} {best.building}";
            return best.building;
        }

        // ============================================================
        // แท็บ: การเรียน
        // ============================================================
        void TabAcademics()
        {
            var reg = CourseRegistrar.Instance;
            if (reg == null || reg.Service == null) { GUILayout.Label("ไม่พบระบบลงทะเบียน (CourseRegistrar)", sRed); return; }
            if (!reg.IsActive) GUILayout.Label($"คณะปัจจุบัน ({FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex)}) ไม่ได้ใช้หลักสูตรลงทะเบียน — สถานการณ์สำเร็จรูปจะตั้งคณะเป็นคณะที่ใช้หลักสูตรให้", sYellow);
            bool can = Gate();
            var svc = reg.Service; var cur = svc.Curriculum; var t = reg.Record.Current;
            int semDay = reg.SemDay;

            Head("สถานะการเรียน (คำนวณจากประวัติรายวิชาเท่านั้น)");
            string term = t == null ? "ยังไม่เปิดภาค" : t.isBreak ? "ปิดภาคฤดูร้อน" : $"{(t.isExtra ? "ภาคเรียนเพิ่มเติม" : $"ชั้นปี {t.classYear} ภาค {t.planSemester}")} · ปีการศึกษา {t.calendarYear} · {(t.confirmed ? "ยืนยันลงทะเบียนแล้ว" : svc.RegistrationWindowOpen(semDay) ? "เปิดลงทะเบียน" : "ปิดลงทะเบียน")}{(t.closed ? " · ปิดภาคแล้ว" : "")}";
            GUILayout.Label($"{term}\nชั้นปี {reg.ClassYear} · เลือกไว้ {svc.SelectedCredits()}/{svc.CreditCap} หน่วยกิต · หน่วยกิตสะสม {svc.EarnedCredits()}/{cur.GraduationCredits} (บังคับ {svc.PassedRequiredCredits()}/{cur.RequiredCredits} · วิชาเลือก {svc.PassedElectiveCount()}/{cur.electivesRequired}) · GPA {(svc.HasGpa ? svc.Gpa().ToString("0.00") : "ยังไม่มีเกรด (0 หน่วยกิตที่คิดเกรด)")} · ภาคที่เรียนไป {reg.Record.regularTermsStarted}", sBody);
            var gs = svc.CheckGraduation();
            GUILayout.Label(gs.eligible ? "เงื่อนไขจบ: ครบแล้ว" : "เงื่อนไขจบที่ยังขาด:\n• " + string.Join("\n• ", gs.reasons), gs.eligible ? sGreen : sBody);

            Head("รายวิชาที่เปิดภาคนี้ (สถานะ + เหตุผลตามกฎจริง)");
            var offered = svc.Offered();
            if (offered.Count == 0) GUILayout.Label(svc.ExplainNoOptions(semDay) ?? "ไม่มีรายวิชาเปิด", sSmall);
            foreach (var o in offered)
            {
                var st = svc.StatusOf(o, out var why);
                GUILayout.BeginHorizontal();
                string pre = o.def.prerequisites.Count > 0 ? " · ต้องผ่าน " + string.Join(",", o.def.prerequisites) : "";
                string grp = o.group == OfferGroup.Plan ? "แผน" : o.group == OfferGroup.Outstanding ? "ค้าง/เรียนซ้ำ" : "เลือก";
                var style = st == CourseStatus.MissingPrerequisite ? sRed : st == CourseStatus.Selected ? sGreen : st == CourseStatus.Passed ? sSmall : sBody;
                GUILayout.Label($"{o.def.code} {o.def.title} ({o.def.credits} นก.) [{grp}{(o.retakeSection ? "·ภาคค่ำ" : "")}] — {StatusName(st)}{(why != null ? ": " + why : "")}{pre}", style, GUILayout.Width(700));
                if (B("เพิ่ม", 60, can && st == CourseStatus.CanRegister)) { bool ok = svc.Add(o.def.code, out var m); reg.NotifyChanged(); Say(ok, m); }
                if (B("ถอน", 60, can && t != null && t.selected.Contains(o.def.code))) { bool ok = svc.Remove(o.def.code, out var m); reg.NotifyChanged(); Say(ok, m); }
                if (bypass && B("บังคับเพิ่ม", 90, can && t != null && !t.confirmed && !t.selected.Contains(o.def.code)))
                { t.selected.Add(o.def.code); reg.NotifyChanged(); BypassUsed = true; Say(true, $"[ข้ามเงื่อนไข — สำหรับ Dev] บังคับเพิ่ม {o.def.code} (ไม่ตรวจ prerequisite/ชนเวลา/เพดาน)"); }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (B("ตรวจรายการที่เลือก (ValidateSelection)", 0, t != null)) { bool ok = svc.ValidateSelection(out var m); Say(ok, ok ? "รายการที่เลือกผ่านทุกเงื่อนไข" : m); }
            if (B("ยืนยันลงทะเบียน (กฎปกติ)", 0, can && t != null && !t.confirmed)) { bool ok = svc.Confirm(out var m); reg.NotifyChanged(); Say(ok, m); }
            if (B("เปิดช่วงลงทะเบียน: จำลองเวลาไปภาคถัดไป", 0, can && !DevTimeTools.Simulating)) SimToNextTerm();
            GUILayout.EndHorizontal();

            Head("วิชาที่ลงภาคนี้");
            foreach (var e in svc.CurrentEnrollments())
                GUILayout.Label($"{e.code} ครั้งที่ {e.attempt}{(e.retakeSection ? " (ภาคค่ำ)" : "")} · เรียน {svc.StudyRatio(e, semDay) * 100:0}% · กลางภาค {(e.midterm >= 0 ? (e.midterm * 100).ToString("0") : "-")}{(e.missedMidterm ? " (ขาด)" : "")} · ปลายภาค {(e.final >= 0 ? (e.final * 100).ToString("0") : "-")}{(e.missedFinal ? " (ขาด)" : "")} · คาดการณ์ {svc.ProjectedScore(e):0}", sBody);

            Head("ประวัติรายวิชาที่ประกาศผลแล้ว");
            var sb = new StringBuilder();
            foreach (var e in reg.Record.enrollments) if (e.graded) sb.Append($"{e.code}#{e.attempt}(ภาค{e.termSerial})={e.letter}{(e.passed ? "" : "✗")}  ");
            GUILayout.Label(sb.Length > 0 ? sb.ToString() : "ยังไม่มี", sSmall);

            Head("สร้างประวัติ + เปิดช่วงลงทะเบียนชั้นปี/ภาคที่เลือก (ผ่านกฎจริงทุกภาค → โหลดใหม่)");
            GUILayout.Label("เรียนทุกภาคก่อนหน้าตามแผนด้วยคุณภาพ q (ลงทะเบียน→เข้าเรียนตามตาราง→สอบ→ประกาศผลด้วยสูตรเดิม) · วิชาที่ติ๊ก \"ตก\" จะได้ F และไม่ลงซ้ำ (วิชาที่ต่อยอดจะลงไม่ได้ตามกฎ) · หน่วยกิต/GPA คำนวณจากประวัติที่สร้าง", sSmall);
            GUILayout.BeginHorizontal();
            GUILayout.Label("ชั้นปี", sBody, GUILayout.Width(45));
            int y = Mathf.Clamp(PI(Fget("cb_y", "1"), 1), 1, 4);
            for (int i = 1; i <= 4; i++) if (GUILayout.Button((y == i ? "● " : "") + i, sBtn, GUILayout.Width(45))) fields["cb_y"] = i.ToString();
            GUILayout.Label("ภาค", sBody, GUILayout.Width(35));
            int s2 = Mathf.Clamp(PI(Fget("cb_s", "1"), 1), 1, 2);
            for (int i = 1; i <= 2; i++) if (GUILayout.Button((s2 == i ? "● " : "") + i, sBtn, GUILayout.Width(45))) fields["cb_s"] = i.ToString();
            GUILayout.Label("q (0.4–1)", sBody, GUILayout.Width(70)); string qs = F("cb_q", "0.75", 50);
            float q = Mathf.Clamp(PF(qs, 0.75f), 0.4f, 1f);
            GUILayout.Label($"≈ {cur.GradeFor(q * 100f).letter}", sBody, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            var fail = new List<string>();
            GUILayout.BeginHorizontal(); int c2 = 0;
            foreach (var d in cur.courses)
            {
                if (d == null || d.planYear <= 0) continue;
                bool before = d.planYear < y || (d.planYear == y && d.planSemester < s2);
                if (!before) continue;
                string key = "cb_f_" + d.code;
                bool on = Fget(key, "0") == "1";
                bool nv = GUILayout.Toggle(on, "ตก " + d.code, sToggle, GUILayout.Width(95));
                if (nv != on) fields[key] = nv ? "1" : "0";
                if (nv) fail.Add(d.code);
                if (++c2 % 9 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
            if (B($"สร้างประวัติ → เปิดลงทะเบียนชั้นปี {y} ภาค {s2}", 0, can))
                ApplyScenario(DevScenarios.BuildCustom(cur, reg.creditCapOverride, y, s2, q, fail));

            Head("คำสั่งข้ามเงื่อนไข");
            bool nb = GUILayout.Toggle(bypass, "  เปิดคำสั่ง \"ข้ามเงื่อนไข — สำหรับ Dev\" (ปิดเป็นค่าเริ่มต้น · ผลที่ได้ไม่ถือเป็นหลักฐานว่ากฎปกติผ่านการทดสอบ)", sToggle);
            if (nb != bypass) { bypass = nb; Log(bypass ? "เปิดคำสั่งข้ามเงื่อนไข" : "ปิดคำสั่งข้ามเงื่อนไข"); }
            if (bypass)
            {
                GUILayout.Label("ข้ามเงื่อนไข — สำหรับ Dev", sRed);
                GUILayout.BeginHorizontal();
                if (B("บังคับเปิดช่วงลงทะเบียนภาคนี้", 0, can && t != null && !t.isBreak && !t.confirmed && !t.closed))
                { t.registrationOpen = true; t.lateRegistration = true; reg.NotifyChanged(); BypassUsed = true; Say(true, "[ข้ามเงื่อนไข — สำหรับ Dev] เปิดช่วงลงทะเบียนภาคนี้ถึงสิ้นภาค"); }
                if (B("บังคับยืนยัน (ไม่ตรวจเงื่อนไข)", 0, can && t != null && !t.confirmed && t.selected.Count > 0))
                {
                    var mi = typeof(RegistrationService).GetMethod("ConfirmInternal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (mi != null) { mi.Invoke(svc, null); reg.NotifyChanged(); BypassUsed = true; Say(true, "[ข้ามเงื่อนไข — สำหรับ Dev] บังคับยืนยันรายการที่เลือก"); }
                    else Say(false, "ไม่พบเมธอดยืนยันภายใน");
                }
                GUILayout.EndHorizontal();
            }
        }

        static string StatusName(CourseStatus s) => s == CourseStatus.CanRegister ? "ลงได้" : s == CourseStatus.MissingPrerequisite ? "ลงไม่ได้" : s == CourseStatus.Passed ? "ผ่านแล้ว" : "เลือกแล้ว";

        // ============================================================
        // แท็บ: การสอบ
        // ============================================================
        void TabExam()
        {
            var ctl = ExamMinigameController.Instance; var exam = DevExamTools.Exam;
            bool can = Gate();
            Head("สถานะ");
            GUILayout.Label(ExamStatus(), sBody);
            GUILayout.Label(DevExamTools.SessionSummary(), sBody);
            GUILayout.Label($"จำนวนครั้งที่บันทึกคะแนนลงรายวิชา (ตั้งแต่เปิดเกม): {ExamMinigameController.DevRecordCount} · ให้รางวัลสอบ: {ExamMinigameController.DevRewardCount}", sSmall);

            Head("สอบจริง (ขั้นตอนปกติ — ตรวจวันสอบและสิทธิ์)");
            GUILayout.BeginHorizontal();
            if (B("เข้าห้องสอบผ่านขั้นตอนปกติ (เหมือนกด E ที่ห้องสอบ)", 0, can && ctl != null && !ctl.IsRoomOpen)) { bool ok = DevExamTools.OpenRoomNormal(out var m); Say(ok, m); if (ok) Close(); }
            GUILayout.Label("seed", sBody, GUILayout.Width(35)); F("ex_seed", "", 70);
            GUILayout.Label("(ว่าง = สุ่มปกติ)", sSmall, GUILayout.Width(110));
            GUILayout.EndHorizontal();
            if (ctl != null && ctl.IsRoomOpen && !ctl.HasSessionInProgress)
            {
                foreach (var e in ctl.BuildEntries())
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{e.code} {e.title} — {e.status}: {e.reason}", e.status == ExamMinigameController.EntryStatus.Ready ? sGreen : sBody, GUILayout.Width(700));
                    if (B("เริ่มสอบ", 90, can && e.status == ExamMinigameController.EntryStatus.Ready))
                    {
                        string sd = Fget("ex_seed", ""); int? seed = sd.Trim().Length > 0 ? PI(sd, 0) : (int?)null;
                        if (seed.HasValue) DevRuntimeChecks.SeedsUsed.Add(seed.Value);
                        bool ok = DevExamTools.StartCourse(e.code, seed, out var m); Say(ok, m);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else if (ctl != null && !ctl.IsRoomOpen) GUILayout.Label("(เข้าห้องสอบก่อนเพื่อดูสิทธิ์สอบรายวิชาตามกฎจริง)", sSmall);

            if (ctl != null && ctl.Session != null)
            {
                Head(ctl.IsPreview ? "เครื่องมือรอบ PREVIEW" : "เครื่องมือรอบสอบจริง (ใช้เส้นทางส่งคำตอบเดียวกับผู้เล่น)");
                bool inProg = ctl.HasSessionInProgress;
                bool allow = ctl.IsPreview || can;
                GUILayout.BeginHorizontal();
                if (B("ตอบถูกทั้งหมด", 0, allow && inProg)) { int x = DevExamTools.AutoAnswer(DevExamTools.AnswerMode.AllCorrect, out var m); Say(x >= 0, $"{m} (คะแนนที่ควรได้ {x}/100)"); }
                if (B("ตอบผิดทั้งหมด", 0, allow && inProg)) { int x = DevExamTools.AutoAnswer(DevExamTools.AnswerMode.AllWrong, out var m); Say(x >= 0, $"{m} (คะแนนที่ควรได้ {x}/100)"); }
                if (B("ตอบเพียงบางข้อ", 0, allow && inProg)) { int x = DevExamTools.AutoAnswer(DevExamTools.AnswerMode.Partial, out var m); Say(x >= 0, $"{m} (คะแนนที่ควรได้ {x}/100)"); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (B("ส่ง (ปุ่มส่ง → ยืนยัน)", 0, allow && inProg)) { int rc = ExamMinigameController.DevRecordCount; DevExamTools.Submit(); Say(true, $"ส่งแล้ว · คะแนน {ctl.Session.score100}/100 · บันทึกผล +{ExamMinigameController.DevRecordCount - rc}"); }
                if (B("กดส่งซ้ำ 2 ครั้ง", 0, allow && ctl.Session.started)) { int rc = ExamMinigameController.DevRecordCount; DevExamTools.Submit(); DevExamTools.Submit(); Say(true, $"กดส่ง 2 ครั้ง · บันทึกผลเพิ่ม {ExamMinigameController.DevRecordCount - rc} ครั้ง · สถานะ submitted={ctl.Session.submitted} recorded={ctl.Session.recorded}"); }
                if (B("จำลองหมดเวลา", 0, allow && inProg)) { bool ok = DevExamTools.SimulateTimeout(out var m); Say(ok, m); if (ok) Close(); }
                if (B("Save → Load ระหว่างสอบ (โหลดฉากใหม่)", 0, can && inProg && !ctl.IsPreview)) SaveLoadMidExam();
                if (B("กลับไปหน้าสอบ", 0, ctl.IsRoomOpen || inProg)) { if (inProg) ctl.ResumeWindow(); Close(); }
                if (B("ออกจากห้องสอบ", 0, ctl.IsRoomOpen && !inProg)) { ctl.LeaveExamRoom(); Say(true, "ออกจากห้องสอบ"); }
                GUILayout.EndHorizontal();
            }

            Head("Preview มินิเกม (UI จริง · ไม่บันทึกคะแนน/หน่วยกิต/รางวัล/เซฟ)");
            var codes = DevExamTools.BankCodes();
            string code = Fget("pv_code", codes.Count > 0 ? codes[0] : "");
            GUILayout.BeginHorizontal();
            GUILayout.Label("วิชา", sBody, GUILayout.Width(40));
            foreach (var c in codes) if (GUILayout.Button((c == code ? "● " : "") + c, sBtn, GUILayout.Width(90))) fields["pv_code"] = code = c;
            GUILayout.EndHorizontal();
            int tsel = PI(Fget("pv_type", "-1"), -1);
            GUILayout.BeginHorizontal();
            GUILayout.Label("ชนิด", sBody, GUILayout.Width(40));
            if (GUILayout.Button((tsel < 0 ? "● " : "") + $"ทั้งหมด ({DevExamTools.CountOfType(code, null)})", sBtn, GUILayout.Width(120))) fields["pv_type"] = "-1";
            foreach (ExamQuestionType qt in Enum.GetValues(typeof(ExamQuestionType)))
            {
                int n = DevExamTools.CountOfType(code, qt);
                bool prev = GUI.enabled; GUI.enabled = n > 0;
                if (GUILayout.Button(((int)qt == tsel ? "● " : "") + $"{ExamMinigameLogic.TypeName(qt)} ({n})", sBtn, GUILayout.Width(130))) fields["pv_type"] = ((int)qt).ToString();
                GUI.enabled = prev;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("จำนวนข้อ", sBody, GUILayout.Width(70)); string cnt = F("pv_n", "5", 45);
            GUILayout.Label("เวลา (วิ)", sBody, GUILayout.Width(65)); string tm = F("pv_t", "180", 55);
            GUILayout.Label("seed", sBody, GUILayout.Width(40)); string sd2 = F("pv_seed", "12345", 70);
            GUILayout.Label("ความรู้วิชา 0–100", sBody, GUILayout.Width(120)); string kn = F("pv_k", "75", 45);
            float kv = Mathf.Clamp(PF(kn, 75f), 0f, 100f);
            GUILayout.Label($"→ สิทธิ์คำใบ้ {ExamMinigameLogic.HintAllowance(kv, DevExamTools.Db)} ครั้ง", sGreen, GUILayout.Width(140));
            GUILayout.EndHorizontal();
            bool busy = ctl != null && ctl.HasSessionInProgress;
            if (B("เปิด Preview ใน UI จริง", 240, !DevProfile.InNetworkSession && !busy && codes.Count > 0))
            {
                int seed = PI(sd2, 12345); LastPreviewSeed = seed;
                var s = DevExamTools.BuildPreview(code, tsel >= 0 ? (ExamQuestionType?)tsel : null, PI(cnt, 5), PF(tm, 180f), seed, kv, out var m);
                if (s == null) Say(false, m);
                else { bool ok = DevExamTools.StartPreview(s, out var m2); Say(ok, m + " · " + m2); if (ok) Close(); }
            }
            if (busy) GUILayout.Label("มีรอบสอบ/Preview ค้างอยู่ — ส่งหรือเลิกก่อนเปิด Preview ใหม่", sSmall);

            Head("ตรวจ: การเข้าเรียนปกติต้องไม่เปิดมินิเกม");
            if (B("ตรวจตอนนี้ (นับชั่วโมงเรียน 1 ครั้งผ่านเส้นทางนั่งเรียนจริง)", 0, can)) StartCoroutine(RunSingle(2));
        }

        IEnumerator RunSingle(int which)
        {
            var m = typeof(DevRuntimeChecks).GetMethod(which == 2 ? "CheckAttendanceNoMinigame" : "CheckRealSaveIsolation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m == null) yield break;
            yield return StartCoroutine((IEnumerator)m.Invoke(null, null));
            var r = DevRuntimeChecks.Results.Find(x => x.id == (which == 2 ? "R02" : "R01"));
            if (r != null) Say(r.status == CheckStatus.Pass, $"{r.id} {r.StatusText}: {r.actual}");
        }

        string pendingExamFp; float pendingExamRemaining;

        void SaveLoadMidExam()
        {
            var ctl = ExamMinigameController.Instance;
            pendingExamFp = DevExamTools.Fingerprint(ctl.Session);
            pendingExamRemaining = ctl.Session.remainingSeconds;
            DevProfile.SaveDev(out _);
            bool ok = DevProfile.LoadDev(out var m);
            Say(ok, m);
            if (!ok) pendingExamFp = null;
        }

        IEnumerator VerifyManualSaveLoad(string fp, float remaining)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                var c = ExamMinigameController.Instance;
                if (c != null && c.HasSessionInProgress && c.IsRoomOpen) break;
                yield return null;
            }
            var ctl = ExamMinigameController.Instance;
            var s = ctl != null ? ctl.Session : null;
            string fp1 = DevExamTools.Fingerprint(s);
            float after = s != null ? s.remainingSeconds : -1f;
            bool ok = s != null && s.InProgress && fp1 == fp && after <= remaining + 0.01f && after >= remaining - 2.5f;
            var r = new CheckResult { id = "M01", name = "Save/Load ระหว่างสอบ (สั่งจาก Panel)", status = ok ? CheckStatus.Pass : CheckStatus.Fail,
                expected = $"ชุดข้อ/ลำดับ/คำตอบเหมือนเดิม · เวลาเหลือ ≈ {remaining:0.0} วิ", actual = $"สอบค้าง={(s != null && s.InProgress)} · เหมือนเดิม={fp1 == fp} · เวลา {remaining:0.0} → {after:0.0} วิ" };
            DevRuntimeChecks.Results.RemoveAll(x => x.id == "M01"); DevRuntimeChecks.Results.Add(r);
            Say(ok, $"Save/Load ระหว่างสอบ: {r.StatusText} — {r.actual}");
        }

        // ============================================================
        // แท็บ: เซฟทดสอบ + สถานการณ์สำเร็จรูป
        // ============================================================
        void TabSave()
        {
            Head("โปรไฟล์ทดสอบ");
            GUILayout.Label(DevProfile.Active ? $"สถานะ: อยู่ใน {DevProfile.ProfileName}" : "สถานะ: เกมปกติ", DevProfile.Active ? sGreen : sBody);
            GUILayout.Label($"ที่เก็บเซฟทดสอบ: <persistentDataPath>/dev_test/dev_profile_save.json (แยกจากเซฟผู้เล่น nisit_save_0..2.json)\nช่องเซฟจริงตอนเข้าโหมดทดสอบ: {DevProfile.RealSlotAtEntry} · บันทึก {DevProfile.DevSaves} · โหลด {DevProfile.DevLoads} · กันการเขียน/ลบเซฟจริง {SaveSystem.DevBlockedRealWrites} ครั้ง", sSmall);
            if (DevProfile.Active) { bool u = DevProfile.RealSaveUntouched(out var d); GUILayout.Label(d, u ? sGreen : sRed); }
            GUILayout.BeginHorizontal();
            if (B("เริ่มโปรไฟล์ทดสอบ (คัดลอกจากเกมปัจจุบัน)", 0, !DevProfile.Active && !DevProfile.InNetworkSession)) { bool ok = DevProfile.Enter(out var m); Say(ok, m); }
            if (B("บันทึกเซฟทดสอบ", 0, DevProfile.Active)) { bool ok = DevProfile.SaveDev(out var m); Say(ok, m); }
            if (B("โหลดเซฟทดสอบ", 0, DevProfile.Active)) { bool ok = DevProfile.LoadDev(out var m); Say(ok, m); }
            if (B("รีเซ็ตเซฟทดสอบ…", 0, DevProfile.Active)) confirmReset = true;
            if (B("ออกจากการทดสอบ (โหลดโปรไฟล์เดิมกลับ)", 0, DevProfile.Active)) { bool ok = DevProfile.Exit(out var m); Say(ok, m); }
            GUILayout.EndHorizontal();
            if (confirmReset && DevProfile.Active)
            {
                GUILayout.Label($"ยืนยันรีเซ็ตโปรไฟล์ \"{DevProfile.ProfileName}\" (dev_profile_save.json)? ข้อมูลทดสอบปัจจุบันจะหาย · เซฟจริงไม่ถูกแตะ", sYellow);
                GUILayout.BeginHorizontal();
                if (B("คืนเป็นสำเนาตอนเริ่มทดสอบ", 0)) { confirmReset = false; bool ok = DevProfile.ResetToEntryCopy(out var m); Say(ok, m); }
                if (B("เริ่มเกมใหม่ในโปรไฟล์ทดสอบ", 0)) { confirmReset = false; bool ok = DevProfile.ResetToNewGame(out var m); Say(ok, m); }
                if (B("ยกเลิก", 90)) confirmReset = false;
                GUILayout.EndHorizontal();
            }

            Head("สถานการณ์ทดสอบสำเร็จรูป (สร้างประวัติด้วยกฎจริง → เขียนลงเซฟทดสอบ → โหลดผ่านเส้นทางโหลดเซฟจริง)");
            bool can = Gate();
            var reg = CourseRegistrar.Instance;
            var cur = reg != null && reg.Service != null ? reg.Service.Curriculum : CurriculumDefinition.LoadDefault();
            int cap = reg != null ? reg.creditCapOverride : 0;
            foreach (var d in DevScenarios.All)
            {
                GUILayout.BeginHorizontal();
                if (B($"{d.id} {d.title}", 300, can)) ApplyScenario(DevScenarios.Build(d.id, cur, cap));
                GUILayout.Label(d.description, sSmall);
                GUILayout.EndHorizontal();
            }
            if (LastScenarioResult != null)
            {
                var r = LastScenarioResult;
                Head("ผลสถานการณ์ล่าสุด: " + r.title);
                if (r.skipped) GUILayout.Label("ข้ามการทดสอบ — " + r.skipReason, sYellow);
                else { GUILayout.Label(r.conditionText, sBody); GUILayout.Label("ทดสอบต่อ: " + r.hint, sSmall); }
                foreach (var l in r.log) GUILayout.Label(l, sSmall);
            }
        }

        DevScenarioResult pendingScenario;

        void ApplyScenario(DevScenarioResult r)
        {
            LastScenarioResult = r;
            if (r == null) return;
            if (r.skipped)
            {
                DevRuntimeChecks.Results.RemoveAll(x => x.id == r.id);
                DevRuntimeChecks.Results.Add(new CheckResult { id = r.id, name = "สถานการณ์: " + r.title, status = CheckStatus.Skip, expected = "สร้างเงื่อนไขได้ด้วยกฎจริง", actual = r.skipReason });
                Say(false, $"ข้ามสถานการณ์ \"{r.title}\": {r.skipReason}");
                return;
            }
            var d = DevProfile.CaptureCurrent();
            if (d == null) { Say(false, "เก็บสถานะปัจจุบันไม่สำเร็จ (ต้องอยู่ในโปรไฟล์ทดสอบ)"); return; }
            var cur = CurriculumDefinition.LoadDefault();
            string facNote = "";
            if (cur != null && !cur.AppliesToFaculty(d.facultyIndex) && cur.facultyIndices.Length > 0)
            { d.facultyIndex = cur.facultyIndices[0]; GameSession.SelectedFacultyIndex = d.facultyIndex; facNote = $" · ตั้งคณะเป็น {FacultyCatalog.NameOf(d.facultyIndex)} (ใช้หลักสูตรลงทะเบียน)"; }
            d.hasAcademicRecord = true;
            d.academic = r.record;
            d.currentYear = r.record.classYear;
            d.calendarYear = r.calendarYear;
            d.dayInYear = r.dayInYear;
            d.gameMinutes = r.minutes;
            d.hasExamSession = false;
            d.examSession = new ExamSessionState();
            d.doneExams = new List<string>();
            d.classAttendance = new List<string>();
            var st = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            d.energy = st != null ? st.maxEnergy : 100f; d.health = st != null ? st.maxHealth : 100f; d.hunger = st != null ? st.maxHunger : 100f; d.stress = 0f;
            pendingScenario = r;
            bool ok = DevProfile.ApplySaveData(d, $"{r.id} {r.title}", out var m);
            Say(ok, $"สถานการณ์ \"{r.title}\" — {m}{facNote} · ฟื้นฟูสถานะให้พร้อมทดสอบ");
            if (!ok) pendingScenario = null;
        }

        // หลังโหลด: ตรวจว่าเกมจริงอยู่ในสถานะเดียวกับประวัติที่สร้าง (ชั้นปี/วัน/หน่วยกิต/GPA/ภาค)
        IEnumerator VerifyScenarioAfterLoad(DevScenarioResult r)
        {
            yield return null; yield return null; yield return null;
            var reg = CourseRegistrar.Instance; var prog = DevTimeTools.Prog; var clock = DevTimeTools.Clock;
            if (reg == null || reg.Service == null || prog == null) { yield break; }
            var tmp = new RegistrationService(reg.Service.Curriculum, r.record) { CreditCapOverride = reg.creditCapOverride };
            var t = reg.Record.Current;
            bool ok = reg.IsActive && prog.CalendarYear == r.calendarYear && prog.DayInYear == r.dayInYear && reg.ClassYear == r.record.classYear
                      && reg.Service.EarnedCredits() == tmp.EarnedCredits() && Mathf.Approximately(reg.Service.Gpa(), tmp.Gpa())
                      && reg.Record.enrollments.Count == r.record.enrollments.Count && t != null && r.record.current != null && t.serial == r.record.current.serial && t.semIndex == r.record.current.semIndex;
            var res = new CheckResult
            {
                id = r.id, name = "สถานการณ์: " + r.title, status = ok && r.conditionOk ? CheckStatus.Pass : CheckStatus.Fail,
                expected = r.conditionText.Split('\n')[0] + $" · โหลดแล้วอยู่ปีการศึกษา {r.calendarYear} วัน {r.dayInYear} ชั้นปี {r.record.classYear} หน่วยกิต {tmp.EarnedCredits()} GPA {tmp.Gpa():0.00}",
                actual = (r.conditionText.Contains("\n") ? r.conditionText.Split('\n')[1] : "") + $" · ในเกม: ปีการศึกษา {prog.CalendarYear} วัน {prog.DayInYear} ชั้นปี {reg.ClassYear} หน่วยกิต {reg.Service.EarnedCredits()} GPA {reg.Service.Gpa():0.00} · ประวัติ {reg.Record.enrollments.Count} รายการ" + (clock != null ? " · " + clock.GetTimeString() : ""),
            };
            DevRuntimeChecks.Results.RemoveAll(x => x.id == r.id); DevRuntimeChecks.Results.Add(res);
            DevRuntimeChecks.Results.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            Log($"สถานการณ์ {r.id} โหลดแล้ว: {res.StatusText} · {r.hint}");
            LastScenarioResult = r;
        }

        // ============================================================
        // แท็บ: ผลทดสอบ
        // ============================================================
        void TabResults()
        {
            Head("การตรวจอัตโนมัติในเกม (ใช้ระบบจริงในฉาก)");
            GUILayout.Label("ต้องอยู่ใน DEV TEST PROFILE · ข้อสอบจริง (R06–R08) ต้องอยู่ในสถานการณ์ \"S5 พร้อมเข้าสอบ\" (วันสอบ + วิชาที่มีคลังข้อสอบ) ไม่งั้นจะ \"ข้าม\" · R09 เดินเวลาข้ามวัน (ทำท้ายสุด)", sSmall);
            GUILayout.BeginHorizontal();
            bool idle = !DevRuntimeChecks.Running && !DevTimeTools.Simulating;
            if (B("รันทั้งหมด (รวม Save/Load — โหลดฉากใหม่)", 0, idle && DevProfile.Active)) { Close(); StartCoroutine(DevRuntimeChecks.RunAll(true)); }
            if (B("รันเฉพาะข้อที่ไม่โหลดฉาก", 0, idle && DevProfile.Active)) { Close(); StartCoroutine(DevRuntimeChecks.RunAll(false)); }
            if (B("ล้างผล", 90, idle)) { DevRuntimeChecks.Results.Clear(); DevRuntimeChecks.SeedsUsed.Clear(); }
            if (B("คัดลอกรายงาน", 130)) { GUIUtility.systemCopyBuffer = DevRuntimeChecks.Report(); Say(true, "คัดลอกรายงานแล้ว (Unity version, Scene, สถานการณ์, seed, ผลทดสอบ — ไม่รวม path/ชื่อผู้ใช้/IP/รหัสห้อง)"); }
            GUILayout.EndHorizontal();
            if (BypassUsed) GUILayout.Label("มีการใช้คำสั่ง \"ข้ามเงื่อนไข — สำหรับ Dev\" ในเซสชันนี้ — ผลที่เกี่ยวข้องไม่ถือเป็นหลักฐานว่ากฎปกติผ่านการทดสอบ", sRed);
            int p = 0, f = 0, s = 0;
            foreach (var r in DevRuntimeChecks.Results) { if (r.status == CheckStatus.Pass) p++; else if (r.status == CheckStatus.Fail) f++; else s++; }
            GUILayout.Label($"ผ่าน {p} · ไม่ผ่าน {f} · ข้าม {s}", sBody);
            foreach (var r in DevRuntimeChecks.Results)
            {
                GUILayout.Label($"[{r.StatusText}] {r.id} {r.name}", r.status == CheckStatus.Pass ? sGreen : r.status == CheckStatus.Fail ? sRed : sYellow);
                if (!string.IsNullOrEmpty(r.expected)) GUILayout.Label("   คาดหวัง: " + r.expected, sSmall);
                if (!string.IsNullOrEmpty(r.actual)) GUILayout.Label("   ผลจริง: " + r.actual, sSmall);
                if (!string.IsNullOrEmpty(r.error)) GUILayout.Label("   ข้อผิดพลาด: " + r.error, sRed);
            }
            Head("Unity Test Framework (EditMode)");
            GUILayout.Label("Window ▸ General ▸ Test Runner ▸ EditMode: CourseRegistrationTests · ExamMinigameTests · DevToolsTests (สถานการณ์ทั้ง 9 + เซฟแยก + ตรวจคำตอบ/Preview)", sSmall);
        }
    }
}
#endif
