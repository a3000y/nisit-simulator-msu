using UnityEngine;
using NisitSimulator.Systems;

namespace NisitSimulator.Interaction
{
    // จุดเปิดหน้าร้าน/เมนู — กด E เปิด/ปิด (ผูกกับ ShopController เฉพาะร้าน)
    public class ShopStation : MonoBehaviour, IInteractable
    {
        public ShopController shop;                       // เซ็ตโดย Build Shop/Cafeteria
        public string prompt = "กด E เพื่อซื้อของ";

        public string GetPrompt() => prompt;

        public void Interact(GameObject interactor)
        {
            var s = shop != null ? shop : Object.FindFirstObjectByType<ShopController>();
            if (s != null) s.Toggle();
        }
    }
}
