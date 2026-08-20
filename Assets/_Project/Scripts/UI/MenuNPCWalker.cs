using UnityEngine;

namespace NisitSimulator.UI
{
    // NPC เดินวนตาม waypoints + เล่นอนิเมชันเดิน (พารามิเตอร์ "Speed" ใน Animator)
    // ใช้ในฉากเมนูตอนนี้ และเอาไปใช้กับ NPC ในเกมจริงได้ในอนาคต
    public class MenuNPCWalker : MonoBehaviour
    {
        public Transform[] waypoints;
        public int startIndex = 0;
        public float speed = 1.6f;
        public float turnSpeed = 8f;
        public float reachDist = 0.4f;
        public float pauseTime = 0.6f;     // หยุดพักที่จุดหมายสั้น ๆ

        private Animator animator;
        private int idx;
        private float pauseUntil;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (waypoints != null && waypoints.Length > 0)
                idx = startIndex % waypoints.Length;
        }

        void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            // พักที่จุดหมาย
            if (Time.time < pauseUntil)
            {
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }

            var target = waypoints[idx];
            if (target == null) return;

            Vector3 to = target.position - transform.position; to.y = 0f;
            float dist = to.magnitude;

            if (dist < reachDist)
            {
                idx = (idx + 1) % waypoints.Length;   // ไป waypoint ถัดไป
                pauseUntil = Time.time + pauseTime;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }

            Vector3 dir = to / Mathf.Max(dist, 0.0001f);
            transform.position += dir * speed * Time.deltaTime;
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
            if (animator != null) animator.SetFloat("Speed", 0.5f);   // ท่าเดิน
        }
    }
}
