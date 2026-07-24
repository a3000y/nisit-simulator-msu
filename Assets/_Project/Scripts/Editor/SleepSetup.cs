#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // ทำให้ "เตียง" ในหอพักนอนได้ — กด E ที่เตียง → นอน (ข้ามไปเช้า)
    //   สร้าง marker "SleepSpot" (Layer Interactable) ที่เตียงทุกตัวใน Interiors
    // ใช้: เมนู  Nisit -> Setup Sleep   (หลัง Build Interiors)
    public static class SleepSetup
    {
        [MenuItem("Nisit/Setup Sleep", false, 11)]
        public static void Setup()
        {
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) { EditorUtility.DisplayDialog("Nisit", "ไม่มี Layer Interactable — รัน Setup M1 Scene ก่อน", "OK"); return; }

            var interiors = GameObject.Find("Interiors");
            if (interiors == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ Interiors — รัน Build Interiors ก่อน", "OK"); return; }

            float unit = 1.3f;
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.height > 0.01f) unit = cc.height * Mathf.Abs(player.transform.lossyScale.y);
            }

            var oldRoot = GameObject.Find("SleepSpots");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            var root = new GameObject("SleepSpots").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Setup Sleep");

            int beds = 0;
            foreach (Transform c in interiors.transform)
                if (c.name.StartsWith("bed")) { MakeSleep(root, c, unit, layer); beds++; }

            EditorUtility.SetDirty(root.gameObject);
            Debug.Log($"<color=lime>[Nisit] ✅ ทำเตียงนอนได้ {beds} เตียง — เดินเข้าใกล้เตียง กด E เพื่อนอน (ข้ามไปเช้า+ฟื้นพลัง)</color>");
            EditorUtility.DisplayDialog("Nisit", $"ทำเตียงนอนได้ {beds} เตียง!\n\nเข้าหอพัก → เดินไปเตียง → กด E → นอน\n(ข้ามไปเช้า + พลังงานเต็ม)\n\nกด Ctrl+S แล้ว Play", "OK");
        }

        private static void MakeSleep(Transform root, Transform bed, float unit, int layer)
        {
            var wb = Bounds(bed);
            var marker = new GameObject("SleepSpot");
            marker.transform.SetParent(root);
            marker.transform.position = new Vector3(wb.center.x, 0f, wb.center.z);
            marker.layer = layer;

            var col = marker.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, wb.center.y, 0f);
            col.size = new Vector3(wb.size.x, wb.size.y, wb.size.z) + Vector3.one * (0.5f * unit);

            marker.AddComponent<SleepStation>();
        }

        private static Bounds Bounds(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
