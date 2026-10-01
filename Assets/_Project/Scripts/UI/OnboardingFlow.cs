using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.SaveLoad;
namespace NisitSimulator.UI
{
    public static class OnboardingFlow
    {
        public const string SceneName = "00_Tutorial";
        public static bool IsPractice => SceneManager.GetActiveScene().name == SceneName;
        public static bool Replay { get; private set; }
        public static void BeginNewGame()
        {
            Replay = false;
            GameSession.PendingLoad = false;
            GameSession.IsContinue = false;
            GameSession.IsMultiplayerGame = false;
            GameSession.OpenNetworkOnStart = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneName);
        }
        public static void BeginReplay()
        {
            Replay = true;
            GameSession.PendingLoad = false; GameSession.IsContinue = false; GameSession.IsMultiplayerGame = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneName);
        }
        public static void Finish(bool skipped)
        {
            PlayerPrefs.SetInt("nisit_tutorial_status", skipped ? 2 : 1);
            PlayerPrefs.SetInt("tut_seen", 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            if (Replay) { Replay = false; SceneManager.LoadScene(GameSession.MenuScene); return; }
            StartFreshGame();
        }
        public static void StartFreshGame()
        {
            // Practice scene has no save/bootstrap/inventory. Only the original new-game path deletes the selected slot.
            GameSession.PendingLoad = false;
            GameSession.IsContinue = false;
            GameSession.IsMultiplayerGame = false;
            GameSession.OpenNetworkOnStart = false;
            // Delete after leaving practice, where saves are deliberately blocked.
            SceneManager.sceneLoaded += DeleteSelectedSave;
            SceneManager.LoadScene(GameSession.GameplayScene);
        }
        static void DeleteSelectedSave(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSession.GameplayScene) return;
            SceneManager.sceneLoaded -= DeleteSelectedSave;
            SaveSystem.DeleteSave();
        }
    }
}