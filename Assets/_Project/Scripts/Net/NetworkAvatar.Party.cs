using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace NisitSimulator.Net
{
    // Extends the existing NetworkBehaviour; no new prefab component or behaviour index.
    public partial class NetworkAvatar
    {
        readonly NetworkVariable<PartySummary> partySummary = new NetworkVariable<PartySummary>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<FixedString64Bytes> partyPlayerId = new NetworkVariable<FixedString64Bytes>();
        readonly NetworkVariable<byte> partySlot = new NetworkVariable<byte>(byte.MaxValue);
        NetworkList<PartyPing> partyPings;
        PartyPingGate pingGate = new PartyPingGate();
        uint nextPingSequence;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test harness uses the real RPC path, including NGO ownership checks.
        public void DevPartyPing(uint sequence, Vector3 position) => SubmitPartyPingRpc(sequence, position);
#endif

        void Awake() { partyPings = new NetworkList<PartyPing>(); }
        public PartySummary TeamSummary => partySummary.Value;
        public string TeamPlayerId => partyPlayerId.Value.ToString();
        public byte TeamSlot => partySlot.Value;
        public int TeamPingCount => partyPings == null ? 0 : partyPings.Count;
        public PartyPing TeamPingAt(int index) => partyPings[index];

        internal void AssignPartyMember(PartyRoster.Member member)
        {
            if (!IsServer || !IsSpawned || !PartyRuntime.IsMultiplayerSession || member == null) return;
            partyPlayerId.Value = member.PlayerId; partySlot.Value = member.Slot;
        }
        internal void PublishPartySummary(PartySummary value)
        {
            if (IsSpawned && IsOwner && PartyRuntime.IsMultiplayerSession && !partySummary.Value.Equals(value))
                partySummary.Value = value;
        }
        internal void ExpirePartyPings(double now)
        {
            if (!IsServer || !IsSpawned || !PartyRuntime.IsMultiplayerSession) return;
            for (int i = partyPings.Count - 1; i >= 0; i--)
                if (!PartyPingGate.IsVisible(partyPings[i], now)) partyPings.RemoveAt(i);
        }
        public bool RequestPartyPing(Vector3 position)
        {
            if (!IsSpawned || !IsOwner || !PartyRuntime.IsMultiplayerSession ||
                !TeamSummary.Ready || !PartyPingGate.ValidPosition(position) || nextPingSequence == uint.MaxValue) return false;
            SubmitPartyPingRpc(++nextPingSequence, position);
            return true;
        }
        // NGO ownership check plus explicit authenticated sender check on the Host.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitPartyPingRpc(uint sequence, Vector3 position, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || !PartyRuntime.IsMultiplayerSession || PartyRuntime.Instance == null) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            bool connected = NetworkManager.ConnectedClients.TryGetValue(sender, out var client) &&
                client.PlayerObject == NetworkObject && partyPlayerId.Value.Length > 0;
            double now = NetworkManager.ServerTime.Time;
            if (!pingGate.TryAccept(sender, OwnerClientId, connected, true, sequence, position, now)) return;
            // At most one live ping per member, at most four for the whole party.
            partyPings.Clear();
            partyPings.Add(new PartyPing { Sequence = sequence, Position = position, ExpiresAt = now + PartyPingGate.Lifetime });
        }
    }
}
