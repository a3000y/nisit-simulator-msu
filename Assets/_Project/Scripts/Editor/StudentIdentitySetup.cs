#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Characters;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    public static class StudentIdentitySetup
    {
        [MenuItem("Nisit/NPC/Setup Student Identities")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/01_Gameplay.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved gameplay scene before setup.");
            string backup = "Assets/_Project/Scenes/Backups/01_Gameplay_BeforeStudentIdentity_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(scene.path, backup, false);
            AssetDatabase.ImportAsset(backup);
            var ids = new HashSet<string>();
            int added = 0;
            foreach (var npc in UnityEngine.Object.FindObjectsByType<TalkNPC>(FindObjectsInactive.Include))
            {
                if (npc.gameObject.scene != scene) continue;
                var binding = npc.GetComponent<CharacterIdentity>();
                if (binding == null) { binding = Undo.AddComponent<CharacterIdentity>(npc.gameObject); binding.ConfigureFrom(npc); added++; }
                Undo.RecordObject(binding, "Assign permanent NPC identity");
                if (string.IsNullOrEmpty(binding.characterId) || !ids.Add(binding.characterId))
                { binding.characterId = "npc:" + Guid.NewGuid().ToString("N"); ids.Add(binding.characterId); }
                EditorUtility.SetDirty(binding);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Nisit Identity] {ids.Count} identities, {added} added. Backup: {backup}");
        }
    }
}
#endif
