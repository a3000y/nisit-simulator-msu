#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.DevTools
{
    // ===== ทัวร์ถ่ายภาพหน้า UI ทุกหน้า (Dev · Editor Play Mode เท่านั้น) =====
    //   ใช้ตรวจตัวอักษรล้น/ถูกตัดก่อน-หลังแก้ · ภาพไปที่ Assets/_Project/Shots~/<tag>/<กว้าง>x<สูง>_<หน้า>.png
    //   (โฟลเดอร์ลงท้าย ~ = Unity ไม่ import) · เรียก: UIShotTour.Run("before", 1920,1080, 1280,720)
    //   ไม่แก้เซฟ: หน้าผลเทอมใช้รายงานจำลอง serial 0 · มินิเกมสอบใช้โหมด PREVIEW · ความเครียดคืนค่าเดิมหลังถ่าย
    public class UIShotTour : MonoBehaviour
    {
        public static bool Running { get; private set; }
        public static string LastLog { get; private set; } = "";

        string tag;
        int[] res;

        public static void Run(string tag, params int[] widthHeightPairs)
        {
            if (Running || !Application.isPlaying) return;
            var go = new GameObject("UIShotTour");
            var t = go.AddComponent<UIShotTour>();
            t.tag = tag;
            t.res = widthHeightPairs != null && widthHeightPairs.Length >= 2 ? widthHeightPairs : new[] { 1920, 1080 };
            t.StartCoroutine(t.Tour());
        }

        // ---------- helpers ----------
        static object Call(object target, string method, params object[] args)
        {
            if (target == null) return null;
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return m != null ? m.Invoke(target, args) : null;
        }

        static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        static IEnumerator Wait(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        string dir;
        readonly List<string> log = new List<string>();

        IEnumerator Shot(string res, string name)
        {
            yield return Wait(0.45f);
            string path = $"{dir}/{res}_{name}.png";
            ScreenCapture.CaptureScreenshot(path);
            yield return null; yield return null; yield return Wait(0.1f);
            log.Add(name);
        }

        IEnumerator ShotScrolled(string resolution, string name, GameObject root)
        {
            if (root == null) yield break;
            var scrolls = root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(false);
            var positions = new float[scrolls.Length];
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < scrolls.Length; i++)
            {
                positions[i] = scrolls[i].verticalNormalizedPosition;
                scrolls[i].StopMovement();
                scrolls[i].verticalNormalizedPosition = 0f;
            }
            yield return Shot(resolution, name);
            for (int i = 0; i < scrolls.Length; i++)
                if (scrolls[i] != null) scrolls[i].verticalNormalizedPosition = positions[i];
        }

        static void CloseAll()
        {
            var phone = Find<PhoneController>();
            if (phone != null && phone.IsOpen) phone.Toggle();
            var reg = Find<RegistrationUI>(); if (reg != null) Call(reg, "Close");
            ClassEventUI.Close();
            var term = Find<TermResultUI>(); if (term != null && TermResultUI.IsOpen) term.Close();
            var day = Find<DaySummaryUI>(); if (day != null && DaySummaryUI.IsShowing) Call(day, "Close");
            foreach (var s in UnityEngine.Object.FindObjectsByType<ShopController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (s.IsOpen) s.Close();
            var inv = Find<InventoryUI>(); if (inv != null && inv.IsOpen) inv.Close();
            var ach = Find<AchievementsUI>(); if (ach != null) ach.Close();
            var lvl = Find<LevelSystem>(); if (lvl != null) Call(lvl, "ClosePicker");
            var gift = Find<GiftUI>(); if (gift != null) gift.Close();
            var pause = Find<PauseMenu>(); if (pause != null) pause.Resume();
            var sleep = Find<NisitSimulator.Interaction.SleepController>(); if (sleep != null) sleep.Cancel();
            if (HUDController.Instance != null) HUDController.Instance.ClearToast();
            Time.timeScale = 1f;
        }

        static TermReport FakeReport()
        {
            var r = new TermReport { serial = 0, calendarYear = 2, semIndex = 1, classYear = 2, termGpa = 2.71f, cumulativeGpa = 2.48f, hasGpa = true,
                creditsAttempted = 21, creditsEarnedTerm = 18, creditsEarnedTotal = 57, graduationCredits = 129, probation = false, minGpa = 2f,
                promoted = true, newClassYear = 3 };
            var cur = CurriculumDefinition.LoadDefault();
            var titles = new List<KeyValuePair<string, string>>();
            if (cur != null) foreach (var c in cur.courses) titles.Add(new KeyValuePair<string, string>(c.code, c.title));
            titles.Sort((a, b) => b.Value.Length.CompareTo(a.Value.Length));   // ชื่อยาวสุดก่อน
            for (int i = 0; i < Mathf.Min(8, titles.Count); i++)
            {
                bool fail = i == 2;
                r.rows.Add(new TermReportRow { code = titles[i].Key, title = titles[i].Value, credits = 3, attempt = i == 3 ? 2 : 1, attendance = 0.5f + 0.06f * i,
                    midterm = i == 4 ? -1f : 0.62f, final = 0.71f, missedMidterm = i == 4, bonusMid = i % 2 == 0 ? 0.05f : 0f, score = fail ? 41f : 74f - i,
                    letter = fail ? "F" : i % 3 == 0 ? "B+" : "C+", passed = !fail, retakeSection = i == 5 });
                if (fail) r.retakeCodes.Add(titles[i].Key);
            }
            r.nextStep = "ต้องลงทะเบียนเรียนซ้ำ (ภาคค่ำ): " + string.Join(", ", r.retakeCodes) + " · เปิดลงทะเบียนภาคถัดไปวันที่ 1 ของภาคต้น ปีการศึกษาที่ 3 ในแอป MSG REG (กด TAB)";
            return r;
        }

        static readonly string[] LongToasts =
        {
            "เริ่มเครียดแล้ว — ความรู้ที่ได้ลดลง 10% · ลองนอน/คุยกับเพื่อน/นั่งพัก",
            "นอนกระสับกระส่าย… เครียดจนหลับไม่สนิท ตื่น 07:00 น. พลังงานฟื้นแค่ 75%",
            "เข้าเรียน CS213 ที่ IT-203 ห้องปฏิบัติการคอมพิวเตอร์ ชั้น 2 อาคารคณะวิทยาการสารสนเทศ",
        };

        IEnumerator Tour()
        {
            Running = true;
            // รอ Start ของระบบที่ GameplayBootstrap เพิ่งสร้าง และการจัด HUD ให้เสร็จ
            yield return Wait(3f);
            dir = "Assets/_Project/Shots~/" + tag;
            System.IO.Directory.CreateDirectory(dir);
            var stats = Find<NisitSimulator.Stats.PlayerStats>();
            float stress0 = stats != null ? stats.Stress : 0f;
            bool rib = Application.runInBackground;
            Application.runInBackground = true;

            // กันป้าย interaction/เวลา/การลดสถานะรบกวนภาพ และคืนสถานะแม้ทัวร์ถูกหยุด
            var interaction = Find<NisitSimulator.Player.PlayerInteraction>();
            var clock = Find<NisitSimulator.TimeSystem.GameClock>();
            var decay = Find<NisitSimulator.Stats.StatDecay>();
            bool interactionEnabled = interaction != null && interaction.enabled;
            bool clockEnabled = clock != null && clock.enabled;
            bool decayEnabled = decay != null && decay.enabled;
            float previousScale = Time.timeScale;
            if (interaction != null) interaction.enabled = false;
            if (clock != null) clock.enabled = false;
            if (decay != null) decay.enabled = false;
            try
            {
            for (int ri = 0; ri + 1 < res.Length; ri += 2)
            {
                int w = res[ri], h = res[ri + 1];
                string rs = w + "x" + h;
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "NisitShot " + rs);
                CloseAll();
                HUDController.Prompt("");
                yield return Wait(0.8f);

                // HUD + ป้ายความเครียด + toast ยาว
                if (stats != null) stats.ChangeStress(60f - stats.Stress);
                for (int i = 0; i < LongToasts.Length; i++) { HUDController.Toast(LongToasts[i]); yield return Shot(rs, "hud_toast" + i); }
                if (HUDController.Instance != null) HUDController.Instance.ClearToast();
                HUDController.Prompt("กด E เพื่อนั่งเรียน (เรียนคอมพิวเตอร์) · ความรู้ +9 · พลังงาน -4 · เครียด +4");
                yield return Shot(rs, "hud_prompt");
                HUDController.Prompt("");

                // โทรศัพท์
                var phone = Find<PhoneController>();
                if (phone != null)
                {
                    if (!phone.IsOpen) phone.Toggle();
                    yield return Shot(rs, "phone_home");
                    for (int a = 0; a <= 5; a++)
                    {
                        phone.OpenApp(a);
                        yield return Shot(rs, "phone_app" + a);
                    }
                    phone.GoHome();
                }

                // แอปลงทะเบียน 3 แท็บ
                var reg = RegistrationUI.EnsureExists();
                reg.Open();
                for (int t = 0; t < 3; t++)
                {
                    reg.ShowTab(t); yield return Shot(rs, "reg_tab" + t);
                    yield return ShotScrolled(rs, "reg_tab" + t + "_bottom", reg.gameObject);
                }
                Call(reg, "Close");
                CloseAll();

                // หน้าผลเทอม (รายงานจำลอง — ไม่แก้เซฟ)
                var term = TermResultUI.EnsureExists();
                Call(term, "Show", FakeReport());
                yield return Shot(rs, "term_result");
                yield return ShotScrolled(rs, "term_result_bottom", term.gameObject);
                term.Close();

                // สรุปวัน
                var day = Find<DaySummaryUI>();
                if (day != null) { Call(day, "Show", 5); yield return Shot(rs, "day_summary"); Call(day, "Close"); }

                // หน้าต่างเหตุการณ์ในคาบ (ข้อความยาว)
                ClassEventUI.Show("Pop quiz! อาจารย์ถามกลางคาบ",
                    "อาจารย์หันมาถามว่า \"ในการออกแบบฐานข้อมูลเชิงสัมพันธ์ การทำ Normalization ระดับที่สาม (3NF) มีจุดประสงค์หลักเพื่อขจัดปัญหาใด\" — ตอบให้ถูกเพื่อรับคะแนนพิเศษกลางภาค",
                    new[] { "ขจัด Transitive Dependency ระหว่างแอตทริบิวต์ที่ไม่ใช่คีย์", "ขจัดกลุ่มข้อมูลซ้ำ (Repeating Group) ในตาราง", "ทำให้ทุกตารางมีคีย์หลักแบบผสม (Composite Key)", "ไม่แน่ใจ ขอผ่าน" },
                    _ => { });
                yield return Shot(rs, "class_event");
                ClassEventUI.Close();

                // ร้านค้า / โรงอาหาร / กระเป๋า
                foreach (var s in UnityEngine.Object.FindObjectsByType<ShopController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    s.Open(); yield return Shot(rs, s.storeToInventory ? "shop" : "cafeteria");
                    yield return ShotScrolled(rs, s.storeToInventory ? "shop_bottom" : "cafeteria_bottom", s.gameObject); s.Close();
                }
                var inv = Find<InventoryUI>();
                var invM = InventoryManager.Instance;
                List<string> invSnap = invM != null ? invM.ToSaveList() : null;
                if (inv != null) { inv.Open(); yield return Shot(rs, "inventory"); inv.Close(); }
                // กระเป๋ามีของครบทุกชนิด (ชื่อยาว) → ตรวจช่องล้น แล้วคืนของเดิม
                if (inv != null && invM != null)
                {
                    foreach (var it in ShopController.DefaultCatalog()) invM.Add(it, 3);
                    inv.Open(); yield return Shot(rs, "inventory_full");
                    // ให้ของขวัญ (ต้องมี NPC + ของในกระเป๋า) — ตั้งเป้าหมายเป็น NPC ตัวแรกในฉาก
                    var giftUi = Find<GiftUI>();
                    var npc = Find<NisitSimulator.Interaction.TalkNPC>();
                    inv.Close();
                    if (giftUi != null && npc != null)
                    {
                        var ft = typeof(GiftUI).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
                        var fp = typeof(GiftUI).GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic);
                        var fo = typeof(GiftUI).GetField("open", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (ft != null && fp != null)
                        {
                            ft.SetValue(giftUi, npc); Call(giftUi, "Populate");
                            if (fo != null) fo.SetValue(giftUi, true);
                            var gp = fp.GetValue(giftUi) as GameObject; if (gp != null) gp.SetActive(true);
                            yield return Shot(rs, "gift_open");
                            yield return ShotScrolled(rs, "gift_open_bottom", giftUi.gameObject);
                            giftUi.Close();
                        }
                    }
                    if (invSnap != null) invM.LoadFromList(invSnap);
                }

                // ความสำเร็จ (J) · ความสามารถ (L) · ของขวัญ (H) · Pause
                var ach = Find<AchievementsUI>(); if (ach != null) { Call(ach, "TryOpen"); yield return Shot(rs, "achievements"); yield return ShotScrolled(rs, "achievements_bottom", ach.gameObject); ach.Close(); }
                var lvl = Find<LevelSystem>();
                if (lvl != null)
                {
                    // บังคับมีแต้มความสามารถ 1 แต้มชั่วคราว (picksMade - 1) แล้วคืนค่า
                    var fpm = typeof(LevelSystem).GetField("picksMade", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    object pm0 = fpm != null ? fpm.GetValue(lvl) : null;
                    try
                    {
                        if (fpm != null && pm0 is int) fpm.SetValue(lvl, (int)pm0 - 1);
                        Call(lvl, "OpenPicker"); yield return Shot(rs, "level"); Call(lvl, "ClosePicker");
                    }
                    finally
                    {
                        if (fpm != null && pm0 != null) fpm.SetValue(lvl, pm0);
                    }
                }
                var gift = Find<GiftUI>(); if (gift != null) { Call(gift, "TryOpen"); yield return Shot(rs, "gift"); gift.Close(); }
                var pause = Find<PauseMenu>(); if (pause != null) { Call(pause, "Pause"); yield return Shot(rs, "pause"); pause.Resume(); }

                // มินิเกมสอบ (PREVIEW — ไม่บันทึกคะแนน)
                var codes = DevExamTools.BankCodes();
                if (codes.Count > 0)
                {
                    var st = DevExamTools.BuildPreview(codes[0], null, 3, 180f, 7, 60f, out _);
                    if (st != null && DevExamTools.StartPreview(st, out _))
                    {
                        yield return Shot(rs, "exam_question");
                        var ctl = ExamMinigameController.EnsureExists();
                        ctl.Submit(false);
                        yield return Shot(rs, "exam_result");
                        ctl.LeaveExamRoom();
                    }
                }
                CloseAll();
            }

            }
            finally
            {
                CloseAll();
                if (stats != null) stats.ChangeStress(stress0 - stats.Stress);
                if (interaction != null) interaction.enabled = interactionEnabled;
                if (clock != null) clock.enabled = clockEnabled;
                if (decay != null) decay.enabled = decayEnabled;
                Time.timeScale = previousScale;
                Application.runInBackground = rib;
                Running = false;
            }
            LastLog = $"[UIShotTour] {tag}: {log.Count} ภาพ → {dir}";
            Debug.Log(LastLog);
            Running = false;
            Destroy(gameObject);
        }
    }
}
#endif
