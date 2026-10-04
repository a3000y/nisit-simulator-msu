using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.GEBuilding;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Net
{
    // One persistent NetworkObject (LobbyState), two server-written 64-bit masks.
    // Catalog paths are relative to the building, independent of layout/world positions.
    public sealed class DoorSyncManager : NetworkBehaviour
    {
        public static DoorSyncManager Instance { get; private set; }
        readonly NetworkVariable<ulong> low = new NetworkVariable<ulong>();
        readonly NetworkVariable<ulong> high = new NetworkVariable<ulong>();
        readonly NetworkVariable<ulong> catalog = new NetworkVariable<ulong>();
        readonly NetworkVariable<int> count = new NetworkVariable<int>();
        readonly NetworkVariable<uint> revision = new NetworkVariable<uint>();
        readonly List<GEDoor> doors = new List<GEDoor>();
        readonly Dictionary<GEDoor, int> ids = new Dictionary<GEDoor, int>();
        DoorToggleAuthority authority;
        ulong localCatalog;
        uint sequence;
        bool mismatchReported;
        public int DoorCount => doors.Count;
        public ulong Catalog => localCatalog;
        public uint Revision => revision.Value;
        public bool Ready => IsSpawned && doors.Count > 0 && count.Value == doors.Count && catalog.Value == localCatalog;
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public string LastDecision { get; private set; } = "";
        public GEDoor Door(int id) => id >= 0 && id < doors.Count ? doors[id] : null;
        public int IdFor(GEDoor door) => door != null && ids.TryGetValue(door, out int id) ? id : -1;
        public bool State(int id) => DoorToggleAuthority.Open(low.Value, high.Value, id);

        public static string Key(GEDoor door)
        {
            if (door == null) return null;
            var parts = new List<string>();
            for (var p = door.transform; p != null; p = p.parent)
            {
                parts.Insert(0, p.name);
                if (p.name == "Dorm_Building" || p.name == "GE_Building" || p.name == "IT_Building")
                    return string.Join("/", parts);
            }
            return null;
        }
        public static bool MultiplayerDoor(GEDoor door)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && nm.IsListening && Key(door) != null;
        }
        // Claim the interaction even while loading: never toggle locally in a network match.
        public static bool RouteInteraction(GEDoor door)
        {
            if (!MultiplayerDoor(door)) return false;
            if (Instance != null) Instance.Request(door);
            return true;
        }
        public bool Request(GEDoor door)
        {
            int id = IdFor(door);
            if (!Ready || id < 0) return false;
            if (++sequence == 0) ++sequence;
            Debug.Log($"[DoorSync] request id={id} seq={sequence} utc={DateTime.UtcNow:O}");
            ToggleRpc(id, localCatalog, sequence);
            return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ToggleRpc(int id, ulong fingerprint, uint requestSequence, RpcParams rpc = default)
        {
            if (!IsServer || !IsSpawned) return;
            ulong sender = rpc.Receive.SenderClientId;
            string reason = "";
            if (!Ready || fingerprint != localCatalog) { Reject(sender, id, "catalog-mismatch"); return; }
            var door = Door(id);
            float distanceSquared = float.PositiveInfinity;
            float range = 1.6f;
            if (door != null && TryPosition(sender, out Vector3 position))
            {
                var trigger = door.GetComponent<BoxCollider>();
                bool finite = !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z) &&
                    !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && !float.IsInfinity(position.z);
                if (finite && trigger != null && trigger.enabled && trigger.isTrigger)
                    distanceSquared = (trigger.ClosestPoint(position) - position).sqrMagnitude;
                var player = GameObject.Find("Player");
                var interaction = player != null ? player.GetComponent<NisitSimulator.Player.PlayerInteraction>() : null;
                if (interaction != null) range = interaction.interactRange;
            }
            bool connected = NetworkManager.ConnectedClients.ContainsKey(sender);
            bool inGame = LobbyState.Instance != null && LobbyState.Instance.RoomState.Value == LobbyRoomPhase.InGame &&
                SceneManager.GetActiveScene().name == GameSession.GameplayScene;
            if (authority == null || !authority.TryToggle(true, connected, inGame, sender, requestSequence, id,
                distanceSquared, range, Time.realtimeSinceStartupAsDouble, NetworkManager.ServerTime.Tick, out reason))
            { Reject(sender, id, authority == null ? "not-ready" : reason); return; }
            low.Value = authority.Low; high.Value = authority.High; revision.Value++;
            Accepted++; LastDecision = "accepted";
            Apply(false);
            Debug.Log($"[DoorSync] accepted sender={sender} id={id} open={State(id)} revision={revision.Value} tick={NetworkManager.ServerTime.Tick} frame={Time.frameCount} utc={DateTime.UtcNow:O}");
        }
        bool TryPosition(ulong sender, out Vector3 position)
        {
            position = Vector3.zero;
            if (sender == Unity.Netcode.NetworkManager.ServerClientId)
            {
                var player = GameObject.Find("Player");
                if (player == null) return false;
                position = player.transform.position; return true;
            }
            foreach (var avatar in FindObjectsByType<NetworkAvatar>(FindObjectsSortMode.None))
                if (avatar.IsSpawned && avatar.OwnerClientId == sender)
                { position = avatar.ReplicatedPosition; return true; }
            return false;
        }
        void Reject(ulong sender, int id, string reason)
        {
            Rejected++; LastDecision = reason;
            Debug.Log($"[DoorSync] rejected sender={sender} id={id} reason={reason} tick={NetworkManager.ServerTime.Tick} frame={Time.frameCount} utc={DateTime.UtcNow:O}");
        }
        public override void OnNetworkSpawn()
        {
            Instance = this;
            low.OnValueChanged += MaskChanged; high.OnValueChanged += MaskChanged;
            catalog.OnValueChanged += CatalogChanged; count.OnValueChanged += CountChanged;
            SceneManager.sceneLoaded += SceneLoaded;
            if (IsServer) NetworkManager.OnClientDisconnectCallback += ClientLeft;
            if (SceneManager.GetActiveScene().name == GameSession.GameplayScene) Bind();
        }
        public override void OnNetworkDespawn()
        {
            low.OnValueChanged -= MaskChanged; high.OnValueChanged -= MaskChanged;
            catalog.OnValueChanged -= CatalogChanged; count.OnValueChanged -= CountChanged;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= ClientLeft;
            if (Instance == this) Instance = null;
            ClearBinding();
        }
        void ClientLeft(ulong sender) { if (authority != null) authority.Forget(sender); }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameSession.GameplayScene) Bind(); else ClearBinding();
        }
        void ClearBinding() { doors.Clear(); ids.Clear(); localCatalog = 0; authority = null; mismatchReported = false; }
        public void ResetMatch()
        {
            if (!IsServer || !IsSpawned) return;
            low.Value = 0; high.Value = 0; count.Value = 0; catalog.Value = 0; revision.Value = 0;
            Accepted = Rejected = 0; ClearBinding();
        }
        void Bind()
        {
            ClearBinding();
            var map = new SortedDictionary<string, GEDoor>(StringComparer.Ordinal);
            foreach (var door in FindObjectsByType<GEDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (door.gameObject.scene.name != GameSession.GameplayScene) continue;
                string key = Key(door); if (key == null) continue;
                if (map.ContainsKey(key)) { Debug.LogError("[DoorSync] duplicate path: " + key); return; }
                map.Add(key, door);
            }
            if (map.Count == 0 || map.Count > DoorToggleAuthority.Capacity)
            { Debug.LogError("[DoorSync] invalid catalog count: " + map.Count); return; }
            foreach (var pair in map) { ids.Add(pair.Value, doors.Count); doors.Add(pair.Value); pair.Value.ApplyState(false, true); }
            localCatalog = DoorToggleAuthority.Fingerprint(map.Keys);
            if (IsServer)
            {
                authority = new DoorToggleAuthority(doors.Count);
                low.Value = 0; high.Value = 0; count.Value = doors.Count; catalog.Value = localCatalog;
            }
            Apply(true);
            Debug.Log($"[DoorSync] bound count={doors.Count} catalog={localCatalog} ready={Ready} utc={DateTime.UtcNow:O}");
        }
        void MaskChanged(ulong previous, ulong current)
        {
            Apply(false);
            Debug.Log($"[DoorSync] applied low={low.Value} high={high.Value} ready={Ready} utc={DateTime.UtcNow:O}");
        }
        void CatalogChanged(ulong previous, ulong current) { Apply(true); }
        void CountChanged(int previous, int current) { Apply(true); }
        void Update()
        {
            if (!IsSpawned) return;
            if (doors.Count == 0 && SceneManager.GetActiveScene().name == GameSession.GameplayScene) Bind();
            if (Ready) Apply(false);
            else if (doors.Count > 0 && count.Value > 0 && catalog.Value != 0 && !mismatchReported)
            { mismatchReported = true; Debug.LogError("[DoorSync] client/server door catalog mismatch"); }
        }
        void Apply(bool snap)
        {
            if (!Ready) return;
            for (int i = 0; i < doors.Count; i++) if (doors[i] != null)
                doors[i].ApplyState(State(i), snap);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Uses the real RPC and all server checks, including range.
        public void DevRawRequest(int id, uint requestSequence) => ToggleRpc(id, localCatalog, requestSequence);
#endif
    }
}
