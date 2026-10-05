#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.DevTools
{
    // Opt-in only. Set the SessionState path before Play Mode to isolate verification saves.
    public static class ArrivalPlaytestGuard
    {
        public const string Key = "Nisit.Arrival.PlaytestSave";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string path = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(path)) return;
            SaveSystem.DevGuard = true;
            SaveSystem.DevPathOverride = path;
            SaveSystem.DevReadOverride = null;
            GameSession.PendingLoad = SessionState.GetBool(Key + ".Continue", false);
            GameSession.IsContinue = GameSession.PendingLoad;
            GameSession.SelectedFacultyIndex = 0;
            Debug.Log("[Arrival QA] Isolated save enabled.");
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            string path = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(path)) return;
            SaveSystem.DevGuard = true;
            SaveSystem.DevPathOverride = path;
        }
    }
}
#endif
