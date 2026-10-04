using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace NisitSimulator.Net
{
    [Flags]
    public enum PartyStatus : byte { Normal = 0, Exhausted = 1, Exam = 2, Sleeping = 4, Indoors = 8 }

    public struct PartySummary : INetworkSerializable, IEquatable<PartySummary>
    {
        public bool Ready;
        public byte EnergyPercent;
        public PartyStatus Status;
        // Exterior doorway while in a teleported interior, otherwise actual world position.
        public Vector3 MapPosition;
        public bool TeleportedInterior;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Ready); s.SerializeValue(ref EnergyPercent);
            s.SerializeValue(ref Status); s.SerializeValue(ref MapPosition);
            s.SerializeValue(ref TeleportedInterior);
        }
        public bool Equals(PartySummary o) => Ready == o.Ready && EnergyPercent == o.EnergyPercent &&
            Status == o.Status && MapPosition.Equals(o.MapPosition) && TeleportedInterior == o.TeleportedInterior;
    }

    public struct PartyPing : INetworkSerializable, IEquatable<PartyPing>
    {
        public uint Sequence;
        public Vector3 Position;
        public double ExpiresAt;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Sequence); s.SerializeValue(ref Position); s.SerializeValue(ref ExpiresAt); }
        public bool Equals(PartyPing o) => Sequence == o.Sequence && Position.Equals(o.Position) && ExpiresAt.Equals(o.ExpiresAt);
    }

    // Host-only session membership. Connection IDs are routing keys, never persistent identities.
    public sealed class PartyRoster
    {
        public sealed class Member
        {
            public readonly FixedString64Bytes PlayerId;
            public readonly byte Slot;
            public Member(byte slot) { Slot = slot; PlayerId = new FixedString64Bytes(Guid.NewGuid().ToString("N")); }
            public Member(byte slot, FixedString64Bytes id) { Slot = slot; PlayerId = id; }
        }
        readonly Dictionary<ulong, Member> members = new Dictionary<ulong, Member>();
        public int Count => members.Count;
        public Member Join(ulong connection)
        {
            if (members.TryGetValue(connection, out var existing)) return existing;
            for (byte slot = 0; slot < NetworkUI.MaxPlayers; slot++)
            {
                bool used = false;
                foreach (var m in members.Values) if (m.Slot == slot) used = true;
                if (used) continue;
                var member = new Member(slot); members.Add(connection, member); return member;
            }
            return null;
        }
        public void Remove(ulong connection) => members.Remove(connection);
        public void Retain(HashSet<ulong> connections)
        {
            var gone = new List<ulong>();
            foreach (var id in members.Keys) if (!connections.Contains(id)) gone.Add(id);
            foreach (var id in gone) Remove(id);
        }
    }

    // One gate per avatar lifetime; bounded memory, replay protection survives ping expiry.
    public sealed class PartyPingGate
    {
        public const double Lifetime = 10, Cooldown = 2;
        uint lastSequence;
        double nextAllowed;
        public bool TryAccept(ulong sender, ulong owner, bool connected, bool multiplayer,
            uint sequence, Vector3 position, double now)
        {
            if (!multiplayer || !connected || sender != owner || sequence == 0 || sequence <= lastSequence ||
                double.IsNaN(now) || double.IsInfinity(now) || !ValidPosition(position)) return false;
            // Consume authenticated IDs even when rate limited; delayed retries cannot bypass cooldown.
            lastSequence = sequence;
            if (now < nextAllowed) return false;
            nextAllowed = now + Cooldown;
            return true;
        }
        public static bool ValidPosition(Vector3 p) =>
            Finite(p.x) && Finite(p.y) && Finite(p.z) &&
            p.x >= -150 && p.x <= 150 && p.z >= -150 && p.z <= 175 && p.y >= -10 && p.y <= 100;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool IsVisible(PartyPing ping, double now) => now < ping.ExpiresAt;
    }

    public static class PartyPresentation
    {
        public static byte Energy(float value, float max) => max <= 0 || float.IsNaN(value) || float.IsNaN(max)
            ? (byte)0 : (byte)Mathf.RoundToInt(Mathf.Clamp01(value / max) * 100);
        public static Vector3 Quantize(Vector3 p) => new Vector3(
            Mathf.Round(p.x * 2) / 2, Mathf.Round(p.y * 2) / 2, Mathf.Round(p.z * 2) / 2);
        public static string StatusText(PartySummary s)
        {
            if (!s.Ready) return "กำลังเข้าฉาก";
            var labels = new List<string>(4);
            if ((s.Status & PartyStatus.Exhausted) != 0) labels.Add("หมดแรง");
            if ((s.Status & PartyStatus.Exam) != 0) labels.Add("กำลังสอบ");
            if ((s.Status & PartyStatus.Sleeping) != 0) labels.Add("กำลังนอน");
            if ((s.Status & PartyStatus.Indoors) != 0) labels.Add("อยู่ในอาคาร");
            return labels.Count == 0 ? "ปกติ" : string.Join(" / ", labels);
        }
    }
}
