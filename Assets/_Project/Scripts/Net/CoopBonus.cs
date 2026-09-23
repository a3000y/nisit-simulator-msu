using UnityEngine;
using Unity.Netcode;
using NisitSimulator.Stats;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // 🤝 Co-op โบนัส — อยู่ใกล้ผู้เล่นอื่น (ระยะ range) = "เรียน/ทำกิจกรรมด้วยกัน" ได้ความรู้+พอใจเพิ่มเป็นระยะ
    //   ทำงานเฉพาะตอนเชื่อมต่อ MP · คำนวณฝั่ง client เอง (ไม่ต้อง sync) · วางบน NetworkManager โดย M29
    public class CoopBonus : MonoBehaviour
    {
        public float range = 6f;          // ระยะที่นับว่า "อยู่ด้วยกัน"
        public float interval = 8f;       // ได้โบนัสทุกกี่วินาที
        public float satPerTick = 4f;
        public float knowledgePerTick = 6f;

        private Transform player;
        private PlayerStats stats;
        private float timer;

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient) { timer = 0f; return; }   // เล่นคนเดียว = ไม่มีโบนัส

            if (player == null)
            {
                var p = GameObject.Find("Player");
                if (p != null) { player = p.transform; stats = p.GetComponent<PlayerStats>(); }
                if (player == null) return;
            }
            if (stats == null) stats = Object.FindFirstObjectByType<PlayerStats>();

            // มีผู้เล่นอื่นอยู่ในระยะไหม
            bool near = false;
            foreach (var av in Object.FindObjectsByType<NetworkAvatar>(FindObjectsSortMode.None))
            {
                if (av.IsOwner) continue;
                if ((av.transform.position - player.position).sqrMagnitude <= range * range) { near = true; break; }
            }

            if (!near) { timer = 0f; return; }

            timer += Time.deltaTime;
            if (timer >= interval)
            {
                timer = 0f;
                if (stats != null)
                {
                    stats.ChangeSatisfaction(satPerTick);
                    stats.ChangeKnowledge(knowledgePerTick);
                }
                HUDController.Toast($"เรียนกับเพื่อน! ความรู้ +{knowledgePerTick:0} พอใจ +{satPerTick:0} 🤝");
            }
        }
    }
}
