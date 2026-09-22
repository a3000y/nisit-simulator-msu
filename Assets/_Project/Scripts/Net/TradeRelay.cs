using Unity.Collections;
using Unity.Netcode;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // รีเลย์เทรด/ให้ของ — วางบน avatar prefab (มี NetworkObject)
    //   owner ส่งของ → server → ส่งต่อเฉพาะผู้รับ → เข้ากระเป๋าผู้รับ
    public class TradeRelay : NetworkBehaviour
    {
        // owner เรียก: ส่งไอเทม itemName ให้ผู้เล่น toClient
        public void Give(ulong toClient, string itemName)
        {
            if (!IsOwner) return;
            SendItemServerRpc(toClient, new FixedString64Bytes(itemName));
        }

        [ServerRpc]
        void SendItemServerRpc(ulong toClient, FixedString64Bytes item)
        {
            var p = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { toClient } } };
            ReceiveItemClientRpc(OwnerClientId, item, p);
        }

        [ClientRpc]
        void ReceiveItemClientRpc(ulong from, FixedString64Bytes item, ClientRpcParams p = default)
        {
            var inv = InventoryManager.Instance;
            if (inv != null && inv.AddByName(item.ToString()))
                HUDController.Toast($"ได้รับ {item} จากผู้เล่น {from + 1}! 🎁");
        }
    }
}
