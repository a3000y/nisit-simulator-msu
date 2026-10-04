using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;

namespace NisitSimulator.Net
{
    public sealed class LobbyState : NetworkBehaviour
    {
        public static LobbyState Instance { get; private set; }
        public readonly NetworkVariable<FixedString512Bytes> RoomName = new NetworkVariable<FixedString512Bytes>();
        public readonly NetworkVariable<byte> MaxPlayers = new NetworkVariable<byte>(4);
        public readonly NetworkVariable<FixedString64Bytes> JoinCode = new NetworkVariable<FixedString64Bytes>();
        public readonly NetworkVariable<LobbyShareMode> ShareCodeMode = new NetworkVariable<LobbyShareMode>();
        public readonly NetworkVariable<LobbyRoomPhase> RoomState = new NetworkVariable<LobbyRoomPhase>();
        public readonly NetworkVariable<LobbyConnectionMode> ConnectionMode = new NetworkVariable<LobbyConnectionMode>();
        public NetworkList<LobbyPlayer> Players;
        public string PrivateJoinCode { get; private set; } = "";
        readonly LobbyRevisionGate gate = new LobbyRevisionGate();
        readonly Dictionary<ulong, float> nextUpdate = new Dictionary<ulong, float>();
        readonly HashSet<ulong> kicking = new HashSet<ulong>();
        float nextPing;
        uint localRevision;
        void Awake() { Players = new NetworkList<LobbyPlayer>(); }
        public override void OnNetworkSpawn()
        {
            Instance = this; DontDestroyOnLoad(gameObject);
            if (!IsServer) return;
            NetworkManager.OnClientConnectedCallback += AddPlayer;
            NetworkManager.OnClientDisconnectCallback += RemovePlayer;
            foreach (var id in NetworkManager.ConnectedClientsIds) AddPlayer(id);
        }
        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
            { NetworkManager.OnClientConnectedCallback -= AddPlayer; NetworkManager.OnClientDisconnectCallback -= RemovePlayer; }
            if (Instance == this) Instance = null;
        }
        public void Configure(LobbyConnection connection)
        {
            if (!IsServer || !IsSpawned) return;
            RoomName.Value = new FixedString512Bytes(LobbyRules.LimitName(connection.RoomTitle, "ห้องของผู้เล่น"));
            MaxPlayers.Value = (byte)Mathf.Clamp(connection.RoomMax, 2, 4);
            ConnectionMode.Value = connection.Mode; ShareCodeMode.Value = connection.Share;
            PrivateJoinCode = connection.Secret;
            PublishCode(); RoomState.Value = LobbyRoomPhase.Lobby;
        }
        public List<LobbyPlayer> Snapshot()
        { var list = new List<LobbyPlayer>(); for (int i = 0; i < Players.Count; i++) list.Add(Players[i]); return list; }
        public bool Find(ulong connection, out LobbyPlayer player)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].ConnectionId == connection) { player = Players[i]; return true; }
            player = default; return false;
        }
        int Index(ulong connection)
        { for (int i = 0; i < Players.Count; i++) if (Players[i].ConnectionId == connection) return i; return -1; }
        void AddPlayer(ulong id)
        {
            if (!IsServer || Index(id) >= 0 || RoomState.Value != LobbyRoomPhase.Lobby) return;
            byte slot = LobbyRules.EmptySlot(Snapshot(), MaxPlayers.Value); if (slot == byte.MaxValue) return;
            bool host = id == Unity.Netcode.NetworkManager.ServerClientId;
            string name = host ? GameSession.PlayerName : LobbyConnection.Instance != null ? LobbyConnection.Instance.NameFor(id) : "";
            name = LobbyRules.LimitName(name, "ผู้เล่น " + (slot + 1));
            Players.Add(new LobbyPlayer { PlayerId = new FixedString64Bytes(Guid.NewGuid().ToString("N")), ConnectionId = id,
                SlotIndex = slot, Name = new FixedString512Bytes(name), IsHost = host, Ready = host, Model = 0, Color = 0 });
        }
        void RemovePlayer(ulong id)
        {
            if (!IsServer) return;
            int i = Index(id); if (i < 0) return;
            gate.Forget(Players[i].PlayerId); nextUpdate.Remove(id); kicking.Remove(id); Players.RemoveAt(i);
        }
        void Update()
        {
            if (!IsSpawned || !IsServer || RoomState.Value != LobbyRoomPhase.Lobby || Time.unscaledTime < nextPing) return;
            nextPing = Time.unscaledTime + 1;
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                ushort ping = p.IsHost ? (ushort)0 : (ushort)Math.Min(999UL, NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(p.ConnectionId));
                if (p.Ping != ping) { p.Ping = ping; Players[i] = p; }
            }
        }
        public void SubmitLocal(bool ready)
        {
            if (!IsSpawned || !IsClient || RoomState.Value != LobbyRoomPhase.Lobby || !Find(NetworkManager.LocalClientId, out var mine)) return;
            string name = LobbyRules.LimitName(GameSession.PlayerName, "ผู้เล่น " + (mine.SlotIndex + 1));
            string accessories = CharacterAccessories.Pack(GameSession.PlayerAccessories);
            UpdateMemberRpc(mine.PlayerId, ++localRevision, ready, new FixedString512Bytes(name), GameSession.PlayerModel,
                GameSession.PlayerColor, new FixedString64Bytes(accessories.Length > 32 ? accessories.Substring(0, 32) : accessories));
        }
        [Rpc(SendTo.Server)]
        void UpdateMemberRpc(FixedString64Bytes id, uint sequence, bool ready, FixedString512Bytes name, int model, int color, FixedString64Bytes accessories, RpcParams rpc = default)
        {
            if (!IsServer || RoomState.Value != LobbyRoomPhase.Lobby) return;
            ulong sender = rpc.Receive.SenderClientId; int index = Index(sender); if (index < 0) return;
            var player = Players[index];
            var cat = CharacterCatalog.Load();
            if (model < 0 || cat == null || model >= cat.Count || color < 0 || color > 4095) return;
            string acc = accessories.ToString();
            foreach (char c in acc) if ((c < '0' || c > '9') && c != ',') return;
            if (!gate.Accept(player, sender, id, sequence, NetworkManager.ConnectedClients.ContainsKey(sender))) return;
            if (nextUpdate.TryGetValue(sender, out float next) && Time.unscaledTime < next) return;
            nextUpdate[sender] = Time.unscaledTime + 0.1f;
            bool lookChanged = !player.Name.Equals(name) || player.Model != model || player.Color != color || !player.Accessories.Equals(accessories);
            player.Name = new FixedString512Bytes(LobbyRules.LimitName(name.ToString(), "ผู้เล่น " + (player.SlotIndex + 1)));
            player.Model = model; player.Color = color; player.Accessories = accessories;
            player.Ready = player.IsHost || (!lookChanged && ready);
            Players[index] = player;
        }
        void PublishCode() => JoinCode.Value = new FixedString64Bytes(LobbyRules.PublicCode(PrivateJoinCode, ShareCodeMode.Value));
        public void SetShareMode(bool everyone)
        { if (!IsServer || RoomState.Value != LobbyRoomPhase.Lobby) return; ShareCodeMode.Value = everyone ? LobbyShareMode.Everyone : LobbyShareMode.HostOnly; PublishCode(); }
        public void SelectLanAddress(string ip)
        { if (!IsServer || ConnectionMode.Value != LobbyConnectionMode.Lan || LobbyConnection.Instance == null || !LobbyConnection.Instance.LanAddresses.Contains(ip)) return; PrivateJoinCode = ip; PublishCode(); }
        public bool KickPlayer(string playerId)
        {
            if (!IsServer || RoomState.Value != LobbyRoomPhase.Lobby) return false;
            foreach (var p in Snapshot()) if (p.PlayerId.ToString() == playerId && !p.IsHost)
            { if (!kicking.Add(p.ConnectionId)) return false; NetworkManager.DisconnectClient(p.ConnectionId, LobbyRules.Kicked); return true; }
            return false;
        }
        public void NotifyHostClosed() { if (IsServer && IsSpawned) HostClosedRpc(); }
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void HostClosedRpc() { if (LobbyConnection.Instance != null) LobbyConnection.Instance.RememberHostClosed(); }
        public bool StartGame()
        {
            if (!IsServer || !IsHost || kicking.Count > 0 || SceneManager.GetActiveScene().name != GameSession.LobbyScene || !LobbyRules.CanStart(Snapshot(), RoomState.Value, out _)) return false;
            // Freeze approvals and repeat clicks before entering NGO scene loading.
            RoomState.Value = LobbyRoomPhase.InGame;
            var doors = GetComponent<DoorSyncManager>();
            if (doors != null) doors.ResetMatch();
            var status = NetworkManager.SceneManager.LoadScene(GameSession.GameplayScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started) { RoomState.Value = LobbyRoomPhase.Lobby; return false; }
            return true;
        }
    }
}
