using Unity.Netcode;
using UnityEngine;
using TMPro;

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

        // อีโมท: encode = seq*10 + kind (kind 1=โบก 2=เชียร์ 3=ทักทาย) — เปลี่ยนค่า = เล่นท่าใหม่
        readonly NetworkVariable<int> emote = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Transform localPlayer;   // owner: อ้างอิง Player ในฉาก
        Animator anim;
        Transform nameTag;       // remote: ป้ายชื่อลอยหัว
        Camera cam;
        int emoteSeq;                              // owner: ตัวนับกันค่าซ้ำ
        int gestureBase; bool gesturing; float gestureUntil;   // remote: เล่นท่าอีโมทแล้วกลับท่าเดิม

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
                CreateNameTag();   // ป้ายชื่อลอยหัว (เฉพาะผู้เล่นอื่น)
                emote.OnValueChanged += OnEmoteChanged;   // ผู้เล่นอื่นส่งอีโมท → เล่นท่า
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) emote.OnValueChanged -= OnEmoteChanged;
        }

        // ป้ายชื่อลอยหัว (TextMeshPro 3D — ไม่ต้องใช้ canvas)
        void CreateNameTag()
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "Player " + (OwnerClientId + 1);
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(20, 22, 38, 255);
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            nameTag = go.transform;
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
                // อีโมทสั่งจาก EmoteWheel (กดค้าง B เลือก) ผ่าน DoEmote()
            }
            else
            {
                // จบอีโมท → กลับท่าเดิน (เดิน/ยืน)
                if (gesturing && Time.time >= gestureUntil)
                {
                    if (anim != null && gestureBase != 0) anim.CrossFade(gestureBase, 0.2f);
                    gesturing = false;
                }

                // remote: เดินตามค่าที่ sync มา (interpolate ให้ลื่น)
                transform.position = Vector3.Lerp(transform.position, netPos.Value, lerp * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, netYaw.Value, 0f), lerp * Time.deltaTime);
                if (anim != null) anim.SetFloat("Speed", netSpeed.Value);

                // ป้ายชื่อหันหน้าเข้ากล้องเสมอ (billboard)
                if (nameTag != null)
                {
                    if (cam == null) cam = Camera.main;
                    if (cam != null) nameTag.forward = cam.transform.forward;
                }
            }
        }

        void CacheLocalPlayer()
        {
            var p = GameObject.Find("Player");
            if (p != null) localPlayer = p.transform;
        }

        // ---------- อีโมท (เรียกจาก EmoteWheel) ----------
        public void DoEmote(int kind)
        {
            emote.Value = (++emoteSeq) * 10 + kind;   // sync ให้ผู้เล่นอื่นเห็น
            if (localPlayer != null)                  // เล่นบน Player จริงของเรา (ให้เราเห็นตัวเองด้วย)
            {
                var pac = localPlayer.GetComponent<NisitSimulator.Player.PlayerActionController>();
                if (pac != null && !pac.IsBusy) pac.PerformState(2.5f, null, StateName(kind));
            }
        }

        void OnEmoteChanged(int prev, int cur) => PlayEmoteRemote(cur % 10);

        void PlayEmoteRemote(int kind)
        {
            string st = StateName(kind);
            if (anim == null || string.IsNullOrEmpty(st)) return;
            if (!gesturing) gestureBase = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
            int h = Animator.StringToHash(st);
            if (anim.HasState(0, h)) anim.CrossFade(h, 0.15f);
            gesturing = true; gestureUntil = Time.time + 2.5f;
        }

        static string StateName(int kind)
        {
            switch (kind) { case 1: return "Waving"; case 2: return "Cheering"; case 3: return "Talking"; }
            return null;
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
        }
    }
}
