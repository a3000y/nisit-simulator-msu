using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Unity.Collections;
using NisitSimulator.Net;

public class LobbyUITests
{
    static LobbyPlayer Player(ulong connection, byte slot, bool ready = true) => new LobbyPlayer { ConnectionId = connection, SlotIndex = slot, Ready = ready, PlayerId = new FixedString64Bytes("p" + connection), IsHost = connection == 0 };
    [TestCase("ROOM_FULL", "เข้าห้องไม่ได้: ห้องเต็มแล้ว")]
    [TestCase("GAME_STARTED", "เกมในห้องนี้เริ่มไปแล้ว ยังเข้าร่วมไม่ได้")]
    [TestCase("HOST_CLOSED", "โฮสต์ปิดห้องแล้ว")]
    [TestCase("KICKED", "คุณถูกนำออกจากห้อง")]
    public void StructuredReasonHasThaiText(string reason, string expected) => Assert.AreEqual(expected, NetworkUI.FriendlyReason(reason, false));
    [Test] public void FirstRejectionKeepsReasonDespiteLaterTransportMessage() => Assert.AreEqual(LobbyRules.Full, LobbyConnection.PreferReason(LobbyRules.Full, "[Disconnect Event] TransportShutdown"));
    [TestCase("x7a 9b2", LobbyAddressKind.Relay, "X7A9B2")]
    [TestCase("127.0.0.1", LobbyAddressKind.Lan, "127.0.0.1")]
    [TestCase("192.168.1.12", LobbyAddressKind.Lan, "192.168.1.12")]
    [TestCase("X7O9B2", LobbyAddressKind.Invalid, "")]
    [TestCase("X7I9B2", LobbyAddressKind.Invalid, "")]
    [TestCase("X709B2", LobbyAddressKind.Invalid, "")]
    [TestCase("X719B2", LobbyAddressKind.Invalid, "")]
    [TestCase("192.168.300.1", LobbyAddressKind.Invalid, "")]
    [TestCase("192.168.1.1:7777", LobbyAddressKind.Invalid, "")]
    [TestCase("0.0.0.0", LobbyAddressKind.Invalid, "")]
    [TestCase("ABC", LobbyAddressKind.Invalid, "")]
    public void AddressFormat(string value, LobbyAddressKind expected, string normalized)
    { Assert.AreEqual(expected, LobbyRules.ParseAddress(value, out string result)); if (expected != LobbyAddressKind.Invalid) Assert.AreEqual(normalized, result); }
    [TestCase("26.1.2.3", "Ethernet", -1)]
    [TestCase("25.1.2.3", "Ethernet", -1)]
    [TestCase("169.254.1.2", "Ethernet", -1)]
    [TestCase("192.168.1.2", "Hamachi", -1)]
    [TestCase("192.168.1.2", "ZeroTier", -1)]
    [TestCase("192.168.1.2", "VirtualBox", -1)]
    [TestCase("172.16.1.2", "vEthernet", -1)]
    [TestCase("192.168.1.2", "Wi-Fi", 0)]
    [TestCase("10.0.0.2", "Ethernet", 1)]
    [TestCase("172.31.1.2", "Ethernet", 2)]
    [TestCase("172.32.1.2", "Ethernet", -1)]
    public void LanFilterAndPriority(string ip, string adapter, int rank) => Assert.AreEqual(rank, LobbyRules.LanRank(ip, adapter));
    [Test] public void HostOnlyNetworkValueContainsNoSecret()
    { Assert.AreEqual("", LobbyRules.PublicCode("X7A9B2", LobbyShareMode.HostOnly)); Assert.AreEqual("X7A9B2", LobbyRules.PublicCode("X7A9B2", LobbyShareMode.Everyone)); Assert.AreEqual("", LobbyRules.PublicCode("192.168.1.2", LobbyShareMode.HostOnly)); }
    [Test] public void GappedConnectionsReuseFreeSlotWithoutMovingOthers()
    { var list = new List<LobbyPlayer> { Player(0, 0), Player(7, 2), Player(19, 3) }; Assert.AreEqual(1, LobbyRules.EmptySlot(list, 4)); list.Add(Player(44, 1)); Assert.AreEqual(byte.MaxValue, LobbyRules.EmptySlot(list, 4)); }
    [Test] public void StartRequiresTwoReadyPlayersAndLobbyPhase()
    { var list = new List<LobbyPlayer> { Player(0, 0) }; Assert.IsFalse(LobbyRules.CanStart(list, LobbyRoomPhase.Lobby, out _)); list.Add(Player(9, 1, false)); Assert.IsFalse(LobbyRules.CanStart(list, LobbyRoomPhase.Lobby, out string why)); Assert.AreEqual("รอ 1 คนกดพร้อม", why); list[1] = Player(9, 1); Assert.IsTrue(LobbyRules.CanStart(list, LobbyRoomPhase.Lobby, out _)); Assert.IsFalse(LobbyRules.CanStart(list, LobbyRoomPhase.InGame, out _)); }
    [Test] public void UpdatesRejectReplayForgedSenderAndDisconnectedPlayer()
    { var p = Player(19, 2); var gate = new LobbyRevisionGate(); Assert.IsFalse(gate.Accept(p, 7, p.PlayerId, 1, true)); Assert.IsFalse(gate.Accept(p, 19, new FixedString64Bytes("forged"), 1, true)); Assert.IsTrue(gate.Accept(p, 19, p.PlayerId, 1, true)); Assert.IsFalse(gate.Accept(p, 19, p.PlayerId, 1, true)); Assert.IsFalse(gate.Accept(p, 19, p.PlayerId, 2, false)); gate.Forget(p.PlayerId); Assert.IsTrue(gate.Accept(p, 19, p.PlayerId, 1, true)); }
    [Test] public void ThaiNameLimitAndFixedStringCapacity()
    { string name = LobbyRules.LimitName(new string('ก', 40), "ผู้เล่น"); Assert.AreEqual(20, new StringInfo(name).LengthInTextElements); Assert.DoesNotThrow(() => new FixedString512Bytes(LobbyRules.LimitName(new string('\u0E01', 1000), "ผู้เล่น"))); }
}
