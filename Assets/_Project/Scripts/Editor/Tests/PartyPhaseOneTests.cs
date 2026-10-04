#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Net;
using System.Collections.Generic;

namespace NisitSimulator.Tests
{
    public class PartyPhaseOneTests
    {
        [Test] public void Membership_IsIdempotent_AndSupportsGappedConnections()
        {
            var roster = new PartyRoster(); var slots = new HashSet<byte>(); var ids = new HashSet<string>();
            foreach (ulong connection in new ulong[] { 0, 4, 19, 87 })
            {
                var m = roster.Join(connection); Assert.AreSame(m, roster.Join(connection));
                slots.Add(m.Slot); ids.Add(m.PlayerId.ToString());
            }
            Assert.AreEqual(4, roster.Count); Assert.AreEqual(4, slots.Count); Assert.AreEqual(4, ids.Count);
            Assert.IsNull(roster.Join(100));
        }
        [Test] public void Disconnect_RetainsOtherSlots_AndNeverReusesPlayerIdentity()
        {
            var roster = new PartyRoster(); var h = roster.Join(0); var gone = roster.Join(4); var keep = roster.Join(19);
            roster.Retain(new HashSet<ulong> { 0, 19 });
            Assert.AreEqual(2, roster.Count); Assert.AreSame(keep, roster.Join(19));
            var replacement = roster.Join(22);
            Assert.AreEqual(gone.Slot, replacement.Slot); Assert.AreNotEqual(gone.PlayerId, replacement.PlayerId);
            Assert.AreSame(h, roster.Join(0));
        }
        [Test] public void ForgedSender_CannotPlacePingOrConsumeOwnersSequence()
        {
            var gate = new PartyPingGate();
            Assert.IsFalse(gate.TryAccept(19, 4, true, true, 10, Vector3.zero, 1));
            Assert.IsTrue(gate.TryAccept(4, 4, true, true, 1, Vector3.zero, 1));
        }
        [Test] public void Replay_IsRejectedEvenAfterPingExpires()
        {
            var gate = new PartyPingGate();
            Assert.IsTrue(gate.TryAccept(4, 4, true, true, 3, Vector3.zero, 1));
            Assert.IsFalse(gate.TryAccept(4, 4, true, true, 3, Vector3.zero, 50));
            Assert.IsFalse(gate.TryAccept(4, 4, true, true, 2, Vector3.zero, 51));
        }
        [Test] public void Cooldown_DropsSpamAndConsumesRejectedSequence()
        {
            var gate = new PartyPingGate();
            Assert.IsTrue(gate.TryAccept(4, 4, true, true, 1, Vector3.zero, 1));
            Assert.IsFalse(gate.TryAccept(4, 4, true, true, 2, Vector3.zero, 1.1));
            Assert.IsFalse(gate.TryAccept(4, 4, true, true, 2, Vector3.zero, 4));
            Assert.IsTrue(gate.TryAccept(4, 4, true, true, 3, Vector3.zero, 4));
        }
        [Test] public void DisconnectedAndSinglePlayerRequests_AreRejected()
        {
            var gate = new PartyPingGate();
            Assert.IsFalse(gate.TryAccept(4, 4, false, true, 1, Vector3.zero, 1));
            Assert.IsFalse(gate.TryAccept(4, 4, true, false, 1, Vector3.zero, 1));
            Assert.IsTrue(gate.TryAccept(4, 4, true, true, 1, Vector3.zero, 1));
        }
        [Test] public void PingCoordinates_RejectInvalidAndOutsideCampus()
        {
            foreach (var p in new[] { new Vector3(float.NaN, 0, 0), new Vector3(0, float.PositiveInfinity, 0), new Vector3(151, 0, 0), new Vector3(0, 0, 176), new Vector3(1000, 0, 1000) })
                Assert.IsFalse(PartyPingGate.ValidPosition(p));
            Assert.IsTrue(PartyPingGate.ValidPosition(new Vector3(-150, 0, 175)));
        }
        [Test] public void PingExpiry_UsesHostTimeAndExactTenSeconds()
        {
            var ping = new PartyPing { Sequence = 1, ExpiresAt = 25 + PartyPingGate.Lifetime };
            Assert.IsTrue(PartyPingGate.IsVisible(ping, 34.999)); Assert.IsFalse(PartyPingGate.IsVisible(ping, 35));
        }
        [Test] public void Summary_ClampsEnergyAndPreservesConcurrentStatuses()
        {
            Assert.AreEqual(0, PartyPresentation.Energy(-2, 100)); Assert.AreEqual(100, PartyPresentation.Energy(200, 100));
            Assert.AreEqual(50, PartyPresentation.Energy(100, 200)); Assert.AreEqual(0, PartyPresentation.Energy(5, 0));
            var s = new PartySummary { Ready = true, Status = PartyStatus.Exhausted | PartyStatus.Indoors };
            StringAssert.Contains("หมดแรง", PartyPresentation.StatusText(s)); StringAssert.Contains("อยู่ในอาคาร", PartyPresentation.StatusText(s));
        }
        [Test] public void Summary_QuantizationSuppressesSubHalfUnitPositionNoise()
        {
            Assert.AreEqual(PartyPresentation.Quantize(new Vector3(1.01f, 0, 2.01f)), PartyPresentation.Quantize(new Vector3(1.02f, 0, 2.02f)));
        }
    }
}
#endif
