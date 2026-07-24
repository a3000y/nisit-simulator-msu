using UnityEngine;

namespace NisitSimulator.Interaction
{
    // ประตูอาคาร — เดินเข้าใกล้แล้วกด E เพื่อเข้าไปในห้อง
    // ใส่ไว้ที่ marker หน้าตึก (Layer = Interactable) โดย Nisit -> Setup Building Doors
    public class BuildingDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("จุดเกิดในห้อง (InteriorSpawn)")]
        public Transform interiorSpawn;
        public string activityName = "เข้าอาคาร";

        public string GetPrompt() => $"กด E เพื่อ{activityName}";

        public void Interact(GameObject interactor)
        {
            if (InteriorManager.Instance != null && interiorSpawn != null)
                InteriorManager.Instance.Enter(interiorSpawn);
            else
                Debug.LogWarning("[BuildingDoor] ยังไม่มี InteriorManager หรือ interiorSpawn");
        }
    }
}
