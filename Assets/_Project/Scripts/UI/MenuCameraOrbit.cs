using UnityEngine;

namespace NisitSimulator.UI
{
    // กล้องหมุนช้าๆ รอบฉากเมนู (ใส่ที่ Main Camera ของฉากเมนู)
    public class MenuCameraOrbit : MonoBehaviour
    {
        public Transform pivot;      // จุดศูนย์กลางที่หมุนรอบ
        public float speed = 5f;     // องศาต่อวินาที
        public float distance = 18f;
        public float height = 7f;
        public float lookHeight = 2.5f;
        public float startAngle = 20f;

        private float angle;

        void Start()
        {
            angle = startAngle;
            if (pivot == null)
            {
                var g = new GameObject("MenuPivot");
                g.transform.position = Vector3.zero;
                pivot = g.transform;
            }
        }

        void LateUpdate()
        {
            angle += speed * Time.deltaTime;
            Vector3 pos = pivot.position + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height, -distance);
            transform.position = pos;
            transform.LookAt(pivot.position + Vector3.up * lookHeight);
        }
    }
}
