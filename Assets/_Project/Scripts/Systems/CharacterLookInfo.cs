using UnityEngine;

namespace NisitSimulator.Systems
{
    // ข้อมูลติดตัว prefab ชุด (ใส่โดย Nisit -> Setup Synty Characters)
    //   presetHair = ผม/หนวดติดมากับโมเดล (POLYGON City) → ไม่ใส่ทรงผม/หนวดเพิ่ม
    public class CharacterLookInfo : MonoBehaviour
    {
        public int gender;          // 0 = ชาย, 1 = หญิง
        public bool presetHair;
    }

}
