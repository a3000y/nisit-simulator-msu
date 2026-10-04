using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.SaveLoad;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.Interaction;
using NisitSimulator.GEBuilding;
using NisitSimulator.CameraRig;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // Scene-local coordinator. Every gameplay read/write is gated; never accesses SaveSystem.
    public sealed class PartyRuntime : MonoBehaviour
    {
        public static PartyRuntime Instance { get; private set; }
        public static bool IsMultiplayerSession => WorldTimeSync.Instance != null && WorldTimeSync.Instance.IsMultiplayerSession;
        public readonly List<NetworkAvatar> Members = new List<NetworkAvatar>(4);
        public NetworkAvatar LocalMember { get; private set; }
        public bool PingArmed { get; private set; }
        public double ServerNow => NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0;
        public int SummaryWrites { get; private set; }
        readonly PartyRoster roster = new PartyRoster();
        readonly HashSet<ulong> connected = new HashSet<ulong>();
        PlayerStats stats;
        Transform player;
        GEBuildingCutaway[] buildings;
        BuildingRoofHider[] roofs;
        float nextSample, nextDiscover;
        double nextLocalPing;
        PartyHUD hud;
        readonly List<PartyMapOverlay> maps = new List<PartyMapOverlay>();

        public static void EnsureExists()
        {
            if (Instance != null || !GameSession.IsMultiplayerGame) return;
            new GameObject("PartyRuntime").AddComponent<PartyRuntime>();
        }
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            buildings = FindObjectsByType<GEBuildingCutaway>(FindObjectsSortMode.None);
            roofs = FindObjectsByType<BuildingRoofHider>(FindObjectsSortMode.None);
        }
        void OnDestroy()
        {
            foreach (var map in maps) if (map != null) Destroy(map.gameObject);
            if (Instance == this) Instance = null;
        }
        void Update()
        {
            bool active = IsMultiplayerSession && SceneManager.GetActiveScene().name == GameSession.GameplayScene;
            if (!active)
            {
                PingArmed = false; Members.Clear(); LocalMember = null;
                if (hud != null) hud.gameObject.SetActive(false);
                return;
            }
            if (hud == null) hud = PartyHUD.Create(transform);
            hud.gameObject.SetActive(true);
            if (Time.unscaledTime >= nextSample)
            {
                nextSample = Time.unscaledTime + 0.5f;
                RefreshMembers(); PublishLocalSummary(); hud.Refresh(this);
            }
            if (Time.unscaledTime >= nextDiscover)
            {
                nextDiscover = Time.unscaledTime + 1;
                DiscoverMaps();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) PingArmed = false;
            if (Input.GetKeyDown(KeyCode.P) && CanUsePingInput())
            {
                PingArmed = !PingArmed;
                if (PingArmed)
                {
                    var map = FindFirstObjectByType<MinimapToggle>();
                    if (map != null) map.Open();
                    HUDController.Toast("คลิกบนแผนที่เพื่อปักหมุดทีม กด P อีกครั้งเพื่อยกเลิก");
                }
            }
            if (!CanUsePingInput()) PingArmed = false;
        }
        public static bool CanUsePingInput()
        {
            if (!IsMultiplayerSession) return false;
            var gm = NisitSimulator.Core.GameManager.Instance;
            if ((gm != null && !gm.IsActive) || Time.timeScale <= 0 || PlayerExhaustion.ExamActive() || SleepController.IsSleeping) return false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected == null || (selected.GetComponent<TMP_InputField>() == null && selected.GetComponent<UnityEngine.UI.InputField>() == null);
        }
        void RefreshMembers()
        {
            Members.Clear(); LocalMember = null; connected.Clear();
            var nm = NetworkManager.Singleton;
            foreach (var av in FindObjectsByType<NetworkAvatar>(FindObjectsSortMode.None))
            {
                if (!av.IsSpawned) continue;
                if (nm.IsServer && !nm.ConnectedClients.ContainsKey(av.OwnerClientId)) continue;
                Members.Add(av); connected.Add(av.OwnerClientId);
                if (av.IsOwner) LocalMember = av;
            }
            if (nm.IsServer)
            {
                roster.Retain(connected);
                // Initial ordering only: never derive an identity or a slot by ClientId modulo.
                Members.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
                foreach (var av in Members)
                {
                    var lobby = LobbyState.Instance;
                    if (lobby != null && lobby.IsSpawned && lobby.Find(av.OwnerClientId, out var member))
                        av.AssignPartyMember(new PartyRoster.Member(member.SlotIndex, member.PlayerId));
                    else av.AssignPartyMember(roster.Join(av.OwnerClientId));
                    av.ExpirePartyPings(ServerNow);
                }
            }
            Members.Sort((a, b) => a.TeamSlot.CompareTo(b.TeamSlot));
        }
        void PublishLocalSummary()
        {
            if (LocalMember == null) return;
            if (player == null)
            {
                var go = GameObject.Find("Player");
                if (go != null) { player = go.transform; stats = go.GetComponent<PlayerStats>(); }
            }
            if (player == null || stats == null) return;
            PartyStatus flags = PartyStatus.Normal;
            if (PlayerExhaustion.Local != null && PlayerExhaustion.Local.IsExhausted) flags |= PartyStatus.Exhausted;
            if (PlayerExhaustion.ExamActive()) flags |= PartyStatus.Exam;
            var sleep = SleepController.Instance;
            if (sleep != null && (sleep.State == SleepController.SleepState.WaitingOthers || sleep.State == SleepController.SleepState.Sleeping))
                flags |= PartyStatus.Sleeping;
            var interior = InteriorManager.Instance;
            bool teleport = interior != null && interior.IsInside;
            bool inside = teleport;
            foreach (var b in buildings) if (b != null && b.isActiveAndEnabled && b.PlayerFloor() >= 0) inside = true;
            foreach (var r in roofs) if (r != null && r.isActiveAndEnabled && r.IsRoofHidden) inside = true;
            if (inside) flags |= PartyStatus.Indoors;
            var summary = new PartySummary { Ready = true, EnergyPercent = PartyPresentation.Energy(stats.Energy, stats.maxEnergy),
                Status = flags, TeleportedInterior = teleport,
                MapPosition = PartyPresentation.Quantize(teleport ? interior.ReturnPosition : player.position) };
            if (!LocalMember.TeamSummary.Equals(summary)) { LocalMember.PublishPartySummary(summary); SummaryWrites++; }
        }
        void DiscoverMaps()
        {
            var toggle = FindFirstObjectByType<MinimapToggle>();
            if (toggle == null || toggle.minimapCam == null || toggle.minimapCam.targetTexture == null) return;
            foreach (var raw in FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (raw.texture != toggle.minimapCam.targetTexture || raw.GetComponentInChildren<PartyMapOverlay>(true) != null) continue;
                maps.Add(PartyMapOverlay.Create(raw, toggle.minimapCam));
            }
        }
        public bool TryPing(Vector3 position)
        {
            if (!IsMultiplayerSession || LocalMember == null || !CanUsePingInput()) return false;
            if (ServerNow < nextLocalPing) { HUDController.Toast("รอสักครู่ก่อนปักหมุดอีกครั้ง"); return false; }
            if (!LocalMember.RequestPartyPing(position)) { HUDController.Toast("ปักหมุดได้เฉพาะพื้นที่มหาวิทยาลัย"); return false; }
            nextLocalPing = ServerNow + PartyPingGate.Cooldown;
            PingArmed = false;
            return true;
        }
    }
}
