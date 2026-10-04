using System;
using System.Collections.Generic;

namespace NisitSimulator.Net
{
    // Server-side decision logic shared by the runtime and adversarial tests.
    public sealed class DoorToggleAuthority
    {
        public const int Capacity = 128;
        public const double Cooldown = .15;
        public int Count { get; private set; }
        public ulong Low { get; private set; }
        public ulong High { get; private set; }
        readonly Dictionary<ulong, uint> sequences = new Dictionary<ulong, uint>();
        readonly Dictionary<int, double> nextToggle = new Dictionary<int, double>();
        readonly Dictionary<int, int> ticks = new Dictionary<int, int>();

        public DoorToggleAuthority(int count)
        {
            if (count < 0 || count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
            Count = count;
        }
        public static bool Open(ulong low, ulong high, int id)
        {
            if (id < 0 || id >= Capacity) return false;
            return ((id < 64 ? low : high) & (1UL << (id % 64))) != 0;
        }
        public bool TryToggle(bool server, bool connected, bool inGame, ulong sender, uint sequence,
            int id, float distanceSquared, float range, double now, int tick, out string reason)
        {
            reason = "";
            if (!server) { reason = "not-server"; return false; }
            if (!connected || !inGame) { reason = "not-in-match"; return false; }
            if (id < 0 || id >= Count) { reason = "invalid-door"; return false; }
            if (float.IsNaN(distanceSquared) || float.IsInfinity(distanceSquared) || distanceSquared < 0 ||
                float.IsNaN(range) || float.IsInfinity(range) || range <= 0 || distanceSquared > range * range)
            { reason = "out-of-range"; return false; }
            if (sequence == 0 || (sequences.TryGetValue(sender, out uint old) && unchecked((int)(sequence - old)) <= 0))
            { reason = "replayed"; return false; }
            sequences[sender] = sequence;
            if ((ticks.TryGetValue(id, out int lastTick) && lastTick == tick) ||
                (nextToggle.TryGetValue(id, out double next) && now < next))
            { reason = "coalesced"; return false; }
            ticks[id] = tick; nextToggle[id] = now + Cooldown;
            if (id < 64) Low ^= 1UL << id; else High ^= 1UL << (id - 64);
            return true;
        }
        public void Forget(ulong sender) => sequences.Remove(sender);
        public static ulong Fingerprint(IEnumerable<string> keys)
        {
            ulong hash = 14695981039346656037UL;
            foreach (string key in keys)
            {
                foreach (char c in key) { hash ^= c; hash = unchecked(hash * 1099511628211UL); }
                hash ^= '\n'; hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }
    }
}
