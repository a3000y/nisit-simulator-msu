using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NisitSimulator.CameraRig
{
    // ทำให้ผนัง/หลังคาที่บังตัวละครโปร่งใสอัตโนมัติ (Wall Fading / X-Ray)
    // ใส่ไว้ที่กล้องหลัก ร่วมกับ IsometricCameraRig
    //
    // วิธีทำงาน: ยิง SphereCast จากกล้องไปหาตัวละครเป็นช่วง ๆ
    // วัตถุที่โดนถือว่า "บังอยู่" แล้วค่อย ๆ ลดค่า alpha ลง พอไม่บังแล้วก็คืนค่าเดิม
    //
    // หมายเหตุเรื่องวัสดุ: URP/Lit เป็นวัสดุทึบ ปรับ alpha เฉย ๆ ไม่มีผล
    // ต้องสลับ Surface Type เป็น Transparent ด้วย จึงสร้างสำเนาวัสดุไว้ใช้ตอนจาง
    // แล้วคืนวัสดุต้นฉบับเมื่อจางจบ เพื่อไม่ให้วัสดุในโปรเจกต์ถูกแก้ถาวร
    [DefaultExecutionOrder(100)]
    public class WallFader : MonoBehaviour
    {
        [Header("เป้าหมายที่ต้องมองเห็นเสมอ")]
        [Tooltip("ปล่อยว่าง = หาตัวที่แท็ก Player ให้เอง")]
        public Transform target;
        [Tooltip("ยกจุดเล็งขึ้นจากเท้าตัวละคร (กันไปโดนพื้น)")]
        public float targetHeightOffset = 1.2f;

        [Header("การตรวจจับสิ่งกีดขวาง")]
        [Tooltip("เลเยอร์ที่ถือว่าเป็นผนัง/อาคาร โดยปกติคือ Default")]
        public Renderer[] additionalObstructions = new Renderer[0]; // Roof panels without physics colliders
        public LayerMask obstructionMask = 1;         // 1 = Default
        [Tooltip("รัศมีลำแสง ยิ่งกว้างยิ่งจับผนังที่เฉียด ๆ ได้")]
        public float castRadius = 0.45f;
        [Tooltip("เว้นระยะก่อนถึงตัวละคร กันจับตัวละครเอง")]
        public float endPadding = 0.6f;
        [Tooltip("ตรวจทุกกี่วินาที (ไม่ต้องตรวจทุกเฟรม)")]
        public float checkInterval = 0.1f;

        [Header("ความโปร่งใส")]
        [Range(0f, 1f)]
        [Tooltip("ค่า alpha ตอนจางเต็มที่")]
        public float fadedAlpha = 0.25f;
        [Tooltip("ความเร็วในการจาง/คืนค่า")]
        public float fadeSpeed = 8f;

        // สถานะของ renderer แต่ละตัวที่กำลังจางอยู่
        private class FadeEntry
        {
            public Renderer Renderer;
            public Material[] Original;    // วัสดุต้นฉบับ (shared ไม่ถูกแก้)
            public Material[] Instances;   // สำเนาที่ทำให้โปร่งใสได้
            public float Alpha = 1f;
            public bool Blocking;
        }

        private readonly Dictionary<Renderer, FadeEntry> _entries = new Dictionary<Renderer, FadeEntry>();
        // renderer ที่มี WallFader ตัวอื่นจัดการอยู่ — ถ้าสองตัวแย่งกัน ตัวหนึ่งจะจำ "วัสดุต้นฉบับ" เป็นสำเนาที่ถูกทำลายไปแล้ว => สีชมพู
        private static readonly HashSet<Renderer> s_claimed = new HashSet<Renderer>();
        private readonly List<Renderer> _hitBuffer = new List<Renderer>();
        private readonly List<Renderer> _finished = new List<Renderer>();
        private float _timer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId   = Shader.PropertyToID("_Surface");
        private static readonly int BlendId     = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId  = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId  = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId    = Shader.PropertyToID("_ZWrite");

        void Start()
        {
            if (target == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) p = GameObject.Find("Player");
                if (p != null) target = p.transform;
            }
        }

        void LateUpdate()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = Mathf.Max(0.02f, checkInterval);
                Scan();
            }
            StepFade();
        }

        // หาว่าตอนนี้มีอะไรบังตัวละครอยู่บ้าง
        private void Scan()
        {
            foreach (var e in _entries.Values) e.Blocking = false;
            if (target == null) return;

            Vector3 from = transform.position;
            Vector3 to   = target.position + Vector3.up * targetHeightOffset;
            Vector3 dir  = to - from;
            float dist   = dir.magnitude - endPadding;
            if (dist <= 0.01f) return;
            dir /= (dist + endPadding);

            var hits = Physics.SphereCastAll(from, castRadius, dir, dist, obstructionMask, QueryTriggerInteraction.Ignore);
            _hitBuffer.Clear();
            for (int i = 0; i < hits.Length; i++)
            {
                var t = hits[i].transform;
                if (target != null && (t == target || t.IsChildOf(target))) continue;

                var rends = t.GetComponentsInChildren<Renderer>();
                for (int r = 0; r < rends.Length; r++)
                {
                    var rend = rends[r];
                    if (rend == null || !rend.enabled) continue;
                    if (rend is ParticleSystemRenderer) continue;
                    _hitBuffer.Add(rend);
                }
            }

            // Render-only roof panels still need to fade when they cross the view ray.
            var viewRay = new Ray(from, dir);
            if (additionalObstructions != null)
            {
                foreach (var rend in additionalObstructions)
                {
                    if (rend == null || !rend.enabled || !rend.gameObject.activeInHierarchy) continue;
                    if (rend.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                    if (rend.transform == target || rend.transform.IsChildOf(target)) continue;
                    var bounds = rend.bounds;
                    bounds.Expand(castRadius * 2f);
                    if (bounds.IntersectRay(viewRay, out float hitDistance) && hitDistance <= dist && !_hitBuffer.Contains(rend))
                        _hitBuffer.Add(rend);
                }
            }

            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                var rend = _hitBuffer[i];
                if (!_entries.TryGetValue(rend, out var entry))
                {
                    if (s_claimed.Contains(rend)) continue;
                    entry = CreateEntry(rend);
                    if (entry == null) continue;
                    _entries[rend] = entry;
                    s_claimed.Add(rend);
                }
                entry.Blocking = true;
            }
        }

        // เดินค่า alpha เข้าหาเป้าหมายทุกเฟรม
        private void StepFade()
        {
            if (_entries.Count == 0) return;
            _finished.Clear();

            foreach (var kv in _entries)
            {
                var e = kv.Value;
                if (e.Renderer == null) { _finished.Add(kv.Key); continue; }

                float want = e.Blocking ? fadedAlpha : 1f;
                e.Alpha = Mathf.MoveTowards(e.Alpha, want, fadeSpeed * Time.deltaTime);
                ApplyAlpha(e);

                // จางกลับเต็มแล้วและไม่บังอีก -> คืนวัสดุต้นฉบับ เลิกจัดการตัวนี้
                if (!e.Blocking && e.Alpha >= 0.999f) _finished.Add(kv.Key);
            }

            for (int i = 0; i < _finished.Count; i++)
            {
                if (_entries.TryGetValue(_finished[i], out var e)) Restore(e);
                _entries.Remove(_finished[i]);
                s_claimed.Remove(_finished[i]);
            }
        }

        private FadeEntry CreateEntry(Renderer rend)
        {
            var shared = rend.sharedMaterials;
            if (shared == null || shared.Length == 0) return null;

            var inst = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) { inst[i] = null; continue; }
                inst[i] = CreateFadeMaterial(shared[i]);
            }

            rend.materials = inst;
            return new FadeEntry { Renderer = rend, Original = shared, Instances = inst, Alpha = 1f };
        }

        // Shader Graph ของ Synty (Synty/Generic_Basic, Generic_Standard) สลับเป็น Transparent ตอนรันไม่ได้
        // ผลคือวัตถุกลายเป็นสีชมพู/หายไปตอนจาง จึงใช้ URP/Lit แทนเฉพาะสำเนาที่ใช้ตอนจาง
        private static Shader _urpLit;
        private static Material CreateFadeMaterial(Material src)
        {
            var shader = src.shader;
            bool needsSwap = shader != null && shader.name.StartsWith("Synty/");
            if (needsSwap && _urpLit == null) _urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material m;
            if (!needsSwap || _urpLit == null)
            {
                m = new Material(src);
            }
            else
            {
                m = new Material(_urpLit) { name = src.name + " (Fade)" };
                string albedo = src.HasProperty("_Albedo_Map") ? "_Albedo_Map" : (src.HasProperty("_BaseMap") ? "_BaseMap" : (src.HasProperty("_MainTex") ? "_MainTex" : null));
                if (albedo != null)
                {
                    m.SetTexture("_BaseMap", src.GetTexture(albedo));
                    m.SetTextureScale("_BaseMap", src.GetTextureScale(albedo));
                    m.SetTextureOffset("_BaseMap", src.GetTextureOffset(albedo));
                }
                if (src.HasProperty(BaseColorId)) m.SetColor(BaseColorId, src.GetColor(BaseColorId));
                else if (src.HasProperty(ColorId)) m.SetColor(BaseColorId, src.GetColor(ColorId));
                string normal = src.HasProperty("_Normal_Map") ? "_Normal_Map" : (src.HasProperty("_BumpMap") ? "_BumpMap" : null);
                if (normal != null && src.GetTexture(normal) != null)
                {
                    m.SetTexture("_BumpMap", src.GetTexture(normal));
                    m.EnableKeyword("_NORMALMAP");
                }
                if (src.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
                if (src.HasProperty("_Metallic")) m.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            }
            MakeTransparent(m);
            return m;
        }

        // สลับวัสดุให้รองรับความโปร่งใส (ทำกับสำเนาเท่านั้น)
        private static void MakeTransparent(Material m)
        {
            if (m.HasProperty(SurfaceId)) m.SetFloat(SurfaceId, 1f);   // 1 = Transparent
            if (m.HasProperty(BlendId))   m.SetFloat(BlendId, 0f);     // 0 = Alpha
            if (m.HasProperty(SrcBlendId)) m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            if (m.HasProperty(DstBlendId)) m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty(ZWriteId))   m.SetFloat(ZWriteId, 0f);

            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private void ApplyAlpha(FadeEntry e)
        {
            if (e.Instances == null) return;
            for (int i = 0; i < e.Instances.Length; i++)
            {
                var m = e.Instances[i];
                if (m == null) continue;
                int id = m.HasProperty(BaseColorId) ? BaseColorId : (m.HasProperty(ColorId) ? ColorId : -1);
                if (id < 0) continue;
                var c = m.GetColor(id);
                c.a = e.Alpha;
                m.SetColor(id, c);
            }
        }

        private void Restore(FadeEntry e)
        {
            if (e.Renderer != null) e.Renderer.materials = e.Original;
            if (e.Instances == null) return;
            for (int i = 0; i < e.Instances.Length; i++)
                if (e.Instances[i] != null) Destroy(e.Instances[i]);
            e.Instances = null;
        }

        void OnDisable()
        {
            foreach (var kv in _entries) { Restore(kv.Value); s_claimed.Remove(kv.Key); }
            _entries.Clear();
        }
    }
}
