using Unity.Collections;
using Unity.Netcode;

namespace NisitSimulator.Net
{
    // รีเลย์แชท — วางบน avatar prefab (มี NetworkObject)
    //   owner ส่งข้อความ → server → กระจายให้ทุกคนแสดงใน ChatUI
    public class ChatRelay : NetworkBehaviour
    {
        [ServerRpc]
        public void SendChatServerRpc(FixedString512Bytes msg)
        {
            ReceiveChatClientRpc(OwnerClientId, msg);
        }

        [ClientRpc]
        void ReceiveChatClientRpc(ulong sender, FixedString512Bytes msg)
        {
            ChatUI.Append($"ผู้เล่น {sender + 1}: {msg}");
        }
    }
}
