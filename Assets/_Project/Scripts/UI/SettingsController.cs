using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // ตั้งค่าเสียง — Master คุมผ่าน AudioListener.volume · Music/SFX/Voice/Ambient คุมแหล่งเสียงใน SFXManager
    //   ปรับสไลเดอร์แล้วมีผลทันที (ถ้ามี SFXManager ในฉาก) + เก็บใน PlayerPrefs ให้ฉากเกมอ่านตอนเริ่ม
    public class SettingsController : MonoBehaviour
    {
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider voiceSlider;     // เสียงพูด NPC / ตอบรับปุ่ม
        public Slider ambientSlider;   // เสียงบรรยากาศ (ลม/นก/ในตึก)
        public Button closeButton;
        public GameObject panel;

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
            if (closeButton && panel) closeButton.onClick.AddListener(() => panel.SetActive(false));
        }

        private void SetMaster(float v)  { AudioListener.volume = v; Save("vol_master", v); }
        private void SetMusic(float v)   { Save("vol_music", v);   var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.musicSource != null) s.musicSource.volume = v; }
        private void SetSfx(float v)     { Save("vol_sfx", v);     var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.sfxSource != null) s.sfxSource.volume = v; }
        private void SetVoice(float v)   { Save("vol_voice", v);   var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.voiceSource != null) s.voiceSource.volume = v; }
        private void SetAmbient(float v) { Save("vol_ambient", v); var s = NisitSimulator.Core.SFXManager.I; if (s != null && s.ambientSource != null) s.ambientSource.volume = v; }
        private void Save(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); }
    }
}
