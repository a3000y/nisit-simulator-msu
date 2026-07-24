using UnityEngine;

namespace NisitSimulator.Interaction
{
    // ประตูออก (อยู่ในห้อง) — กด E เพื่อกลับออกไปข้างนอก
    public class InteriorExit : MonoBehaviour, IInteractable
    {
        public string GetPrompt() => "กด E เพื่อออกจากอาคาร";

        public void Interact(GameObject interactor)
        {
            if (InteriorManager.Instance != null) InteriorManager.Instance.Exit();
        }
    }
}
