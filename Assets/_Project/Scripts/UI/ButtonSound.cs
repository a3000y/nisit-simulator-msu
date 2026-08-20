using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.Core;

namespace NisitSimulator.UI
{
    // เล่นเสียงคลิกเมื่อกดปุ่ม (Editor tool แปะให้ทุกปุ่มอัตโนมัติ)
    [RequireComponent(typeof(Button))]
    public class ButtonSound : MonoBehaviour
    {
        void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => SFXManager.Click());
        }
    }
}
