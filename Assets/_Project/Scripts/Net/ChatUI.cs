using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Collections;
using Unity.Netcode;

namespace NisitSimulator.Net
{
    // แชท — กด Y เปิด/ปิด · พิมพ์เอง (Enter ส่ง) หรือกดปุ่มข้อความสำเร็จรูป · แสดง log 8 บรรทัด
    //   หยุดเดินระหว่างพิมพ์ (WASD ไม่เลื่อนตัว) · UI สร้าง+ต่อโดย M29 · เล่นคนเดียว = โชว์ในเครื่อง
    public class ChatUI : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text log;
        public TMP_InputField input;      // ช่องพิมพ์เอง
        public Button[] presetButtons;
        public string[] presets = { "สวัสดี!", "รอด้วย~", "ไปไหนกัน?", "เก่งมาก!" };

        static ChatUI _me;
        readonly List<string> lines = new List<string>();
        NisitSimulator.Player.PlayerMovement move;
        bool moveWasOn;

        void Awake() { _me = this; }
        void OnDestroy() { if (_me == this) _me = null; }

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (input != null) input.onSubmit.AddListener(OnSubmit);
            if (presetButtons != null)
                for (int i = 0; i < presetButtons.Length; i++)
                {
                    int idx = i;
                    if (presetButtons[i] != null)
                        presetButtons[i].onClick.AddListener(() => { Send(idx < presets.Length ? presets[idx] : "..."); Close(); });
                }
            var p = GameObject.Find("Player");
            if (p != null) move = p.GetComponent<NisitSimulator.Player.PlayerMovement>();
            Render();
        }

        void Update()
        {
            if (panel == null) return;
            if (Input.GetKeyDown(KeyCode.Y))
            {
                if (panel.activeSelf) Close();
                else if (NisitSimulator.Core.GameManager.Instance == null || NisitSimulator.Core.GameManager.Instance.IsActive) Open();
            }
            else if (panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void Open()
        {
            panel.SetActive(true);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (move != null && move.enabled) { move.enabled = false; moveWasOn = true; }   // หยุดเดินระหว่างพิมพ์
            if (input != null) { input.text = ""; input.ActivateInputField(); }
        }

        void Close()
        {
            panel.SetActive(false);
            if (move != null && moveWasOn) { move.enabled = true; moveWasOn = false; }
        }

        void OnSubmit(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) Send(text.Trim());
            if (input != null) { input.text = ""; input.ActivateInputField(); }   // พิมพ์ต่อได้เลย
        }

        public void Send(string text)
        {
            var relay = LocalRelay();
            if (relay != null) relay.SendChatServerRpc(new FixedString512Bytes(Trunc(text)));
            else Append("(ยังไม่เชื่อมต่อ) " + text);   // เล่นคนเดียว
        }

        static string Trunc(string s) => (s != null && s.Length > 120) ? s.Substring(0, 120) : s;

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
