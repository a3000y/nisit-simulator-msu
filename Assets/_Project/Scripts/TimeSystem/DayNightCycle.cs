using System;
using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Interaction;

namespace NisitSimulator.TimeSystem
{
    // ===== ช่วงของวัน =====
    public enum DayPhase { Dawn, Day, Dusk, Night }

    // ค่าแสงหนึ่งชุด (ปรับใน Inspector ได้ทั้งหมด)
    [Serializable]
    public class LightingPreset
    {
        [Header("ดวงอาทิตย์ / ดวงจันทร์ (Directional Light หลัก)")]
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1f;
        [Range(0f, 1f)] public float shadowStrength = 1f;

        [Header("Ambient (โหมด Trilight ของฉากเดิม)")]
        public Color ambientSky = new Color(0.85f, 0.88f, 0.95f);
        public Color ambientEquator = new Color(0.62f, 0.63f, 0.60f);
        public Color ambientGround = new Color(0.42f, 0.42f, 0.40f);
        [Range(0f, 1.5f)] public float reflectionIntensity = 1f;

        [Header("หมอก / ท้องฟ้า (Skybox Procedural)")]
        public Color fogColor = new Color(0.72f, 0.78f, 0.85f);
        public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
        [Min(0f)] public float skyExposure = 1.3f;
        [Range(0f, 1f)] public float atmosphereThickness = 1f;
        [Range(0f, 1f)] public float sunSize = 0.04f;

        public LightingPreset Clone() => (LightingPreset)MemberwiseClone();

        // ผสม a→b ลง into (ไม่สร้าง object ใหม่ทุกเฟรม)
        public static void Lerp(LightingPreset a, LightingPreset b, float t, LightingPreset into)
        {
            into.sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
            into.sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            into.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t);
            into.ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
            into.ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            into.ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
            into.reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t);
            into.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            into.skyTint = Color.Lerp(a.skyTint, b.skyTint, t);
            into.skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t);
            into.atmosphereThickness = Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, t);
            into.sunSize = Mathf.Lerp(a.sunSize, b.sunSize, t);
        }
    }

    // ===== ระบบกลางวัน–กลางคืน =====
    //   • อ่านเวลาจาก GameClock เดิมเท่านั้น (แหล่งเวลาเดียวกับ HUD/ตารางเรียน/การสอบ) — ไม่มีนาฬิกาของตัวเอง
    //   • หมุน Directional Light หลักของฉาก (ไม่สร้างดวงใหม่) + ผสมสี/ความสว่าง/Ambient/หมอก/ท้องฟ้าอย่างต่อเนื่อง
    //   • ฉากนี้ไม่มี lightmap ที่อบไว้ (แสงทั้งหมดเป็น Realtime) จึงเปลี่ยนตามเวลาได้จริง — ถ้าในอนาคตอบแสง
    //     ส่วนที่อบไว้จะ "ไม่" เปลี่ยนตามเวลา (ดู Validate() ที่เตือนใน Console)
    //   • ไฟถนน (Light ที่ชื่อขึ้นต้นด้วย "NightLight") เปิดช่วงค่ำ ปิดช่วงเช้า
    //   • อยู่ในอาคาร (InteriorManager) → ใช้ชุดแสงภายใน ไม่มืดตามข้างนอก
    //   • อัปเดตเป็นช่วง (updateInterval) + ทันทีเมื่อเวลากระโดด/เข้า-ออกอาคาร (โหลดเซฟ/นอน/Dev) — ไม่ทำงานหนักทุกเฟรม
    [DefaultExecutionOrder(-50)]
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("อ้างอิง (ปล่อยว่าง = หาเอง)")]
        public GameClock clock;
        [Tooltip("Directional Light ที่ใช้เป็นดวงอาทิตย์ (ว่าง = RenderSettings.sun หรือ Directional Light ดวงแรกในฉาก)")]
        public Light sun;

        [Header("ช่วงเวลา (ชั่วโมง 0–24)")]
        [Tooltip("เริ่มรุ่งเช้า")] public float dawnStart = 5f;
        [Tooltip("เริ่มกลางวัน")] public float dayStart = 7f;
        [Tooltip("เริ่มช่วงเย็น")] public float duskStart = 17f;
        [Tooltip("เริ่มกลางคืน")] public float nightStart = 19f;

        [Header("ทิศทางแสง")]
        [Tooltip("มุมทิศ (yaw) ของแสงตอนเที่ยง")] public float yawAtNoon = 40f;
        [Tooltip("แสงหมุนรอบกี่องศาต่อชั่วโมงเกม (15 = ครบรอบใน 24 ชม.)")] public float degreesPerHour = 15f;
        [Tooltip("มุมเงยของแสงตามชั่วโมง (แกน X = ชั่วโมง 0–24) · กลางคืนคือแสงจันทร์ มุมสูงพอให้เห็นทาง")]
        public AnimationCurve elevationByHour = DefaultElevation();

        [Header("ชุดแสงภายนอก")]
        public LightingPreset night = DefaultNight();
        public LightingPreset dawn = DefaultDawn();
        public LightingPreset day = new LightingPreset();   // = ค่าเดิมของฉาก (เก็บจากฉากตอนติดตั้ง)
        [Tooltip("false = ยังไม่เคยเก็บค่า \"กลางวัน\" จากฉาก → เก็บจากแสง/Ambient/ท้องฟ้าเดิมของฉากอัตโนมัติตอนเริ่ม")]
        public bool dayCapturedFromScene = false;
        public LightingPreset dusk = DefaultDusk();

        [Header("ชุดแสงภายในอาคาร (หอพัก/ตึกเรียน)")]
        public bool useInteriorLighting = true;
        public LightingPreset interiorDay = DefaultInteriorDay();
        public LightingPreset interiorNight = DefaultInteriorNight();
        [Tooltip("มุมเงยแสงตอนอยู่ในอาคาร (คงที่ ไม่หมุน)")] public float interiorElevation = 65f;

        [Header("ไฟภายนอก (ไฟถนน)")]
        [Tooltip("เวลาเปิดไฟ")] public float lightsOnHour = 18f;
        [Tooltip("เวลาปิดไฟ")] public float lightsOffHour = 6f;
        [Tooltip("ใช้เวลาเฟดกี่ชั่วโมงเกม")] public float lightsFadeHours = 0.5f;
        [Tooltip("ว่าง = หา Light ทุกดวงที่ชื่อขึ้นต้นด้วย NightLight อัตโนมัติ")]
        public List<Light> outdoorLights = new List<Light>();

        [Header("ส่วนที่ควบคุม")]
        public bool controlAmbient = true;
        public bool controlFog = true;
        public bool controlSkybox = true;

        [Header("ประสิทธิภาพ")]
        [Tooltip("อัปเดตแสงทุกกี่วินาทีจริง (เวลาเกมเดิน 3 นาที/วินาที → 0.1 วิ = 0.3 นาทีเกม ลื่นพอ)")]
        [Min(0.02f)] public float updateInterval = 0.1f;

        // ---- สถานะ ----
        public DayPhase CurrentPhase { get; private set; }
        public float NightFactor { get; private set; }        // 0 = กลางวัน · 1 = กลางคืน
        public float OutdoorLightsFactor { get; private set; } // 0 = ไฟถนนปิด · 1 = เปิดเต็ม
        public bool IndoorLighting { get; private set; }
        public float LastAppliedHour { get; private set; } = -1f;

        readonly LightingPreset current = new LightingPreset();
        readonly LightingPreset tmp = new LightingPreset();
        readonly List<float> outdoorBaseIntensity = new List<float>();
        float nextUpdate;
        bool hooked;

        // ค่าเดิมของ RenderSettings (คืนเมื่อถูกทำลาย — กันค่าจาก Play Mode ค้าง)
        Material originalSkybox, skyInstance;
        Color origSky, origEq, origGround, origFog; float origRefl;
        bool savedOriginals;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            ResolveRefs();
            SaveOriginals();
            if (!dayCapturedFromScene) { day = CaptureFromScene(sun); dayCapturedFromScene = true; }
            SetupSkyboxInstance();
            CollectOutdoorLights();
            Hook();
            Validate();
        }

        void OnEnable() { Hook(); }

        void Start()
        {
            ApplyNow();   // ก่อนเฟรมแรกถูกวาด (ผู้โหลดเซฟเรียก ApplyNow ซ้ำหลังคืนเวลา)
        }

        void OnDisable() { Unhook(); }

        void OnDestroy()
        {
            Unhook();
            if (Instance == this) Instance = null;
            RestoreOriginals();
        }

        void Hook()
        {
            if (hooked || (Instance != null && Instance != this)) return;
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (clock != null) { clock.OnTimeChanged += OnClockTime; clock.OnDayChanged += OnClockDay; }
            InteriorManager.OnInsideChanged += OnInsideChanged;
            hooked = true;
        }

        void Unhook()
        {
            if (!hooked) return;
            if (clock != null) { clock.OnTimeChanged -= OnClockTime; clock.OnDayChanged -= OnClockDay; }
            InteriorManager.OnInsideChanged -= OnInsideChanged;
            hooked = false;
        }

        // เวลาเปลี่ยนทีละนาทีปกติไม่ต้องทำอะไร (Update ทำเป็นช่วงอยู่แล้ว) · ถ้า "กระโดด" (โหลดเซฟ/นอน/Dev) → อัปเดตทันที
        void OnClockTime(int h, int m)
        {
            if (clock == null) return;
            float cur = clock.HourFloat;
            if (LastAppliedHour < 0f || HourDistance(cur, LastAppliedHour) > 0.25f) ApplyNow();
        }
        void OnClockDay(int d) => ApplyNow();
        void OnInsideChanged(bool inside) => ApplyNow();

        void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + updateInterval;
            Apply();
        }

        // บังคับอัปเดตทันที (เรียกได้จากระบบอื่น เช่น หลังโหลดเซฟ/ตื่นนอน/วาร์ป)
        public void ApplyNow()
        {
            nextUpdate = Time.unscaledTime + updateInterval;
            Apply();
        }

        void Apply()
        {
            ResolveRefs();
            if (clock == null) return;
            float h = clock.HourFloat;
            LastAppliedHour = h;

            CurrentPhase = PhaseAt(h, dawnStart, dayStart, duskStart, nightStart);
            NightFactor = ComputeNightFactor(h);
            OutdoorLightsFactor = ComputeLightsFactor(h, lightsOnHour, lightsOffHour, lightsFadeHours);

            bool inside = useInteriorLighting && InteriorManager.Instance != null && InteriorManager.Instance.IsInside;
            IndoorLighting = inside;

            if (inside) LightingPreset.Lerp(interiorDay, interiorNight, NightFactor, current);
            else EvaluateOutdoor(h, current);

            // ---- ดวงอาทิตย์/ดวงจันทร์ ----
            if (sun != null)
            {
                float yaw = inside ? yawAtNoon : yawAtNoon + (h - 12f) * degreesPerHour;
                float elev = inside ? interiorElevation : Mathf.Clamp(elevationByHour.Evaluate(h), 2f, 89f);
                sun.transform.rotation = Quaternion.Euler(elev, yaw, 0f);
                sun.color = current.sunColor;
                sun.intensity = current.sunIntensity;
                sun.shadowStrength = current.shadowStrength;
            }

            if (controlAmbient)
            {
                RenderSettings.ambientSkyColor = current.ambientSky;
                RenderSettings.ambientEquatorColor = current.ambientEquator;
                RenderSettings.ambientGroundColor = current.ambientGround;
                RenderSettings.reflectionIntensity = current.reflectionIntensity;
            }
            if (controlFog) RenderSettings.fogColor = current.fogColor;
            if (controlSkybox && skyInstance != null)
            {
                SetIf(skyInstance, "_SkyTint", current.skyTint);
                SetIf(skyInstance, "_Exposure", current.skyExposure);
                SetIf(skyInstance, "_AtmosphereThickness", current.atmosphereThickness);
                SetIf(skyInstance, "_SunSize", current.sunSize);
            }

            // ---- ไฟถนน: เปิดเฉพาะตอนมืด + ปิด component เมื่อไม่ใช้ (ไม่เสียค่าเรนเดอร์ตอนกลางวัน) ----
            for (int i = 0; i < outdoorLights.Count; i++)
            {
                var l = outdoorLights[i];
                if (l == null) continue;
                float baseI = i < outdoorBaseIntensity.Count ? outdoorBaseIntensity[i] : 1f;
                float k = inside ? 0f : OutdoorLightsFactor;   // อยู่ในอาคารมองไม่เห็นไฟถนนอยู่แล้ว
                l.intensity = baseI * k;
                bool on = k > 0.001f;
                if (l.enabled != on) l.enabled = on;
            }
        }

        void EvaluateOutdoor(float h, LightingPreset into)
        {
            if (h >= dawnStart && h < dayStart)
            {
                float t = Mathf.InverseLerp(dawnStart, dayStart, h);
                if (t < 0.5f) LightingPreset.Lerp(night, dawn, Smooth(t * 2f), into);
                else LightingPreset.Lerp(dawn, day, Smooth((t - 0.5f) * 2f), into);
            }
            else if (h >= dayStart && h < duskStart) LightingPreset.Lerp(day, day, 0f, into);
            else if (h >= duskStart && h < nightStart)
            {
                float t = Mathf.InverseLerp(duskStart, nightStart, h);
                if (t < 0.5f) LightingPreset.Lerp(day, dusk, Smooth(t * 2f), into);
                else LightingPreset.Lerp(dusk, night, Smooth((t - 0.5f) * 2f), into);
            }
            else LightingPreset.Lerp(night, night, 0f, into);
        }

        float ComputeNightFactor(float h)
        {
            if (h >= dayStart && h < duskStart) return 0f;
            if (h >= dawnStart && h < dayStart) return 1f - Smooth(Mathf.InverseLerp(dawnStart, dayStart, h));
            if (h >= duskStart && h < nightStart) return Smooth(Mathf.InverseLerp(duskStart, nightStart, h));
            return 1f;
        }

        // มีตัวเดียวในฉาก — ถ้าฉากยังไม่มี (เช่นฉากทดสอบ) สร้างให้อัตโนมัติด้วยค่าเริ่มต้น
        public static DayNightCycle EnsureExists()
        {
            if (Instance != null) return Instance;
            var found = FindFirstObjectByType<DayNightCycle>();
            if (found != null) return found;
            return new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
        }

        // เก็บค่าแสงปัจจุบันของฉากเป็นชุด "กลางวัน" (รักษาบรรยากาศเดิมของฉากไว้ตอนกลางวัน)
        public static LightingPreset CaptureFromScene(Light sunLight)
        {
            var p = new LightingPreset();
            if (sunLight != null) { p.sunColor = sunLight.color; p.sunIntensity = sunLight.intensity; p.shadowStrength = sunLight.shadowStrength; }
            p.ambientSky = RenderSettings.ambientSkyColor; p.ambientEquator = RenderSettings.ambientEquatorColor; p.ambientGround = RenderSettings.ambientGroundColor;
            p.reflectionIntensity = RenderSettings.reflectionIntensity;
            p.fogColor = RenderSettings.fogColor;
            var sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (sky.HasProperty("_SkyTint")) p.skyTint = sky.GetColor("_SkyTint");
                if (sky.HasProperty("_Exposure")) p.skyExposure = sky.GetFloat("_Exposure");
                if (sky.HasProperty("_AtmosphereThickness")) p.atmosphereThickness = sky.GetFloat("_AtmosphereThickness");
                if (sky.HasProperty("_SunSize")) p.sunSize = sky.GetFloat("_SunSize");
            }
            return p;
        }

        // ===== ฟังก์ชันล้วน (ใช้ในเทสต์/Dev Panel) =====
        public static DayPhase PhaseAt(float h, float dawnStart, float dayStart, float duskStart, float nightStart)
        {
            h = Mathf.Repeat(h, 24f);
            if (h >= dawnStart && h < dayStart) return DayPhase.Dawn;
            if (h >= dayStart && h < duskStart) return DayPhase.Day;
            if (h >= duskStart && h < nightStart) return DayPhase.Dusk;
            return DayPhase.Night;
        }

        // 1 = ไฟเปิดเต็ม · เฟดขึ้นช่วง [on, on+fade] · เฟดลงช่วง [off-fade, off] (รองรับช่วงข้ามเที่ยงคืน)
        public static float ComputeLightsFactor(float h, float onHour, float offHour, float fadeHours)
        {
            h = Mathf.Repeat(h, 24f);
            fadeHours = Mathf.Max(0.01f, fadeHours);
            float sinceOn = Mathf.Repeat(h - onHour, 24f);           // ชั่วโมงนับจากเวลาเปิด
            float litSpan = Mathf.Repeat(offHour - onHour, 24f);      // ช่วงที่ไฟเปิดทั้งหมด
            if (sinceOn > litSpan) return 0f;                         // ช่วงกลางวัน
            float up = Mathf.Clamp01(sinceOn / fadeHours);
            float down = Mathf.Clamp01((litSpan - sinceOn) / fadeHours);
            return Mathf.Min(up, down);
        }

        public static string PhaseNameThai(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Dawn: return "รุ่งเช้า";
                case DayPhase.Day: return "กลางวัน";
                case DayPhase.Dusk: return "ช่วงเย็น";
                default: return "กลางคืน";
            }
        }

        public string PhaseName => PhaseNameThai(CurrentPhase);

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        static float HourDistance(float a, float b) { float d = Mathf.Abs(a - b) % 24f; return Mathf.Min(d, 24f - d); }

        // ===== ตั้งค่า / ค่าเริ่มต้น =====
        void ResolveRefs()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        void CollectOutdoorLights()
        {
            if (outdoorLights.Count == 0)
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (l != sun && l.name.StartsWith("NightLight")) outdoorLights.Add(l);
            outdoorBaseIntensity.Clear();
            foreach (var l in outdoorLights) outdoorBaseIntensity.Add(l != null ? Mathf.Max(0.01f, l.intensity) : 1f);
        }

        void SaveOriginals()
        {
            if (savedOriginals) return;
            origSky = RenderSettings.ambientSkyColor; origEq = RenderSettings.ambientEquatorColor; origGround = RenderSettings.ambientGroundColor;
            origFog = RenderSettings.fogColor; origRefl = RenderSettings.reflectionIntensity;
            savedOriginals = true;
        }

        void RestoreOriginals()
        {
            if (!savedOriginals) return;
            RenderSettings.ambientSkyColor = origSky; RenderSettings.ambientEquatorColor = origEq; RenderSettings.ambientGroundColor = origGround;
            RenderSettings.fogColor = origFog; RenderSettings.reflectionIntensity = origRefl;
            if (skyInstance != null)
            {
                if (RenderSettings.skybox == skyInstance) RenderSettings.skybox = originalSkybox;
                Destroy(skyInstance);
                skyInstance = null;
            }
        }

        // ใช้สำเนาของวัสดุท้องฟ้า (ไม่แก้ไฟล์ Material เดิมของโปรเจกต์)
        void SetupSkyboxInstance()
        {
            if (!controlSkybox || RenderSettings.skybox == null) return;
            if (!RenderSettings.skybox.HasProperty("_Exposure")) return;   // ไม่ใช่ท้องฟ้าแบบปรับได้ → ไม่แตะ
            originalSkybox = RenderSettings.skybox;
            skyInstance = new Material(originalSkybox) { name = originalSkybox.name + " (DayNight)" };
            RenderSettings.skybox = skyInstance;
        }

        static void SetIf(Material m, string prop, Color c) { if (m.HasProperty(prop)) m.SetColor(prop, c); }
        static void SetIf(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }

        void Validate()
        {
            if (sun == null) Debug.LogWarning("[DayNight] ไม่พบ Directional Light หลัก — ระบบจะไม่หมุนแสง");
            else if (sun.lightmapBakeType == LightmapBakeType.Baked)
                Debug.LogWarning("[DayNight] Directional Light ตั้งเป็น Baked — แสงที่อบไว้จะไม่เปลี่ยนตามเวลา (ควรเป็น Realtime/Mixed)");
            if (LightmapSettings.lightmaps != null && LightmapSettings.lightmaps.Length > 0)
                Debug.LogWarning("[DayNight] ฉากมี lightmap ที่อบไว้ — ส่วนที่อบไว้จะไม่เปลี่ยนตามเวลา มีผลเฉพาะแสง realtime/ambient");
        }

        public static AnimationCurve DefaultElevation()
        {
            // กลางคืน = แสงจันทร์มุมสูง (เห็นทาง/ตัวละคร) · รุ่งเช้า/เย็น = มุมต่ำ เงายาว · เที่ยง = สูงสุด
            var c = new AnimationCurve(
                new Keyframe(0f, 50f), new Keyframe(4.5f, 38f), new Keyframe(6f, 16f), new Keyframe(9f, 42f),
                new Keyframe(12f, 62f), new Keyframe(15f, 42f), new Keyframe(18f, 16f), new Keyframe(19.5f, 38f), new Keyframe(24f, 50f));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        public static LightingPreset DefaultNight() => new LightingPreset
        {
            sunColor = new Color(0.62f, 0.72f, 1f), sunIntensity = 0.45f, shadowStrength = 0.6f,
            ambientSky = new Color(0.34f, 0.40f, 0.58f), ambientEquator = new Color(0.26f, 0.29f, 0.40f), ambientGround = new Color(0.17f, 0.18f, 0.24f),
            reflectionIntensity = 0.35f, fogColor = new Color(0.10f, 0.13f, 0.22f),
            skyTint = new Color(0.20f, 0.24f, 0.42f), skyExposure = 0.18f, atmosphereThickness = 0.5f, sunSize = 0f
        };

        public static LightingPreset DefaultDawn() => new LightingPreset
        {
            sunColor = new Color(1f, 0.74f, 0.52f), sunIntensity = 0.8f, shadowStrength = 0.85f,
            ambientSky = new Color(0.72f, 0.68f, 0.76f), ambientEquator = new Color(0.60f, 0.52f, 0.52f), ambientGround = new Color(0.32f, 0.30f, 0.32f),
            reflectionIntensity = 0.7f, fogColor = new Color(0.86f, 0.72f, 0.64f),
            skyTint = new Color(0.62f, 0.48f, 0.44f), skyExposure = 0.9f, atmosphereThickness = 1.2f, sunSize = 0.05f
        };

        public static LightingPreset DefaultDusk() => new LightingPreset
        {
            sunColor = new Color(1f, 0.58f, 0.36f), sunIntensity = 0.75f, shadowStrength = 0.85f,
            ambientSky = new Color(0.70f, 0.56f, 0.62f), ambientEquator = new Color(0.56f, 0.44f, 0.44f), ambientGround = new Color(0.28f, 0.25f, 0.27f),
            reflectionIntensity = 0.65f, fogColor = new Color(0.80f, 0.58f, 0.50f),
            skyTint = new Color(0.66f, 0.42f, 0.40f), skyExposure = 0.85f, atmosphereThickness = 1.3f, sunSize = 0.05f
        };

        public static LightingPreset DefaultInteriorDay() => new LightingPreset
        {
            sunColor = new Color(1f, 0.97f, 0.90f), sunIntensity = 1.0f, shadowStrength = 0.7f,
            ambientSky = new Color(0.86f, 0.86f, 0.88f), ambientEquator = new Color(0.66f, 0.65f, 0.62f), ambientGround = new Color(0.45f, 0.44f, 0.42f),
            reflectionIntensity = 0.8f, fogColor = new Color(0.72f, 0.78f, 0.85f),
            skyTint = new Color(0.5f, 0.5f, 0.5f), skyExposure = 1.3f, atmosphereThickness = 1f, sunSize = 0.04f
        };

        public static LightingPreset DefaultInteriorNight() => new LightingPreset
        {
            sunColor = new Color(1f, 0.86f, 0.66f), sunIntensity = 0.85f, shadowStrength = 0.6f,
            ambientSky = new Color(0.76f, 0.70f, 0.64f), ambientEquator = new Color(0.58f, 0.52f, 0.46f), ambientGround = new Color(0.36f, 0.33f, 0.30f),
            reflectionIntensity = 0.6f, fogColor = new Color(0.30f, 0.27f, 0.25f),
            skyTint = new Color(0.20f, 0.24f, 0.42f), skyExposure = 0.18f, atmosphereThickness = 0.5f, sunSize = 0f
        };
    }
}
