#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Tests
{
    // ทดสอบสถานะหมดแรง (EditMode — ไม่ต้องเปิดฉาก) : เข้า/ออก, เกณฑ์แยก, ไม่ Game Over จากพลังงาน, กติกาเริ่มกิจกรรม, เซฟ/โหลด
    public class ExhaustionTests
    {
        GameObject go;
        PlayerStats stats;
        PlayerExhaustion ex;
        int critical;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestPlayer");
            stats = go.AddComponent<PlayerStats>();
            ex = go.AddComponent<PlayerExhaustion>();
            ex.Bind();   // Awake ไม่ทำงานใน EditMode
            critical = 0;
            stats.OnCriticalState += () => critical++;
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(go); }

        [Test]
        public void EnergyZero_EntersExhausted_Once_NoCriticalState()
        {
            stats.ChangeEnergy(-1000f);
            Assert.AreEqual(0f, stats.Energy, 1e-4, "พลังงานถูกจำกัดไม่ต่ำกว่า 0");
            Assert.IsTrue(ex.IsExhausted);
            Assert.AreEqual(1, ex.EnterCount);
            stats.ChangeEnergy(-5f);     // ลดซ้ำตอนเป็น 0
            stats.ChangeEnergy(0f);
            Assert.AreEqual(1, ex.EnterCount, "เข้าสถานะเพียงครั้งเดียว");
            Assert.AreEqual(0, critical, "พลังงาน 0 ต้องไม่ยิง OnCriticalState (Game Over)");
        }

        [Test]
        public void HealthZero_StillFiresCriticalState()
        {
            stats.ChangeHealth(-1000f);
            Assert.GreaterOrEqual(critical, 1, "สุขภาพหมดยังนำไป Game Over ตามเดิม");
        }

        [Test]
        public void Hysteresis_ExitOnlyAtThreshold()
        {
            stats.ChangeEnergy(-1000f);
            stats.ChangeEnergy(5f);
            Assert.IsTrue(ex.IsExhausted, "5 < 15 ยังหมดแรง");
            stats.ChangeEnergy(9.9f);
            Assert.IsTrue(ex.IsExhausted, "14.9 ยังหมดแรง");
            stats.ChangeEnergy(0.1f);
            Assert.IsFalse(ex.IsExhausted, "ถึง 15% ของ 100 → ออก");
            stats.ChangeEnergy(-10f);
            Assert.IsFalse(ex.IsExhausted, "ลดลงเหลือ 5 (ยังไม่ถึง 0) → ไม่เข้าใหม่");
            stats.ChangeEnergy(-10f);
            Assert.IsTrue(ex.IsExhausted);
            Assert.AreEqual(2, ex.EnterCount);
        }

        [Test]
        public void MovementFlags_FollowState_NotCumulative()
        {
            Assert.AreEqual(1f, ex.MoveSpeedMultiplier, 1e-4);
            stats.ChangeEnergy(-1000f);
            for (int i = 0; i < 10; i++) Assert.AreEqual(0.6f, ex.MoveSpeedMultiplier, 1e-4, "อ่านซ้ำกี่ครั้งก็ 60% (ไม่คูณสะสม)");
            Assert.IsFalse(ex.CanRun); Assert.IsFalse(ex.CanJump);
            stats.ChangeEnergy(50f);
            Assert.AreEqual(1f, ex.MoveSpeedMultiplier, 1e-4);
            Assert.IsTrue(ex.CanRun); Assert.IsTrue(ex.CanJump);
        }

        [Test]
        public void EnergyActivity_RejectedWhenExhaustedOrInsufficient()
        {
            Assert.IsTrue(PlayerExhaustion.CanStartEnergyActivity(go, 25f, "ทำงาน", out _));
            stats.ChangeEnergy(-80f);   // เหลือ 20
            Assert.IsFalse(PlayerExhaustion.CanStartEnergyActivity(go, 25f, "ทำงาน", out var r1));
            StringAssert.Contains("พลังงานไม่พอ", r1);
            stats.ChangeEnergy(-1000f);
            stats.ChangeEnergy(20f);    // 20 ≥ ต้องใช้ 12 แต่ยังหมดแรงหรือไม่? (20 ≥ 15 → ออกแล้ว)
            Assert.IsFalse(ex.IsExhausted);
            stats.ChangeEnergy(-1000f);
            Assert.IsFalse(PlayerExhaustion.CanStartEnergyActivity(go, 0f, "เรียน", out var r2));
            StringAssert.Contains("หมดแรง", r2);
        }

        [Test]
        public void RestoreFromSave_Cases()
        {
            // เซฟพลังงาน 0 (รวมเซฟเก่าไม่มีธง) → หมดแรง
            stats.LoadState(0f, 100f, 100f, 0f, 50f, 0, 0, 0f);
            ex.RestoreFromSave(false);
            Assert.IsTrue(ex.IsExhausted);
            Assert.AreEqual(0, critical);

            // เซฟหมดแรงค้างที่ 10 → ยังหมดแรง (ห้ามโหลดแล้ววิ่งได้)
            Object.DestroyImmediate(go); SetUp();
            stats.LoadState(10f, 100f, 100f, 0f, 50f, 0, 0, 0f);
            ex.RestoreFromSave(true);
            Assert.IsTrue(ex.IsExhausted);

            // ไม่หมดแรง 10 → ปกติ
            Object.DestroyImmediate(go); SetUp();
            stats.LoadState(10f, 100f, 100f, 0f, 50f, 0, 0, 0f);
            ex.RestoreFromSave(false);
            Assert.IsFalse(ex.IsExhausted);

            // ธงค้างแต่พลังงาน ≥ เกณฑ์ออก → ปกติ
            Object.DestroyImmediate(go); SetUp();
            stats.LoadState(40f, 100f, 100f, 0f, 50f, 0, 0, 0f);
            ex.RestoreFromSave(true);
            Assert.IsFalse(ex.IsExhausted);
        }

        [Test]
        public void LoadState_ClampsEnergy()
        {
            stats.LoadState(-20f, 100f, 100f, 0f, 50f, 0, 0, 0f);
            Assert.AreEqual(0f, stats.Energy, 1e-4);
            stats.LoadState(500f, 100f, 100f, 0f, 50f, 0, 0, 0f);
            Assert.AreEqual(stats.maxEnergy, stats.Energy, 1e-4);
        }

        [Test]
        public void Rules_Recover_And_Thresholds()
        {
            Assert.AreEqual(15f, ExhaustionRules.RecoverAmount(100f, 0.15f), 1e-4);
            Assert.AreEqual(20f, ExhaustionRules.RecoverAmount(100f, 0.20f), 1e-4);
            Assert.AreEqual(15f, ExhaustionRules.ExitEnergy(100f, 0.15f), 1e-4);
            Assert.IsTrue(ExhaustionRules.ShouldEnter(false, 0f, 0f));
            Assert.IsFalse(ExhaustionRules.ShouldEnter(true, 0f, 0f), "เข้าแล้วห้ามเข้าซ้ำ");
            Assert.IsFalse(ExhaustionRules.ShouldExit(true, 14.9f, 100f, 0.15f));
            Assert.IsTrue(ExhaustionRules.ShouldExit(true, 15f, 100f, 0.15f));
        }

        [Test]
        public void SaveData_ExhaustedFlag_RoundTrip_And_OldSaveDefault()
        {
            var d = new SaveData { energy = 0f, exhausted = true };
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d));
            Assert.IsTrue(back.exhausted);
            var old = JsonUtility.FromJson<SaveData>("{\"energy\":0}");
            Assert.IsFalse(old.exhausted, "เซฟเก่าไม่มีฟิลด์ → false แล้วคำนวณคืนจากพลังงาน");
        }

        [Test]
        public void NotResting_ByDefault_And_RestRequiresExhausted()
        {
            Assert.IsFalse(ex.IsResting);
            Assert.IsFalse(ex.CanRestNow(PlayerExhaustion.RestKind.Here, out var why));
            StringAssert.Contains("ยังไม่หมดแรง", why);
        }
    }
}
#endif
