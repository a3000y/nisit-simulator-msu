#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.Core;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.DevTools
{
    // ===== โปรไฟล์ทดสอบ (DEV TEST PROFILE) — Editor / Development Build เท่านั้น =====
    //   • เข้าโหมดทดสอบ: เก็บสถานะเกมจริงในหน่วยความจำเป็นสำเนา (return_snapshot) + คัดลอกเป็นเซฟทดสอบ แล้วเปลี่ยนเส้นทางเซฟทั้งหมดไปที่โฟลเดอร์ dev_test
    //   • ระหว่างทดสอบ SaveSystem.DevGuard กันการเขียน/ลบไฟล์เซฟช่องจริงทุกกรณี (autosave ขึ้นวันใหม่ / ปิดเกม / จบเกม / สอบ)
    //   • โหลด/รีเซ็ต/สถานการณ์สำเร็จรูป = เขียนเซฟทดสอบแล้วโหลดฉากใหม่ผ่านเส้นทางโหลดเซฟจริง (ทุกระบบอ่านจากเซฟทดสอบ)
    //   • ออก: โหลดฉากใหม่จากสำเนาสถานะเดิม → ไม่มีค่าจาก Dev เหลือในหน่วยความจำ · คืน PlayerPrefs ที่ระบบเกมเขียนระหว่างทดสอบ (ความสำเร็จ / NG+)
    public static class DevProfile
    {
        public const string ProfileName = "DEV TEST PROFILE";
        static readonly string[] GuardedPrefs = { "nisit_ach", "nisit_ngplus" };   // โปรไฟล์ถาวรที่ระบบเกมเขียนเองระหว่างเล่น

        public static string Dir => Path.Combine(Application.persistentDataPath, "dev_test");
        public static string ProfilePath => Path.Combine(Dir, "dev_profile_save.json");
        public static string ReturnSnapshotPath => Path.Combine(Dir, "return_snapshot.json");
        static string MarkerPath => Path.Combine(Dir, "session_active.json");

        public static bool Active { get; private set; }
        public static string RealFingerprintAtEntry { get; private set; } = "";
        public static int RealSlotAtEntry { get; private set; }
        public static int DevSaves, DevLoads;
        public static string LastScenario = "";     // ชื่อสถานการณ์ล่าสุดที่โหลด (ไว้ใส่รายงาน)

        [Serializable]
        class Marker
        {
            public string started;
            public int slot;
            public string[] keys;
            public bool[] had;
            public string[] strVals;
            public int[] intVals;
            public bool[] isInt;
        }

        public static bool InNetworkSession
        {
            get
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                return nm != null && (nm.IsClient || nm.IsServer || nm.IsListening);
            }
        }

        public static bool InGameplayScene => SceneManager.GetActiveScene().name == GameSession.GameplayScene;

        // ---------- เริ่มต้นแอป: ถ้าปิดเกมค้างอยู่ในโหมดทดสอบ → คืน PlayerPrefs ที่เก็บไว้ ----------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Active = false;
            SaveSystem.DevGuard = false;
            SaveSystem.DevPathOverride = null;
            SaveSystem.DevReadOverride = null;
            try
            {
                if (File.Exists(MarkerPath))
                {
                    RestorePrefs(JsonUtility.FromJson<Marker>(File.ReadAllText(MarkerPath)));
                    File.Delete(MarkerPath);
                    Debug.LogWarning("[DEV] พบโหมดทดสอบค้างจากการเปิดครั้งก่อน — คืนค่าโปรไฟล์ถาวร (ความสำเร็จ/NG+) แล้ว · เซฟจริงไม่ถูกแตะ");
                }
            }
            catch (Exception e) { Debug.LogWarning("[DEV] คืนค่า marker ไม่สำเร็จ: " + e.Message); }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        // ปิดเกม/ออกจาก Play Mode ขณะยังอยู่ในโหมดทดสอบ → คืน PlayerPrefs ที่ระบบเกมเขียนระหว่างทดสอบทันที (เซฟจริงถูกกันอยู่แล้วโดย DevGuard)
        static void OnQuitting()
        {
            if (!Active && !exiting) return;
            RestoreMarkerPrefs();
            Debug.Log("[DEV] ปิดเกมระหว่างโหมดทดสอบ — คืนค่าโปรไฟล์ถาวร (ความสำเร็จ/NG+) แล้ว");
        }

        // ออกจากฉากเล่นเกมไปเมนู/ล็อบบี้ระหว่างทดสอบ → ออกจากโหมดทดสอบอัตโนมัติ (ไม่ให้เมนู \"เล่นต่อ\" เห็นเซฟทดสอบ)
        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (!Active)
            {
                if (SaveSystem.DevGuard && !exiting) { SaveSystem.DevGuard = false; }
                if (exiting) { exiting = false; SaveSystem.DevGuard = false; DevPanel.Log("ออกจากโหมดทดสอบแล้ว — โหลดสถานะเดิมจากสำเนา · " + VerifyRealUntouched()); }
                return;
            }
            if (s.name != GameSession.GameplayScene)
            {
                Active = false;
                SaveSystem.DevPathOverride = null;
                SaveSystem.DevReadOverride = null;
                SaveSystem.DevGuard = false;
                RestoreMarkerPrefs();
                Debug.LogWarning("[DEV] ออกจากฉากเล่นเกมระหว่างทดสอบ → ออกจากโหมดทดสอบอัตโนมัติ (เซฟจริงไม่ถูกแตะ · สำเนาสถานะเดิมอยู่ที่ dev_test/return_snapshot.json)");
            }
        }

        static bool exiting;

        // ---------- เข้าโหมดทดสอบ (คัดลอกข้อมูลเกมปัจจุบัน → โปรไฟล์ Dev) ----------
        public static bool Enter(out string msg)
        {
            if (Active) { msg = "อยู่ในโปรไฟล์ทดสอบแล้ว"; return true; }
            if (InNetworkSession) { msg = "ใช้โปรไฟล์ทดสอบไม่ได้ระหว่างเล่นหลายคน — ออกจากห้อง Multiplayer ก่อน (เกมนี้ไม่มีห้อง Dev ที่ Host ตรวจสอบสิทธิ์ได้)"; return false; }
            if (!InGameplayScene) { msg = "เข้าโหมดทดสอบได้เฉพาะในฉากเล่นเกม (" + GameSession.GameplayScene + ")"; return false; }
            var gm = GameManager.Instance;
            if (gm != null && (gm.State == GameState.GameOver || gm.State == GameState.Win)) { msg = "เกมจบแล้ว — เริ่มเกม/โหลดเซฟก่อน"; return false; }
            var mini = NisitSimulator.Academics.ExamMinigame.ExamMinigameController.Instance;
            if (mini != null && mini.IsPreview && mini.HasSessionInProgress) { msg = "ปิด Preview ข้อสอบก่อน"; return false; }
            if (UnityEngine.Object.FindFirstObjectByType<NisitSimulator.Stats.PlayerStats>() == null) { msg = "ไม่พบ PlayerStats ในฉาก"; return false; }

            try
            {
                Directory.CreateDirectory(Dir);
                RealSlotAtEntry = GameSession.SaveSlot;
                RealFingerprintAtEntry = Fingerprint(SaveSystem.RealSlotPath(RealSlotAtEntry));
                File.WriteAllText(MarkerPath, JsonUtility.ToJson(CapturePrefs()));

                // เก็บสถานะในหน่วยความจำตอนนี้ด้วยตัวเก็บเซฟจริง (SaveManager) → สำเนาไว้คืนตอนออก
                SaveSystem.DevGuard = true;
                if (File.Exists(ReturnSnapshotPath)) File.Delete(ReturnSnapshotPath);
                SaveSystem.DevPathOverride = ReturnSnapshotPath;
                SaveManager.Save();
                if (!File.Exists(ReturnSnapshotPath)) throw new Exception("เก็บสถานะเกมปัจจุบันไม่สำเร็จ");
                File.Copy(ReturnSnapshotPath, ProfilePath, true);
                SaveSystem.DevPathOverride = ProfilePath;
                SaveSystem.DevReadOverride = null;
                Active = true;
                DevSaves = DevLoads = 0;
                msg = "เข้าโปรไฟล์ทดสอบแล้ว — คัดลอกสถานะเกมปัจจุบันไปเป็นเซฟทดสอบ · autosave ทั้งหมดเขียนลง dev_test เท่านั้น";
                return true;
            }
            catch (Exception e)
            {
                SaveSystem.DevPathOverride = null;
                SaveSystem.DevGuard = false;
                try { if (File.Exists(MarkerPath)) File.Delete(MarkerPath); } catch { }
                msg = "เข้าโหมดทดสอบไม่สำเร็จ: " + e.Message;
                return false;
            }
        }

        // ---------- บันทึก / โหลด / รีเซ็ต (เฉพาะเซฟทดสอบ) ----------
        public static bool SaveDev(out string msg)
        {
            if (!Active) { msg = "ยังไม่ได้เข้าโปรไฟล์ทดสอบ"; return false; }
            SaveManager.Save();
            DevSaves++;
            msg = "บันทึกเซฟทดสอบแล้ว (" + Path.GetFileName(ProfilePath) + ")";
            return true;
        }

        public static bool LoadDev(out string msg)
        {
            if (!Active) { msg = "ยังไม่ได้เข้าโปรไฟล์ทดสอบ"; return false; }
            if (!File.Exists(ProfilePath)) { msg = "ยังไม่มีเซฟทดสอบ — กดบันทึกก่อน"; return false; }
            DevLoads++;
            GameSession.PendingLoad = true;
            GameSession.IsContinue = true;
            ReloadGameplay();
            msg = "กำลังโหลดเซฟทดสอบ (โหลดฉากใหม่ผ่านเส้นทางโหลดเซฟจริง)";
            return true;
        }

        // รีเซ็ต: คืนเซฟทดสอบเป็นสำเนาตอนเริ่มทดสอบ แล้วโหลด
        public static bool ResetToEntryCopy(out string msg)
        {
            if (!Active) { msg = "ยังไม่ได้เข้าโปรไฟล์ทดสอบ"; return false; }
            if (!File.Exists(ReturnSnapshotPath)) { msg = "ไม่พบสำเนาตอนเริ่มทดสอบ"; return false; }
            File.Copy(ReturnSnapshotPath, ProfilePath, true);
            LastScenario = "รีเซ็ต (สำเนาตอนเริ่มทดสอบ)";
            return LoadDev(out msg);
        }

        // รีเซ็ต: เริ่มเกมใหม่ในโปรไฟล์ทดสอบ (เส้นทางเกมใหม่จริง: เงินตั้งต้น ปี 1 ภาค 1 เปิดลงทะเบียน)
        public static bool ResetToNewGame(out string msg)
        {
            if (!Active) { msg = "ยังไม่ได้เข้าโปรไฟล์ทดสอบ"; return false; }
            if (File.Exists(ProfilePath)) File.Delete(ProfilePath);
            LastScenario = "รีเซ็ต (เกมใหม่)";
            GameSession.PendingLoad = false;
            GameSession.IsContinue = false;
            DevLoads++;
            ReloadGameplay();
            msg = "เริ่มเกมใหม่ในโปรไฟล์ทดสอบ";
            return true;
        }

        // เขียนข้อมูลสถานการณ์ลงเซฟทดสอบแล้วโหลด (ใช้โดยสถานการณ์สำเร็จรูป)
        public static bool ApplySaveData(SaveData d, string scenarioName, out string msg)
        {
            if (!Active) { msg = "ต้องเข้าโปรไฟล์ทดสอบก่อน"; return false; }
            if (d == null) { msg = "ไม่มีข้อมูล"; return false; }
            File.WriteAllText(ProfilePath, JsonUtility.ToJson(d, true));
            LastScenario = scenarioName;
            return LoadDev(out msg);
        }

        // สถานะเกมตอนนี้ในรูป SaveData (เก็บผ่าน SaveManager จริงลงเซฟทดสอบ แล้วอ่านกลับ)
        public static SaveData CaptureCurrent()
        {
            if (!Active) return null;
            SaveManager.Save();
            return File.Exists(ProfilePath) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(ProfilePath)) : null;
        }

        // ---------- ออกจากโหมดทดสอบ: โหลดโปรไฟล์เดิมกลับ ----------
        public static bool Exit(out string msg)
        {
            if (!Active) { msg = "ไม่ได้อยู่ในโปรไฟล์ทดสอบ"; return false; }
            if (!File.Exists(ReturnSnapshotPath)) { msg = "ไม่พบสำเนาสถานะเดิม — ยกเลิกการออก (เซฟจริงยังไม่ถูกแตะ)"; return false; }
            Active = false;
            exiting = true;
            SaveSystem.DevPathOverride = null;
            SaveSystem.DevReadOverride = ReturnSnapshotPath;   // ทุกระบบอ่านสถานะเดิมจากสำเนา จนกว่าจะมีการเซฟจริงครั้งถัดไป
            // DevGuard ยังเปิดไว้จนโหลดฉากเสร็จ (กันเขียนเซฟจริงจากหน่วยความจำ Dev ในเฟรมสุดท้าย)
            RestoreMarkerPrefs();
            DevTimeTools.ResetSpeedState();
            GameSession.PendingLoad = true;
            GameSession.IsContinue = true;
            LastScenario = "";
            ReloadGameplay();
            msg = "ออกจากโหมดทดสอบ — โหลดโปรไฟล์เดิมกลับ";
            return true;
        }

        static void ReloadGameplay()
        {
            Time.timeScale = 1f;
            NisitSimulator.TimeSystem.GameClock.Suspended = false;
            SceneManager.LoadScene(GameSession.GameplayScene);
        }

        // ---------- ตรวจว่าเซฟจริงไม่ถูกเปลี่ยน ----------
        public static bool RealSaveUntouched(out string detail)
        {
            string now = Fingerprint(SaveSystem.RealSlotPath(RealSlotAtEntry));
            bool same = now == RealFingerprintAtEntry;
            detail = same ? "เซฟจริงช่อง " + RealSlotAtEntry + " ไม่เปลี่ยน (" + Short(now) + ")"
                          : "เซฟจริงช่อง " + RealSlotAtEntry + " เปลี่ยน! ก่อน " + Short(RealFingerprintAtEntry) + " → ตอนนี้ " + Short(now);
            return same;
        }

        static string VerifyRealUntouched()
        {
            RealSaveUntouched(out var d);
            return d + (SaveSystem.DevBlockedRealWrites > 0 ? $" · กันการเขียนเซฟจริงไว้ {SaveSystem.DevBlockedRealWrites} ครั้ง" : "");
        }

        public static string Fingerprint(string path)
        {
            if (!File.Exists(path)) return "none";
            var bytes = File.ReadAllBytes(path);
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return bytes.Length + ":" + BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "");
        }

        static string Short(string fp) => fp == null ? "-" : fp.Length > 16 ? fp.Substring(0, 16) + "…" : fp;

        // ---------- PlayerPrefs ที่ระบบเกมเขียนเอง ----------
        static Marker CapturePrefs()
        {
            int n = GuardedPrefs.Length;
            var m = new Marker { started = DateTime.Now.ToString("s"), slot = GameSession.SaveSlot, keys = GuardedPrefs, had = new bool[n], strVals = new string[n], intVals = new int[n], isInt = new bool[n] };
            for (int i = 0; i < n; i++)
            {
                string k = GuardedPrefs[i];
                m.had[i] = PlayerPrefs.HasKey(k);
                m.isInt[i] = k == "nisit_ngplus";
                if (m.isInt[i]) m.intVals[i] = PlayerPrefs.GetInt(k, 0); else m.strVals[i] = PlayerPrefs.GetString(k, "");
            }
            return m;
        }

        static void RestoreMarkerPrefs()
        {
            try
            {
                if (!File.Exists(MarkerPath)) return;
                RestorePrefs(JsonUtility.FromJson<Marker>(File.ReadAllText(MarkerPath)));
                File.Delete(MarkerPath);
            }
            catch (Exception e) { Debug.LogWarning("[DEV] คืน PlayerPrefs ไม่สำเร็จ: " + e.Message); }
        }

        static void RestorePrefs(Marker m)
        {
            if (m == null || m.keys == null) return;
            for (int i = 0; i < m.keys.Length; i++)
            {
                string k = m.keys[i];
                if (!m.had[i]) { PlayerPrefs.DeleteKey(k); continue; }
                if (m.isInt[i]) PlayerPrefs.SetInt(k, m.intVals[i]); else PlayerPrefs.SetString(k, m.strVals[i]);
            }
            PlayerPrefs.Save();
        }
    }
}
#endif
