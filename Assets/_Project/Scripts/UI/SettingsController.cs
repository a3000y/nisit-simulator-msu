using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.UI
{
    // ตั้งค่าเสียง + กราฟิก — เสียงมีผลทันที · กราฟิก (คุณภาพ/เต็มจอ) เป็นปุ่มวน · เก็บใน PlayerPrefs
    public class SettingsController : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider voiceSlider;     // เสียงพูด NPC / ตอบรับปุ่ม
        public Slider ambientSlider;   // เสียงบรรยากาศ (ลม/นก/ในตึก)
        public Button qualityButton;   // กราฟิก: วนระดับคุณภาพ
        public Button fullscreenButton;// กราฟิก: สลับเต็มจอ/หน้าต่าง
        public Button resolutionButton;// กราฟิก: วนความละเอียด
        public Button closeButton;
        public GameObject panel;

        private TMP_Text qualityLabel, fullscreenLabel, resolutionLabel;
        private System.Collections.Generic.List<Vector2Int> resList;
        private int resIndex;

        // ใช้ค่าที่เซฟไว้ตอนเปิดแอป (ก่อนโหลดฉากแรก) — เพราะแผงตั้งค่าเริ่มปิด Start จึงยังไม่รัน
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedOnLaunch()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("vol_master", 1f);
            if (PlayerPrefs.HasKey("gfx_quality"))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("gfx_quality"), 0, QualitySettings.names.Length - 1), true);
            if (PlayerPrefs.HasKey("gfx_fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt("gfx_fullscreen") == 1;
            if (PlayerPrefs.HasKey("gfx_resW"))
                Screen.SetResolution(PlayerPrefs.GetInt("gfx_resW"), PlayerPrefs.GetInt("gfx_resH"), Screen.fullScreenMode);
        }

        void Start()
        {
            float m  = PlayerPrefs.GetFloat("vol_master", 1f);
            float mu = PlayerPrefs.GetFloat("vol_music", 0.8f);
            float sf = PlayerPrefs.GetFloat("vol_sfx", 0.9f);
            float vo = PlayerPrefs.GetFloat("vol_voice", 0.9f);
            float am = PlayerPrefs.GetFloat("vol_ambient", 0.7f);

            AudioListener.volume = m;

            if (masterSlider)  { masterSlider.value = m;   masterSlider.onValueChanged.AddListener(SetMaster); }
            if (musicSlider)   { musicSlider.value = mu;   musicSlider.onValueChanged.AddListener(SetMusic); }
            if (sfxSlider)     { sfxSlider.value = sf;     sfxSlider.onValueChanged.AddListener(SetSfx); }
            if (voiceSlider)   { voiceSlider.value = vo;   voiceSlider.onValueChanged.AddListener(SetVoice); }
            if (ambientSlider) { ambientSlider.value = am; ambientSlider.onValueChanged.AddListener(SetAmbient); }
            // ---- กราฟิก ----
            int q = PlayerPrefs.GetInt("gfx_quality", QualitySettings.GetQualityLevel());
            QualitySettings.SetQualityLevel(Mathf.Clamp(q, 0, QualitySettings.names.Length - 1), true);
            bool fs = PlayerPrefs.GetInt("gfx_fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            Screen.fullScreen = fs;

            if (qualityButton != null)
            {
                qualityLabel = qualityButton.GetComponentInChildren<TMP_Text>();
                UpdateQualityLabel();
                qualityButton.onClick.AddListener(CycleQuality);
            }
            if (fullscreenButton != null)
            {
                fullscreenLabel = fullscreenButton.GetComponentInChildren<TMP_Text>();
                UpdateFullscreenLabel();
                fullscreenButton.onClick.AddListener(ToggleFullscreen);
            }
            if (resolutionButton != null)
            {
                resolutionLabel = resolutionButton.GetComponentInChildren<TMP_Text>();
                BuildResList();
                UpdateResLabel();
                resolutionButton.onClick.AddListener(CycleResolution);
            }

            if (closeButton && panel) closeButton.onClick.AddListener(() => panel.SetActive(false));
        }

        void BuildResList()
        {
            resList = new System.Collections.Generic.List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!resList.Contains(v)) resList.Add(v);
            }
            if (resList.Count == 0) resList.Add(new Vector2Int(Screen.width, Screen.height));
            // หา index ปัจจุบัน
            resIndex = resList.FindIndex(v => v.x == Screen.width && v.y == Screen.height);
            if (resIndex < 0) resIndex = resList.Count - 1;
        }

        private void CycleResolution()
        {
            if (resList == null || resList.Count == 0) return;
            resIndex = (resIndex + 1) % resList.Count;
            var v = resList[resIndex];
            Screen.SetResolution(v.x, v.y, Screen.fullScreenMode);
            PlayerPrefs.SetInt("gfx_resW", v.x); PlayerPrefs.SetInt("gfx_resH", v.y); PlayerPrefs.Save();
            UpdateResLabel();
        }
        private void UpdateResLabel()
        {
            if (resolutionLabel != null && resList != null && resList.Count > 0)
                resolutionLabel.text = $"ความละเอียด: {resList[resIndex].x}×{resList[resIndex].y}";
        }

        private void CycleQuality()
        {
            int n = QualitySettings.names.Length;
            int next = (QualitySettings.GetQualityLevel() + 1) % n;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetInt("gfx_quality", next); PlayerPrefs.Save();
            UpdateQualityLabel();
        }
        private void UpdateQualityLabel()
        {
            if (qualityLabel != null) qualityLabel.text = "คุณภาพ: " + QualityThai(QualitySettings.GetQualityLevel());
        }
        // แปลงระดับคุณภาพเป็นไทย (แทนชื่อ level เช่น PC/Mobile ที่ผู้เล่นงง)
        static string QualityThai(int i)
        {
            int n = QualitySettings.names.Length;
            if (n <= 1) return "ปกติ";
            if (i <= 0) return "ต่ำ";
            if (i >= n - 1) return "สูง";
            return "กลาง";
        }
        private void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            PlayerPrefs.SetInt("gfx_fullscreen", Screen.fullScreen ? 1 : 0); PlayerPrefs.Save();
            UpdateFullscreenLabel();
        }
        private void UpdateFullscreenLabel()
        {
            if (fullscreenLabel != null) fullscreenLabel.text = "เต็มจอ: " + (Screen.fullScreen ? "เปิด" : "ปิด");
        }

        private void SetMaster(float v)  { AudioListener.volume = v; Save("vol_master", v); }
        private void SetMusic(float v)   { Save("vol_music", v);   var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.musicSource != null) s.musicSource.volume = v; }
        private void SetSfx(float v)     { Save("vol_sfx", v);     var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.sfxSource != null) s.sfxSource.volume = v; }
        private void SetVoice(float v)   { Save("vol_voice", v);   var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.voiceSource != null) s.voiceSource.volume = v; }
        private void SetAmbient(float v) { Save("vol_ambient", v); var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.ambientSource != null) s.ambientSource.volume = v; }
        private void Save(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); }
    }
}
