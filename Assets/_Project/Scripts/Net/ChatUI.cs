using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Collections;
using Unity.Netcode;

namespace NisitSimulator.Net
{
    // แชทด่วน (quick-chat) — กด Y เปิด/ปิดแผงข้อความสำเร็จรูป · แสดง log 8 บรรทัดล่าสุด
    //   UI สร้าง+ต่อโดย M29 · เล่นคนเดียว = โชว์ข้อความในเครื่องเฉย ๆ (ไม่พัง)
    public class ChatUI : MonoBehaviour
    {
        public GameObject panel;          // แผงปุ่มข้อความ
        public TMP_Text log;
        public Button[] presetButtons;
        public string[] presets = { "สวัสดี!", "รอด้วย~", "ไปไหนกัน?", "เก่งมาก!" };

        static ChatUI _me;
        readonly List<string> lines = new List<string>();

        void Awake() { _me = this; }
        void OnDestroy() { if (_me == this) _me = null; }

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (presetButtons != null)
                for (int i = 0; i < presetButtons.Length; i++)
                {
                    int idx = i;
                    if (presetButtons[i] != null)
                        presetButtons[i].onClick.AddListener(() => Send(idx < presets.Length ? presets[idx] : "..."));
                }
            Render();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Y) && panel != null) panel.SetActive(!panel.activeSelf);
        }

        public void Send(string text)
        {
            if (panel != null) panel.SetActive(false);
            var relay = LocalRelay();
            if (relay != null) relay.SendChatServerRpc(new FixedString512Bytes(text));
            else Append("(ยังไม่เชื่อมต่อ) " + text);   // เล่นคนเดียว
        }

        static ChatRelay LocalRelay()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.LocalClient == null) return null;
            var po = nm.LocalClient.PlayerObject;
            return po != null ? po.GetComponent<ChatRelay>() : null;
        }

        public static void Append(string line) { if (_me != null) _me.AddLine(line); }

        void AddLine(string s)
        {
            lines.Add(s);
            if (lines.Count > 8) lines.RemoveAt(0);
            Render();
        }

        void Render() { if (log != null) log.text = string.Join("\n", lines); }
    }
}
