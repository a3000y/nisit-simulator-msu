using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Net
{
    // Scene-local connection controller. Every async completion must match the active operation.
    public sealed class LobbyConnection : MonoBehaviour
    {
        public static LobbyConnection Instance { get; private set; }
        public string RoomTitle = "";
        public int RoomMax = 4;
        public LobbyConnectionMode Mode = LobbyConnectionMode.Online;
        public LobbyShareMode Share = LobbyShareMode.HostOnly;
        public string Secret { get; private set; } = "";
        public bool Busy { get; private set; }
        public string Message { get; private set; } = "";
        public bool OfferLan { get; private set; }
        public List<string> LanAddresses { get; private set; }
        public bool Connected => nm != null && (nm.IsHost || nm.IsConnectedClient);
        NetworkManager nm;
        GameObject prefab;
        int operation;
        float deadline;
        bool sawConnected, leaving;
        string capturedReason = "";
        readonly Dictionary<ulong, string> pendingNames = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, float> reserved = new Dictionary<ulong, float>();
        static Task serviceInit;
        public static LobbyConnection EnsureExists(GameObject owner)
        {
            if (SceneManager.GetActiveScene().name != GameSession.LobbyScene) return null;
            return Instance != null ? Instance : owner.AddComponent<LobbyConnection>();
        }
        void Awake()
        {
            Instance = this; nm = NetworkManager.Singleton; LanAddresses = LobbyRules.LocalLanAddresses();
            RoomTitle = LobbyRules.LimitName("ห้องของ " + LobbyRules.LimitName(GameSession.PlayerName, "ผู้เล่น"), "ห้องของผู้เล่น");
            prefab = Resources.Load<GameObject>("Net/LobbyState");
            if (nm != null)
            {
                if (prefab != null && !nm.IsListening && !nm.NetworkConfig.Prefabs.Contains(prefab)) nm.AddNetworkPrefab(prefab);
                nm.OnServerStarted += ServerStarted; nm.OnClientConnectedCallback += ClientConnected;
                nm.OnClientStopped += Stopped; nm.OnConnectionEvent += ConnectionEvent;
            }
        }
        void OnDestroy()
        {
            operation++;
            if (nm != null)
            { nm.OnServerStarted -= ServerStarted; nm.OnClientConnectedCallback -= ClientConnected; nm.OnClientStopped -= Stopped; nm.OnConnectionEvent -= ConnectionEvent; }
            if (Instance == this) Instance = null;
        }
        void Update()
        {
            if (Busy && Time.realtimeSinceStartup >= deadline) Fail("TIMEOUT");
            if (nm != null && nm.IsServer)
            {
                var expired = new List<ulong>();
                foreach (var kv in reserved) if (nm.ConnectedClients.ContainsKey(kv.Key) || Time.realtimeSinceStartup - kv.Value > 10) expired.Add(kv.Key);
                foreach (var id in expired) { reserved.Remove(id); if (!nm.ConnectedClients.ContainsKey(id)) pendingNames.Remove(id); }
            }
        }
        bool Begin()
        {
            if (Busy || Connected || nm == null || nm.ShutdownInProgress) return false;
            if (prefab == null || nm.NetworkConfig.PlayerPrefab == null) { Message = "ยังไม่ได้สร้างข้อมูลห้อง ใช้เมนู Nisit / Build Lobby UI ก่อน"; return false; }
            operation++; Busy = true; deadline = Time.realtimeSinceStartup + 10;
            Message = "กำลังเชื่อมต่อ..."; capturedReason = ""; sawConnected = false; leaving = false; OfferLan = false;
            WorldTimeSync.ExpectDisconnect = false;
            return true;
        }
        void Prepare()
        {
            nm.NetworkConfig.ConnectionApproval = true; nm.ConnectionApprovalCallback = Approve;
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(LobbyRules.LimitName(GameSession.PlayerName, "ผู้เล่น"));
            var transport = nm.GetComponent<UnityTransport>();
            transport.MaxConnectAttempts = 10; transport.ConnectTimeoutMS = 1000;
        }
        public void OpenHost()
        {
            if (!Begin()) return;
            RoomMax = Mathf.Clamp(RoomMax, 2, 4); reserved.Clear(); pendingNames.Clear();
            if (Mode == LobbyConnectionMode.Online) { OpenRelay(operation); return; }
            Secret = LanAddresses.Count > 0 ? LanAddresses[0] : "127.0.0.1";
            nm.GetComponent<UnityTransport>().SetConnectionData("0.0.0.0", 7777, "0.0.0.0");
            Prepare(); if (!nm.StartHost()) Fail("SERVICE");
        }
        public void JoinAddress(string address)
        {
            var kind = LobbyRules.ParseAddress(address, out string value);
            if (kind == LobbyAddressKind.Invalid) { Message = LobbyRules.ErrorText("INVALID_ADDRESS"); return; }
            if (!Begin()) return;
            if (kind == LobbyAddressKind.Relay) { Mode = LobbyConnectionMode.Online; JoinRelay(value, operation); return; }
            Mode = LobbyConnectionMode.Lan;
            nm.GetComponent<UnityTransport>().SetConnectionData(value, 7777);
            Prepare(); if (!nm.StartClient()) Fail("SERVICE");
        }
        bool Current(int op) => this != null && Busy && op == operation && Time.realtimeSinceStartup < deadline;
        static async Task InitializeServices()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        static Task Services()
        {
            if (serviceInit == null || serviceInit.IsFaulted || serviceInit.IsCanceled || serviceInit.IsCompleted) serviceInit = InitializeServices();
            return serviceInit;
        }
        async void OpenRelay(int op)
        {
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable) { Fail("OFFLINE"); return; }
                await Services(); if (!Current(op)) return;
                var allocation = await RelayService.Instance.CreateAllocationAsync(RoomMax - 1); if (!Current(op)) return;
                string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId); if (!Current(op)) return;
                if (LobbyRules.ParseAddress(code, out code) != LobbyAddressKind.Relay) { Fail("SERVICE"); return; }
                Secret = code; nm.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));
                Prepare(); if (!nm.StartHost()) Fail("SERVICE");
            }
            catch (Exception e) { if (Current(op)) RelayFailed(e); }
        }
        async void JoinRelay(string code, int op)
        {
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable) { Fail("OFFLINE"); return; }
                await Services(); if (!Current(op)) return;
                var allocation = await RelayService.Instance.JoinAllocationAsync(code); if (!Current(op)) return;
                nm.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));
                Prepare(); if (!nm.StartClient()) Fail("SERVICE");
            }
            catch (Exception e) { if (Current(op)) RelayFailed(e); }
        }
        void RelayFailed(Exception e)
        {
            // The SDK supplies structured failure reasons; don't show exception text to players.
            string code = "SERVICE";
            if (e is RelayServiceException relay)
            {
                string reason = relay.Reason.ToString();
                if (reason.Contains("JoinCodeNotFound") || reason.Contains("AllocationNotFound") || reason.Contains("InvalidRequest") || reason.Contains("EntityNotFound")) code = "INVALID_CODE";
                if (reason.Contains("Capacity") || reason.Contains("Full")) code = LobbyRules.Full;
            }
            if (Application.internetReachability == NetworkReachability.NotReachable) code = "OFFLINE";
            Debug.LogWarning("[Net][Lobby] Relay failure: " + e.GetType().Name); Fail(code);
        }
        void ServerStarted()
        {
            var go = Instantiate(prefab); var state = go.GetComponent<LobbyState>();
            go.GetComponent<NetworkObject>().Spawn(false); state.Configure(this);
            Busy = false; Message = ""; sawConnected = true;
        }
        void ClientConnected(ulong id)
        {
            reserved.Remove(id);
            if (id != nm.LocalClientId) return;
            Busy = false; Message = ""; sawConnected = true;
        }
        public string NameFor(ulong id) => pendingNames.TryGetValue(id, out string name) ? name : "";
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool self = request.ClientNetworkId == nm.LocalClientId;
            int max = LobbyState.Instance != null ? LobbyState.Instance.MaxPlayers.Value : RoomMax;
            bool started = SceneManager.GetActiveScene().name == GameSession.GameplayScene ||
                (LobbyState.Instance != null && LobbyState.Instance.RoomState.Value != LobbyRoomPhase.Lobby);
            response.Pending = false; response.CreatePlayerObject = true;
            response.Approved = self || (!started && nm.ConnectedClientsIds.Count + reserved.Count < max);
            response.Reason = response.Approved ? "" : started ? LobbyRules.Started : LobbyRules.Full;
            if (response.Approved && !self)
            {
                reserved[request.ClientNetworkId] = Time.realtimeSinceStartup;
                var payload = request.Payload;
                pendingNames[request.ClientNetworkId] = payload != null && payload.Length <= 400 ? LobbyRules.LimitName(Encoding.UTF8.GetString(payload), "ผู้เล่น") : "ผู้เล่น";
            }
        }
        public static string PreferReason(string captured, string latest)
        {
            foreach (string reason in new[] { captured, latest })
                if (reason == LobbyRules.Full || reason == LobbyRules.Started || reason == LobbyRules.Closed || reason == LobbyRules.Kicked) return reason;
            return !string.IsNullOrEmpty(captured) ? captured : latest ?? "";
        }
        void ConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (data.EventType == Unity.Netcode.ConnectionEvent.ClientDisconnected && !manager.IsServer)
                capturedReason = PreferReason(capturedReason, manager.DisconnectReason);
        }
        public void RememberHostClosed() { capturedReason = LobbyRules.Closed; }
        void Stopped(bool wasHost)
        {
            if (leaving || wasHost || SceneManager.GetActiveScene().name != GameSession.LobbyScene) return;
            StartCoroutine(FinishStopped());
        }
        IEnumerator FinishStopped()
        {
            // Defer final wording until the DisconnectReason/connection event has arrived.
            yield return null; yield return new WaitForSecondsRealtime(0.1f);
            if (leaving) yield break;
            string reason = PreferReason(capturedReason, nm.DisconnectReason);
            bool wasInRoom = sawConnected; Busy = false; operation++;
            Message = NetworkUI.FriendlyReason(reason, wasInRoom); sawConnected = false;
            if (wasInRoom) { LobbyNotice.Show(Message); SceneManager.LoadScene(GameSession.MenuScene); }
        }
        void Fail(string code)
        {
            Busy = false; operation++; leaving = true;
            if (nm != null && nm.IsListening) nm.Shutdown();
            Message = LobbyRules.ErrorText(code); OfferLan = Mode == LobbyConnectionMode.Online && code != "CANCELLED";
            StartCoroutine(ReleaseFailedOperation());
        }
        IEnumerator ReleaseFailedOperation()
        { while (nm != null && nm.ShutdownInProgress) yield return null; yield return null; if (!Busy) leaving = false; }
        public void Cancel() { if (Busy) Fail("CANCELLED"); }
        public void Leave(bool toMenu = true)
        {
            if (leaving) return;
            leaving = true; operation++; Busy = false; WorldTimeSync.ExpectDisconnect = true;
            if (nm != null && nm.IsHost && LobbyState.Instance != null) LobbyState.Instance.NotifyHostClosed();
            StartCoroutine(LeaveRoutine(toMenu));
        }
        IEnumerator LeaveRoutine(bool toMenu)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            if (nm != null && nm.IsListening) nm.Shutdown();
            if (toMenu) SceneManager.LoadScene(GameSession.MenuScene);
            else { leaving = false; sawConnected = false; Message = "ออกจากห้องแล้ว"; }
        }
    }
}
