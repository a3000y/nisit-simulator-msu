using UnityEngine;

namespace NisitSimulator.Player
{
    // จัดการ "ทำกิจกรรม" ของตัวละคร เช่น นั่ง — หยุดเดิน + ย้ายไปจุดนั่ง + เล่นท่านั่ง
    // ใส่ไว้ที่ Player (คู่กับ PlayerMovement + Animator ที่โมเดลลูก)
    public class PlayerActivity : MonoBehaviour
    {
        private Animator _anim;
        private PlayerMovement _move;
        private CharacterController _cc;

        public bool IsBusy { get; private set; }

        void Awake()
        {
            _anim = GetComponentInChildren<Animator>();
            _move = GetComponent<PlayerMovement>();
            _cc = GetComponent<CharacterController>();
        }

        // นั่งที่ตำแหน่ง/มุมที่กำหนด
        public void Sit(Vector3 pos, Quaternion rot)
        {
            if (IsBusy) return;
            IsBusy = true;
            if (_move != null) _move.enabled = false;   // หยุดเดินชั่วคราว
            if (_cc != null) _cc.enabled = false;       // ปิด CC ค้างไว้ตอนนั่ง (กันโดนตึก/ต้นไม้ดันหลุด)
            transform.SetPositionAndRotation(pos, rot);
            if (_anim != null) _anim.SetBool("Sitting", true);
        }

        // ลุกขึ้น กลับมาเดินได้
        public void Stand()
        {
            if (!IsBusy) return;
            IsBusy = false;
            if (_anim != null) _anim.SetBool("Sitting", false);
            if (_cc != null) _cc.enabled = true;        // เปิด CC กลับตอนลุก
            if (_move != null) _move.enabled = true;
        }
    }
}
