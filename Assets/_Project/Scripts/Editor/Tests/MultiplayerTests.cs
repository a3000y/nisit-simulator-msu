#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.Net;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.Tests
{
    // ทดสอบตรรกะสำคัญของ Multiplayer (EditMode — ไม่ต้องเปิดเครือข่ายจริง)
    //   เวลาโลก (Host เป็นผู้คุม/ไม่หยุดเพราะคนเดียว) · กติการับเข้าห้อง · ตารางเทรดฝั่ง Host (กันปลอมผล/ส่งซ้ำ)
    //   แยกไฟล์ autosave โหมดหลายคน · สลับโมเดล avatar โดยไม่ทำลาย NetworkObject
    public class MultiplayerTests
    {
        [Test] public void Doors_Dorm53InitiallyClosed()
        {
            var authority = new DoorToggleAuthority(53);
            for (int i = 0; i < 53; i++) Assert.IsFalse(DoorToggleAuthority.Open(authority.Low, authority.High, i));
        }
        [Test] public void Doors_HostTogglesAcrossBothMaskWords()
        {
            var authority = new DoorToggleAuthority(108);
            Assert.IsTrue(authority.TryToggle(true, true, true, 0, 1, 107, 0, 1.6f, 1, 30, out _));
            Assert.IsTrue(DoorToggleAuthority.Open(authority.Low, authority.High, 107));
            Assert.IsTrue(authority.TryToggle(true, true, true, 0, 2, 107, 0, 1.6f, 2, 60, out _));
            Assert.IsFalse(DoorToggleAuthority.Open(authority.Low, authority.High, 107));
            Assert.AreEqual(0UL, authority.Low); Assert.AreEqual(0UL, authority.High);
        }
        [Test] public void Doors_ForgedDistantInvalidAndNonServerRequestsRejected()
        {
            var authority = new DoorToggleAuthority(108);
            Assert.IsFalse(authority.TryToggle(true, true, true, 1, 1, 0, 10000, 1.6f, 1, 1, out var reason));
            Assert.AreEqual("out-of-range", reason);
            Assert.IsFalse(authority.TryToggle(true, true, true, 1, 2, 108, 0, 1.6f, 2, 2, out _));
            Assert.IsFalse(authority.TryToggle(false, true, true, 1, 3, 0, 0, 1.6f, 3, 3, out _));
            Assert.IsFalse(authority.TryToggle(true, false, true, 1, 4, 0, 0, 1.6f, 4, 4, out _));
            Assert.IsFalse(authority.TryToggle(true, true, false, 1, 5, 0, 0, 1.6f, 5, 5, out _));
            Assert.IsFalse(authority.TryToggle(true, true, true, 1, 6, 0, float.NaN, 1.6f, 6, 6, out _));
            Assert.AreEqual(0UL, authority.Low); Assert.AreEqual(0UL, authority.High);
        }
        [Test] public void Doors_SimultaneousAndTenRapidPressesToggleOnce()
        {
            var authority = new DoorToggleAuthority(108); int accepted = 0;
            for (uint seq = 1; seq <= 10; seq++)
                if (authority.TryToggle(true, true, true, seq % 2, seq, 1, 0, 1.6f, 1, 30, out _)) accepted++;
            Assert.AreEqual(1, accepted);
            Assert.IsTrue(DoorToggleAuthority.Open(authority.Low, authority.High, 1));
            Assert.IsFalse(authority.TryToggle(true, true, true, 1, 9, 1, 0, 1.6f, 2, 60, out var reason));
            Assert.AreEqual("replayed", reason);
        }
        [Test] public void Doors_Complete108OpenCloseAndNewMatchResets()
        {
            var authority = new DoorToggleAuthority(108);
            for (int i = 0; i < 108; i++)
                Assert.IsTrue(authority.TryToggle(true, true, true, 0, (uint)i + 1, i, 0, 1.6f, 1, 30, out _));
            for (int i = 0; i < 108; i++) Assert.IsTrue(DoorToggleAuthority.Open(authority.Low, authority.High, i));
            for (int i = 0; i < 108; i++)
                Assert.IsTrue(authority.TryToggle(true, true, true, 0, (uint)i + 109, i, 0, 1.6f, 2, 60, out _));
            Assert.AreEqual(0UL, authority.Low); Assert.AreEqual(0UL, authority.High);
            var nextMatch = new DoorToggleAuthority(108);
            Assert.AreEqual(0UL, nextMatch.Low); Assert.AreEqual(0UL, nextMatch.High);
        }
        [Test] public void Doors_CatalogFingerprintChangesWhenDoorPathChanges()
        {
            Assert.AreEqual(DoorToggleAuthority.Fingerprint(new[] { "Dorm_Building/A", "GE_Building/B" }),
                DoorToggleAuthority.Fingerprint(new[] { "Dorm_Building/A", "GE_Building/B" }));
            Assert.AreNotEqual(DoorToggleAuthority.Fingerprint(new[] { "Dorm_Building/A" }),
                DoorToggleAuthority.Fingerprint(new[] { "Dorm_Building/B" }));
        }
        GameObject go;
        GameClock clock;

        [SetUp] public void SetUp() { go = new GameObject("TestClock"); clock = go.AddComponent<GameClock>(); TradeRelay.ServerClearAll(); }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            GameClock.NetworkFollower = false; GameClock.NetworkAuthoritative = false;
            GameSession.IsMultiplayerGame = false;
            TradeRelay.ServerClearAll();
        }

        // ---------- เวลาโลก ----------
        [Test]
        public void SinglePlayer_PausedOrSuspended_ClockStops()   // พฤติกรรมเดิมของเล่นคนเดียวต้องไม่เปลี่ยน
        {
            Assert.AreEqual(0f, GameClock.WorldDeltaSeconds(true, GameState.Paused, false, false, 0f, 0f, 0.02f, 0.33f));
            Assert.AreEqual(0f, GameClock.WorldDeltaSeconds(true, GameState.Playing, true, false, 1f, 0.02f, 0.02f, 0.33f));
            Assert.AreEqual(0.02f, GameClock.WorldDeltaSeconds(true, GameState.Playing, false, false, 1f, 0.02f, 0.02f, 0.33f), 1e-6);
            Assert.AreEqual(0f, GameClock.WorldDeltaSeconds(true, GameState.Playing, false, false, 0f, 0f, 0.02f, 0.33f));   // หน้าต่างหยุดเวลา
        }

        [Test]
        public void Host_PauseOrModal_DoesNotFreezeWorld()
        {
            // Host เปิดเมนูหยุด (State=Paused, timeScale 0) → เวลาโลกยังเดินด้วยเวลาจริง
            Assert.AreEqual(0.02f, GameClock.WorldDeltaSeconds(true, GameState.Paused, false, true, 0f, 0f, 0.02f, 0.33f), 1e-6);
            // Host อยู่ในห้องสอบ → เวลาโลกไม่หยุด
            Assert.AreEqual(0.02f, GameClock.WorldDeltaSeconds(true, GameState.Playing, true, true, 1f, 0.02f, 0.02f, 0.33f), 1e-6);
            // เฟรมกระตุกนาน ๆ ถูกจำกัดเท่า maximumDeltaTime (ไม่กระโดดข้ามเวลามาก)
            Assert.AreEqual(0.33f, GameClock.WorldDeltaSeconds(true, GameState.Paused, false, true, 0f, 0f, 5f, 0.33f), 1e-6);
        }

        [Test]
        public void Host_GameEnded_ClockStops()
        {
            Assert.AreEqual(0f, GameClock.WorldDeltaSeconds(true, GameState.GameOver, false, true, 0f, 0f, 0.02f, 0.33f));
            Assert.AreEqual(0f, GameClock.WorldDeltaSeconds(true, GameState.Win, false, true, 1f, 0.02f, 0.02f, 0.33f));
        }

        [Test]
        public void Client_FirstSync_NoDayEvent_ThenOneEventPerDay()
        {
            int fired = 0; clock.OnDayChanged += d => fired++;
            GameClock.NetworkFollower = true;
            clock.RestoreClock(5, 600f);                 // เข้าห้องครั้งแรก: ตั้งตาม Host โดยไม่ยิงวันใหม่ (ไม่แจกค่าขนมย้อนหลัง)
            Assert.AreEqual(0, fired);
            clock.ApplyAuthoritativeTime(7, 30f);        // Host ข้ามไป 2 วัน → ยิงวันละครั้ง
            Assert.AreEqual(2, fired);
            clock.ApplyAuthoritativeTime(7, 31f);        // ซิงค์ซ้ำวันเดิม → ไม่ยิงซ้ำ
            clock.ApplyAuthoritativeTime(7, 31f);
            Assert.AreEqual(2, fired);
        }

        // ---------- รับเข้าห้อง ----------
        [Test]
        public void Approval_RoomFull_RejectedWithReason()
        {
            Assert.IsTrue(NetworkUI.ShouldApprove(true, 0, 4, false, out _));
            Assert.IsTrue(NetworkUI.ShouldApprove(false, 3, 4, false, out var r0));
            Assert.AreEqual("", r0);
            Assert.IsFalse(NetworkUI.ShouldApprove(false, 4, 4, false, out var r1));
            StringAssert.Contains("ห้องเต็ม", r1);
            Assert.AreEqual(4, NetworkUI.MaxPlayers);
        }

        [Test]
        public void Approval_GameInProgress_RejectedClearly()
        {
            Assert.IsFalse(NetworkUI.ShouldApprove(false, 1, 4, true, out var r));
            StringAssert.Contains("เริ่มไปแล้ว", r);
            Assert.IsTrue(NetworkUI.ShouldApprove(true, 0, 4, true, out _), "Host เองต้องผ่านเสมอ");
        }

        [Test]
        public void DisconnectReason_ReadableForPlayers()
        {
            StringAssert.Contains("ห้องเต็ม", NetworkUI.FriendlyReason("ห้องเต็มแล้ว (สูงสุด 4 คน)", false));
            StringAssert.Contains("เชื่อมต่อไม่ได้", NetworkUI.FriendlyReason("[Disconnect Event][Client-0][TransportClientId-1][MaxConnectionAttempts] Connection closed", false));
            StringAssert.Contains("โฮสต์ปิดห้อง", NetworkUI.FriendlyReason("Disconnected due to host shutting down.", true));
            StringAssert.Contains("หลุดจากห้อง", NetworkUI.FriendlyReason("", true));
            StringAssert.DoesNotContain("Disconnect Event", NetworkUI.FriendlyReason("[Disconnect Event][Client-1] Client-1 disconnected by server.", true));
        }

        // ---------- จุดเกิดหอพัก: ไม่ซ้อนกันแม้ ClientId กระโดด (หลุด/เข้าใหม่ในล็อบบี้) ----------
        [Test]
        public void DormSlot_UniquePerPlayer_EvenWithGappedClientIds()
        {
            var ids = new ulong[] { 0, 4, 5, 6 };   // เหตุการณ์จริงจากการทดสอบ: Host=0 · ผู้เล่นอื่นได้ id 4/5/6 หลังเชื่อมต่อใหม่
            var slots = new System.Collections.Generic.HashSet<int>();
            foreach (var id in ids) slots.Add(NisitSimulator.SaveLoad.PlayerSpawnSystem.SlotFor(id, ids, 4));
            Assert.AreEqual(4, slots.Count, "4 คนต้องได้ 4 ช่องไม่ซ้ำกัน");
            Assert.AreEqual(0, NisitSimulator.SaveLoad.PlayerSpawnSystem.SlotFor(0, ids, 4), "Host ได้ช่องหลักเสมอ");
            Assert.AreEqual(1, NisitSimulator.SaveLoad.PlayerSpawnSystem.SlotFor(4, ids, 4));
        }

        // ---------- ตารางเทรดฝั่ง Host ----------
        [Test]
        public void Trade_ResultOnlyFromRealReceiver_AndOnlyOnce()
        {
            var item = new FixedString64Bytes("กาแฟ");
            TradeRelay.ServerRegister(1, 7, 2, item);
            Assert.IsFalse(TradeRelay.ServerTryComplete(1, 7, 3, out _), "เครื่องที่ 3 ปลอมผลแทนผู้รับไม่ได้");
            Assert.IsFalse(TradeRelay.ServerTryComplete(1, 7, 1, out _), "ผู้ให้ยืนยันแทนผู้รับเองไม่ได้");
            Assert.IsTrue(TradeRelay.ServerTryComplete(1, 7, 2, out var got));
            Assert.AreEqual("กาแฟ", got.ToString());
            Assert.IsFalse(TradeRelay.ServerTryComplete(1, 7, 2, out _), "ส่งผลซ้ำต้องไม่ผ่าน (กันของเพิ่มซ้ำ)");
            Assert.AreEqual(0, TradeRelay.ServerPendingCount);
        }

        [Test]
        public void Trade_ClientDisconnect_ClearsPending()
        {
            TradeRelay.ServerRegister(1, 1, 2, new FixedString64Bytes("ขนม"));
            TradeRelay.ServerRegister(3, 1, 1, new FixedString64Bytes("น้ำ"));
            TradeRelay.ServerRegister(3, 2, 4, new FixedString64Bytes("น้ำ"));
            TradeRelay.ServerForgetClient(1);
            Assert.AreEqual(1, TradeRelay.ServerPendingCount);
            Assert.IsFalse(TradeRelay.ServerTryComplete(1, 1, 2, out _));
        }

        // ---------- เซฟ: โหมดหลายคนไม่แตะช่องเซฟเล่นคนเดียว ----------
        static string CurrentSavePath()
        {
            var p = typeof(SaveSystem).GetProperty("SavePath", BindingFlags.NonPublic | BindingFlags.Static);
            return (string)p.GetValue(null);
        }

        [Test]
        public void MultiplayerGame_SavesToSeparateFile_NotRealSlot()
        {
            string ov = SaveSystem.DevPathOverride;
            try
            {
                SaveSystem.DevPathOverride = null;   // อ่านอย่างเดียว — ไม่มีการเขียนไฟล์ในเทสต์นี้
                GameSession.IsMultiplayerGame = false;
                Assert.AreEqual(SaveSystem.RealSlotPath(GameSession.SaveSlot), CurrentSavePath());
                GameSession.IsMultiplayerGame = true;
                Assert.AreEqual(SaveSystem.MultiplayerPath, CurrentSavePath());
                for (int s = 0; s <= 2; s++) Assert.AreNotEqual(SaveSystem.RealSlotPath(s), CurrentSavePath());
            }
            finally { SaveSystem.DevPathOverride = ov; GameSession.IsMultiplayerGame = false; }
        }

        // ---------- avatar: สลับโมเดลแต่ห้ามทำลาย root (ที่มี NetworkObject) ----------
        [Test]
        public void ApplyModel_RigOnRoot_KeepsRoot()
        {
            var cat = CharacterCatalog.Load();
            Assume.That(cat != null && cat.Count > 0 && cat.Model(0) != null, "ไม่มีแคตตาล็อกตัวละครในโปรเจกต์");
            var root = new GameObject("AvatarRoot");
            try
            {
                root.AddComponent<Animator>();                        // แบบเดียวกับ NetworkAvatar.prefab (Animator อยู่ที่ root)
                new GameObject("OldMesh").transform.SetParent(root.transform, false);
                new GameObject("NameTag").transform.SetParent(root.transform, false);
                var anim = CharacterCatalog.Apply(root.transform, 0);
                Assert.IsTrue(root != null, "root ต้องไม่ถูกทำลาย");
                Assert.IsNull(root.GetComponent<Animator>(), "Animator เดิมบน root ต้องถูกถอด");
                Assert.IsNull(root.transform.Find("OldMesh"), "โมเดลเดิมต้องถูกลบ");
                Assert.IsNotNull(root.transform.Find("NameTag"), "ป้ายชื่อต้องอยู่");
                Assert.IsNotNull(anim);
                Assert.AreNotSame(root.transform, anim.transform);
                Assert.IsTrue(anim.transform.IsChildOf(root.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AvatarFeet_UsesControllerCenterHeightAndParentScale()
        {
            var parent = new GameObject("ScaledParent");
            try
            {
                parent.transform.position = new Vector3(20, 3, 10);
                parent.transform.localScale = new Vector3(2, 3, 4);
                var player = new GameObject("Player"); player.transform.SetParent(parent.transform, false);
                player.transform.localPosition = new Vector3(1, 2, 3);
                player.transform.localScale = Vector3.one * .66f;
                var controller = player.AddComponent<CharacterController>();
                controller.height = 2; controller.center = new Vector3(.2f, .4f, -.3f);
                var expected = player.transform.TransformPoint(new Vector3(.2f, -.6f, -.3f));
                Assert.That(Vector3.Distance(expected, AvatarGeometry.Feet(controller)), Is.LessThan(1e-5f));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void PuppetScale_ApplyLookAndRepeatedSwapCannotAccumulateScaleOrDestroyNetworkRoot()
        {
            var root = new GameObject("Puppet");
            try
            {
                var network = root.AddComponent<Unity.Netcode.NetworkObject>();
                new GameObject("NameTag").transform.SetParent(root.transform, false);
                Vector3 reference = Vector3.one * .7341991f;
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    var animator = CharacterCatalog.ApplyPuppet(root.transform, repeat % CharacterCatalog.Load().Count, reference);
                    Vector3 scale = animator.transform.localScale;
                    for (int i = 0; i < 5; i++) CharacterCatalog.ApplyLook(animator.gameObject, i, NetworkAvatar.Palette);
                    Assert.AreEqual(scale, animator.transform.localScale);
                    Assert.That(Vector3.Distance(reference, animator.transform.lossyScale), Is.LessThan(1e-5f));
                    Assert.AreEqual(Vector3.one, root.transform.localScale);
                    Assert.NotNull(network); Assert.NotNull(root.transform.Find("NameTag"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)]
        public void CatalogPuppet_EveryOutfitHasCorrectAvatarNoRootMotionAndEqualNormalizedHeight(int index)
        {
            var catalog = CharacterCatalog.Load(); Assert.AreEqual(17, catalog.Count);
            var root = new GameObject("Puppet");
            try
            {
                var animator = CharacterCatalog.ApplyPuppet(root.transform, index, Vector3.one);
                Assert.NotNull(animator);
                Assert.IsFalse(animator.applyRootMotion);
                Assert.AreSame(catalog.Model(index).GetComponentInChildren<Animator>().avatar, animator.avatar);
                Assert.AreSame(catalog.ControllerFor(index), animator.runtimeAnimatorController);
                Assert.IsTrue(animator.isHuman);
                Assert.IsTrue(AvatarGeometry.NormalizeHeight(animator, 1.4f));
                Assert.IsTrue(AvatarGeometry.VisualBounds(animator.transform, out var bounds));
                Assert.AreEqual(1.4f, bounds.size.y, .002f);
                Assert.AreEqual(root.transform.position.y, bounds.min.y, .002f);
                Vector3 scale = animator.transform.localScale;
                Assert.IsTrue(AvatarGeometry.NormalizeHeight(animator, 1.4f));
                Assert.That(Vector3.Distance(scale, animator.transform.localScale), Is.LessThan(.002f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
