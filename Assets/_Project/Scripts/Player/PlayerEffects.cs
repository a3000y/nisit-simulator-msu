using UnityEngine;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Player
{
    // ผลกระทบชั่วคราว "ทั้งวัน" จากเหตุการณ์ (B) — หายเมื่อขึ้นวันใหม่ (นอน)
    //   ป่วย = เดินช้า + เหนื่อยเร็ว · ไฟแรง = เข้าเรียนได้ความรู้ ×1.5
    public class PlayerEffects : MonoBehaviour
    {
        public float moveMult = 1f;          // ตัวคูณความเร็วเดิน (PlayerMovement อ่าน)
        public float energyDrainMult = 1f;   // ตัวคูณการเสียพลังงาน (PlayerMovement + StatDecay อ่าน)
        public float knowledgeMult = 1f;      // ตัวคูณความรู้ที่ได้ (ClassStation อ่าน)

        private GameClock clock;

        void Start()
        {
            clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) clock.OnDayChanged += OnNewDay;
        }

        void OnDestroy() { if (clock != null) clock.OnDayChanged -= OnNewDay; }

        void OnNewDay(int _)
        {
            if (HasAny) { ClearAll(); HUDController.Toast("อาการต่าง ๆ หายแล้ว (วันใหม่)"); }
        }

        public bool HasAny => moveMult != 1f || energyDrainMult != 1f || knowledgeMult != 1f;

        public void ClearAll() { moveMult = 1f; energyDrainMult = 1f; knowledgeMult = 1f; }

        public void ApplySick()
        {
            moveMult = 0.6f; energyDrainMult = 1.8f;
            HUDController.Toast("เป็นหวัด! เดินช้าลง + เหนื่อยเร็ว จนกว่าจะได้นอน");
            var pac = GetComponent<PlayerActionController>();   // ท่าไอ 🤧
            if (pac != null) pac.PerformState(2f, null, "Coughing");
        }

        public void ApplyInspired()
        {
            knowledgeMult = 1.5f;
            HUDController.Toast("ไฟแรง! วันนี้เข้าเรียนได้ความรู้ ×1.5");
        }
    }
}
