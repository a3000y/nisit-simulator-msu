#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.TimeSystem;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Tests
{
    // ทดสอบตรรกะกลางวัน–กลางคืน / เวลาตื่นนอน / การข้ามวัน (EditMode — ไม่ต้องเปิดฉาก)
    public class DayNightTests
    {
        GameObject go;
        GameClock clock;

        [SetUp] public void SetUp() { go = new GameObject("TestClock"); clock = go.AddComponent<GameClock>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(go); GameClock.NetworkFollower = false; GameClock.NetworkAuthoritative = false; }

        [TestCase(4.99f, DayPhase.Night)]
        [TestCase(5f, DayPhase.Dawn)]
        [TestCase(6.5f, DayPhase.Dawn)]
        [TestCase(7f, DayPhase.Day)]
        [TestCase(12f, DayPhase.Day)]
        [TestCase(17f, DayPhase.Dusk)]
        [TestCase(18.99f, DayPhase.Dusk)]
        [TestCase(19f, DayPhase.Night)]
        [TestCase(0f, DayPhase.Night)]
        public void Phase_DefaultRanges(float h, DayPhase expected)
        {
            Assert.AreEqual(expected, DayNightCycle.PhaseAt(h, 5f, 7f, 17f, 19f));
        }

        [Test]
        public void StreetLights_OnAtNight_OffAtDay()
        {
            Assert.AreEqual(0f, DayNightCycle.ComputeLightsFactor(12f, 18f, 6f, 0.5f), 1e-4);
            Assert.AreEqual(1f, DayNightCycle.ComputeLightsFactor(22f, 18f, 6f, 0.5f), 1e-4);
            Assert.AreEqual(1f, DayNightCycle.ComputeLightsFactor(2f, 18f, 6f, 0.5f), 1e-4);
            Assert.AreEqual(0.5f, DayNightCycle.ComputeLightsFactor(18.25f, 18f, 6f, 0.5f), 1e-3);   // กำลังเฟดเปิด
            Assert.AreEqual(0f, DayNightCycle.ComputeLightsFactor(6.5f, 18f, 6f, 0.5f), 1e-4);
        }

        [Test]
        public void Wake_0100_SameDay_2200_NextDay()
        {
            GameClock.ComputeWake(5, 60f, 7, out int d1);
            GameClock.ComputeWake(5, 22 * 60f, 7, out int d2);
            Assert.AreEqual(5, d1);
            Assert.AreEqual(6, d2);
        }

        [Test]
        public void SkipToNextMorning_FiresDayChangeOnce()
        {
            int fired = 0; clock.OnDayChanged += d => fired++;
            clock.RestoreClock(3, 22 * 60f);
            Assert.IsTrue(clock.SkipToNextMorning(7));
            Assert.AreEqual(4, clock.Day); Assert.AreEqual(7, clock.Hour); Assert.AreEqual(1, fired);

            clock.RestoreClock(4, 60f);   // 01:00
            Assert.IsFalse(clock.SkipToNextMorning(7));
            Assert.AreEqual(4, clock.Day); Assert.AreEqual(7, clock.Hour); Assert.AreEqual(1, fired);
        }

        [Test]
        public void Midnight_2359_RollsToNextDayOnce()
        {
            int fired = 0; clock.OnDayChanged += d => fired++;
            clock.RestoreClock(1, 23 * 60 + 59);
            clock.AdvanceMinutes(2f);
            Assert.AreEqual(2, clock.Day); Assert.AreEqual(0, clock.Hour); Assert.AreEqual(1, clock.Minute); Assert.AreEqual(1, fired);
        }

        [Test]
        public void NetworkFollower_DoesNotCrossMidnightAlone_ThenFollowsHost()
        {
            int fired = 0; clock.OnDayChanged += d => fired++;
            GameClock.NetworkFollower = true;
            clock.RestoreClock(1, 23 * 60 + 59);
            clock.AdvanceMinutes(5f);
            Assert.AreEqual(1, clock.Day); Assert.AreEqual(0, fired);
            clock.ApplyAuthoritativeTime(2, 3f);
            Assert.AreEqual(2, clock.Day); Assert.AreEqual(1, fired);
            clock.ApplyAuthoritativeTime(2, 10f);   // วันเดิม → ไม่ยิงซ้ำ
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void OldSave_HasNoTransformFlag()
        {
            var d = JsonUtility.FromJson<SaveData>("{\"money\":10,\"posX\":0,\"posY\":0,\"posZ\":0,\"gameDay\":4,\"gameMinutes\":1200}");
            Assert.IsFalse(d.hasPlayerTransform);
            Assert.AreEqual(4, d.gameDay);
            Assert.AreEqual(10, d.money);
        }
    }
}
#endif
