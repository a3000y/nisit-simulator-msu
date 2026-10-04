#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using NisitSimulator.Net;

namespace NisitSimulator.EditorTools
{
    public static class LobbyUIBuilder
    {
        [MenuItem("Nisit/Build Lobby UI", false, 41)]
        public static void Build()
        {
            // Only creates the new data prefab. UI is built at runtime; existing scene/prefabs are preserved.
            const string dir = "Assets/_Project/Resources/Net";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Net");
            }
            var go = new GameObject("LobbyState", typeof(NetworkObject), typeof(LobbyState), typeof(DoorSyncManager));
            try { PrefabUtility.SaveAsPrefabAsset(go, dir + "/LobbyState.prefab"); }
            finally { Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Net][Lobby] สร้างข้อมูลห้องแล้ว UI จะสร้างเมื่อเข้าฉาก 02_Lobby");
        }
    }
}
#endif
