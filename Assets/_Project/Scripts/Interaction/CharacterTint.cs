using UnityEngine;

namespace NisitSimulator.Interaction
{
    // ย้อมสีตัวละคร/สัตว์ทั้งตัวด้วย MaterialPropertyBlock (ไม่แก้ material asset — ปลอดภัย)
    //   ใช้ให้ NPC ที่ใช้โมเดลเดียวกันดูแตกต่างกัน (คนละโทนสี)
    public class CharacterTint : MonoBehaviour
    {
        public Color tint = Color.white;

        void Start() { Apply(); }

        public void Apply()
        {
            if (tint == Color.white) return;   // ขาว = ไม่ย้อม (ใช้สีจริงของโมเดล)
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", tint);   // URP Lit
                mpb.SetColor("_Color", tint);       // Standard/เผื่อไว้
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
