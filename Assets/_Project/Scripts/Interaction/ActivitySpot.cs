using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Player;
using NisitSimulator.Stats;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // จุดทำกิจกรรมแบบ "นั่ง" — กด E นั่ง (ค่าสถานะเปลี่ยน) / กด E อีกที ลุก
    // ใส่ไว้ที่ marker (Layer = Interactable) + มี seat anchor บอกจุดนั่ง
    public class ActivitySpot : MonoBehaviour, IInteractable
    {
        [Header("จุดนั่ง (ปล่อยว่าง = ใช้ตำแหน่งตัวเอง)")]
        public Transform seat;
        [Tooltip("ยกตัวขึ้นตอนนั่ง (กันจมพื้น) — ปรับได้ตอน Play")]
        public float sitYOffset = 0.4f;
        public string activityName = "นั่งพัก";

        [Header("ผลต่อสถานะ (บวก=เพิ่ม)")]
        public float energyChange = 0f;
        public float knowledgeChange = 0f;
        public float hungerChange = 0f;
        public float satisfactionChange = 0f;
        public int expReward = 0;

        private bool _seated;

        public string GetPrompt() => _seated ? "กด E เพื่อลุกขึ้น" : $"กด E เพื่อ{activityName}";

        public void Interact(GameObject interactor)
        {
            var act = interactor.GetComponent<PlayerActivity>();
            if (act == null) return;

            if (_seated)
            {
                act.Stand();
                _seated = false;
            }
            else
            {
                if (act.IsBusy) return;                 // กำลังนั่งที่อื่นอยู่
                var t = seat != null ? seat : transform;
                act.Sit(t.position + Vector3.up * sitYOffset, t.rotation);
                _seated = true;
                ApplyEffect(interactor);
            }
        }

        private void ApplyEffect(GameObject who)
        {
            if (who.TryGetComponent<PlayerStats>(out var s))
            {
                s.ChangeEnergy(energyChange);
                s.ChangeKnowledge(knowledgeChange);
                s.ChangeHunger(hungerChange);
                s.ChangeSatisfaction(satisfactionChange);
                s.AddExp(expReward);
                HUDController.Toast(BuildToast());   // ป๊อปอัปสรุปผลบนจอ
            }
        }

        // สรุปผลที่เปลี่ยนเป็นข้อความ (เฉพาะค่าที่ไม่เป็น 0)
        private string BuildToast()
        {
            var parts = new List<string>();
            if (knowledgeChange != 0f)    parts.Add($"ความรู้ {knowledgeChange:+0;-0}");
            if (energyChange != 0f)       parts.Add($"พลังงาน {energyChange:+0;-0}");
            if (hungerChange != 0f)       parts.Add($"ความอิ่ม {hungerChange:+0;-0}");
            if (satisfactionChange != 0f) parts.Add($"พอใจ {satisfactionChange:+0;-0}");
            if (expReward != 0)           parts.Add($"EXP {expReward:+0;-0}");
            return parts.Count > 0 ? $"{activityName}  ({string.Join("  ", parts)})" : activityName;
        }
    }
}
