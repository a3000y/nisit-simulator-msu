using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.Net;

namespace NisitSimulator.UI
{
    // โชว์ตัวละครที่เลือกไว้ (แบบ/สี/ของแต่ง) หมุนช้า ๆ ด้านขวาของเมนูหลัก — display อย่างเดียว
    //   เวที(กล้อง/ไฟ/จุดวาง) + RawImage สร้างโดย M4MenuBuilder
    public class MenuCharacterPreview : MonoBehaviour
    {
        public Camera cam;
        public Transform root;
        public RawImage image;
        public float rotate = 20f;
        public float targetHeight = 1.75f;

        GameObject current;
        RenderTexture rt;

        void Start()
        {
            if (cam != null && image != null)
            {
                rt = new RenderTexture(560, 760, 16) { name = "MenuCharRT" };
                cam.targetTexture = rt; image.texture = rt; cam.enabled = true;
            }
            Spawn();
        }

        void Update()
        {
            if (current != null && root != null) root.Rotate(0f, rotate * Time.unscaledDeltaTime, 0f);
        }

        void OnDestroy()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        void Spawn()
        {
            if (root == null) return;
            var cat = CharacterCatalog.Load();
            var prefab = cat != null ? (cat.Model(GameSession.PlayerModel) ?? cat.Model(0)) : null;
            if (prefab == null) return;

            current = Instantiate(prefab, root);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            current.transform.localScale = Vector3.one;

            var anim = current.GetComponentInChildren<Animator>();
            if (anim != null && cat.controller != null) anim.runtimeAnimatorController = cat.controller;

            // auto-scale + วางเท้าที่ระดับ root
            var rends = current.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) current.transform.localScale *= targetHeight / b.size.y;
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                current.transform.position += new Vector3(0f, root.position.y - b.min.y, 0f);
            }

            NetworkAvatar.ApplyColor(current, GameSession.PlayerColor);
            CharacterAccessories.Apply(current.transform, GameSession.PlayerAccessories);
        }
    }
}
