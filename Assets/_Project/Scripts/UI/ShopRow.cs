using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.UI
{
    // ตัวอ้างอิงของ 1 แถวสินค้าในร้าน (ตั้งโดย Editor tool, ShopController โคลนไปใช้)
    public class ShopRow : MonoBehaviour
    {
        public Image iconBg;   // วงกลมสีตามหมวด
        public Image icon;     // รูปอาหารจริง (bake จากโมเดล)
        public TMP_Text nameText;
        public TMP_Text effectText;
        public TMP_Text priceText;
        public Button buyButton;
        public GameObject qtyBadge;   // ป้ายจำนวนในตะกร้า (ซ่อนเมื่อ 0)
        public TMP_Text qtyText;
    }
}
