#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NisitSimulator.GEBuilding;
using NisitSimulator.Net;

namespace NisitSimulator.DevTools
{
    public partial class MPTestAgent
    {
        int guardFrames, guardViolations;
        [Serializable] public class DoorInfo
        {
            public int id; public string path; public bool open, authoritativeOpen, startOpen;
            public float angle, targetAngle; public bool triggerEnabled, triggerIsTrigger, solidLeaf;
            public string layer; public Vector3 position;
        }
        List<GEDoor> TestDoors() => FindObjectsByType<GEDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(d => DoorSyncManager.Key(d) != null).OrderBy(d => DoorSyncManager.Key(d), StringComparer.Ordinal).ToList();
        GEDoor TestDoor(int id)
        {
            var sync = DoorSyncManager.Instance;
            if (sync != null && sync.DoorCount > 0) return sync.Door(id);
            var all = TestDoors(); return id >= 0 && id < all.Count ? all[id] : null;
        }
        void FillDoorState(State state)
        {
            var sync = DoorSyncManager.Instance;
            state.doorsReady = sync != null && sync.Ready;
            if (sync != null)
            {
                state.doorRevision = sync.Revision; state.doorCatalog = sync.Catalog.ToString();
                state.doorAccepted = sync.Accepted; state.doorRejected = sync.Rejected; state.doorLastDecision = sync.LastDecision;
            }
            int index = 0;
            foreach (var door in TestDoors())
            {
                var trigger = door.GetComponent<BoxCollider>();
                bool solid = door.hinge != null && door.hinge.GetComponentsInChildren<Collider>(true)
                    .Any(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger);
                float angle = door.hinge == null ? -1 : Quaternion.Angle(Quaternion.identity, door.hinge.localRotation);
                var info = new DoorInfo { id = index++, path = DoorSyncManager.Key(door), open = door.IsOpen,
                    authoritativeOpen = sync != null && sync.Ready ? sync.State(sync.IdFor(door)) : door.IsOpen,
                    startOpen = door.startOpen, angle = angle, targetAngle = door.IsOpen ? Mathf.Abs(door.openAngle) : 0,
                    triggerEnabled = trigger != null && trigger.enabled, triggerIsTrigger = trigger != null && trigger.isTrigger,
                    solidLeaf = solid, layer = LayerMask.LayerToName(door.gameObject.layer), position = door.transform.position };
                state.doors.Add(info);
                if (!info.open && angle >= 0 && angle < .5f && solid) state.doorsClosed++;
            }
            state.doorCount = state.doors.Count;
        }
        Vector3 DoorFoot(GEDoor door)
        {
            var trigger = door.GetComponent<BoxCollider>();
            Vector3 point = trigger != null ? door.transform.TransformPoint(trigger.center) : door.transform.position;
            point -= door.transform.forward * .55f;
            float y = door.transform.position.y;
            var hits = Physics.RaycastAll(new Vector3(point.x, y + 1.6f, point.z), Vector3.down, 3,
                ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
            foreach (var hit in hits)
                if (hit.normal.y > .9f && hit.point.y >= y - .5f && hit.point.y <= y + .6f)
                { point.y = hit.point.y + .04f; return point; }
            point.y = y + .04f; return point;
        }
        string DoorApproach(int id)
        {
            var door = TestDoor(id); if (door == null) return "invalid door";
            string result = Teleport(DoorFoot(door));
            if (Player != null) Player.transform.rotation = Quaternion.LookRotation(door.transform.forward, Vector3.up);
            return DoorSyncManager.Key(door) + " " + result;
        }
        string DoorInteract(int id)
        {
            var door = TestDoor(id); if (door == null || Player == null) return "no door/player";
            door.Interact(Player); return "original Interact " + DoorSyncManager.Key(door);
        }
        string DoorView()
        {
            var front = TestDoors().Where(d => d.name.StartsWith("Door_Main_")).ToList();
            var camera = Camera.main;
            if (front.Count != 2 || camera == null) return "front pair/camera missing";
            var rig = camera.GetComponent<NisitSimulator.CameraRig.IsometricCameraRig>();
            if (rig != null) rig.enabled = false;
            camera.orthographic = false; camera.fieldOfView = 50;
            Vector3 center = (front[0].transform.position + front[1].transform.position) * .5f +
                front[0].transform.right * .45f + Vector3.up * .9f;
            camera.transform.position = center - front[0].transform.forward * 4 + Vector3.up * 1;
            camera.transform.LookAt(center);
            return "front pair framed";
        }
        IEnumerator DoorAll(bool open, string building)
        {
            var all = TestDoors(); int done = 0;
            for (int id = 0; id < all.Count; id++)
            {
                var door = all[id]; if (!DoorSyncManager.Key(door).StartsWith(building + "/", StringComparison.Ordinal)) continue;
                if (door.IsOpen != open)
                {
                    DoorApproach(id); yield return new WaitForSecondsRealtime(.5f);
                    door.Interact(Player); yield return new WaitForSecondsRealtime(.5f);
                }
                bool passed = door.IsOpen == open && door.hinge != null &&
                    Quaternion.Angle(door.hinge.localRotation, Quaternion.Euler(0, open ? door.openAngle : 0, 0)) < .5f;
                if (passed) done++;
                Log($"DOOR all id={id} desired={open} measured={door.IsOpen} angle={door.hinge.localEulerAngles.y} passed={passed}");
            }
            Log($"DOOR all complete building={building} desired={open} passed={done}");
            WriteState();
        }
        IEnumerator DoorCollision(int id)
        {
            var door = TestDoor(id); var player = Player; if (door == null || player == null) yield break;
            var controller = player.GetComponent<CharacterController>(); if (controller == null) yield break;
            DoorApproach(id); yield return new WaitForSecondsRealtime(.5f);
            if (door.IsOpen) { door.Interact(player); yield return new WaitForSecondsRealtime(.5f); }
            var interaction = player.GetComponent<NisitSimulator.Player.PlayerInteraction>();
            var detect = typeof(NisitSimulator.Player.PlayerInteraction).GetMethod("DetectNearest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (interaction != null) detect.Invoke(interaction, null);
            var current = typeof(NisitSimulator.Player.PlayerInteraction).GetField("current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool detected = interaction != null && ReferenceEquals(current.GetValue(interaction), door);
            Vector3 center = door.transform.TransformPoint(door.GetComponent<BoxCollider>().center);
            Vector3 axis = door.transform.forward;
            for (int pass = 0; pass < 2; pass++)
            {
                DoorApproach(id); yield return new WaitForSecondsRealtime(.5f);
                if (pass == 1) { door.Interact(player); yield return new WaitForSecondsRealtime(.5f); }
                for (int step = 0; step < 70; step++)
                { controller.Move(axis * .04f + Vector3.down * .02f); yield return new WaitForFixedUpdate(); }
                float side = Vector3.Dot(player.transform.position - center, axis);
                bool passed = pass == 0 ? !door.IsOpen && side < -.05f : door.IsOpen && side > .1f;
                Log($"DOOR collision id={id} key={DoorSyncManager.Key(door)} desiredOpen={pass == 1} measuredOpen={door.IsOpen} side={side:F4} detected={detected} grounded={controller.isGrounded} passed={passed}");
            }
            DoorApproach(id); yield return new WaitForSecondsRealtime(.5f);
            if (door.IsOpen) { door.Interact(player); yield return new WaitForSecondsRealtime(.5f); }
            WriteState();
        }
        IEnumerator DoorCycle(string building)
        {
            var all = TestDoors();
            for (int id = 0; id < all.Count; id++)
                if (DoorSyncManager.Key(all[id]).StartsWith(building + "/", StringComparison.Ordinal))
                    yield return DoorCollision(id);
            Log("DOOR cycle complete building=" + building);
        }
        string DoorBurst(int id, int presses)
        {
            var door = TestDoor(id); if (door == null) return "invalid door";
            for (int i = 0; i < presses; i++) door.Interact(Player);
            return "original Interact burst=" + presses;
        }
        IEnumerator DoorAt(int id, long epochMilliseconds)
        {
            while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < epochMilliseconds) yield return null;
            Log("DOOR scheduled id=" + id + " " + DoorInteract(id) + " utc=" + DateTime.UtcNow.ToString("O"));
        }
        IEnumerator DoorProbeAll(string building)
        {
            var all = TestDoors(); int done = 0, passedCount = 0;
            for (int id = 0; id < all.Count; id++)
            {
                var door = all[id];
                if (!DoorSyncManager.Key(door).StartsWith(building + "/", StringComparison.Ordinal)) continue;
                DoorApproach(id); yield return new WaitForSecondsRealtime(.15f);
                var player = Player; var controller = player == null ? null : player.GetComponent<CharacterController>();
                if (controller == null) { Log("DOOR probe missing CharacterController id=" + id); continue; }
                var interaction = player.GetComponent<NisitSimulator.Player.PlayerInteraction>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var detect = typeof(NisitSimulator.Player.PlayerInteraction).GetMethod("DetectNearest", flags);
                if (interaction != null) detect.Invoke(interaction, null);
                var current = typeof(NisitSimulator.Player.PlayerInteraction).GetField("current", flags);
                bool detected = interaction != null && ReferenceEquals(current.GetValue(interaction), door);
                Vector3 center = door.transform.TransformPoint(door.GetComponent<BoxCollider>().center), axis = door.transform.forward;
                bool expectedOpen = door.IsOpen; float side = Vector3.Dot(player.transform.position - center, axis);
                for (int step = 0; step < 40; step++)
                {
                    controller.Move(axis * .04f + Vector3.down * .02f);
                    yield return new WaitForFixedUpdate();
                    side = Vector3.Dot(player.transform.position - center, axis);
                    if (expectedOpen && side > .2f) break;
                }
                bool passed = door.IsOpen == expectedOpen && (expectedOpen ? side > .1f : side < -.05f);
                done++; if (passed) passedCount++;
                Log($"DOOR probe id={id} key={DoorSyncManager.Key(door)} expectedOpen={expectedOpen} measuredOpen={door.IsOpen} side={side:F4} detected={detected} grounded={controller.isGrounded} passed={passed}");
            }
            Log($"DOOR probe complete building={building} passed={passedCount}/{done}"); WriteState();
        }
    }
}
#endif
