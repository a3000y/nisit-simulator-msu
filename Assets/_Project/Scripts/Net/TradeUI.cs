using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // เทรด/ให้ของผู้เล่นที่อยู่ใกล้ — กด G เปิดรายการไอเทมในกระเป๋า → คลิกส่งให้ผู้เล่นที่ใกล้สุด
    //   UI สร้าง+ต่อโดย M29 Setup Multiplayer
    public class TradeUI : MonoBehaviour
    {
        public GameObject panel;
        public Transform content;        // ที่วางปุ่มไอเทม (มี VerticalLayoutGroup)
        public GameObject rowTemplate;   // ปุ่มไอเทม (Button + TMP_Text ลูก)
        public TMP_Text titleText;
        public KeyCode key = KeyCode.G;
        public float range = 5f;

        private Transform player;
        private readonly List<GameObject> rows = new List<GameObject>();

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(key))
            {
                if (panel != null && panel.activeSelf) Close();
                else Open();
            }
        }

        void Open()
        {
            var target = NearestRemote();
            if (target == null) { HUDController.Toast("ไม่มีผู้เล่นอื่นอยู่ใกล้ ๆ"); return; }

            var inv = InventoryManager.Instance;
            if (inv == null || inv.TotalCount() == 0) { HUDController.Toast("กระเป๋าว่าง ไม่มีของให้"); return; }

            Build(target);
            if (panel != null) panel.SetActive(true);
        }

        void Close() { if (panel != null) panel.SetActive(false); }

        void Build(NetworkObject target)
        {
            foreach (var r in rows) Destroy(r);
            rows.Clear();

            if (titleText != null) titleText.text = $"ให้ของกับ ผู้เล่น {target.OwnerClientId + 1}";

            var inv = InventoryManager.Instance;
            if (inv == null || rowTemplate == null || content == null) return;

            foreach (var s in inv.Stacks)
            {
                if (s.item == null) continue;
                var go = Instantiate(rowTemplate, content);
                go.SetActive(true);
                var t = go.GetComponentInChildren<TMP_Text>();
                if (t != null) t.text = $"{s.item.name}  x{s.count}";
                string itemName = s.item.name;
                var btn = go.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => Give(target, itemName));
                rows.Add(go);
            }
        }

        void Give(NetworkObject target, string itemName)
        {
            var inv = InventoryManager.Instance;
            if (inv == null || !inv.RemoveByName(itemName)) return;   // เอาออกจากกระเป๋าเรา

            var local = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                        ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            var relay = local != null ? local.GetComponent<TradeRelay>() : null;
            if (relay != null) relay.Give(target.OwnerClientId, itemName);

            HUDController.Toast($"ให้ {itemName} แล้ว!");
            Close();
        }

        NetworkObject NearestRemote()
        {
            if (player == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return null;
            NetworkObject best = null; float bd = range * range;
            foreach (var av in Object.FindObjectsByType<NetworkAvatar>(FindObjectsSortMode.None))
            {
                if (av.IsOwner) continue;
                float d = (av.transform.position - player.position).sqrMagnitude;
                if (d < bd) { bd = d; best = av.GetComponent<NetworkObject>(); }
            }
            return best;
        }
    }
}
