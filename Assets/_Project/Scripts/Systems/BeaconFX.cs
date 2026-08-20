using UnityEngine;

namespace NisitSimulator.Systems
{
    // เอฟเฟกต์เสาสัญญาณภารกิจ: ลำแสงเด้งขึ้นลง + วงแสงที่พื้นพัลส์ (ให้เด่น เห็นง่าย)
    public class BeaconFX : MonoBehaviour
    {
        public Transform beam;   // ลำแสง
        public Transform ring;   // วงที่พื้น
        public float bob = 0.6f, bobSpeed = 2.2f, spin = 60f;

        private float t;
        private float beamBaseY;
        private float ringBase = 2.5f;

        void Start()
        {
            if (beam != null) beamBaseY = beam.localPosition.y;
            if (ring != null) ringBase = ring.localScale.x;
        }

        void Update()
        {
            t += Time.deltaTime;
            float s = Mathf.Sin(t * bobSpeed);

            if (beam != null)
            {
                var p = beam.localPosition; p.y = beamBaseY + s * bob; beam.localPosition = p;
                beam.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
            }
            if (ring != null)
            {
                float k = ringBase + s * 0.5f;
                ring.localScale = new Vector3(k, ring.localScale.y, k);
                ring.Rotate(0f, -spin * 0.5f * Time.deltaTime, 0f, Space.Self);
            }
        }
    }
}
