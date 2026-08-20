using UnityEngine;
using NisitSimulator.Stats;

namespace NisitSimulator.Core
{
    // ตัวจัดการเสียงกลาง — SFX (one-shot) + เพลงพื้นหลัง (loop)
    //   Master คุมผ่าน AudioListener.volume (SettingsController) · SFX/Music ใช้ vol_sfx/vol_music
    //   สร้าง+ต่อคลิปโดย Editor tool (Nisit -> Build Audio)
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager I;

        [Header("คลิปเสียง (เซ็ตโดย Editor)")]
        public AudioClip click, notify, coin, success, error, footstep, jump, sleep, whoosh, music;
        public AudioClip fanfare, eat, page, ambient;   // ดีใจ / กินข้าว / เปิดหน้า / บรรยากาศ

        [Header("แหล่งเสียง")]
        public AudioSource sfxSource;
        public AudioSource musicSource;
        public AudioSource ambientSource;   // เสียงบรรยากาศ (นก/ลม) วนลูป

        private PlayerStats stats;
        private int lastMoney;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void Start()
        {
            if (sfxSource != null) sfxSource.volume = PlayerPrefs.GetFloat("vol_sfx", 0.9f);
            if (musicSource != null)
            {
                musicSource.clip = music; musicSource.loop = true;
                musicSource.volume = PlayerPrefs.GetFloat("vol_music", 0.8f);
                if (music != null) musicSource.Play();
            }
            if (ambientSource != null)
            {
                ambientSource.clip = ambient; ambientSource.loop = true;
                ambientSource.volume = PlayerPrefs.GetFloat("vol_ambient", 0.7f);
                if (ambient != null) ambientSource.Play();
            }

            // เสียงเงินอัตโนมัติ (ได้เงิน = ปิ๊ง)
            stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null) { lastMoney = stats.Money; stats.OnMoneyChanged += OnMoney; }
        }

        void OnDestroy() { if (stats != null) stats.OnMoneyChanged -= OnMoney; if (I == this) I = null; }

        void OnMoney(int v) { if (v > lastMoney) PlayClip(coin); lastMoney = v; }

        public void PlayClip(AudioClip c, float vol = 1f)
        {
            if (c != null && sfxSource != null) sfxSource.PlayOneShot(c, vol);
        }

        // ---------- static helpers (null-safe เรียกจากที่ไหนก็ได้) ----------
        public static void Click()    { if (I != null) I.PlayClip(I.click); }
        public static void Notify()   { if (I != null) I.PlayClip(I.notify, 0.8f); }
        public static void Coin()     { if (I != null) I.PlayClip(I.coin); }
        public static void Success()  { if (I != null) I.PlayClip(I.success); }
        public static void Error()    { if (I != null) I.PlayClip(I.error); }
        public static void Footstep() { if (I != null) I.PlayClip(I.footstep, 0.45f); }
        public static void Jump()     { if (I != null) I.PlayClip(I.jump, 0.7f); }
        public static void Sleep()    { if (I != null) I.PlayClip(I.sleep); }
        public static void Whoosh()   { if (I != null) I.PlayClip(I.whoosh, 0.6f); }
        public static void Fanfare()  { if (I != null) I.PlayClip(I.fanfare, 0.9f); }   // ดีใจ/จบการศึกษา
        public static void Eat()      { if (I != null) I.PlayClip(I.eat, 0.8f); }        // กินข้าว
        public static void Page()     { if (I != null) I.PlayClip(I.page, 0.55f); }      // เปิดหน้า/เปลี่ยนแอป
    }
}
