using UnityEngine;

namespace NisitSimulator.Data
{
    // ประเภทไอเทม (อาหาร=เพิ่มพลังงาน, ชุด=เปลี่ยนเครื่องแต่งกาย ฯลฯ)
    public enum ItemType { Food, Drink, Clothing, Book, Misc }

    // นิยามไอเทม 1 ชิ้น — สร้างเป็นไฟล์ asset ได้จากเมนู
    // คลิกขวาใน Project → Create → Nisit → Item
    [CreateAssetMenu(fileName = "NewItem", menuName = "Nisit/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("ข้อมูลทั่วไป")]
        public string itemId;          // รหัสไม่ซ้ำ เช่น "food_burger"
        public string displayName;     // ชื่อที่โชว์ เช่น "เบอร์เกอร์"
        [TextArea] public string description;
        public Sprite icon;            // ไอคอนในกระเป๋า/ร้านค้า
        public ItemType type;
        public int price = 5;          // ราคาในร้านค้า (ตามตาราง 3.3)

        [Header("ผลเมื่อใช้ (บวก=เพิ่ม, ลบ=ลด)")]
        public float energyEffect = 0f;
        public float healthEffect = 0f;
        public float hungerEffect = 0f;
        public float knowledgeEffect = 0f;

        // TODO (M6): เมธอด ApplyEffect(PlayerStats) เมื่อผู้เล่นกิน/ใช้ไอเทม
    }
}
