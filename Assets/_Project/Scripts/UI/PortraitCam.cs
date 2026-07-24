using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // กล้องเรนเดอร์ตัวละคร 3D จริงลงกรอบพอร์ตเทรต (RenderTexture) — เกาะหน้าตัวละคร ขยับตาม
    //   ตัวอ้างอิงถูกเซ็ตโดย M3HudBuilder (display, target, cullingMask)
    [RequireComponent(typeof(Camera))]
    public class PortraitCam : MonoBehaviour
    {
        public RawImage display;
        public Transform target;
        public Color background = new Color(0.56f, 0.80f, 0.93f);

        private Camera cam;
        private float headY = 1.5f, charH = 1.7f;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (target == null) { var p = GameObject.Find("Player"); if (p != null) target = p.transform; }

            var rt = new RenderTexture(256, 256, 16) { name = "PortraitRT" };
            rt.Create();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.03f;
            if (display != null) { display.texture = rt; display.color = Color.white; }

            if (target != null)
            {
                var r = target.GetComponentInChildren<Renderer>();
                if (r != null) { charH = r.bounds.size.y; headY = r.bounds.max.y - target.position.y; }
            }
            Frame();
        }

        void LateUpdate() => Frame();

        void Frame()
        {
            if (target == null) return;
            Vector3 head = target.position + Vector3.up * (headY * 0.92f);
            transform.position = head + target.forward * (charH * 0.95f) + Vector3.up * (charH * 0.04f);
            transform.LookAt(head);
        }
    }
}
