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

        // จำนวนผู้เล่นสูงสุดต่อห้อง = จำนวนจุดเกิดในหอพัก (DormSpawnPoint หลัก + 3 ช่องเสริม) → ไม่มีใครเกิดซ้อนกัน
        public const int MaxPlayers = 4;
        // เข้าร่วม/กลับเข้าระหว่างเกม: โปรเจกต์ยังไม่มีระบบคืนความคืบหน้าให้ผู้เล่นที่กลับเข้ามา (เงิน/กระเป๋า/ลงทะเบียนเก็บที่เครื่องผู้เล่น)
        //   และลงทะเบียนได้เฉพาะวันแรกของภาค → ผู้ที่เข้ากลางเกมจะกลายเป็นนิสิตใหม่ที่ลงทะเบียนไม่ได้ · จึงปฏิเสธพร้อมเหตุผลชัดเจน
        public static bool AllowJoinInProgress = false;
        [Tooltip("จำนวนครั้งที่ลองต่อก่อนแจ้งว่าเชื่อมต่อไม่ได้ (x ConnectTimeoutMS ของ UnityTransport)")] public int clientConnectAttempts = 10;
        string notice;              // ข้อความล่าสุดเมื่อหลุด/ถูกปฏิเสธ/ต่อไม่ได้ (แสดงตอนยังไม่เชื่อมต่อ)
        bool sawConnected;          // เคยเชื่อมต่อสำเร็จในรอบนี้ไหม (แยก "หลุด" กับ "ต่อไม่ได้")
        NetworkManager hookedNm;
        static bool InLobby => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameSession.LobbyScene;

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
            if (InLobby)
            {
                LobbyConnection.EnsureExists(gameObject);
                HighlightColor(GameSession.PlayerColor & 15); HighlightModel(GameSession.PlayerModel);
                return;
            }
            HookNetworkEvents();
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

        // ---------- ข้อความสถานะเมื่อหลุด/ถูกปฏิเสธ/ต่อไม่ได้ ----------
        void HookNetworkEvents()
        {
            var nm = NetworkManager.Singleton;
            if (nm == hookedNm) return;
            UnhookNetworkEvents();
            hookedNm = nm;
            if (nm != null) { nm.OnClientStopped += OnLocalStopped; nm.OnConnectionEvent += OnConnEvent; }
        }

        string capturedReason;
        void OnConnEvent(NetworkManager nm, ConnectionEventData e)
        {
            // เก็บเหตุผลตอนหลุด "ก่อน" transport ปิด (หลังปิด DisconnectReason ถูกแทนด้วยข้อความ TransportShutdown)
            if (e.EventType == ConnectionEvent.ClientDisconnected && !nm.IsServer) capturedReason = nm.DisconnectReason;
        }

        void UnhookNetworkEvents()
        {
            if (hookedNm != null) { hookedNm.OnClientStopped -= OnLocalStopped; hookedNm.OnConnectionEvent -= OnConnEvent; }
            hookedNm = null;
        }

        void OnDestroy() => UnhookNetworkEvents();

        void OnLocalStopped(bool wasHost)
        {
            if (wasHost) { sawConnected = false; return; }
            string reason = !string.IsNullOrEmpty(capturedReason) ? capturedReason : (hookedNm != null ? hookedNm.DisconnectReason : "");
            notice = FriendlyReason(reason, sawConnected);
            capturedReason = null;
            sawConnected = false;
            Debug.LogWarning("[Net] " + notice);
        }

        // Host ตรวจก่อนรับผู้เล่นเข้า: ห้องเต็ม = ปฏิเสธพร้อมเหตุผล (ผู้เล่นฝั่งนั้นเห็นข้อความ)
        //   ต้องตั้ง ConnectionApproval ให้ตรงกันทั้ง Host และ Client (อยู่ใน NetworkConfig hash — ไม่ตรง = Host ตัดทิ้ง "config mismatch")
        void EnableApproval()
        {
            var nm = NetworkManager.Singleton;
            nm.NetworkConfig.ConnectionApproval = true;
            if (!nm.IsListening) nm.ConnectionApprovalCallback = ApproveConnection;
        }

        // แปลงเหตุผลการหลุดเป็นข้อความผู้เล่น (เหตุผลจาก Host เช่น "ห้องเต็ม" แสดงตรง ๆ · ข้อความภายในของ Netcode แปลงเป็นภาษาคน)
        public static string FriendlyReason(string reason, bool wasConnected)
        {
            reason = reason ?? "";
            if (reason == LobbyRules.Full || reason == LobbyRules.Started || reason == LobbyRules.Closed || reason == LobbyRules.Kicked)
                return LobbyRules.ErrorText(reason);
            if (reason.Contains("host shutting down")) return "โฮสต์ปิดห้องแล้ว";
            bool internalMsg = reason.Length == 0 || reason.StartsWith("[Disconnect Event]") || reason.Contains("disconnected by server") || reason.Contains("TransportShutdown");
            if (!internalMsg) return "เข้าห้องไม่ได้: " + reason;
            if (reason.Contains("MaxConnectionAttempts") || !wasConnected) return "เชื่อมต่อไม่ได้ — ตรวจ IP/โค้ดห้อง และให้โฮสต์สร้างห้องก่อน";
            return "หลุดจากห้องแล้ว (โฮสต์ปิดห้องหรือการเชื่อมต่อขาด)";
        }

        static void ApproveConnection(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            var nm = NetworkManager.Singleton;
            res.CreatePlayerObject = true;
            res.Pending = false;
            bool self = nm != null && req.ClientNetworkId == nm.LocalClientId;
            int count = nm != null ? nm.ConnectedClientsIds.Count : 0;
            bool started = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameSession.GameplayScene;
            res.Approved = ShouldApprove(self, count, MaxPlayers, started && !AllowJoinInProgress, out var reason);
            res.Reason = reason;
            if (!res.Approved) Debug.LogWarning($"[Net] ปฏิเสธผู้เล่น {req.ClientNetworkId}: {reason} ({count}/{MaxPlayers})");
        }

        // กติกาการรับเข้าห้อง (ฟังก์ชันล้วน ทดสอบได้) — Host เองผ่านเสมอ · คนอื่นเข้าได้จนครบ max
        public static bool ShouldApprove(bool isHostSelf, int connectedCount, int max, bool gameInProgress, out string reason)
        {
            reason = "";
            if (isHostSelf) return true;
            if (gameInProgress) { reason = "เกมในห้องนี้เริ่มไปแล้ว — ยังไม่รองรับการเข้าร่วม/กลับเข้าระหว่างเกม"; return false; }
            if (connectedCount >= max) { reason = $"ห้องเต็มแล้ว (สูงสุด {max} คน)"; return false; }
            return true;
        }

        public void Hide() { if (panel != null) panel.SetActive(false); }

        void Update()
        {
            if (InLobby) return;
            if (Input.GetKeyDown(KeyCode.F11) && panel != null) panel.SetActive(!panel.activeSelf);
            HookNetworkEvents();
            var cur = NetworkManager.Singleton;
            if (cur != null && cur.IsConnectedClient) sawConnected = true;
            if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }   // ไม่ต้องทุกเฟรม
        }

        void Host()
        {
            if (InLobby) { var connection = LobbyConnection.EnsureExists(gameObject); connection.Mode = LobbyConnectionMode.Lan; connection.OpenHost(); return; }
            if (NetworkManager.Singleton == null || Connected()) return;
            if (!HasPlayerPrefab()) return;
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");   // ฟังทุก interface → LAN ต่อได้
            notice = null;
            EnableApproval();
            WorldTimeSync.ExpectDisconnect = false;
            NetworkManager.Singleton.StartHost();
        }

        void Client()
        {
            if (InLobby) { LobbyConnection.EnsureExists(gameObject).JoinAddress(ipInput != null && !string.IsNullOrWhiteSpace(ipInput.text) ? ipInput.text : "127.0.0.1"); return; }
            if (NetworkManager.Singleton == null || Connected()) return;
            string ip = (ipInput != null && !string.IsNullOrWhiteSpace(ipInput.text)) ? ipInput.text.Trim() : "127.0.0.1";
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) { utp.SetConnectionData(ip, port); utp.MaxConnectAttempts = Mathf.Max(1, clientConnectAttempts); }   // ต่อไปหา host ตาม IP ที่กรอก
            notice = null; sawConnected = false; capturedReason = null;
            EnableApproval();
            WorldTimeSync.ExpectDisconnect = false;
            NetworkManager.Singleton.StartClient();
        }

        // ---------- ออนไลน์ (Relay / Join Code) — เล่นข้ามเน็ตได้ ต้องต่อ Unity Cloud ----------
        async void HostRelay()
        {
            if (InLobby) { var connection = LobbyConnection.EnsureExists(gameObject); connection.Mode = LobbyConnectionMode.Online; connection.OpenHost(); return; }
            if (NetworkManager.Singleton == null || Connected()) return;
            if (!HasPlayerPrefab()) return;
            SetStatus("กำลังสร้างห้องออนไลน์...");
            if (!await EnsureServices()) return;
            try
            {
                var alloc = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);   // ผู้เล่นอื่นได้อีก MaxPlayers-1 คน
                relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (utp != null) utp.SetRelayServerData(alloc.ToRelayServerData("dtls"));
                notice = null;
                EnableApproval();
                WorldTimeSync.ExpectDisconnect = false;
                NetworkManager.Singleton.StartHost();
                SetStatus($"ออนไลน์! โค้ดห้อง: {relayCode}");
            }
            catch (Exception e) { notice = "สร้างห้องออนไลน์ไม่สำเร็จ (ต้องมีอินเทอร์เน็ต + ลิงก์ Unity Cloud)"; SetStatus(notice); Debug.LogWarning("[Relay] host: " + e); }
        }

        async void JoinRelay()
        {
            if (InLobby) { LobbyConnection.EnsureExists(gameObject).JoinAddress(codeInput != null ? codeInput.text : ""); return; }
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
                notice = null; sawConnected = false; capturedReason = null;
                EnableApproval();
                WorldTimeSync.ExpectDisconnect = false;
                NetworkManager.Singleton.StartClient();
                SetStatus("กำลังเข้าห้องออนไลน์...");
            }
            catch (Exception e) { notice = "เข้าห้องไม่สำเร็จ: โค้ดห้องไม่ถูกต้องหรือห้องปิดแล้ว (หรือไม่มีอินเทอร์เน็ต)"; SetStatus(notice); Debug.LogWarning("[Relay] join: " + e); }
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
                notice = "ต่อ Unity Services ไม่ได้ (ต้องมีอินเทอร์เน็ต + ลิงก์ Unity Cloud)";
                SetStatus(notice);
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
            if (InLobby) { LobbyConnection.EnsureExists(gameObject).Leave(false); return; }
            if (NetworkManager.Singleton != null && Connected()) { WorldTimeSync.ExpectDisconnect = true; NetworkManager.Singleton.Shutdown(); }
            relayCode = null;
            notice = null;
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
                    ? (notice != null ? $"<color=#E0664F>{notice}</color>\n" : "") + $"เล่น LAN: Host (IP {LocalIP()}) / กรอก IP แล้ว Join\nหรือเล่นออนไลน์ด้วยโค้ด · F11 ปิด/เปิด"
                    : (nm.IsHost ? (relayCode != null ? $"ออนไลน์! โค้ดห้อง: {relayCode}" : $"Host! บอกเพื่อน Join IP: {LocalIP()}")
                                 : nm.IsServer ? "เซิร์ฟเวอร์"
                                 : !nm.IsConnectedClient ? "กำลังเชื่อมต่อ... (กด \"ออกจากห้อง\" เพื่อยกเลิก)"
                                 : "เชื่อมต่อแล้ว (ผู้เล่น)");
            }
        }
    }
}
