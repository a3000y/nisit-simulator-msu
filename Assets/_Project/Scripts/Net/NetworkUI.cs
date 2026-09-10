using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.Net
{
    // ปุ่ม Host / Join / ออก สำหรับทดสอบ MP (localhost 127.0.0.1)
    //   กด F3 เปิด/ปิดแผง · UI สร้างโดย M29 Setup Multiplayer
    public class NetworkUI : MonoBehaviour
    {
        public GameObject panel;
        public Button hostButton, clientButton, disconnectButton;
        public TMP_Text statusText;

        void Start()
        {
            if (hostButton) hostButton.onClick.AddListener(Host);
            if (clientButton) clientButton.onClick.AddListener(Client);
            if (disconnectButton) disconnectButton.onClick.AddListener(Disconnect);
            Refresh();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3) && panel != null) panel.SetActive(!panel.activeSelf);
            Refresh();
        }

        void Host()
        {
            if (NetworkManager.Singleton == null || Connected()) return;
            if (!HasPlayerPrefab()) return;
            NetworkManager.Singleton.StartHost();
        }

        void Client()
        {
            if (NetworkManager.Singleton == null || Connected()) return;
            NetworkManager.Singleton.StartClient();
        }

        // เช็ก PlayerPrefab (จุดพังบ่อยสุด — ถ้าว่าง จะไม่มีตัวละครโผล่)
        bool HasPlayerPrefab()
        {
            if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null) return true;
            if (statusText != null) statusText.text = "⚠ PlayerPrefab ว่าง! ลาก NetworkAvatar ใส่ NetworkManager";
            Debug.LogWarning("[Net] NetworkManager.PlayerPrefab ว่าง — ลาก Assets/_Project/Prefabs/NetworkAvatar.prefab ใส่ช่อง PlayerPrefab");
            return false;
        }

        void Disconnect()
        {
            if (NetworkManager.Singleton != null && Connected()) NetworkManager.Singleton.Shutdown();
        }

        static bool Connected()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && (nm.IsHost || nm.IsClient || nm.IsServer);
        }

        void Refresh()
        {
            bool on = Connected();
            if (hostButton) hostButton.gameObject.SetActive(!on);
            if (clientButton) clientButton.gameObject.SetActive(!on);
            if (disconnectButton) disconnectButton.gameObject.SetActive(on);
            if (statusText != null)
            {
                var nm = NetworkManager.Singleton;
                statusText.text = !on
                    ? "ยังไม่เชื่อมต่อ — กด Host หรือ Join"
                    : (nm.IsHost ? "กำลัง Host (เซิร์ฟเวอร์ + ผู้เล่น)"
                                 : nm.IsServer ? "เซิร์ฟเวอร์"
                                 : "เชื่อมต่อแล้ว (ผู้เล่น)");
            }
        }
    }
}
