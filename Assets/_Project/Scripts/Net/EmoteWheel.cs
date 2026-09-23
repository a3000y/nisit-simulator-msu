using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using NisitSimulator.Player;

namespace NisitSimulator.Net
{
    // วงล้ออีโมท (emote wheel) — กดค้าง B → เลื่อนเมาส์ไปทางอีโมท → ปล่อยเพื่อเล่น (แบบเกมส่วนใหญ่)
    //   เล่นได้ทั้งเล่นคนเดียว (PlayerActionController) และหลายคน (broadcast ผ่าน NetworkAvatar)
    //   UI สร้าง+ต่อโดย M29 Setup Multiplayer
    public class EmoteWheel : MonoBehaviour
    {
        public KeyCode key = KeyCode.B;
        public GameObject panel;
        public RectTransform[] items;        // ปุ่มอีโมทวางเป็นวง
        public Image[] itemBg;               // ไว้ไฮไลต์ตัวที่เลือก (ย่อ/ขยาย)
        public Image[] rings;                // วงแหวนไฮไลต์ตอนเลือก (เปิด/ปิด)
        public TMP_Text centerLabel;         // ชื่ออีโมทที่เลือกอยู่ (กลางวง)
        public string[] names = { "โบกมือ", "เชียร์", "ทักทาย" };
        public int[] kinds = { 1, 2, 3 };    // 1=โบก 2=เชียร์ 3=ทักทาย

        int selected = -1;

        void Start() { if (panel != null) panel.SetActive(false); }

        void Update()
        {
            if (panel == null) return;
            if (Input.GetKeyDown(key))
            {
                if (NisitSimulator.Core.GameManager.Instance == null || NisitSimulator.Core.GameManager.Instance.IsActive) Open();
            }
            else if (Input.GetKey(key) && panel.activeSelf) Hover();
            else if (Input.GetKeyUp(key) && panel.activeSelf) Choose();
        }

        void Open()
        {
            panel.SetActive(true);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            selected = -1;
            if (rings != null) foreach (var r in rings) if (r != null) r.enabled = false;
            if (centerLabel != null) centerLabel.text = "เลือกอีโมท";
        }

        // เลือกไอเทมที่มุมเมาส์ (จากกลางจอ) ใกล้สุด
        void Hover()
        {
            Vector2 c = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 d = (Vector2)Input.mousePosition - c;
            selected = -1;
            if (d.magnitude > 55f && items != null)
            {
                float best = 999f;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null) continue;
                    float ang = Vector2.Angle(d, (Vector2)items[i].position - c);
                    if (ang < best) { best = ang; selected = i; }
                }
            }
            if (itemBg != null)
                for (int i = 0; i < itemBg.Length; i++)
                    if (itemBg[i] != null)
                        itemBg[i].transform.localScale = Vector3.one * (i == selected ? 1.25f : 1f);
            if (rings != null)
                for (int i = 0; i < rings.Length; i++)
                    if (rings[i] != null) rings[i].enabled = (i == selected);
            if (centerLabel != null)
                centerLabel.text = (selected >= 0 && names != null && selected < names.Length) ? names[selected] : "เลือกอีโมท";
        }

        void Choose()
        {
            panel.SetActive(false);
            if (selected >= 0 && kinds != null && selected < kinds.Length) Play(kinds[selected]);
            selected = -1;
        }

        void Play(int kind)
        {
            // หลายคน: broadcast ผ่าน avatar ของเรา (เล่นบน Player จริง + คนอื่นเห็น)
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsClient && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            {
                var av = nm.LocalClient.PlayerObject.GetComponent<NetworkAvatar>();
                if (av != null) { av.DoEmote(kind); return; }
            }
            // เล่นคนเดียว: เล่นท่าบน Player ตรง ๆ
            var p = GameObject.Find("Player");
            if (p != null)
            {
                var pac = p.GetComponent<PlayerActionController>() ?? p.AddComponent<PlayerActionController>();
                if (!pac.IsBusy) pac.PerformState(2.5f, null, StateName(kind));
            }
        }

        static string StateName(int k) { return k == 1 ? "Waving" : k == 2 ? "Cheering" : k == 3 ? "Talking" : null; }
    }
}
