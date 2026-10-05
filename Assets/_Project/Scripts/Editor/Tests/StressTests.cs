#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Interaction;
using NisitSimulator.Stats;

namespace NisitSimulator.Tests
{
    // EditMode tests: ระบบความเครียด 4 ช่วง · กังวลก่อนสอบ · นอนไม่หลับ · ทางคลายเครียด — ตรรกะล้วน
    public class StressTests
    {
        [Test]
        public void Bands_Boundaries()
        {
            Assert.AreEqual(StressBand.Calm, StressBands.BandOf(0f));
            Assert.AreEqual(StressBand.Calm, StressBands.BandOf(29.9f));
            Assert.AreEqual(StressBand.Tense, StressBands.BandOf(30f));
            Assert.AreEqual(StressBand.Tense, StressBands.BandOf(54.9f));
            Assert.AreEqual(StressBand.Stressed, StressBands.BandOf(55f));
            Assert.AreEqual(StressBand.Severe, StressBands.BandOf(80f));
            Assert.AreEqual(StressBand.Severe, StressBands.BandOf(100f));
        }

        [Test]
        public void KnowledgeMult_PerBand()
        {
            Assert.AreEqual(1f, StressBands.KnowledgeMult(10f), 1e-4f);
            Assert.AreEqual(1.05f, StressBands.KnowledgeMult(40f), 1e-4f);
            Assert.AreEqual(0.90f, StressBands.KnowledgeMult(60f), 1e-4f);
            Assert.AreEqual(0.80f, StressBands.KnowledgeMult(80f), 1e-4f);
            Assert.AreEqual(0.65f, StressBands.KnowledgeMult(90f), 1e-3f);
            Assert.AreEqual(0.50f, StressBands.KnowledgeMult(100f), 1e-4f);
        }

        [Test]
        public void PlayerStats_UsesBands()
        {
            var go = new GameObject("StressTestPlayer");
            try
            {
                var s = go.AddComponent<PlayerStats>();
                s.LoadState(100, 100, 100, 0, 50, 0, 0, 40f);
                Assert.AreEqual(1.05f, s.KnowledgeMultiplier, 1e-4f);
                Assert.AreEqual(StressBand.Tense, s.StressBand);
                s.LoadState(100, 100, 100, 0, 50, 0, 0, 100f);
                Assert.AreEqual(0.5f, s.KnowledgeMultiplier, 1e-4f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hysteresis_HoldsNearEdge_AndDropsPastIt()
        {
            Assert.AreEqual(StressBand.Stressed, StressBands.BandWithHysteresis(StressBand.Tense, 55f), "ขึ้นทันทีเมื่อถึงเส้น");
            Assert.AreEqual(StressBand.Stressed, StressBands.BandWithHysteresis(StressBand.Stressed, 53f), "ต่ำกว่าเส้นไม่เกิน 3 = ยังอยู่ช่วงเดิม");
            Assert.AreEqual(StressBand.Tense, StressBands.BandWithHysteresis(StressBand.Stressed, 51.9f));
            Assert.AreEqual(StressBand.Calm, StressBands.BandWithHysteresis(StressBand.Severe, 10f), "ตื่นนอนลดหลายช่วงพร้อมกัน");
        }

        [Test]
        public void CrossingMessages_MentionEffect()
        {
            StringAssert.Contains("10%", StressBands.CrossingMessage(StressBand.Tense, StressBand.Stressed, 56f));
            StringAssert.Contains("ผ่อนคลาย", StressBands.CrossingMessage(StressBand.Tense, StressBand.Calm, 20f));
            StringAssert.Contains("+5%", StressBands.EffectText(40f));
            StringAssert.Contains("-10%", StressBands.EffectText(60f));
        }

        [Test]
        public void ExamAnxiety_ByPreparation()
        {
            Assert.AreEqual(8f, ExamStress.PerCourse(0f), 1e-4f);
            Assert.AreEqual(2f, ExamStress.PerCourse(1f), 1e-4f);
            Assert.AreEqual(5f, ExamStress.PerCourse(0.5f), 1e-4f);
        }

        [Test]
        public void ExamAnxiety_CappedAt30PerDay()
        {
            var plan = ExamStress.Plan(new List<float> { 0f, 0f, 0f, 0f, 0f, 0f });   // 6 วิชา ไม่เคยเรียน = 48 → บีบเหลือ 30
            float sum = 0f; foreach (var a in plan) sum += a;
            Assert.AreEqual(30f, sum, 1e-3f);
            var small = ExamStress.Plan(new List<float> { 1f, 1f, 1f });
            float s2 = 0f; foreach (var a in small) s2 += a;
            Assert.AreEqual(6f, s2, 1e-4f, "ไม่ถึงเพดาน = ไม่บีบ");
        }

        [Test]
        public void ExamRelief_HalfOrMoreWhenGood()
        {
            Assert.AreEqual(-4f, ExamStress.Relief(8f, 0.5f), 1e-4f);
            Assert.AreEqual(-6.4f, ExamStress.Relief(8f, 0.75f), 1e-4f);
            Assert.AreEqual(0f, ExamStress.Relief(0f, 1f), 1e-4f);
        }

        [Test]
        public void Insomnia_Above70()
        {
            Assert.IsFalse(SleepStation.IsRestless(70f));
            Assert.IsTrue(SleepStation.IsRestless(70.5f));
            Assert.AreEqual(999f, SleepStation.EnergyRestoreFor(10f, 100f, 999f, false), 1e-4f, "ปกติ = เต็ม");
            Assert.AreEqual(65f, SleepStation.EnergyRestoreFor(10f, 100f, 999f, true), 1e-4f, "นอนไม่หลับ = ฟื้นถึง 75");
            Assert.AreEqual(0f, SleepStation.EnergyRestoreFor(90f, 100f, 999f, true), 1e-4f, "มีเกิน 75 อยู่แล้ว = ไม่ลด");
        }

        [Test]
        public void Sleep_DefaultRelief_Is25()
        {
            var go = new GameObject("BedTest");
            try { Assert.AreEqual(-25f, go.AddComponent<SleepStation>().stressChange, 1e-4f); }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TalkRelief_CloseFriendDoubles()
        {
            Assert.AreEqual(-3f, TalkNPC.TalkStressRelief(0), 1e-4f);
            Assert.AreEqual(-3f, TalkNPC.TalkStressRelief(2), 1e-4f);
            Assert.AreEqual(-6f, TalkNPC.TalkStressRelief(3), 1e-4f);
        }

        [Test]
        public void Record_OldJsonWithoutStressKeys_Loads()
        {
            var r = JsonUtility.FromJson<AcademicRecord>("{\"version\":1,\"classYear\":2}");
            Assert.IsNotNull(r.stressKeys);
            Assert.AreEqual(0, r.stressKeys.Count);
        }
    }
}
#endif
