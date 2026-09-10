using Unity.Netcode;
using UnityEngine;

namespace NisitSimulator.Net
{
    // อวตารเครือข่าย (puppet) — sync ตำแหน่ง/ทิศ/ความเร็ว ระหว่างผู้เล่นหลายคน
    //   • เจ้าของ (IsOwner): ลอกท่าจาก "Player" ในฉาก (SP เดิม) → broadcast · ซ่อนตัวเอง (เห็น Player จริงแทน)
    //   • ไม่ใช่เจ้าของ: มองเห็น เดินตามค่า sync + เล่นท่าเดิน (Speed) + หันตาม
    //   ออกแบบให้ "เสริมทับ" SP ไม่แตะระบบเดิม — วางโดย Nisit -> Setup Multiplayer (MP-1)
    public class NetworkAvatar : NetworkBehaviour
    {
        [Tooltip("ความไวในการ interpolate ของฝั่ง remote")]
        public float lerp = 12f;

        // ค่าที่ sync (เจ้าของเขียนได้คนเดียว, ทุกคนอ่านได้)
        readonly NetworkVariable<Vector3> netPos = new NetworkVariable<Vector3>(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netYaw = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netSpeed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Transform localPlayer;   // owner: อ้างอิง Player ในฉาก
        Animator anim;

        public override void OnNetworkSpawn()
        {
            anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = false;   // กันหุ่นไถล/ลอย (ตำแหน่งมาจาก sync ไม่ใช่ root motion)

            if (IsOwner)
            {
                gameObject.name = "MyAvatar";
                CacheLocalPlayer();
                SetVisible(false);                 // ซ่อน puppet ของตัวเอง (มองเห็น Player จริงแทน)
                if (anim != null) anim.enabled = false;   // owner ไม่ต้องเล่นท่า (อ่านจาก Player จริงแทน)
            }
            else
            {
                gameObject.name = "RemoteAvatar";
                SetVisible(true);
                // เริ่มที่ตำแหน่งล่าสุดทันที (กันวาร์ปจาก 0,0,0)
                transform.position = netPos.Value;
                transform.rotation = Quaternion.Euler(0f, netYaw.Value, 0f);
            }
        }

        void Update()
        {
            if (IsOwner)
            {
                if (localPlayer == null) CacheLocalPlayer();
                if (localPlayer != null)
                {
                    netPos.Value = localPlayer.position;
                    netYaw.Value = localPlayer.eulerAngles.y;
                    var a = localPlayer.GetComponentInChildren<Animator>();
                    if (a != null) netSpeed.Value = a.GetFloat("Speed");
                }
            }
            else
            {
                // remote: เดินตามค่าที่ sync มา (interpolate ให้ลื่น)
                transform.position = Vector3.Lerp(transform.position, netPos.Value, lerp * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, netYaw.Value, 0f), lerp * Time.deltaTime);
                if (anim != null) anim.SetFloat("Speed", netSpeed.Value);
            }
        }

        void CacheLocalPlayer()
        {
            var p = GameObject.Find("Player");
            if (p != null) localPlayer = p.transform;
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
        }
    }
}
