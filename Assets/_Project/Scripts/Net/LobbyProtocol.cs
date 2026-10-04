using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Netcode;

namespace NisitSimulator.Net
{
    public enum LobbyRoomPhase : byte { Lobby, Starting, InGame }
    public enum LobbyShareMode : byte { HostOnly, Everyone }
    public enum LobbyConnectionMode : byte { Online, Lan }
    public enum LobbyAddressKind { Invalid, Lan, Relay }

    public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
    {
        public FixedString64Bytes PlayerId, Accessories;
        public FixedString512Bytes Name;
        public ulong ConnectionId; // Routing only, never saved as player identity.
        public byte SlotIndex;
        public bool Ready, IsHost;
        public int Model, Color;
        public ushort Ping;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref PlayerId); s.SerializeValue(ref Name); s.SerializeValue(ref Accessories);
            s.SerializeValue(ref ConnectionId); s.SerializeValue(ref SlotIndex); s.SerializeValue(ref Ready);
            s.SerializeValue(ref IsHost); s.SerializeValue(ref Model); s.SerializeValue(ref Color); s.SerializeValue(ref Ping);
        }
        public bool Equals(LobbyPlayer p) => PlayerId.Equals(p.PlayerId) && Name.Equals(p.Name) && Accessories.Equals(p.Accessories) &&
            ConnectionId == p.ConnectionId && SlotIndex == p.SlotIndex && Ready == p.Ready && IsHost == p.IsHost &&
            Model == p.Model && Color == p.Color && Ping == p.Ping;
    }

    public static class LobbyRules
    {
        public const string Full = "ROOM_FULL", Started = "GAME_STARTED", Closed = "HOST_CLOSED", Kicked = "KICKED";
        public static LobbyAddressKind ParseAddress(string text, out string value)
        {
            value = (text ?? "").Trim().ToUpperInvariant();
            if (value.Contains('.'))
            {
                var parts = value.Split('.');
                if (parts.Length != 4 || parts.Any(p => p.Length == 0 || p.Length > 3 || p.Any(c => c < '0' || c > '9') || !byte.TryParse(p, out _))) return LobbyAddressKind.Invalid;
                if (!IPAddress.TryParse(value, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
                    ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224) return LobbyAddressKind.Invalid;
                value = ip.ToString(); return LobbyAddressKind.Lan;
            }
            value = value.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
            const string allowed = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return value.Length == 6 && value.All(c => allowed.IndexOf(c) >= 0) ? LobbyAddressKind.Relay : LobbyAddressKind.Invalid;
        }
        public static string LimitName(string text, string fallback, int limit = 20)
        {
            text = (text ?? "").Trim(); if (text.Length == 0) text = fallback;
            text = text.Replace("<", "").Replace(">", "").Replace("\n", " ").Replace("\r", " ");
            var info = new StringInfo(text); int n = Math.Min(limit, info.LengthInTextElements);
            text = info.SubstringByTextElements(0, n);
            // Bound pathological combining sequences before putting names into a fixed network string.
            while (System.Text.Encoding.UTF8.GetByteCount(text) > 400) text = text.Substring(0, text.Length - 1);
            return text;
        }
        public static string ErrorText(string code)
        {
            switch (code)
            {
                case Full: return "เข้าห้องไม่ได้: ห้องเต็มแล้ว";
                case Started: return "เกมในห้องนี้เริ่มไปแล้ว ยังเข้าร่วมไม่ได้";
                case Closed: return "โฮสต์ปิดห้องแล้ว";
                case Kicked: return "คุณถูกนำออกจากห้อง";
                case "INVALID_ADDRESS": return "รหัสไม่ถูกต้อง กรอกรหัสห้อง 6 ตัวหรือ IP";
                case "INVALID_CODE": return "รหัสห้องไม่ถูกต้องหรือห้องปิดแล้ว";
                case "OFFLINE": return "ไม่มีอินเทอร์เน็ต ตรวจการเชื่อมต่อแล้วลองอีกครั้ง";
                case "TIMEOUT": return "เชื่อมต่อไม่ได้ภายใน 10 วินาที ตรวจ IP หรือรหัสห้อง";
                case "CANCELLED": return "ยกเลิกการเชื่อมต่อแล้ว";
                case "SERVICE": return "บริการออนไลน์ไม่พร้อม ลองใหม่หรือใช้โหมด LAN แทน";
                default: return "เชื่อมต่อไม่ได้ ตรวจ IP หรือรหัสห้องและให้โฮสต์เปิดห้องก่อน";
            }
        }
        public static string PublicCode(string privateCode, LobbyShareMode share) => share == LobbyShareMode.Everyone ? privateCode ?? "" : "";
        public static bool CanStart(IList<LobbyPlayer> players, LobbyRoomPhase phase, out string why)
        {
            if (phase != LobbyRoomPhase.Lobby) { why = "กำลังเริ่มเกม"; return false; }
            if (players.Count < 2) { why = "ต้องมีผู้เล่นอย่างน้อย 2 คน"; return false; }
            int missing = players.Count(p => !p.Ready);
            if (missing > 0) { why = "รอ " + missing + " คนกดพร้อม"; return false; }
            why = "ทุกคนพร้อมแล้ว"; return true;
        }
        public static byte EmptySlot(IEnumerable<LobbyPlayer> players, int max)
        {
            var occupied = new HashSet<byte>(players.Select(p => p.SlotIndex));
            for (byte i = 0; i < max; i++) if (!occupied.Contains(i)) return i;
            return byte.MaxValue;
        }
        public static bool CanUpdate(ulong sender, LobbyPlayer player, FixedString64Bytes claimedId, bool connected)
            => connected && sender == player.ConnectionId && claimedId.Equals(player.PlayerId);
        public static int LanRank(string address, string adapter)
        {
            string label = (adapter ?? "").ToLowerInvariant();
            foreach (var bad in new[] { "vpn", "hamachi", "zerotier", "virtualbox", "vethernet", "vmware", "virtual", "tailscale", "wireguard", "tunnel", "radmin", "tap-", "tap adapter" })
                if (label.Contains(bad)) return -1;
            if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return -1;
            var b = ip.GetAddressBytes();
            if (b[0] == 192 && b[1] == 168) return 0;
            if (b[0] == 10) return 1;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return 2;
            return -1;
        }
        public static List<string> LocalLanAddresses()
        {
            var addresses = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var a in nic.GetIPProperties().UnicastAddresses)
                    {
                        string ip = a.Address.ToString();
                        if (LanRank(ip, nic.Name + " " + nic.Description) >= 0 && !addresses.Contains(ip)) addresses.Add(ip);
                    }
                }
            }
            catch (NetworkInformationException) { }
            return addresses.OrderBy(ip => LanRank(ip, "")).ThenBy(ip => ip, StringComparer.Ordinal).ToList();
        }
    }

    public sealed class LobbyRevisionGate
    {
        readonly Dictionary<string, uint> revisions = new Dictionary<string, uint>();
        public bool Accept(LobbyPlayer player, ulong sender, FixedString64Bytes id, uint sequence, bool connected)
        {
            if (!LobbyRules.CanUpdate(sender, player, id, connected) || sequence == 0) return false;
            string key = id.ToString();
            if (revisions.TryGetValue(key, out uint old) && sequence <= old) return false;
            revisions[key] = sequence; return true;
        }
        public void Forget(FixedString64Bytes id) => revisions.Remove(id.ToString());
    }
}
