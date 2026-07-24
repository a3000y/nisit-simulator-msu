#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Player;

namespace NisitSimulator.EditorTools
{
    // ตั้งระยะกด E (interactRange) ของ Player ตัวปัจจุบันในฉาก
    //   เปลี่ยนค่า Range ด้านล่างได้ (world units — เล็กลง = ต้องเข้าใกล้กว่า)
    public static class InteractTuner
    {
        const float Range = 1.6f;

        [MenuItem("Nisit/Set Interact Range (แคบ)")]
        public static void Set()
        {
            var p = GameObject.Find("Player");
            var pi = p != null ? p.GetComponent<PlayerInteraction>() : null;
            if (pi == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ Player / PlayerInteraction ในฉาก", "OK"); return; }

            Undo.RecordObject(pi, "Interact Range");
            pi.interactRange = Range;
            EditorUtility.SetDirty(pi);
            EditorSceneManager.MarkSceneDirty(pi.gameObject.scene);
            Debug.Log($"<color=lime>[Nisit] ตั้ง Interact Range = {Range} (แคบลง — ต้องเข้าใกล้ถึงจะกด E ได้)</color>");
        }
    }
}
#endif
