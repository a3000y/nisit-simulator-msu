using System.Collections;
using UnityEngine;
using NisitSimulator.Core;

namespace NisitSimulator.Player
{
    // เล่น "แอคชัน" ของตัวละครตามระยะเวลา แล้วค่อยกลับมาปกติ (หยุดเดินระหว่างทำ)
    // ตอนนี้ใช้ท่านั่ง (bool "Sitting") เป็นค่าเริ่มต้น — เพิ่มท่าใหม่ได้โดยส่งชื่อ animBool อื่น
    // (นำเข้าคลิป Mixamo → สร้าง state + bool param ใน Animator → ส่งชื่อ bool นั้นมาที่ Perform)
    public class PlayerActionController : MonoBehaviour
    {
        private Animator anim;
        private PlayerMovement move;
        private string holdBool;

        public bool IsBusy { get; private set; }

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            move = GetComponent<PlayerMovement>();
        }

        // ทำแอคชันเป็นเวลา duration วินาที แล้วเรียก onDone (หยุดเดินระหว่างนั้น)
        public void Perform(float duration, System.Action onDone, string animBool = "Sitting")
        {
            if (IsBusy) { onDone?.Invoke(); return; }
            StartCoroutine(Routine(duration, onDone, animBool));
        }

        IEnumerator Routine(float duration, System.Action onDone, string animBool)
        {
            IsBusy = true;
            if (move != null) move.enabled = false;
            bool hasParam = anim != null && HasParam(animBool);
            if (hasParam) anim.SetBool(animBool, true);

            float t = 0f;
            while (t < duration)
            {
                // นับเวลาเฉพาะตอนเกมเดินอยู่ (เผื่อ pause)
                if (GameManager.Instance == null || GameManager.Instance.IsActive)
                    t += Time.deltaTime;
                yield return null;
            }

            if (hasParam) anim.SetBool(animBool, false);
            if (move != null) move.enabled = true;
            IsBusy = false;
            onDone?.Invoke();
        }

        // เล่นท่าเจาะจงตามชื่อ state (ส่งหลายท่า = สุ่มเลือก) แล้วกลับสู่ท่าเดิม (locomotion)
        public void PerformState(float duration, System.Action onDone, params string[] states)
        {
            if (IsBusy) { onDone?.Invoke(); return; }
            StartCoroutine(RoutineState(duration, onDone, states));
        }

        IEnumerator RoutineState(float duration, System.Action onDone, string[] states)
        {
            IsBusy = true;
            if (move != null) move.enabled = false;

            int baseHash = anim != null ? anim.GetCurrentAnimatorStateInfo(0).shortNameHash : 0;
            if (anim != null && states != null && states.Length > 0)
            {
                int h = Animator.StringToHash(states[Random.Range(0, states.Length)]);   // สุ่มท่า
                if (anim.HasState(0, h)) anim.CrossFade(h, 0.15f);
            }

            float t = 0f;
            while (t < duration)
            {
                if (GameManager.Instance == null || GameManager.Instance.IsActive) t += Time.deltaTime;
                yield return null;
            }

            if (anim != null && baseHash != 0) anim.CrossFade(baseHash, 0.2f);   // กลับท่าเดิม
            if (move != null) move.enabled = true;
            IsBusy = false;
            onDone?.Invoke();
        }

        // ทำท่าค้างไว้ (ไม่จับเวลา) — ใช้ตอนสอบ: นั่งค้างระหว่างทำข้อสอบ แล้วค่อย EndHold
        public void BeginHold(string animBool = "Sitting")
        {
            if (IsBusy) return;
            IsBusy = true;
            holdBool = animBool;
            if (move != null) move.enabled = false;
            if (anim != null && HasParam(animBool)) anim.SetBool(animBool, true);
        }

        public void EndHold()
        {
            if (!IsBusy) return;
            if (anim != null && !string.IsNullOrEmpty(holdBool) && HasParam(holdBool)) anim.SetBool(holdBool, false);
            if (move != null) move.enabled = true;
            IsBusy = false;
        }

        bool HasParam(string n)
        {
            foreach (var p in anim.parameters)
                if (p.name == n) return true;
            return false;
        }
    }
}
