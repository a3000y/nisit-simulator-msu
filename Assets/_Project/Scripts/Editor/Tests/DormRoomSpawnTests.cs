#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Tests
{
    public class DormRoomSpawnTests
    {
        const string Prefab = "Assets/_Project/Prefabs/Dorm/Dorm_Building.prefab";
        GameObject root;
        DormSpawnPoint dorm;
        [SetUp] public void SetUp() { root = PrefabUtility.LoadPrefabContents(Prefab); dorm = root.GetComponentInChildren<DormSpawnPoint>(); }
        [TearDown] public void TearDown() { if (root != null) PrefabUtility.UnloadPrefabContents(root); }

        [Test] public void SpawnPointsAreInsideTheirAssignedRoom()
        {
            Assert.AreEqual(4, dorm.SlotCount);
            foreach (var slot in dorm.roomSlots)
            {
                Assert.IsTrue(slot.spawnPoint.IsChildOf(slot.room));
                var local = slot.room.InverseTransformPoint(slot.spawnPoint.position);
                Assert.That(local.x, Is.InRange(-1.8f, 1.8f));
                Assert.That(local.z, Is.InRange(2.35f, 6.5f));
                Assert.AreEqual(0f, local.y, 0.025f);
                Assert.Less(Vector3.Dot(slot.spawnPoint.forward, slot.room.forward), -0.99f);
            }
        }
        [Test] public void SpawnAndWakePositionsAreDistinctAndHaveCapsuleSpacing()
        {
            var spawns = new HashSet<Transform>(); var wakes = new HashSet<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var slot = dorm.roomSlots[i];
                Assert.IsTrue(spawns.Add(slot.spawnPoint)); Assert.IsTrue(wakes.Add(slot.wakePoint));
                for (int j = 0; j < i; j++)
                {
                    // Prefab units: .5 radius * .66 player scale, converted through .75 building scale.
                    Assert.Greater(Vector3.Distance(slot.spawnPoint.position, dorm.roomSlots[j].spawnPoint.position), 0.88f);
                    Assert.Greater(Vector3.Distance(slot.wakePoint.position, dorm.roomSlots[j].wakePoint.position), 0.88f);
                }
            }
        }
        [Test] public void EachAssignedBedHasTriggerStationAndItsOwnWake()
        {
            Assert.IsFalse(dorm.usesWarpInterior);
            var beds = new HashSet<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var slot = dorm.roomSlots[i]; Assert.IsTrue(beds.Add(slot.bed));
                Assert.AreEqual(i, slot.station.assignedDormSlot); Assert.IsTrue(slot.station.useBedWakePoint);
                Assert.IsTrue(slot.station.transform.IsChildOf(slot.bed));
                Assert.AreSame(slot.wakePoint, slot.station.wakePoint);
                Assert.IsTrue(slot.station.GetComponent<Collider>().isTrigger);
                Assert.AreEqual(LayerMask.NameToLayer("Interactable"), slot.station.gameObject.layer);
                Assert.IsFalse(slot.bed.GetComponent<Collider>().isTrigger);
                Assert.AreSame(slot.spawnPoint, dorm.GetSlot(i));
            }
        }
        [Test] public void BuildingMoveCarriesSpawnAndWakePoints()
        {
            root.transform.position = new Vector3(45f, 0f, 78f); root.transform.localScale = Vector3.one * 0.75f;
            foreach (var slot in dorm.roomSlots)
            {
                Assert.Less(Vector3.Distance(slot.room.TransformPoint(slot.roomLocalPoint), slot.spawnPoint.position), 0.03f);
                Assert.Less(Vector3.Distance(slot.spawnPoint.position, slot.wakePoint.position), 0.01f);
            }
        }
        [Test] public void RebuildSettingsCopyRetainsConfigurationWithoutDestroyedReferences()
        {
            var settings = dorm.CopyRoomSettings();
            Assert.AreEqual(4, settings.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(dorm.roomSlots[i].roomPath, settings[i].roomPath);
                Assert.AreEqual(dorm.roomSlots[i].roomLocalPoint, settings[i].roomLocalPoint);
                Assert.IsNull(settings[i].room); Assert.IsNull(settings[i].spawnPoint);
            }
        }
        [Test] public void SavedGameplaySceneHasExactlyFourStationsAfterPrefabRefresh()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_Gameplay.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                DormSpawnPoint sceneDorm = null;
                foreach (var sceneRoot in scene.GetRootGameObjects())
                    foreach (var candidate in sceneRoot.GetComponentsInChildren<DormSpawnPoint>(true)) sceneDorm = candidate;
                Assert.IsNotNull(sceneDorm);
                var building = sceneDorm.GetComponentInParent<NisitSimulator.GEBuilding.GEBuildingCutaway>();
                Assert.AreEqual(4, building.GetComponentsInChildren<SleepStation>(true).Length);
                Assert.IsTrue(building.hideAssignedRoomObstructions);
                foreach (var slot in sceneDorm.roomSlots)
                {
                    Assert.IsNotNull(slot.station);
                    Assert.AreEqual(1, slot.bed.GetComponentsInChildren<SleepStation>(true).Length);
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }
        [TestCase("{")]
        [TestCase("{\"hasPlayerTransform\":")]
        public void CorruptTestProfileReturnsNullWithoutDeletingItsFile(string invalidJson)
        {
            string file = System.IO.Path.GetTempFileName();
            var guard = SaveSystem.DevGuard; var path = SaveSystem.DevPathOverride; var read = SaveSystem.DevReadOverride;
            try
            {
                SaveSystem.DevGuard = true; SaveSystem.DevPathOverride = file; SaveSystem.DevReadOverride = null;
                System.IO.File.WriteAllText(file, invalidJson);
                Assert.IsNull(SaveSystem.Load());
                Assert.AreEqual(invalidJson, System.IO.File.ReadAllText(file));
                Assert.AreEqual("unreadable_save", SaveSystem.LastLoadIssue);
            }
            finally
            {
                SaveSystem.DevGuard = guard; SaveSystem.DevPathOverride = path; SaveSystem.DevReadOverride = read;
                System.IO.File.Delete(file);
            }
        }
        [TestCase("Spawn_หอพัก", true, true)]
        [TestCase("หอพัก", true, true)]
        [TestCase("Spawn_ห้องสมุด", true, false)]
        [TestCase("", false, false)]
        public void OnlyOldWarpDormSavesMigrate(string name, bool inside, bool expected)
        {
            var save = new SaveData { hasPlayerTransform = true, insideInterior = inside, interiorName = name,
                posX = 45f, posY = 1f, posZ = 78f };
            Assert.AreEqual(expected, PlayerSpawnSystem.IsLegacyDormSave(save));
        }
    }
}
#endif
