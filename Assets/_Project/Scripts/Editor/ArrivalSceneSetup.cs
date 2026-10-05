#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Linq;
using NisitSimulator.Academics;
using NisitSimulator.Interaction;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.EditorTools
{
    public static class ArrivalSceneSetup
    {
        [MenuItem("Nisit/Arrival/Setup Arrival and Calendar V3")]
        public static void Apply()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().name != "01_Gameplay") throw new System.InvalidOperationException("Open 01_Gameplay in Edit Mode first.");
            if (!System.IO.File.Exists("Assets/_Project/Scenes/Backups/01_Gameplay_before_arrival_weekdays_20261004.unity")) throw new System.InvalidOperationException("Back up the gameplay scene before setup.");
            var curriculum = AssetDatabase.LoadAssetAtPath<CurriculumDefinition>("Assets/_Project/Resources/Curricula/CS_Curriculum.asset");
            if (curriculum != null)
            {
                Undo.RecordObject(curriculum, "Calendar V3");
                bool old = curriculum.courses.Any(c => c.sessions.Any(s => s.day == 1));
                if (old) foreach (var c in curriculum.courses)
                {
                    foreach (var s in c.sessions) s.day = CalendarMigration.MapV2TermDay(s.day);
                    foreach (var s in c.retakeSessions) s.day = CalendarMigration.MapV2TermDay(s.day);
                }
                curriculum.registrationDays = 1; curriculum.autoConfirmAtDeadline = false;
                EditorUtility.SetDirty(curriculum); AssetDatabase.SaveAssets();
            }
            if (Object.FindAnyObjectByType<ArrivalIntroController>() != null) return;
            var source = GameObject.Find("TalkNPCs/NPC_นิสิตปี 3");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonCity/Prefabs/Vehicles/SM_Veh_Car_Taxi_01.prefab");
            if (source == null || prefab == null) throw new System.InvalidOperationException("Existing senior model or taxi is missing.");
            var root = new GameObject("ArrivalIntro"); Undo.RegisterCreatedObjectUndo(root, "Add arrival sequence");
            var intro = root.AddComponent<ArrivalIntroController>();
            intro.driveStart = Marker(root, "CarStart", new Vector3(-35, 0, 119));
            intro.driveStop = Marker(root, "CarStop", new Vector3(-58, 0, 119));
            intro.driveEnd = Marker(root, "CarEnd", new Vector3(-86, 0, 119));
            intro.arrivalPoint = Marker(root, "ArrivalOutsideDorm", new Vector3(-58, .1f, 123));
            intro.seniorHome = Marker(root, "SeniorHome", new Vector3(-60, .1f, 123));
            intro.stops = new[] {
                Marker(root, "Tour_Dorm_Building", new Vector3(-58, .1f, 125)),
                Marker(root, "Tour_Central_Canteen", new Vector3(-96, .1f, 103)),
                Marker(root, "Tour_ร้านค้า", new Vector3(-72.02f, .1f, 111.05f)),
                Marker(root, "Tour_IT_Building", new Vector3(23.15f, .1f, -48)),
                Marker(root, "Tour_GE_Building", new Vector3(30, .1f, 33)),
                Marker(root, "Tour_ห้องสมุด", new Vector3(33.3f, .1f, -6)),
                Marker(root, "Tour_อาคารชมรม", new Vector3(-78, .1f, 124.84f))
            };
            intro.vehicle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            intro.vehicle.name = "ArrivalTaxi"; intro.vehicle.SetActive(false);
            // Duplicate the existing model and its configured animator, leave the source intact.
            var senior = Object.Instantiate(source, root.transform);
            senior.name = "ArrivalSenior"; senior.transform.position = intro.seniorHome.position;
            intro.senior = senior.GetComponent<TalkNPC>();
            intro.senior.npcName = "พี่ต้นกล้า ปี 3"; intro.senior.stableRelationshipId = "arrival_senior_tonkla";
            intro.senior.isQuestGiver = false; intro.senior.isVendor = false;
            var walker = senior.GetComponent<NisitSimulator.UI.MenuNPCWalker>(); if (walker != null) walker.enabled = false;
            senior.AddComponent<SeniorAdvisor>();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Arrival] Added taxi, senior and seven tour stops. Calendar V3 is ready.");
        }
        static Transform Marker(GameObject parent, string name, Vector3 pos)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform); go.transform.position = pos; return go.transform;
        }
    }
}
#endif
