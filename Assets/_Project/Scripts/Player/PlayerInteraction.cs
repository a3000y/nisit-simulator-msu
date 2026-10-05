using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.Core;
using NisitSimulator.UI;

namespace NisitSimulator.Player
{
    // ตรวจจับวัตถุที่โต้ตอบได้รอบตัวผู้เล่น แล้วกด E เพื่อโต้ตอบ
    // ใส่สคริปต์นี้ไว้ที่ตัวละคร Player
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("การตรวจจับ")]
        public float interactRange = 1.6f;   // ระยะที่โต้ตอบได้ (แคบ = ต้องเข้าใกล้)
        public LayerMask interactableLayer;  // เลเยอร์ของวัตถุที่โต้ตอบได้
        public KeyCode interactKey = KeyCode.E;

        private IInteractable current;       // วัตถุที่เล็งอยู่ตอนนี้

        void Update()
        {
            if (NisitSimulator.Systems.ArrivalIntroController.Active) { ClearCurrent(); return; }
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) { ClearCurrent(); return; }
            if (Time.timeScale == 0f) { ClearCurrent(); return; }   // มีหน้าต่างหยุดเวลา (สอบ/เหตุการณ์/คู่มือ) → ไม่รับ E ซ้อน
            if (NisitSimulator.Academics.ExamMinigame.ExamMinigameController.BlocksWorld) { ClearCurrent(); return; }   // อยู่ในห้องสอบมินิเกม → ไม่รับการโต้ตอบในโลก

            DetectNearest();

            if (current != null && Input.GetKeyDown(interactKey))
            {
                current.Interact(gameObject);
            }
        }

        void ClearCurrent() { current = null; HUDController.Prompt(""); }

        // หาวัตถุที่โต้ตอบได้ที่ใกล้ที่สุดในระยะ
        private void DetectNearest()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, interactableLayer);

            IInteractable nearest = null;
            float minDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<IInteractable>(out var interactable))
                {
                    float d = Vector3.Distance(transform.position, hit.transform.position);
                    // NPC/สัตว์ (เดินไปมา) ให้ระยะ "หนักขึ้น" → ประตู/ห้องสอบชนะเมื่อระยะใกล้เคียงกัน (กันบังทางเข้า)
                    if (interactable is TalkNPC || interactable is PetAnimal) d += 1.2f;
                    if (d < minDist)
                    {
                        minDist = d;
                        nearest = interactable;
                    }
                }
            }

            current = nearest;

            // แสดงป้าย "กด E เพื่อ..." บน HUD (ว่าง = ซ่อน)
            HUDController.Prompt(current != null ? current.GetPrompt() : "");
        }

        // วาดรัศมีให้เห็นใน Scene view (debug)
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
