using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.UI
{
    // ตั้งค่าเสียง + กราฟิก — เสียงมีผลทันที · กราฟิก: คุณภาพ/ความละเอียดเลือกด้วย ◀▶ · เต็มจอเป็นสวิตช์ · เก็บใน PlayerPrefs
    public class SettingsController : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider voiceSlider;     // เสียงพูด NPC / ตอบรับปุ่ม
        public Slider ambientSlider;   // เสียงบรรยากาศ (ลม/นก/ในตึก)

        [Header("กราฟิก — ตัวเลือกแบบ ◀ ค่า ▶")]
        public Button qualityPrev, qualityNext;  // คุณภาพ: เลือกซ้าย/ขวา
        public TMP_Text qualityValue;
        public Button resPrev, resNext;           // ความละเอียด: เลือกซ้าย/ขวา
        public TMP_Text resValue;
        [Header("กราฟิก — สวิตช์เต็มจอ")]
        public Button fullscreenToggle;           // ปุ่มรางสวิตช์
        public RectTransform fullscreenKnob;      // ลูกบิดเลื่อนซ้าย(ปิด)/ขวา(เปิด)
        public Image fullscreenTrack;             // ราง(เปลี่ยนสีเขียวเมื่อเปิด)
        public TMP_Text fullscreenValue;          // ข้อความ เปิด/ปิด
        public float knobOnX = 45f;               // ระยะเลื่อนลูกบิดเมื่อเปิด

        public Button closeButton;
        public GameObject panel;

        [Header("แท็บ")]
        public GameObject audioGroup, graphicsGroup;
        public Button audioTab, graphicsTab;

        private System.Collections.Generic.List<Vector2Int> resList;
        private int resIndex;

        static readonly Color TrackOff = new Color(0.55f, 0.55f, 0.62f);
        static readonly Color TrackOn  = new Color(0.42f, 0.80f, 0.52f);

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

            // คุณภาพ: ◀ / ▶
            UpdateQualityLabel();
            if (qualityPrev != null) qualityPrev.onClick.AddListener(() => CycleQuality(-1));
            if (qualityNext != null) qualityNext.onClick.AddListener(() => CycleQuality(+1));
            // ความละเอียด: ◀ / ▶
            BuildResList();
            UpdateResLabel();
            if (resPrev != null) resPrev.onClick.AddListener(() => CycleResolution(-1));
            if (resNext != null) resNext.onClick.AddListener(() => CycleResolution(+1));
            // เต็มจอ: สวิตช์
            UpdateFullscreenLabel();
            if (fullscreenToggle != null) fullscreenToggle.onClick.AddListener(ToggleFullscreen);

            if (closeButton && panel) closeButton.onClick.AddListener(() => panel.SetActive(false));

            if (audioTab != null) audioTab.onClick.AddListener(() => ShowTab(true));
            if (graphicsTab != null) graphicsTab.onClick.AddListener(() => ShowTab(false));
            if (audioGroup != null || graphicsGroup != null) ShowTab(true);   // เริ่มที่แท็บเสียง
        }

        void ShowTab(bool audio)
        {
            if (audioGroup != null) audioGroup.SetActive(audio);
            if (graphicsGroup != null) graphicsGroup.SetActive(!audio);
            if (audioTab != null) audioTab.transform.localScale = Vector3.one * (audio ? 0.94f : 0.8f);
            if (graphicsTab != null) graphicsTab.transform.localScale = Vector3.one * (audio ? 0.8f : 0.94f);
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

        private void CycleResolution(int dir)
        {
            if (resList == null || resList.Count == 0) return;
            resIndex = ((resIndex + dir) % resList.Count + resList.Count) % resList.Count;
            var v = resList[resIndex];
            Screen.SetResolution(v.x, v.y, Screen.fullScreenMode);
            PlayerPrefs.SetInt("gfx_resW", v.x); PlayerPrefs.SetInt("gfx_resH", v.y); PlayerPrefs.Save();
            UpdateResLabel();
        }
        private void UpdateResLabel()
        {
            if (resValue != null && resList != null && resList.Count > 0)
                resValue.text = $"{resList[resIndex].x}×{resList[resIndex].y}";
        }

        private void CycleQuality(int dir)
        {
            int n = QualitySettings.names.Length;
            int next = ((QualitySettings.GetQualityLevel() + dir) % n + n) % n;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetInt("gfx_quality", next); PlayerPrefs.Save();
            UpdateQualityLabel();
        }
        private void UpdateQualityLabel()
        {
            if (qualityValue != null) qualityValue.text = QualityThai(QualitySettings.GetQualityLevel());
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
        // เลื่อนลูกบิด + เปลี่ยนสีราง + ข้อความ ตามสถานะเต็มจอ
        private void UpdateFullscreenLabel()
        {
            bool fs = Screen.fullScreen;
            if (fullscreenKnob != null) fullscreenKnob.anchoredPosition = new Vector2(fs ? knobOnX : -knobOnX, 0f);
            if (fullscreenTrack != null) fullscreenTrack.color = fs ? TrackOn : TrackOff;
            if (fullscreenValue != null) fullscreenValue.text = fs ? "เปิด" : "ปิด";
        }

        private void SetMaster(float v)  { AudioListener.volume = v; Save("vol_master", v); }
        private void SetMusic(float v)   { Save("vol_music", v);   var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.musicSource != null) s.musicSource.volume = v; }
        private void SetSfx(float v)     { Save("vol_sfx", v);     var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.sfxSource != null) s.sfxSource.volume = v; }
        private void SetVoice(float v)   { Save("vol_voice", v);   var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.voiceSource != null) s.voiceSource.volume = v; }
        private void SetAmbient(float v) { Save("vol_ambient", v); var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.ambientSource != null) s.ambientSource.volume = v; }
        private void Save(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); }
    }
}
