using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace NisitSimulator.DevTools
{
    // เครื่องมือวัดประสิทธิภาพ (ใช้ตอนพัฒนาเท่านั้น) — เพิ่มลงฉากตอน Play Mode เพื่อพาผู้เล่นเดินตามเส้นทางคงที่
    // แล้วเก็บค่าจาก ProfilerRecorder ต่อช่วง (segment): frame time, CPU main/render, GPU, draw calls, batches,
    // SetPass, shadow casters, triangles, physics, scripts, memory · ผลอยู่ใน Report (string)
    public class BuildingPerfProbe : MonoBehaviour
    {
        public class Segment
        {
            public string name;
            public List<Vector3> points = new List<Vector3>();   // ว่าง = ยืนนิ่ง
            public float holdSeconds = 2f;                       // ใช้เมื่อยืนนิ่ง หรือเวลารอหลังถึงจุดสุดท้าย
            public Vector3? teleport;                            // วาร์ปไปจุดนี้ก่อนเริ่ม (ไม่นับเฟรม warm-up)
        }

        public List<Segment> segments = new List<Segment>();
        public CharacterController controller;
        public MonoBehaviour movementToDisable;
        public float walkSpeed = 2.64f, gravity = -20f;
        public int warmupFrames = 20;
        public bool done;
        public string Report = "";

        struct Rec { public string label; public ProfilerRecorder r; public double scale; }
        readonly List<Rec> recs = new List<Rec>();
        readonly Dictionary<string, List<double>> samples = new Dictionary<string, List<double>>();
        int seg = -1, idx, warm; float hold, vy, half;
        bool holding;

        void AddRec(string label, ProfilerCategory cat, string stat, double scale)
        {
            var r = ProfilerRecorder.StartNew(cat, stat, 1);
            recs.Add(new Rec { label = label, r = r, scale = scale });
        }

        void OnEnable()
        {
            AddRec("cpuMainMs", ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1e-6);
            AddRec("cpuRenderMs", ProfilerCategory.Internal, "CPU Render Thread Frame Time", 1e-6);
            AddRec("gpuMs", ProfilerCategory.Internal, "GPU Frame Time", 1e-6);
            AddRec("mainThreadMs", ProfilerCategory.Internal, "Main Thread", 1e-6);
            AddRec("drawCalls", ProfilerCategory.Render, "Draw Calls Count", 1);
            AddRec("batches", ProfilerCategory.Render, "Batches Count", 1);
            AddRec("setPass", ProfilerCategory.Render, "SetPass Calls Count", 1);
            AddRec("shadowCasters", ProfilerCategory.Render, "Shadow Casters Count", 1);
            AddRec("trisK", ProfilerCategory.Render, "Triangles Count", 1e-3);
            AddRec("vertsK", ProfilerCategory.Render, "Vertices Count", 1e-3);
            AddRec("physicsMs", ProfilerCategory.Physics, "Physics.Simulate", 1e-6);
            AddRec("scriptsUpdMs", ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", 1e-6);
            AddRec("scriptsLateMs", ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", 1e-6);
            AddRec("cullingMs", ProfilerCategory.Render, "CullScriptable", 1e-6);
            AddRec("totalUsedMB", ProfilerCategory.Memory, "Total Used Memory", 1.0 / (1024 * 1024));
            AddRec("gfxUsedMB", ProfilerCategory.Memory, "Gfx Used Memory", 1.0 / (1024 * 1024));
            AddRec("textureMB", ProfilerCategory.Memory, "Texture Memory", 1.0 / (1024 * 1024));
            AddRec("meshMB", ProfilerCategory.Memory, "Mesh Memory", 1.0 / (1024 * 1024));
            AddRec("gcAllocKB", ProfilerCategory.Memory, "GC Allocated In Frame", 1.0 / 1024);
        }

        void OnDisable() { foreach (var r in recs) r.r.Dispose(); recs.Clear(); }

        public void Begin()
        {
            if (controller != null) half = controller.height * 0.5f * controller.transform.lossyScale.y;
            if (movementToDisable != null) movementToDisable.enabled = false;
            seg = -1; done = false; Report = "";
            NextSegment();
        }

        void NextSegment()
        {
            seg++;
            if (seg >= segments.Count) { Finish(); return; }
            var s = segments[seg];
            if (s.teleport.HasValue && controller != null)
            {
                controller.enabled = false; controller.transform.position = s.teleport.Value + Vector3.up * (half + 0.05f); controller.enabled = true;
                var rig = Camera.main != null ? Camera.main.GetComponent<NisitSimulator.CameraRig.IsometricCameraRig>() : null;
                if (rig != null) rig.SnapToTarget();
            }
            idx = 0; warm = warmupFrames; holding = s.points.Count == 0; hold = s.holdSeconds; vy = 0;
        }

        void Sample(string key, double v)
        {
            if (!samples.TryGetValue(key, out var list)) { list = new List<double>(); samples[key] = list; }
            list.Add(v);
        }

        void Update()
        {
            if (seg < 0 || done) return;
            var s = segments[seg];
            float dt = Time.deltaTime;

            // move like PlayerMovement (horizontal Move, then gravity Move)
            if (!holding && controller != null)
            {
                Vector3 d = s.points[idx] - controller.transform.position; d.y = 0;
                if (d.magnitude < 0.25f) { idx++; if (idx >= s.points.Count) holding = true; }
                else
                {
                    controller.Move(d.normalized * walkSpeed * dt);
                    controller.transform.rotation = Quaternion.LookRotation(d.normalized);
                }
            }
            if (controller != null)
            {
                if (controller.isGrounded && vy < 0) vy = -2f;
                vy += gravity * dt; controller.Move(Vector3.up * vy * dt);
            }

            if (warm > 0) { warm--; return; }   // skip frames right after teleports/segment change

            string p = s.name + "|";
            Sample(p + "frameMs", Time.unscaledDeltaTime * 1000.0);
            foreach (var r in recs)
                if (r.r.Valid) Sample(p + r.label, r.r.LastValue * r.scale);

            if (holding) { hold -= dt; if (hold <= 0f) NextSegment(); }
        }

        void Finish()
        {
            done = true;
            if (movementToDisable != null) movementToDisable.enabled = true;
            var sb = new StringBuilder();
            foreach (var s in segments)
            {
                sb.Append("## ").Append(s.name).Append('\n');
                foreach (var key in new[] { "frameMs", "cpuMainMs", "cpuRenderMs", "gpuMs", "mainThreadMs", "cullingMs", "drawCalls", "batches", "setPass", "shadowCasters", "trisK", "vertsK", "physicsMs", "scriptsUpdMs", "scriptsLateMs", "gcAllocKB", "totalUsedMB", "gfxUsedMB", "textureMB", "meshMB" })
                {
                    if (!samples.TryGetValue(s.name + "|" + key, out var list) || list.Count == 0) { sb.Append("  ").Append(key).Append(": n/a\n"); continue; }
                    var sorted = new List<double>(list); sorted.Sort();
                    double avg = 0; foreach (var v in list) avg += v; avg /= list.Count;
                    double p95 = sorted[Mathf.Clamp((int)(sorted.Count * 0.95), 0, sorted.Count - 1)], mx = sorted[sorted.Count - 1];
                    bool allZero = mx == 0;
                    sb.Append("  ").Append(key).Append(": avg=").Append(avg.ToString("F2")).Append(" p95=").Append(p95.ToString("F2")).Append(" max=").Append(mx.ToString("F2")).Append(" n=").Append(list.Count).Append(allZero ? " (always 0 → not measured)" : "").Append('\n');
                }
            }
            Report = sb.ToString();
        }
    }
}
