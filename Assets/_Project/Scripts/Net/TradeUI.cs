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
        private NisitSimulator.Player.PlayerMovement move;
        private bool moveWasOn;
        private readonly List<GameObject> rows = new List<GameObject>();

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) { player = p.transform; move = p.GetComponent<NisitSimulator.Player.PlayerMovement>(); }
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(key))
            {
                bool open = panel != null && panel.activeSelf;
                if (open) Close();
                else if (NisitSimulator.Core.GameManager.Instance == null || NisitSimulator.Core.GameManager.Instance.IsActive) Open();
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
            if (move != null && move.enabled) { move.enabled = false; moveWasOn = true; }   // หยุดเดินตอนเลือกของ
        }

        void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (move != null && moveWasOn) { move.enabled = true; moveWasOn = false; }
        }

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
            // เช็คระยะอีกครั้ง (กันเดินไกลแล้วยังให้ได้)
            if (target == null || player == null ||
                (target.transform.position - player.position).sqrMagnitude > range * range)
            { HUDController.Toast("ผู้เล่นอยู่ไกลเกินไป"); Close(); return; }

            var nm = NetworkManager.Singleton;
            var local = (nm != null && nm.LocalClient != null) ? nm.LocalClient.PlayerObject : null;
            var relay = local != null ? local.GetComponent<TradeRelay>() : null;
            if (relay == null) { HUDController.Toast("ยังไม่เชื่อมต่อ"); Close(); return; }

            // ไม่ลบของที่นี่ — TradeRelay จะลบ "เมื่อผู้รับยืนยันสำเร็จ" (กันของหาย)
            relay.Give(target.OwnerClientId, itemName);
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
