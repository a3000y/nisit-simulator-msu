#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.Interaction;
using NisitSimulator.SaveLoad;
using NisitSimulator.GEBuilding;

namespace NisitSimulator.DevTools
{
    public partial class MPTestAgent
    {
        [Serializable] class RoomTestInfo
        {
            public int slot, cutFloor, hiddenWalls, stationCount;
            public bool roomVisibilityEnabled;
            public Vector3 playerViewport, cameraPosition, visualCenter, visualViewport;
            public string cameraTarget;
            public string roomId, bed, spawn, wake, prompt;
            public Vector3 spawnPos, wakePos, roomLocalPlayer;
            public bool insideInterior, interiorLighting, grounded, capsuleClear;
            public float wakeDistance, capsuleRadius;
        }
        void FillRoomState(State state)
        {
            var dorm = DormSpawnPoint.Main; var pl = Player;
            if (dorm == null || pl == null) return;
            var slot = dorm.RoomSlot(PlayerSpawnSystem.AssignedSlotIndex); if (slot == null) return;
            var cc = pl.GetComponent<CharacterController>(); var scale = pl.transform.lossyScale;
            float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float half = Mathf.Max(0f, cc.height * Mathf.Abs(scale.y) * 0.5f - radius);
            var center = pl.transform.position + Vector3.Scale(cc.center, scale);
            bool clear = true;
            foreach (var h in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half,
                radius * 0.98f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(pl.transform) && h.bounds.max.y > cc.bounds.min.y + 0.08f) clear = false;
            var cut = dorm.GetComponentInParent<GEBuildingCutaway>();
            var hud = NisitSimulator.UI.HUDController.Instance;
            var visual = pl.GetComponentInChildren<SkinnedMeshRenderer>();
            state.room = new RoomTestInfo {
                slot = PlayerSpawnSystem.AssignedSlotIndex, roomId = PlayerSpawnSystem.AssignedRoomId,
                bed = slot.room.name + "/" + slot.bed.name, spawn = slot.spawnPoint.name, wake = slot.wakePoint.name,
                spawnPos = slot.spawnPoint.position, wakePos = slot.wakePoint.position,
                wakeDistance = Vector2.Distance(new Vector2(pl.transform.position.x, pl.transform.position.z), new Vector2(slot.wakePoint.position.x, slot.wakePoint.position.z)),
                roomLocalPlayer = slot.room.InverseTransformPoint(pl.transform.position),
                insideInterior = InteriorManager.Instance != null && InteriorManager.Instance.IsInside,
                interiorLighting = NisitSimulator.TimeSystem.DayNightCycle.Instance != null && NisitSimulator.TimeSystem.DayNightCycle.Instance.IndoorLighting,
                grounded = cc.isGrounded, capsuleRadius = radius, capsuleClear = clear,
                cutFloor = cut != null ? cut.CurrentCutFloor : -999,
                hiddenWalls = cut != null ? cut.HiddenRoomWallCount : 0,
                roomVisibilityEnabled = cut != null && cut.hideAssignedRoomObstructions,
                stationCount = dorm.GetComponentInParent<GEBuildingCutaway>().GetComponentsInChildren<SleepStation>().Length,
                playerViewport = Camera.main != null ? Camera.main.WorldToViewportPoint(pl.transform.position) : Vector3.zero,
                cameraPosition = Camera.main != null ? Camera.main.transform.position : Vector3.zero,
                cameraTarget = Camera.main != null && Camera.main.GetComponent<NisitSimulator.CameraRig.IsometricCameraRig>() != null ? Camera.main.GetComponent<NisitSimulator.CameraRig.IsometricCameraRig>().target.name : "none",
                visualCenter = visual != null ? visual.bounds.center : Vector3.zero,
                visualViewport = visual != null && Camera.main != null ? Camera.main.WorldToViewportPoint(visual.bounds.center) : Vector3.zero,
                prompt = hud != null && hud.promptText != null ? hud.promptText.text : ""
            };
        }
        string OwnBedInteract(bool interact)
        {
            var station = DormSpawnPoint.Main?.StationFor(PlayerSpawnSystem.AssignedSlotIndex);
            var pi = Player != null ? Player.GetComponent<NisitSimulator.Player.PlayerInteraction>() : null;
            if (station == null || pi == null) return "no assigned station/player";
            typeof(NisitSimulator.Player.PlayerInteraction).GetMethod("DetectNearest", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pi, null);
            var detected = typeof(NisitSimulator.Player.PlayerInteraction).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pi);
            bool own = ReferenceEquals(detected, station);
            Log("bed detection own=" + own + " prompt=" + station.GetPrompt());
            if (own && interact) station.Interact(Player);
            return "own=" + own + " state=" + SleepController.Instance.State;
        }
        string TestRoomContinue(string kind)
        {
            if (!MPTestProfile.Enabled || !SaveSystem.DevGuard || SaveSystem.DevPathOverride != MPTestProfile.SavePath) return "unsafe profile blocked";
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening) return "SP only";
            SaveManager.Save(); var data = SaveSystem.Load();
            if (kind == "legacy")
            {
                data.hasPlayerTransform = true; data.insideInterior = true; data.interiorName = "Spawn_หอพัก";
                data.posX = 1316.8f; data.posY = 0.9f; data.posZ = -3.564f; data.dormRoomId = "dorm_1";
                SaveSystem.Save(data);
            }
            else if (kind == "invalid") { data.posX = 99999f; SaveSystem.Save(data); }
            else if (kind == "corrupt") File.WriteAllText(SaveSystem.DevPathOverride, "{ invalid json");
            GameSession.PendingLoad = true; GameSession.IsContinue = true;
            SceneManager.LoadScene(GameSession.GameplayScene);
            return "guarded continue " + kind;
        }
        IEnumerator RoomWalk(bool back)
        {
            var slot = DormSpawnPoint.Main.RoomSlot(PlayerSpawnSystem.AssignedSlotIndex);
            var room = slot.room; var building = DormSpawnPoint.Main.GetComponentInParent<GEBuildingCutaway>().transform;
            var cc = Player.GetComponent<CharacterController>();
            var points = new List<Vector3> {
                room.TransformPoint(new Vector3(-0.6f,0,3.7f)), room.TransformPoint(new Vector3(-0.6f,0,2.6f)),
                room.TransformPoint(new Vector3(-0.9f,0,1.3f)), room.TransformPoint(new Vector3(-0.9f,0,0.65f)),
                room.TransformPoint(new Vector3(-0.9f,0,-0.65f)), building.TransformPoint(new Vector3(-0.6f,0.3f,7.25f)),
                building.TransformPoint(new Vector3(-0.6f,0.3f,3f)), building.TransformPoint(new Vector3(-0.6f,0.3f,1.25f)),
                building.TransformPoint(new Vector3(-0.6f,0.3f,-0.8f)), building.TransformPoint(new Vector3(-0.6f,0,-3f))
            };
            if (back) { points.Reverse(); points.RemoveAt(0); points.Add(slot.spawnPoint.position); }
            foreach (var target in points)
            {
                // Interact with the actual door only while in range; use the server-authoritative flow in MP.
                foreach (var door in building.GetComponentsInChildren<GEDoor>())
                    if (!door.IsOpen && Vector3.Distance(Player.transform.position, door.transform.position) < 1.5f &&
                        (door.name == "Door_Main_Left" || door.transform.parent == room)) door.Interact(Player);
                yield return new WaitForSecondsRealtime(0.6f);
                float elapsed = 0f;
                while (elapsed < 12f)
                {
                    var delta = target - Player.transform.position; delta.y = 0f;
                    if (delta.magnitude < 0.12f) break;
                    var step = Vector3.ClampMagnitude(delta, 2.5f * Time.deltaTime);
                    cc.Move(step + Vector3.down * 2f * Time.deltaTime);
                    if (delta.sqrMagnitude > 0.001f) Player.transform.rotation = Quaternion.LookRotation(delta);
                    elapsed += Time.unscaledDeltaTime; yield return null;
                }
                var horizontal = target - Player.transform.position; horizontal.y = 0;
                Log("room walk waypoint back=" + back + " target=" + target + " remaining=" + horizontal.magnitude.ToString("F3") + " pos=" + Player.transform.position);
                if (horizontal.magnitude > 0.2f) { Log("room walk FAILED"); yield break; }
            }
            Log("room walk COMPLETE back=" + back);
        }
    }
}
#endif
