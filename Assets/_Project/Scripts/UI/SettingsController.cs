using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // ตั้งค่าเสียง (ฉาก 5) — Master คุมจริงผ่าน AudioListener.volume, เก็บใน PlayerPrefs
    // Music/SFX เก็บค่าไว้ให้ระบบเสียงในอนาคตอ่านไปใช้
    public class SettingsController : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Button closeButton;
        public GameObject panel;

        void Start()
        {
            float m  = PlayerPrefs.GetFloat("vol_master", 1f);
            float mu = PlayerPrefs.GetFloat("vol_music", 0.8f);
            float sf = PlayerPrefs.GetFloat("vol_sfx", 0.9f);

            AudioListener.volume = m;

            if (masterSlider) { masterSlider.value = m;  masterSlider.onValueChanged.AddListener(SetMaster); }
            if (musicSlider)  { musicSlider.value = mu;  musicSlider.onValueChanged.AddListener(v => Save("vol_music", v)); }
            if (sfxSlider)    { sfxSlider.value = sf;    sfxSlider.onValueChanged.AddListener(v => Save("vol_sfx", v)); }
            if (closeButton && panel) closeButton.onClick.AddListener(() => panel.SetActive(false));
        }

        private void SetMaster(float v) { AudioListener.volume = v; Save("vol_master", v); }
        private void Save(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); }
    }
}
