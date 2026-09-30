using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NisitSimulator.TimeSystem;
using NisitSimulator.Interaction;

namespace NisitSimulator.Net
{
    // ===== ซิงค์เวลาโลกใน Multiplayer (Host เป็นผู้ควบคุม) =====
    //   • ใช้ GameClock เดิมของแต่ละเครื่อง (แหล่งเวลาเดียวของ HUD/ตารางเรียน/สอบ) — ไม่มีนาฬิกาใหม่
    //   • Host: นาฬิกาเดินตามปกติ + ส่ง (วัน, นาที) ให้ทุกเครื่องทุก broadcastInterval วินาที
    //   • Client: GameClock.NetworkFollower = true → เดินเวลาเองให้ลื่นแต่ "ไม่ข้ามเที่ยงคืนเอง" · ปรับตาม Host เมื่อคลาดเกินเกณฑ์
    //             วันใหม่จาก Host → ยิง OnDayChanged ครั้งเดียวต่อวัน (ค่าขนม/สรุปวัน/autosave ของเครื่องนั้นทำงานปกติ ไม่ซ้ำ)
    //             อยู่ในห้องสอบ (GameClock.Suspended) → พักการรับเวลาไว้ แล้วตามทันเมื่อออกจากห้องสอบ (ตัวจับเวลาสอบไม่ถูกเร่ง)
    //   • การนอน: ผู้เล่นกดนอน = "พร้อม" · Host ข้ามเวลาไป 07:00 เมื่อผู้เล่นที่เชื่อมต่ออยู่ทุกคนพร้อม (คนที่สอบอยู่กดพร้อมไม่ได้)
    //   • ใช้ Named Message ของ NGO — ไม่ต้องมี NetworkObject ในฉาก · เล่นคนเดียว/ไม่เชื่อมต่อ = ไม่ทำอะไร (พฤติกรรมเดิม)
    public class WorldTimeSync : MonoBehaviour
    {
        const string MsgTime = "nisit_world_time";
        const string MsgReady = "nisit_sleep_ready";
        const string MsgStatus = "nisit_sleep_status";
        const string MsgWake = "nisit_sleep_wake";

        public static WorldTimeSync Instance { get; private set; }
        public static bool IsClientFollower => GameClock.NetworkFollower;

        [Tooltip("Host ส่งเวลาโลกทุกกี่วินาทีจริง")] public float broadcastInterval = 1f;
        [Tooltip("Client ปรับเวลาตาม Host เมื่อคลาดเกินกี่นาทีเกม")] public float resyncThresholdMinutes = 1.5f;
        [Tooltip("เวลาตื่นเมื่อทุกคนนอน")] public int wakeHour = 7;

        NetworkManager nm;
        bool registered;
        bool firstSync = true;
        float nextSend;
        GameClock clock;
        readonly HashSet<ulong> ready = new HashSet<ulong>();
        bool hasPending; int pendingDay; float pendingMinutes;

        public static WorldTimeSync EnsureExists()
        {
            if (Instance != null) return Instance;
            return new GameObject("WorldTimeSync").AddComponent<WorldTimeSync>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            Unregister();
            GameClock.NetworkFollower = false;
            GameClock.NetworkAuthoritative = false;
            if (Instance == this) Instance = null;
        }

        // เชื่อมต่อกับผู้เล่นอื่นอยู่จริงไหม (Host ที่ยังไม่มีใครเข้า = เล่นคนเดียว)
        public bool IsMultiplayerSession
        {
            get
            {
                if (nm == null || !nm.IsListening) return false;
                if (nm.IsServer) return nm.ConnectedClientsIds.Count > 1;
                return nm.IsConnectedClient;
            }
        }

        void Update()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            var cur = NetworkManager.Singleton;
            if (cur != nm) { Unregister(); nm = cur; }
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null)
            {
                if (registered) Unregister();
                GameClock.NetworkFollower = false;
                GameClock.NetworkAuthoritative = false;
                firstSync = true;
                return;
            }
            if (!registered) Register();

            GameClock.NetworkFollower = nm.IsClient && !nm.IsServer;
            GameClock.NetworkAuthoritative = nm.IsServer && nm.ConnectedClientsIds.Count > 1;

            if (nm.IsServer)
            {
                if (Time.unscaledTime >= nextSend) { nextSend = Time.unscaledTime + broadcastInterval; BroadcastTime(); }
            }
            else if (hasPending && !GameClock.Suspended)
            {
                hasPending = false;
                ApplyFromHost(pendingDay, pendingMinutes);
            }
        }

        void Register()
        {
            var cmm = nm.CustomMessagingManager;
            cmm.RegisterNamedMessageHandler(MsgTime, OnTimeMsg);
            cmm.RegisterNamedMessageHandler(MsgReady, OnReadyMsg);
            cmm.RegisterNamedMessageHandler(MsgStatus, OnStatusMsg);
            cmm.RegisterNamedMessageHandler(MsgWake, OnWakeMsg);
            nm.OnClientDisconnectCallback += OnClientDisconnect;
            registered = true;
            firstSync = true;
            ready.Clear();
        }

        void Unregister()
        {
            if (!registered) return;
            registered = false;
            if (nm == null) return;
            var cmm = nm.CustomMessagingManager;
            if (cmm != null)
            {
                cmm.UnregisterNamedMessageHandler(MsgTime);
                cmm.UnregisterNamedMessageHandler(MsgReady);
                cmm.UnregisterNamedMessageHandler(MsgStatus);
                cmm.UnregisterNamedMessageHandler(MsgWake);
            }
            nm.OnClientDisconnectCallback -= OnClientDisconnect;
            ready.Clear();
        }

        // ---------- Host → ทุกคน: เวลาโลก ----------
        public void BroadcastTime()
        {
            if (nm == null || !nm.IsServer || clock == null || !registered) return;
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe(clock.Day);
                w.WriteValueSafe(clock.TotalMinutes);
                nm.CustomMessagingManager.SendNamedMessageToAll(MsgTime, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnTimeMsg(ulong sender, FastBufferReader r)
        {
            if (nm == null || nm.IsServer) return;   // Host เป็นต้นทางเอง
            r.ReadValueSafe(out int day);
            r.ReadValueSafe(out float minutes);
            if (GameClock.Suspended) { hasPending = true; pendingDay = day; pendingMinutes = minutes; return; }
            ApplyFromHost(day, minutes);
        }

        void ApplyFromHost(int day, float minutes)
        {
            if (clock == null) return;
            if (firstSync)
            {
                // เข้าห้องครั้งแรก: ตั้งเวลาตาม Host โดยไม่ยิงเหตุการณ์ข้ามวัน (ไม่แจกค่าขนมย้อนหลัง)
                firstSync = false;
                clock.RestoreClock(day, minutes);
            }
            else if (day != clock.Day || Mathf.Abs(minutes - clock.TotalMinutes) > resyncThresholdMinutes)
                clock.ApplyAuthoritativeTime(day, minutes);
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ApplyNow();
        }

        // ---------- การนอนร่วมกัน ----------
        public void SetSleepReady(bool isReady)
        {
            if (nm == null || !registered) return;
            if (nm.IsServer) { SetReadyOnHost(nm.LocalClientId, isReady); return; }
            using (var w = new FastBufferWriter(4, Allocator.Temp))
            {
                w.WriteValueSafe(isReady);
                nm.CustomMessagingManager.SendNamedMessage(MsgReady, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnReadyMsg(ulong sender, FastBufferReader r)
        {
            if (nm == null || !nm.IsServer) return;
            r.ReadValueSafe(out bool isReady);
            SetReadyOnHost(sender, isReady);
        }

        void SetReadyOnHost(ulong id, bool isReady)
        {
            if (isReady) ready.Add(id); else ready.Remove(id);
            EvaluateSleep();
        }

        void OnClientDisconnect(ulong id)
        {
            if (nm == null || !nm.IsServer) return;
            ready.Remove(id);
            EvaluateSleep();
        }

        void EvaluateSleep()
        {
            if (nm == null || !nm.IsServer) return;
            var ids = nm.ConnectedClientsIds;
            int total = ids.Count, count = 0;
            foreach (var id in ids) if (ready.Contains(id)) count++;
            SendStatus(count, total);
            if (count == 0 || count < total || clock == null) return;

            // ทุกคนพร้อม → Host ข้ามเวลาโลก (ครั้งเดียว) แล้วประกาศให้ทุกเครื่องตื่น
            ready.Clear();
            clock.SkipToNextMorning(wakeHour);
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe(clock.Day);
                w.WriteValueSafe(clock.TotalMinutes);
                nm.CustomMessagingManager.SendNamedMessageToAll(MsgWake, w, NetworkDelivery.ReliableSequenced);
            }
            if (SleepController.Instance != null) SleepController.Instance.OnNetworkWake(false);   // เวลาข้ามแล้วบน Host
            BroadcastTime();
        }

        void SendStatus(int count, int total)
        {
            if (SleepController.Instance != null) SleepController.Instance.UpdateWaiting(count, total);
            using (var w = new FastBufferWriter(8, Allocator.Temp))
            {
                w.WriteValueSafe(count);
                w.WriteValueSafe(total);
                nm.CustomMessagingManager.SendNamedMessageToAll(MsgStatus, w, NetworkDelivery.ReliableSequenced);
            }
        }

        void OnStatusMsg(ulong sender, FastBufferReader r)
        {
            if (nm == null || nm.IsServer) return;
            r.ReadValueSafe(out int count);
            r.ReadValueSafe(out int total);
            if (SleepController.Instance != null) SleepController.Instance.UpdateWaiting(count, total);
        }

        void OnWakeMsg(ulong sender, FastBufferReader r)
        {
            if (nm == null || nm.IsServer) return;
            r.ReadValueSafe(out int day);
            r.ReadValueSafe(out float minutes);
            if (clock != null) { firstSync = false; clock.ApplyAuthoritativeTime(day, minutes); }
            if (SleepController.Instance != null) SleepController.Instance.OnNetworkWake(false);
        }
    }
}
