using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Net
{
    // ปุ่ม Host / Join / ออก สำหรับทดสอบ MP (localhost 127.0.0.1)
    //   กด F3 เปิด/ปิดแผง · UI สร้างโดย M29 Setup Multiplayer
    public class NetworkUI : MonoBehaviour
    {
        public GameObject panel;
        public Button hostButton, clientButton, disconnectButton;
        public TMP_Text statusText;
        public TMP_InputField ipInput;        // ช่องกรอก IP ของ host (เล่น LAN)
        public ushort port = 7777;

        [Header("ปรับแต่งตัวละคร (ชื่อ+สี)")]
        public TMP_InputField nameInput;
        public Button[] colorButtons;

        void Start()
        {
            if (hostButton) hostButton.onClick.AddListener(Host);
            if (clientButton) clientButton.onClick.AddListener(Client);
            if (disconnectButton) disconnectButton.onClick.AddListener(Disconnect);

            if (nameInput != null)
            {
                nameInput.text = GameSession.PlayerName;
                nameInput.onValueChanged.AddListener(OnNameEdited);
            }
            if (colorButtons != null)
                for (int i = 0; i < colorButtons.Length; i++)
                {
                    int idx = i;
                    if (colorButtons[i] != null) colorButtons[i].onClick.AddListener(() => PickColor(idx));
                }
            HighlightColor(GameSession.PlayerColor);

            Refresh();
        }

        // ---------- ปรับแต่งตัวละคร ----------
        void OnNameEdited(string s) { GameSession.PlayerName = s; PushIdentity(); }

        void PickColor(int i) { GameSession.PlayerColor = i; HighlightColor(i); PushIdentity(); }

        void HighlightColor(int sel)
        {
            if (colorButtons == null) return;
            for (int i = 0; i < colorButtons.Length; i++)
                if (colorButtons[i] != null)
                    colorButtons[i].transform.localScale = Vector3.one * (i == sel ? 1.35f : 1f);
        }

        // อัปเดตชื่อ/สีให้ avatar ของเราสด ๆ (ถ้าเชื่อมต่ออยู่)
        void PushIdentity()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.LocalClient == null) return;
            var po = nm.LocalClient.PlayerObject;
            var av = po != null ? po.GetComponent<NetworkAvatar>() : null;
            if (av != null) av.SetIdentity(GameSession.PlayerName, GameSession.PlayerColor);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3) && panel != null) panel.SetActive(!panel.activeSelf);
            Refresh();
        }

        void Host()
        {
            if (NetworkManager.Singleton == null || Connected()) return;
            if (!HasPlayerPrefab()) return;
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");   // ฟังทุก interface → LAN ต่อได้
            NetworkManager.Singleton.StartHost();
        }

        void Client()
        {
            if (NetworkManager.Singleton == null || Connected()) return;
            string ip = (ipInput != null && !string.IsNullOrWhiteSpace(ipInput.text)) ? ipInput.text.Trim() : "127.0.0.1";
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) utp.SetConnectionData(ip, port);   // ต่อไปหา host ตาม IP ที่กรอก
            NetworkManager.Singleton.StartClient();
        }

        // IP ในวง LAN ของเครื่องนี้ (บอกเพื่อนให้ Join)
        static string LocalIP()
        {
            try
            {
                foreach (var a in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.ToString().StartsWith("127."))
                        return a.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        // เช็ก PlayerPrefab (จุดพังบ่อยสุด — ถ้าว่าง จะไม่มีตัวละครโผล่)
        bool HasPlayerPrefab()
        {
            if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null) return true;
            if (statusText != null) statusText.text = "⚠ PlayerPrefab ว่าง! ลาก NetworkAvatar ใส่ NetworkManager";
            Debug.LogWarning("[Net] NetworkManager.PlayerPrefab ว่าง — ลาก Assets/_Project/Prefabs/NetworkAvatar.prefab ใส่ช่อง PlayerPrefab");
            return false;
        }

        void Disconnect()
        {
            if (NetworkManager.Singleton != null && Connected()) NetworkManager.Singleton.Shutdown();
        }

        static bool Connected()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && (nm.IsHost || nm.IsClient || nm.IsServer);
        }

        void Refresh()
        {
            bool on = Connected();
            if (hostButton) hostButton.gameObject.SetActive(!on);
            if (clientButton) clientButton.gameObject.SetActive(!on);
            if (disconnectButton) disconnectButton.gameObject.SetActive(on);
            if (statusText != null)
            {
                var nm = NetworkManager.Singleton;
                statusText.text = !on
                    ? "ยังไม่เชื่อมต่อ"
                    : (nm.IsHost ? $"Host! บอกเพื่อน Join IP: {LocalIP()}"
                                 : nm.IsServer ? "เซิร์ฟเวอร์"
                                 : "เชื่อมต่อแล้ว (ผู้เล่น)");
            }
        }
    }
}
