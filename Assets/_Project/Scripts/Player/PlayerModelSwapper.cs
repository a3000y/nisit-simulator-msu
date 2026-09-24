using UnityEngine;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Player
{
    // สลับโมเดลตัวละครผู้เล่นตามที่เลือก (แต่งตัว/เลือกเพศ) — วางบน Player โดย M34
    //   Awake รันก่อน PlayerMovement/PAC (order ต่ำ) → สลับโมเดลก่อนสคริปต์อื่น cache Animator
    [DefaultExecutionOrder(-500)]
    public class PlayerModelSwapper : MonoBehaviour
    {
        void Awake()
        {
            if (GameSession.PlayerModel > 0)   // 0 = โมเดลเริ่มต้นในฉาก (ไม่ต้องสลับ)
                CharacterCatalog.Apply(transform, GameSession.PlayerModel);
        }

        // เปลี่ยนแบบระหว่างเล่น (จากแผงแต่งตัว) — ต้อง refresh Animator ให้สคริปต์อื่น
        public void SwapTo(int index)
        {
            CharacterCatalog.Apply(transform, index);
            var mv = GetComponent<PlayerMovement>(); if (mv != null) mv.RefreshAnimator();
            var pac = GetComponent<PlayerActionController>(); if (pac != null) pac.RefreshAnimator();
        }
    }
}
