using Unity.Collections;
using Unity.Netcode;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // รีเลย์เทรด/ให้ของ — วางบน avatar prefab (มี NetworkObject)
    //   ปลอดภัยจากของหาย: ลบของจากผู้ให้ "เมื่อผู้รับยืนยันรับสำเร็จ" เท่านั้น (ack)
    //   owner ส่ง → server → ผู้รับ (ลองเข้ากระเป๋า) → แจ้งผลกลับ server → ผู้ให้ (ลบของถ้าสำเร็จ)
    public class TradeRelay : NetworkBehaviour
    {
        // owner เรียก: ขอส่งไอเทมให้ผู้เล่น toClient (ยังไม่ลบของจนกว่าจะยืนยัน)
        public void Give(ulong toClient, string itemName)
        {
            if (!IsOwner) return;
            SendItemServerRpc(toClient, new FixedString64Bytes(itemName));
        }

        [ServerRpc]
        void SendItemServerRpc(ulong toClient, FixedString64Bytes item, ServerRpcParams sp = default)
        {
            ulong from = sp.Receive.SenderClientId;
            ReceiveItemClientRpc(from, item, To(toClient));
        }

        [ClientRpc]
        void ReceiveItemClientRpc(ulong from, FixedString64Bytes item, ClientRpcParams p = default)
        {
            var inv = InventoryManager.Instance;
            bool ok = inv != null && inv.AddByName(item.ToString());
            if (ok) HUDController.Toast($"ได้รับ {item} จากผู้เล่น {from + 1}! 🎁");
            ResultServerRpc(from, item, ok);   // แจ้งผลกลับไปหาผู้ให้
        }

        [ServerRpc(RequireOwnership = false)]
        void ResultServerRpc(ulong toGiver, FixedString64Bytes item, bool ok, ServerRpcParams sp = default)
        {
            ConfirmClientRpc(item, ok, To(toGiver));
        }

        [ClientRpc]
        void ConfirmClientRpc(FixedString64Bytes item, bool ok, ClientRpcParams p = default)
        {
            var inv = InventoryManager.Instance;
            if (ok)
            {
                if (inv != null) inv.RemoveByName(item.ToString());   // ลบของ "หลังยืนยันสำเร็จ" → ไม่หาย
                HUDController.Toast($"ให้ {item} สำเร็จ!");
            }
            else HUDController.Toast($"ให้ {item} ไม่สำเร็จ (กระเป๋าเขาเต็ม/ไม่มีของ) — ของยังอยู่กับคุณ");
        }

        static ClientRpcParams To(ulong id)
            => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } };
    }
}
