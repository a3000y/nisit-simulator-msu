using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Net
{
    // ควบคุมฉากล็อบบี้เล่นหลายคน — Host/Join แล้วแต่งตัว+แชท+เห็นรายชื่อ · โฮสต์กด "เริ่มเกม" → ทุกคนเข้าพร้อมกัน
    //   ต้องเปิด NetworkConfig.EnableSceneManagement ที่ NetworkManager (M38 ตั้งให้)
    //   UI สร้างโดย Editor tool (Nisit -> Build Lobby)
    public class LobbyController : MonoBehaviour
    {
        public Button startButton;   // โฮสต์เท่านั้น
        public Button backButton;
        public TMP_Text hintText;
        public TMP_Text playerListText;
        public string gameplayScene = "01_Gameplay";

        void Start()
        {
            if (startButton) startButton.onClick.AddListener(StartGame);
            if (backButton) backButton.onClick.AddListener(BackToMenu);
            // เข้าเกมแบบใหม่ (ไม่โหลดเซฟ) เมื่อเริ่มจากล็อบบี้
            GameSession.PendingLoad = false;
            GameSession.IsContinue = false;
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsHost || nm.IsClient || nm.IsServer);
            bool isHost = nm != null && nm.IsHost;

            if (startButton && startButton.gameObject.activeSelf != (connected && isHost))
                startButton.gameObject.SetActive(connected && isHost);

            if (hintText != null)
            {
                int n = connected ? nm.ConnectedClientsList.Count : 0;
                hintText.text = !connected
                    ? "สร้างห้อง (Host) หรือใส่ IP/โค้ดแล้วเข้าห้อง (Join)"
                    : (isHost ? $"มีผู้เล่น {n} คน · รอเพื่อนเข้าครบแล้วกด \"เริ่มเกม\""
                              : "เข้าห้องแล้ว · รอโฮสต์กดเริ่มเกม...");
            }

            if (playerListText != null)
            {
                if (!connected) { playerListText.text = "ยังไม่มีผู้เล่นในห้อง"; return; }
                var sb = new System.Text.StringBuilder("ผู้เล่นในห้อง:\n");
                foreach (var cl in nm.ConnectedClientsList)
                {
                    var av = cl.PlayerObject != null ? cl.PlayerObject.GetComponent<NetworkAvatar>() : null;
                    string nm2 = av != null ? av.DisplayName : ("ผู้เล่น " + (cl.ClientId + 1));
                    sb.AppendLine("• " + nm2);
                }
                playerListText.text = sb.ToString();
            }
        }

        void StartGame()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost) return;
            // NGO โหลดฉากให้ทุกคนพร้อมกัน (server-authoritative)
            nm.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
        }

        void BackToMenu()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsHost || nm.IsClient || nm.IsServer)) nm.Shutdown();
            SceneManager.LoadScene(GameSession.MenuScene);
        }
    }
}
