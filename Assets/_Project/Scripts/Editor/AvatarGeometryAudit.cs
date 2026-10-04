#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.Systems;
using NisitSimulator.DevTools;

namespace NisitSimulator.EditorTools
{
    public static class AvatarGeometryAudit
    {
        [MenuItem("Nisit/Diagnose Avatar Geometry")]
        public static void Run()
        {
            string output = Path.Combine(Application.dataPath, "../Temp/AvatarGeometryAudit.json");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/01_Gameplay.unity");
            var testing = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject original = null;
                foreach (var root in scene.GetRootGameObjects()) if (root.name == "Player") { original = root; break; }
                if (original == null) throw new System.InvalidOperationException("Player missing");
                var puppet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkAvatar.prefab");
                var catalog = CharacterCatalog.Load();
                var report = new AvatarGeometryProbe.Report { label = "catalog-audit" };
                for (int index = 0; index < catalog.Count; index++)
                {
                    var player = Object.Instantiate(original); SceneManager.MoveGameObjectToScene(player, testing);
                    var remote = Object.Instantiate(puppet); SceneManager.MoveGameObjectToScene(remote, testing);
                    var playerAnim = CharacterCatalog.Apply(player.transform, index);
                    var remoteAnim = CharacterCatalog.ApplyPuppet(remote.transform, index, playerAnim.transform.lossyScale);
                    remote.transform.position = NisitSimulator.Net.AvatarGeometry.Feet(player.GetComponent<CharacterController>());
                    remoteAnim.transform.localPosition = Quaternion.Inverse(player.transform.rotation) * (playerAnim.transform.position - remote.transform.position);
                    if (playerAnim != null) { playerAnim.Rebind(); playerAnim.Update(0); }
                    if (remoteAnim != null) { remoteAnim.Rebind(); remoteAnim.Update(0); }
                    report.samples.Add(AvatarGeometryProbe.Measure(player, "PlayerReference", 0, index));
                    report.samples.Add(AvatarGeometryProbe.Measure(remote, "PuppetAfter", 1, index));
                    Object.DestroyImmediate(player); Object.DestroyImmediate(remote);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("[AvatarGeometry] " + output);
            }
            finally { EditorSceneManager.ClosePreviewScene(testing); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
