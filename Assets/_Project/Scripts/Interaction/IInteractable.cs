using UnityEngine;

namespace NisitSimulator.Interaction
{
    // สัญญา (interface) สำหรับทุกสิ่งที่ผู้เล่นกด E โต้ตอบได้
    // เช่น โต๊ะเรียน, ร้านค้า, เตียง, NPC — ทุกตัว implement อันนี้
    public interface IInteractable
    {
        // ข้อความที่โชว์เมื่อผู้เล่นเข้าใกล้ เช่น "กด E เพื่อเข้าเรียน"
        string GetPrompt();

        // สิ่งที่เกิดขึ้นเมื่อกด E
        void Interact(GameObject interactor);
    }
}
