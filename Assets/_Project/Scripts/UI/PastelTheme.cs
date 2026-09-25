using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace NisitSimulator.UI
{
    // แปลงธีม UI เป็น "พาสเทลอ่อน" ตอนรัน — การ์ดเข้ม(กรมท่า)→สว่างพาสเทล + ตัวอักษรขาว→เข้ม
    //   ทำงานทุกฉากอัตโนมัติ (ไม่ต้อง re-bake) · ปรับสีได้ที่ค่าคงที่ด้านล่าง
    public class PastelTheme : MonoBehaviour
    {
        // สีการ์ดพาสเทล + สีตัวอักษรเข้ม
        static readonly Color CardCol = new Color(0.96f, 0.94f, 0.99f);
        static readonly Color TextDark = new Color(0.28f, 0.24f, 0.40f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("PastelTheme");
            DontDestroyOnLoad(go);
            var inst = go.AddComponent<PastelTheme>();
            SceneManager.sceneLoaded += (s, m) => inst.StartCoroutine(inst.Soon());
            inst.StartCoroutine(inst.Soon());
        }

        IEnumerator Soon()
        {
            Apply(); yield return null;
            Apply(); yield return new WaitForSecondsRealtime(0.7f);
            Apply();
        }

        void Apply()
        {
            // การ์ด: Image สีกรมท่าเข้ม + ทึบ → เปลี่ยนเป็นพาสเทลอ่อน (คงค่า alpha)
            foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (img == null || img.GetComponent<Canvas>() != null) continue;
                var c = img.color;
                if (c.a >= 0.88f && c.r < 0.22f && c.g < 0.24f && c.b < 0.34f)
                    img.color = new Color(CardCol.r, CardCol.g, CardCol.b, c.a);
            }

            // ตัวอักษร: สีขาว/เทาอ่อน (ไม่มีสีสัน) → เปลี่ยนเป็นเข้ม (คงสี accent เช่น ทอง/เขียวไว้)
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                var c = t.color;
                float mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (mn > 0.72f && (mx - mn) < 0.12f)   // ขาว/เทาอ่อน (saturation ต่ำ + สว่าง)
                    t.color = new Color(TextDark.r, TextDark.g, TextDark.b, c.a);
            }
        }
    }
}
