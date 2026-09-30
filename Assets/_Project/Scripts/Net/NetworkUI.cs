using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;   // RelayServerData
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
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

        [Header("ปรับแต่งตัวละคร (ชื่อ+สี+แบบ)")]
        public TMP_InputField nameInput;
        public Button[] colorButtons;
        public Button[] modelButtons;

        [Header("ออนไลน์ (Relay / Join Code)")]
        public TMP_InputField codeInput;
        public Button hostRelayButton, joinRelayButton;
        private string relayCode;   // โค้ดห้องตอนเป็น Host ออนไลน์

        void Awake()
        {
            if (panel != null && !GameSession.OpenNetworkOnStart)
                panel.SetActive(false);
        }

        void Start()
        {
            if (hostButton) hostButton.onClick.AddListener(Host);
            if (clientButton) clientButton.onClick.AddListener(Client);
            if (disconnectButton) disconnectButton.onClick.AddListener(Disconnect);
            if (hostRelayButton) hostRelayButton.onClick.AddListener(() => HostRelay());
            if (joinRelayButton) joinRelayButton.onClick.AddListener(() => JoinRelay());

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
            if (modelButtons != null)
                for (int i = 0; i < modelButtons.Length; i++)
                {
                    int idx = i;
                    if (modelButtons[i] != null) modelButtons[i].onClick.AddListener(() => PickModel(idx));
                }
            HighlightColor(GameSession.PlayerColor & 15);
            HighlightModel(GameSession.PlayerModel);

            // ซ่อนแผงเป็นค่าเริ่มต้น (เปิดเฉพาะเมื่อมาจากปุ่มเล่นหลายคน)
            if (panel != null)
            {
                panel.SetActive(GameSession.OpenNetworkOnStart);
                GameSession.OpenNetworkOnStart = false;
            }

            Refresh();
        }

        // ---------- ปรับแต่งตัวละคร ----------
        void OnNameEdited(string s) { GameSession.PlayerName = s; PushIdentity(); }

        void PickColor(int i) { GameSession.PlayerColor = (GameSession.PlayerColor & ~15) | (i & 15); HighlightColor(i); PushIdentity(); }

        void PickModel(int i)
        {
            GameSession.PlayerModel = i;
            HighlightModel(i);
            var p = GameObject.Find("Player");
            var sw = p != null ? p.GetComponent<NisitSimulator.Player.PlayerModelSwapper>() : null;
            if (sw != null) sw.SwapTo(i);   // สลับโมเดล Player จริงของเราทันที
            PushIdentity();
        }

        void HighlightColor(int sel)
        {
            if (colorButtons == null) return;
            for (int i = 0; i < colorButtons.Length; i++)
                if (colorButtons[i] != null)
                    colorButtons[i].transform.localScale = Vector3.one * (i == sel ? 1.35f : 1f);
        }

        void HighlightModel(int sel)
        {
            if (modelButtons == null) return;
            for (int i = 0; i < modelButtons.Length; i++)
                if (modelButtons[i] != null)
                    modelButtons[i].transform.localScale = Vector3.one * (i == sel ? 1.15f : 1f);
        }

        // อัปเดตชื่อ/สีให้ avatar ของเราสด ๆ (ถ้าเชื่อมต่ออยู่)
        void PushIdentity()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.LocalClient == null) return;
            var po = nm.LocalClient.PlayerObject;
            var av = po != null ? po.GetComponent<NetworkAvatar>() : null;
            if (av != null) av.SetIdentity(GameSession.PlayerName, GameSession.PlayerColor, GameSession.PlayerModel);
        }

        float nextRefresh;

        public void Hide() { if (panel != null) panel.SetActive(false); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11) && panel != null) panel.SetActive(!panel.activeSelf);
            if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }   // ไม่ต้องทุกเฟรม
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

        // ---------- ออนไลน์ (Relay / Join Code) — เล่นข้ามเน็ตได้ ต้องต่อ Unity Cloud ----------
        async void HostRelay()
        {
            if (NetworkManager.Singleton == null || Connected()) return;
            if (!HasPlayerPrefab()) return;
            SetStatus("กำลังสร้างห้องออนไลน์...");
            if (!await EnsureServices()) return;
            try
            {
                var alloc = await RelayService.Instance.CreateAllocationAsync(8);   // สูงสุด 8 คนอื่น
                relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (utp != null) utp.SetRelayServerData(alloc.ToRelayServerData("dtls"));
                NetworkManager.Singleton.StartHost();
                SetStatus($"ออนไลน์! โค้ดห้อง: {relayCode}");
            }
            catch (Exception e) { SetStatus("สร้างห้องไม่สำเร็จ (ต้องมีเน็ต+ลิงก์ Cloud)"); Debug.LogWarning("[Relay] host: " + e); }
        }

        async void JoinRelay()
        {
            if (NetworkManager.Singleton == null || Connected()) return;
            string c = codeInput != null ? codeInput.text.Trim().ToUpperInvariant() : "";
            if (string.IsNullOrEmpty(c)) { SetStatus("กรอกโค้ดห้องก่อน"); return; }
            SetStatus("กำลังเข้าห้องออนไลน์...");
            if (!await EnsureServices()) return;
            try
            {
                var join = await RelayService.Instance.JoinAllocationAsync(c);
                var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (utp != null) utp.SetRelayServerData(join.ToRelayServerData("dtls"));
                NetworkManager.Singleton.StartClient();
                SetStatus("เข้าห้องออนไลน์แล้ว!");
            }
            catch (Exception e) { SetStatus("เข้าห้องไม่สำเร็จ (โค้ดผิด/เน็ต)"); Debug.LogWarning("[Relay] join: " + e); }
        }

        // เตรียม Unity Services + ล็อกอินแบบ anonymous (ครั้งเดียว)
        async Task<bool> EnsureServices()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                SetStatus("ต่อ Unity Services ไม่ได้ (ต้องมีเน็ต+ลิงก์ Cloud)");
                Debug.LogWarning("[Relay] services: " + e);
                return false;
            }
        }

        void SetStatus(string s) { if (statusText != null) statusText.text = s; }

        // IP ในวง LAN ของเครื่องนี้ (คำนวณครั้งเดียว cache ไว้ — DNS lookup ช้า อย่าเรียกทุกเฟรม)
        static string _cachedIP;
        static string LocalIP()
        {
            if (_cachedIP != null) return _cachedIP;
            _cachedIP = "127.0.0.1";
            try
            {
                foreach (var a in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.ToString().StartsWith("127."))
                    { _cachedIP = a.ToString(); break; }
            }
            catch { }
            return _cachedIP;
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
            relayCode = null;
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
            if (hostRelayButton) hostRelayButton.gameObject.SetActive(!on);
            if (joinRelayButton) joinRelayButton.gameObject.SetActive(!on);
            if (disconnectButton) disconnectButton.gameObject.SetActive(on);
            if (statusText != null)
            {
                var nm = NetworkManager.Singleton;
                statusText.text = !on
                    ? $"เล่น LAN: Host (IP {LocalIP()}) / กรอก IP แล้ว Join\nหรือเล่นออนไลน์ด้วยโค้ด · F11 ปิด/เปิด"
                    : (nm.IsHost ? (relayCode != null ? $"ออนไลน์! โค้ดห้อง: {relayCode}" : $"Host! บอกเพื่อน Join IP: {LocalIP()}")
                                 : nm.IsServer ? "เซิร์ฟเวอร์"
                                 : "เชื่อมต่อแล้ว (ผู้เล่น)");
            }
        }
    }
}
