using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NisitSimulator.UI
{
    // ติด ButtonJuice ให้ปุ่มทุกอันในทุกฉากอัตโนมัติ (ไม่ต้อง re-bake)
    //   ข้ามปุ่มเล็ก (< 160px) เพราะพวกสวอตช์สี/แบบ/ของแต่ง ใช้การไฮไลต์ด้วย scale เองอยู่แล้ว (กันชนกัน)
    public class UIJuiceInstaller : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("UIJuiceInstaller");
            DontDestroyOnLoad(go);
            var inst = go.AddComponent<UIJuiceInstaller>();
            SceneManager.sceneLoaded += (s, m) => inst.StartCoroutine(inst.InstallSoon());
            inst.StartCoroutine(inst.InstallSoon());
        }

        // สแกนหลายครั้ง กันปุ่มที่สร้างทีหลัง (UI สร้าง runtime) หรือ layout ยังไม่นิ่ง
        IEnumerator InstallSoon()
        {
            Install();
            yield return null;
            Install();
            yield return new WaitForSecondsRealtime(0.7f);
            Install();
        }

        void Install()
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var rt = b.transform as RectTransform;
                if (rt == null || rt.rect.width < 160f) continue;             // ข้ามปุ่มเล็ก/สวอตช์
                if (b.GetComponent<ButtonJuice>() == null) b.gameObject.AddComponent<ButtonJuice>();
            }

            // แผง popup ที่มีลูกชื่อ "Card" → ใส่ PanelPop (เด้งเข้านุ่ม ๆ)
            var rts = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rt in rts)
            {
                if (rt == null || rt.GetComponent<PanelPop>() != null) continue;
                if (rt.GetComponent<Canvas>() != null) continue;             // ไม่ใส่บน root canvas
                if (rt.Find("Card") != null) rt.gameObject.AddComponent<PanelPop>();
            }
        }
    }
}
