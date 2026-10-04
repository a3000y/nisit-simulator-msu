using UnityEngine;
using NisitSimulator.Interaction;

namespace NisitSimulator.GEBuilding
{
    // ประตูบานพับจริงสำหรับอาคาร GE — กด E (ผ่าน IInteractable) เพื่อเปิด/ปิด
    // โครงสร้าง: root (สคริปต์นี้) > Hinge (จุดหมุน) > Leaf (บานประตู + Collider)
    public class GEDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("จุดหมุนของบานประตู")]
        public Transform hinge;
        [Tooltip("มุมตอนเปิด (องศา รอบแกน Y) — ค่าลบ = เปิดเข้าด้าน +Z ของประตู")]
        public float openAngle = -90f;
        [Tooltip("เริ่มเกมโดยเปิดประตูค้างไว้ เพื่อไม่ให้ขวางทางเดิน")]
        public bool startOpen = true;
        [Tooltip("ความเร็วหมุน (องศา/วินาที)")]
        public float speed = 240f;

        private bool isOpen;
        public bool IsOpen => isOpen;

        void Awake()
        {
            isOpen = NisitSimulator.Net.DoorSyncManager.MultiplayerDoor(this) ? false : startOpen;
            if (hinge != null) hinge.localRotation = Target();
        }

        void Update()
        {
            if (hinge == null) return;
            var target = Target();
            if (hinge.localRotation != target)
            {
                float elapsed = NisitSimulator.Net.DoorSyncManager.MultiplayerDoor(this) ? Time.unscaledDeltaTime : Time.deltaTime;
                hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, target, speed * elapsed);
                if (hinge.localRotation == target && NisitSimulator.Net.DoorSyncManager.MultiplayerDoor(this))
                    Debug.Log($"[DoorSync] settled key={NisitSimulator.Net.DoorSyncManager.Key(this)} open={isOpen} utc={System.DateTime.UtcNow:O}");
            }
        }

        Quaternion Target() => Quaternion.Euler(0f, isOpen ? openAngle : 0f, 0f);

        public void Toggle()
        {
            if (!NisitSimulator.Net.DoorSyncManager.RouteInteraction(this)) isOpen = !isOpen;
        }
        public void SetOpen(bool open) => isOpen = open;

        public void ApplyState(bool open, bool snap)
        {
            isOpen = open;
            if (snap && hinge != null) hinge.localRotation = Target();
        }

        public string GetPrompt() => isOpen ? "กด E เพื่อปิดประตู" : "กด E เพื่อเปิดประตู";
        public void Interact(GameObject interactor) => Toggle();

        void OnValidate()
        {
            if (!Application.isPlaying && hinge != null)
                hinge.localRotation = Quaternion.Euler(0f, startOpen ? openAngle : 0f, 0f);
        }
    }
}
