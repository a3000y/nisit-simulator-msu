using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // รีเลย์เทรด/ให้ของ — วางบน avatar prefab (มี NetworkObject)
    //   กันของหาย + กันของเพิ่มซ้ำ:
    //   1) ผู้ให้ "กันของไว้" (ย้ายออกจากกระเป๋าเข้าที่พักชั่วคราว) ทันทีที่กดให้ → กดให้ซ้ำ/ใช้ของชิ้นเดียวกันระหว่างรอไม่ได้
    //   2) Host บันทึกรายการเทรด (id, ผู้ให้, ผู้รับ, ของ) และรับผลจาก "ผู้รับตัวจริง" ได้ครั้งเดียวต่อรายการ
    //   3) ผู้รับใส่กระเป๋า → แจ้งผลกลับ Host → Host แจ้งผู้ให้: สำเร็จ = ปล่อยของที่กันไว้ · ไม่สำเร็จ/หมดเวลา/ผู้รับหลุด = คืนของเข้ากระเป๋า
    public class TradeRelay : NetworkBehaviour
    {
        [Tooltip("วินาทีจริงที่รอผลก่อนคืนของให้ผู้ให้")] public float timeoutSeconds = 12f;

        // ฝั่งผู้ให้ (owner): ของที่กันไว้ระหว่างรอผล
        class Escrow { public string item; public float deadline; }
        readonly Dictionary<int, Escrow> escrow = new Dictionary<int, Escrow>();
        int nextLocalId;

        // ฝั่ง Host: รายการเทรดที่รอผู้รับตอบ
        class ServerTrade { public ulong from, to; public FixedString64Bytes item; }
        static readonly Dictionary<long, ServerTrade> serverTrades = new Dictionary<long, ServerTrade>();

        public int PendingCount => escrow.Count;

        // owner เรียก: ขอส่งไอเทมให้ผู้เล่น toClient — กันของออกจากกระเป๋าก่อน (ไม่มีของ = ไม่ส่ง)
        public bool Give(ulong toClient, string itemName)
        {
            if (!IsOwner) return false;
            var inv = InventoryManager.Instance;
            if (inv == null || !inv.RemoveByName(itemName)) { HUDController.Toast("ไม่มีไอเทมนี้ในกระเป๋าแล้ว"); return false; }
            int id = ++nextLocalId;
            escrow[id] = new Escrow { item = itemName, deadline = Time.unscaledTime + timeoutSeconds };
            SendItemServerRpc(toClient, new FixedString64Bytes(itemName), id);
            return true;
        }

        void Update()
        {
            if (!IsOwner || escrow.Count == 0) return;
            List<int> expired = null;
            foreach (var kv in escrow)
                if (Time.unscaledTime >= kv.Value.deadline) (expired ??= new List<int>()).Add(kv.Key);
            if (expired == null) return;
            foreach (var id in expired) Refund(id, "ไม่มีการตอบกลับ");
        }

        public override void OnNetworkDespawn()
        {
            // ออกจากห้องระหว่างรอผล → คืนของที่กันไว้ (ไม่ให้หายไปเฉย ๆ)
            if (IsOwner) foreach (var id in new List<int>(escrow.Keys)) Refund(id, "ออกจากห้อง");
        }

        void Refund(int id, string why)
        {
            if (!escrow.TryGetValue(id, out var e)) return;
            escrow.Remove(id);
            var inv = InventoryManager.Instance;
            if (inv != null) inv.AddByName(e.item);
            HUDController.Toast($"ให้ {e.item} ไม่สำเร็จ ({why}) — ของยังอยู่กับคุณ");
        }

        static long Key(ulong from, int localId) => ((long)from << 32) ^ (uint)localId;

        [ServerRpc]
        void SendItemServerRpc(ulong toClient, FixedString64Bytes item, int localId, ServerRpcParams sp = default)
        {
            ulong from = sp.Receive.SenderClientId;
            bool valid = toClient != from && NetworkManager.ConnectedClients.ContainsKey(toClient) && item.Length > 0;
            if (!valid)
            {
                Debug.LogWarning($"[Net][Trade] ปฏิเสธคำขอจาก {from} → {toClient} ({item})");
                ConfirmClientRpc(localId, item, false, To(from));
                return;
            }
            ServerRegister(from, localId, toClient, item);
            ReceiveItemClientRpc(from, localId, item, To(toClient));
        }

        [ClientRpc]
        void ReceiveItemClientRpc(ulong from, int localId, FixedString64Bytes item, ClientRpcParams p = default)
        {
            var inv = InventoryManager.Instance;
            bool ok = inv != null && inv.AddByName(item.ToString());
            if (ok) HUDController.Toast($"ได้รับ {item} จากผู้เล่น {from + 1}! 🎁");
            ResultServerRpc(from, localId, ok);   // แจ้งผลกลับ Host
        }

        [ServerRpc(RequireOwnership = false)]
        void ResultServerRpc(ulong giver, int localId, bool ok, ServerRpcParams sp = default)
        {
            // รับผลเฉพาะจากผู้รับตัวจริง และครั้งเดียวต่อรายการ (กันเครื่องอื่นปลอมผล/ส่งซ้ำ)
            if (!ServerTryComplete(giver, localId, sp.Receive.SenderClientId, out var item))
            {
                Debug.LogWarning($"[Net][Trade] ผลเทรดไม่ถูกต้องจาก {sp.Receive.SenderClientId} (giver={giver}, id={localId}) — ไม่สนใจ");
                return;
            }
            ConfirmClientRpc(localId, item, ok, To(giver));
        }

        [ClientRpc]
        void ConfirmClientRpc(int localId, FixedString64Bytes item, bool ok, ClientRpcParams p = default)
        {
            if (!escrow.ContainsKey(localId)) return;   // หมดเวลาและคืนของไปแล้ว / ผลซ้ำ
            if (ok)
            {
                escrow.Remove(localId);                  // ของอยู่กับผู้รับแล้ว — ปล่อยของที่กันไว้
                HUDController.Toast($"ให้ {item} สำเร็จ!");
            }
            else Refund(localId, "กระเป๋าเขาเต็ม/ส่งไม่ได้");
        }

        // ---------- ตารางเทรดฝั่ง Host (static ล้วน ทดสอบได้) ----------
        public static void ServerRegister(ulong from, int localId, ulong to, FixedString64Bytes item)
            => serverTrades[Key(from, localId)] = new ServerTrade { from = from, to = to, item = item };

        // true = ผลนี้มาจากผู้รับตัวจริงของรายการที่ยังค้าง (และปิดรายการทันที — ส่งซ้ำไม่ผ่าน)
        public static bool ServerTryComplete(ulong giver, int localId, ulong sender, out FixedString64Bytes item)
        {
            item = default;
            long key = Key(giver, localId);
            if (!serverTrades.TryGetValue(key, out var t) || t.to != sender) return false;
            serverTrades.Remove(key);
            item = t.item;
            return true;
        }

        public static int ServerPendingCount => serverTrades.Count;
        public static void ServerClearAll() => serverTrades.Clear();

        // Host: ผู้เล่นหลุด → ล้างรายการที่ค้างของคนนั้น (ผู้ให้จะคืนของเองเมื่อหมดเวลา)
        public static void ServerForgetClient(ulong id)
        {
            var dead = new List<long>();
            foreach (var kv in serverTrades) if (kv.Value.from == id || kv.Value.to == id) dead.Add(kv.Key);
            foreach (var k in dead) serverTrades.Remove(k);
        }

        static ClientRpcParams To(ulong id)
            => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } };
    }
}
