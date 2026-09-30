using UnityEngine;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Player
{
    // สลับโมเดลตัวละครผู้เล่นตามที่เลือก (หน้าแต่งตัว: ชุด/ทรงผม/สี) — วางบน Player โดย M34
    //   Awake รันก่อน PlayerMovement/PAC (order ต่ำ) → สลับโมเดลก่อนสคริปต์อื่น cache Animator
    [DefaultExecutionOrder(-500)]
    public class PlayerModelSwapper : MonoBehaviour
    {
        void Awake()
        {
            // ใส่ชุดที่เลือกเสมอ (โมเดลในฉากเป็นแค่ตัวสำรองตอนไม่มีแคตตาล็อก)
            CharacterCatalog.Apply(transform, GameSession.PlayerModel);
        }

        void Start()
        {
            // ใส่ของแต่ง (ทรงผม/หมวก/...) + สี ตามที่เลือก — ทำใน Start เพราะกระดูกโมเดลพร้อมแล้ว
            RefreshLook();
        }

        // เปลี่ยนแบบระหว่างเล่น (จากแผงแต่งตัว) — ต้อง refresh Animator ให้สคริปต์อื่น
        public void SwapTo(int index)
        {
            CharacterCatalog.Apply(transform, index);
            RefreshLook();
            var mv = GetComponent<PlayerMovement>(); if (mv != null) mv.RefreshAnimator();
            var pac = GetComponent<PlayerActionController>(); if (pac != null) pac.RefreshAnimator();
        }

        // เปลี่ยนของแต่งระหว่างเล่น
        public void RefreshAccessories() => RefreshLook();

        void RefreshLook()
        {
            var anim = GetComponentInChildren<Animator>();
            var model = anim != null ? anim.gameObject : gameObject;
            CharacterCatalog.ApplyLook(model, GameSession.PlayerColor, NisitSimulator.Net.NetworkAvatar.Palette);
            CharacterAccessories.Apply(transform, GameSession.PlayerAccessories);
        }
    }
}
