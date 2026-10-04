using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;

namespace NisitSimulator.Net
{
    public class LobbyController : MonoBehaviour
    {
        // Retain serialized references so the original lobby and character selector stay compatible.
        public Button startButton, backButton;
        public TMP_Text hintText, playerListText;
        public GameObject connectGroup, customizeGroup;
        public string gameplayScene = "01_Gameplay";
        public static LobbyController Instance { get; private set; }
        public LobbyView View { get; private set; }
        LobbyConnection connection;
        float nextSubmit;
        bool desiredReady;
        string previousLook;
        string localPlayerId;
        void Start()
        {
            Instance = this; GameSession.PendingLoad = false; GameSession.IsContinue = false;
            connection = LobbyConnection.EnsureExists(gameObject);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            var oldChat = GetComponent<ChatUI>(); if (oldChat != null) oldChat.enabled = false;
            View = LobbyView.Create(this, connection);
            var oldCanvas = GetComponent<Canvas>(); if (oldCanvas != null) oldCanvas.enabled = false;
        }
        void Update()
        {
            var state = LobbyState.Instance; var nm = NetworkManager.Singleton;
            if (state == null || nm == null || !state.IsSpawned || !connection.Connected || Time.unscaledTime < nextSubmit) return;
            nextSubmit = Time.unscaledTime + 0.5f;
            if (!state.Find(nm.LocalClientId, out var mine)) return;
            CaptureIntent(mine);
            string accessories = CharacterAccessories.Pack(GameSession.PlayerAccessories);
            bool ready = nm.IsHost || desiredReady;
            string name = LobbyRules.LimitName(GameSession.PlayerName, "ผู้เล่น " + (mine.SlotIndex + 1));
            if (mine.Ready != ready || mine.Name.ToString() != name || mine.Model != GameSession.PlayerModel || mine.Color != GameSession.PlayerColor || mine.Accessories.ToString() != accessories)
                state.SubmitLocal(ready);
        }
        public void ToggleReady()
        {
            var state = LobbyState.Instance; var nm = NetworkManager.Singleton;
            if (state == null || nm == null || nm.IsHost || !state.Find(nm.LocalClientId, out var mine)) return;
            CaptureIntent(mine);
            desiredReady = !mine.Ready; state.SubmitLocal(desiredReady);
        }
        void CaptureIntent(LobbyPlayer mine)
        {
            string look = GameSession.PlayerName + "/" + GameSession.PlayerModel + "/" + GameSession.PlayerColor + "/" + CharacterAccessories.Pack(GameSession.PlayerAccessories);
            if (localPlayerId != mine.PlayerId.ToString() || previousLook != look) desiredReady = false;
            localPlayerId = mine.PlayerId.ToString(); previousLook = look;
        }
        public void Customize()
        { desiredReady = false; if (LobbyState.Instance != null) LobbyState.Instance.SubmitLocal(false); if (View != null) View.ShowCustomize(); }
        public void StartGame() { if (LobbyState.Instance != null) LobbyState.Instance.StartGame(); }
        public void BackToMenu()
        { if (connection != null) connection.Leave(); else SceneManager.LoadScene(GameSession.MenuScene); }
        void OnDestroy()
        { if (Instance == this) Instance = null; if (View != null) Destroy(View.gameObject); }
    }
}
