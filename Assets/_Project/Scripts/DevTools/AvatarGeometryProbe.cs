#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using NisitSimulator.Net;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.DevTools
{
    // Opt-in through MPTestAgent. Never writes game saves or changes gameplay objects.
    [DefaultExecutionOrder(10000)]
    public sealed class AvatarGeometryProbe : MonoBehaviour
    {
        readonly Queue<string> pendingDumps = new Queue<string>();
        readonly List<Report> activeReports = new List<Report>();
        [Serializable] public class Sample
        {
            public string kind, name, modelName, groundName, controller, avatar;
            public ulong owner;
            public int modelIndex, frame, rendererCount, animatorCount, colliderCount, rigidbodyCount, networkTransformCount;
            public float time, boundsMinY, boundsHeight, bodyMinY, bodyHeight, groundY, groundGap, bodyGap, humanScale, ccHeight, ccCenterY, ccSkin, capsuleBottomY;
            public Vector3 rootPosition, rootLocalScale, rootLossyScale, modelPosition, modelLocalPosition, modelLocalScale, modelLossyScale;
            public bool hasGround, grounded, rootMotion, isHuman, scaleSync, dynamicPhysics;
        }
        [Serializable] public class Report { public string label; public List<Sample> samples = new List<Sample>(); }

        public static Sample Measure(GameObject go, string kind, ulong owner, int modelIndex)
        {
            var sample = new Sample { kind = kind, owner = owner, modelIndex = modelIndex, name = go.name, frame = Time.frameCount, time = Time.realtimeSinceStartup,
                rootPosition = go.transform.position, rootLocalScale = go.transform.localScale, rootLossyScale = go.transform.lossyScale };
            var animators = go.GetComponentsInChildren<Animator>();
            sample.animatorCount = animators.Length;
            var anim = animators.Length > 0 ? animators[0] : null;
            if (anim != null)
            {
                var model = anim.transform;
                sample.modelName = model.name; sample.modelPosition = model.position; sample.modelLocalPosition = model.localPosition;
                sample.modelLocalScale = model.localScale; sample.modelLossyScale = model.lossyScale;
                sample.isHuman = anim.isHuman; sample.humanScale = anim.isHuman ? anim.humanScale : 0;
                sample.rootMotion = anim.applyRootMotion;
                sample.avatar = anim.avatar != null ? anim.avatar.name : "";
                sample.controller = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "";
            }
            Bounds bounds = default; bool found = false;
            Renderer body = null; int mostVertices = -1;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
                sample.rendererCount++;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
                if (renderer.GetComponent<NisitSimulator.Systems.AccessoryTag>() != null) continue;
                var skin = renderer as SkinnedMeshRenderer;
                int vertices = skin != null && skin.sharedMesh != null ? skin.sharedMesh.vertexCount : 0;
                if (vertices > mostVertices) { mostVertices = vertices; body = renderer; }
            }
            if (found) { sample.boundsMinY = bounds.min.y; sample.boundsHeight = bounds.size.y; }
            if (body != null) { sample.bodyMinY = body.bounds.min.y; sample.bodyHeight = body.bounds.size.y; }
            var cc = go.GetComponent<CharacterController>();
            if (cc != null)
            {
                sample.ccHeight = cc.height; sample.ccCenterY = cc.center.y; sample.ccSkin = cc.skinWidth; sample.grounded = cc.isGrounded;
                sample.capsuleBottomY = cc.transform.TransformPoint(cc.center - Vector3.up * cc.height * .5f).y;
            }
            var colliders = go.GetComponentsInChildren<Collider>(); sample.colliderCount = colliders.Length;
            foreach (var collider in colliders) if (collider.enabled && !collider.isTrigger) sample.dynamicPhysics = true;
            var bodies = go.GetComponentsInChildren<Rigidbody>(); sample.rigidbodyCount = bodies.Length;
            foreach (var bodyRb in bodies) if (!bodyRb.isKinematic) sample.dynamicPhysics = true;
            var transforms = go.GetComponentsInChildren<NetworkTransform>(); sample.networkTransformCount = transforms.Length;
            foreach (var nt in transforms) if (nt.SyncScaleX || nt.SyncScaleY || nt.SyncScaleZ) sample.scaleSync = true;
            float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(go.transform.position + Vector3.up * .25f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<NetworkAvatar>() != null || hit.collider.GetComponentInParent<NisitSimulator.Player.PlayerMovement>() != null) continue;
                if (hit.normal.y < .5f || hit.distance >= nearest) continue;
                nearest = hit.distance; sample.hasGround = true; sample.groundY = hit.point.y; sample.groundName = hit.collider.name;
            }
            sample.groundGap = sample.boundsMinY - sample.groundY; sample.bodyGap = sample.bodyMinY - sample.groundY;
            return sample;
        }

        static void Collect(Report report)
        {
            var player = GameObject.Find("Player");
            var manager = NetworkManager.Singleton;
            if (player != null) report.samples.Add(Measure(player, "Player", manager != null ? manager.LocalClientId : 0, GameSession.PlayerModel));
            foreach (var avatar in FindObjectsByType<NetworkAvatar>())
            {
                int model = 0;
                // The baseline tool must work before and after the network schema changes.
                var field = typeof(NetworkAvatar).GetField("netModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field?.GetValue(avatar) is NetworkVariable<int> value) model = value.Value;
                report.samples.Add(Measure(avatar.gameObject, avatar.IsOwner ? "OwnerPuppetHidden" : "Remote", avatar.OwnerClientId, model));
            }
        }
        static string Save(Report report)
        {
            var agent = MPTestAgent.Instance;
            if (agent == null) throw new InvalidOperationException("ใช้กับ MPTestAgent เพื่อแยกโปรไฟล์ทดสอบ");
            if (report.label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || report.label.Contains("..")) throw new ArgumentException("invalid label");
            string path = Path.Combine(agent.Dir, agent.Id + "_geometry_" + report.label + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            agent.Log("geometry " + path + " samples=" + report.samples.Count);
            return path;
        }
        public static string Dump(string label)
        {
            var agent = MPTestAgent.Instance; if (agent == null) throw new InvalidOperationException("MPTestAgent required");
            var probe = agent.GetComponent<AvatarGeometryProbe>() ?? agent.gameObject.AddComponent<AvatarGeometryProbe>();
            probe.pendingDumps.Enqueue(label);
            return "scheduled after render geometry: " + label;
        }
        public static string Trace(string label, float seconds)
        {
            var agent = MPTestAgent.Instance;
            if (agent == null) throw new InvalidOperationException("MPTestAgent required");
            var probe = agent.GetComponent<AvatarGeometryProbe>() ?? agent.gameObject.AddComponent<AvatarGeometryProbe>();
            probe.StartCoroutine(probe.Record(label, Mathf.Clamp(seconds, .1f, 30f)));
            return "recording " + label;
        }
        public static string PlaceAt(string building, float x, float y, float z)
        {
            if (MPTestAgent.Instance == null) throw new InvalidOperationException("MPTestAgent required");
            building = building.Replace(' ', '_'); // MPTestAgent decodes underscores in string arguments.
            var anchor = GameObject.Find(building); var player = GameObject.Find("Player");
            if (anchor == null || player == null) return "building/player missing: " + building;
            var point = anchor.transform.TransformPoint(new Vector3(x, y, z));
            var position = PlayerSpawnSystem.GroundSnap(player, point);
            NisitSimulator.Interaction.InteriorManager.Teleport(player.transform, position, Quaternion.identity);
            return "placed " + building + " at " + position;
        }
        public static string Stairs(string building, bool running)
        {
            var agent = MPTestAgent.Instance; if (agent == null) throw new InvalidOperationException("MPTestAgent required");
            building = building.Replace(' ', '_');
            var anchor = GameObject.Find(building); if (anchor == null) return "building missing: " + building;
            bool dorm = building == "Dorm_Building";
            var points = new List<Vector3>();
            float baseY = dorm ? .3f : .45f, height = dorm ? 3.2f : 3.6f;
            float xa = dorm ? -2.75f : -4.7f, xb = dorm ? -.85f : -1.3f;
            // Cross between flights in the corridor, clear of the floor-gap railing.
            float bottom = dorm ? 9.4f : 8.2f, top = dorm ? 13.3f : 13f;
            points.Add(new Vector3(xa, baseY, bottom));
            for (int floor = 0; floor < 2; floor++)
            {
                float y = baseY + floor * height;
                points.Add(new Vector3(xa, y + height / 2, top));
                points.Add(new Vector3(xb, y + height / 2, top));
                points.Add(new Vector3(xb, y + height, bottom));
                if (floor == 0) points.Add(new Vector3(xa, y + height, bottom));
            }
            for (int i = points.Count - 2; i >= 0; i--) points.Add(points[i]);
            var probe = agent.GetComponent<AvatarGeometryProbe>() ?? agent.gameObject.AddComponent<AvatarGeometryProbe>();
            probe.StartCoroutine(probe.FollowStairs(anchor.transform, points, running, building));
            return "walking real stairs 1-3-1: " + building;
        }
        IEnumerator FollowStairs(Transform anchor, List<Vector3> points, bool running, string building)
        {
            PlaceAt(building, points[0].x, points[0].y, points[0].z);
            yield return null;
            var player = GameObject.Find("Player"); var cc = player.GetComponent<CharacterController>();
            var movement = player.GetComponent<NisitSimulator.Player.PlayerMovement>();
            var animator = player.GetComponentInChildren<Animator>();
            var report = new Report { label = building + (running ? "-run" : "-walk") };
            activeReports.Add(report);
            for (int step = 1; step < points.Count; step++)
            {
                var target = anchor.TransformPoint(points[step]); float end = Time.realtimeSinceStartup + 12;
                while (Time.realtimeSinceStartup < end)
                {
                    Vector3 delta = target - player.transform.position; delta.y = 0;
                    if (delta.magnitude < .12f) break;
                    var direction = delta.normalized;
                    cc.Move(direction * Mathf.Min(delta.magnitude, (running ? movement.runSpeed : movement.walkSpeed) * Time.deltaTime) + Vector3.down * (2f * Time.deltaTime));
                    player.transform.rotation = Quaternion.LookRotation(direction);
                    if (animator != null) animator.SetFloat("Speed", running ? 1 : .5f);
                    yield return null;
                }
                float feetY = AvatarGeometryLocalFeet(cc).y;
                MPTestAgent.Instance.Log("stairs " + building + " point=" + step + " feet=" + feetY + " expected=" + target.y);
                Vector3 remaining = target - player.transform.position; remaining.y = 0;
                if (remaining.magnitude >= .12f || Mathf.Abs(feetY - target.y) > .6f)
                { MPTestAgent.Instance.Log("stairs route blocked at " + player.transform.position); break; }
            }
            if (animator != null) animator.SetFloat("Speed", 0);
            activeReports.Remove(report);
            Save(report);
        }
        static Vector3 AvatarGeometryLocalFeet(CharacterController cc) => cc.transform.TransformPoint(cc.center - Vector3.up * cc.height * .5f);
        public static string Jump()
        {
            var player = GameObject.Find("Player"); var movement = player != null ? player.GetComponent<NisitSimulator.Player.PlayerMovement>() : null;
            if (MPTestAgent.Instance == null || movement == null || !player.GetComponent<CharacterController>().isGrounded) return "not grounded";
            var field = typeof(NisitSimulator.Player.PlayerMovement).GetField("velocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var velocity = (Vector3)field.GetValue(movement); velocity.y = Mathf.Sqrt(movement.jumpHeight * -2 * movement.gravity); field.SetValue(movement, velocity);
            return "jump velocity applied through PlayerMovement";
        }
        IEnumerator Record(string label, float seconds)
        {
            var report = new Report { label = label }; activeReports.Add(report);
            yield return new WaitForSecondsRealtime(seconds);
            activeReports.Remove(report);
            Save(report);
        }
        void LateUpdate()
        {
            // NetworkAvatar applies its visual ground correction in LateUpdate. Measuring in Update
            // would report the intermediate interpolation position instead of the rendered result.
            foreach (var report in activeReports) Collect(report);
            while (pendingDumps.Count > 0)
            {
                var report = new Report { label = pendingDumps.Dequeue() }; Collect(report); Save(report);
            }
        }
    }
}
#endif
