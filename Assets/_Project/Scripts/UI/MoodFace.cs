using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.Stats;

namespace NisitSimulator.UI
{
    // เปลี่ยนสีหน้าอวตารในพอร์ตเทรตตามค่าสถานะ (ยิ้ม / เฉย / เศร้า-เหนื่อย)
    //   ตัวอ้างอิงถูกเซ็ตโดย M3HudBuilder
    public class MoodFace : MonoBehaviour
    {
        public Image mouth;
        public Sprite smile, flat, frown;
        public RectTransform leftEye, rightEye;   // สำหรับหรี่ตาตอนเหนื่อย

        private PlayerStats stats;
        private int last = -99;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            Apply(1);
        }

        void Update()
        {
            if (stats == null) { stats = Object.FindFirstObjectByType<PlayerStats>(); return; }

            int mood;
            if (stats.Energy < 25f || stats.Hunger < 20f || stats.Health < 30f) mood = -1;   // เหนื่อย/หิว/ป่วย
            else if (stats.Satisfaction >= 60f && stats.Energy >= 45f)           mood = 1;    // มีความสุข
            else                                                                mood = 0;    // เฉยๆ
            Apply(mood);
        }

        void Apply(int mood)
        {
            if (mood == last) return;
            last = mood;

            if (mouth != null)
                mouth.sprite = mood > 0 ? smile : (mood < 0 ? frown : flat);

            // หรี่ตาตอนเหนื่อย (บีบตาให้แบน)
            float eyeY = mood < 0 ? 0.5f : 1f;
            if (leftEye != null)  leftEye.localScale  = new Vector3(1f, eyeY, 1f);
            if (rightEye != null) rightEye.localScale = new Vector3(1f, eyeY, 1f);
        }
    }
}
