#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NisitSimulator.EditorTools
{
    // เพิ่ม "มิติ" ให้ฉาก: Post-processing + แสงเงานุ่ม + หมอก + รายละเอียด 3D
    // ใช้: เมนู  Nisit -> Add Depth & Mood   (ทำหลัง Make It Pretty)
    public static class DepthAndMood
    {
        const string MatDir = "Assets/_Project/Art/Materials/";
        const string PPPath = "Assets/_Project/Art/PostFX_Profile.asset";

        [MenuItem("Nisit/Add Depth & Mood")]
        public static void AddDepth()
        {
            SetupLighting();
            SetupFog();
            try { SetupPostProcessing(); }
            catch (System.Exception e) { Debug.LogWarning("[Nisit] ข้าม Post-processing: " + e.Message); }
            AddDetails();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=orange>[Nisit] เพิ่มมิติเสร็จ! กด Ctrl+S แล้ว Play</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "เพิ่มมิติเสร็จแล้ว! 🌅\n\n- แสงเฉียง + เงานุ่ม (soft shadow)\n- Post-processing: Bloom, Vignette, สีสด, Tonemapping\n- หมอกไกลๆ ให้ภาพลึก\n- หลังคา + หน้าต่างเรืองแสงบนตึก\n- เสาไฟตามทาง + พุ่มไม้\n\nกด Ctrl+S แล้ว Play", "เยี่ยม!");
        }

        // ---------- แสง ----------
        private static void SetupLighting()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f); // มุมเฉียง เงายาวสวย
                sun.intensity = 1.3f;
                sun.color = new Color(1f, 0.96f, 0.88f);                 // แสงอุ่น
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.7f;
            }
            // แสงแวดล้อมฟ้าอมฟ้าเล็กน้อย ให้ที่มืดไม่ดำสนิท
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor= new Color(0.45f, 0.50f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.32f, 0.28f);
        }

        // ---------- หมอกให้ภาพลึก ----------
        private static void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 150f;
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.88f);
        }

        // ---------- Post-processing ----------
        private static void SetupPostProcessing()
        {
            // เปิด post-processing ที่กล้อง
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            // โปรไฟล์
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PPPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PPPath);
            }
            // ล้างของเดิม (ให้กดซ้ำได้)
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                var c = profile.components[i];
                profile.components.RemoveAt(i);
                Object.DestroyImmediate(c, true);
            }

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true; tone.mode.value = TonemappingMode.Neutral;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.7f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.9f;
            bloom.scatter.overrideState = true;   bloom.scatter.value = 0.6f;

            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true; vig.intensity.value = 0.30f;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.4f;

            var col = profile.Add<ColorAdjustments>(true);
            col.contrast.overrideState = true;    col.contrast.value = 12f;
            col.saturation.overrideState = true;  col.saturation.value = 15f;
            col.postExposure.overrideState = true; col.postExposure.value = 0.15f;

            EditorUtility.SetDirty(profile);

            // ผูกกับ Global Volume
            var gv = GameObject.Find("Global Volume");
            if (gv == null) gv = new GameObject("Global Volume");
            var vol = gv.GetComponent<Volume>();
            if (vol == null) vol = gv.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        // ---------- รายละเอียด 3D ----------
        private static void AddDetails()
        {
            DestroyIfExists("Details");
            var details = new GameObject("Details").transform;

            var roofMat   = GetOrCreateMat("Roof",   new Color(0.35f, 0.30f, 0.32f));
            var lampMat   = GetOrCreateMat("LampPole", new Color(0.22f, 0.22f, 0.25f));
            var bushMat   = GetOrCreateMat("Bush",   new Color(0.22f, 0.52f, 0.26f));

            var windowMat = GetOrCreateMat("Window", new Color(1f, 0.92f, 0.65f));
            windowMat.EnableKeyword("_EMISSION");
            windowMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.5f) * 2.2f);
            windowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(windowMat);

            var glowMat = GetOrCreateMat("LampGlow", new Color(1f, 0.9f, 0.6f));
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.4f) * 3f);
            glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glowMat);

            // หลังคา + หน้าต่าง บนตึกทุกหลัง
            var env = GameObject.Find("Environment");
            if (env != null)
            {
                foreach (Transform child in env.transform)
                {
                    if (child.name != "Building") continue;
                    Vector3 pos = child.position;
                    Vector3 s = child.localScale;

                    // หลังคา
                    var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roof.name = "Roof"; roof.transform.SetParent(details);
                    roof.transform.position = new Vector3(pos.x, s.y + 0.3f, pos.z);
                    roof.transform.localScale = new Vector3(s.x * 1.08f, 0.6f, s.z * 1.08f);
                    roof.GetComponent<Renderer>().sharedMaterial = roofMat;
                    StripCollider(roof);

                    // หน้าต่าง 2 แถวบน 4 ด้าน
                    for (int fy = 0; fy < 2; fy++)
                    {
                        float wy = s.y * (0.35f + fy * 0.32f);
                        foreach (float ox in new float[] { -s.x * 0.24f, s.x * 0.24f })
                        {
                            AddWindow(details, new Vector3(pos.x + ox, wy, pos.z - s.z / 2f - 0.06f),
                                      new Vector3(s.x * 0.26f, s.y * 0.22f, 0.12f), windowMat);
                            AddWindow(details, new Vector3(pos.x + ox, wy, pos.z + s.z / 2f + 0.06f),
                                      new Vector3(s.x * 0.26f, s.y * 0.22f, 0.12f), windowMat);
                        }
                        foreach (float oz in new float[] { -s.z * 0.24f, s.z * 0.24f })
                        {
                            AddWindow(details, new Vector3(pos.x - s.x / 2f - 0.06f, wy, pos.z + oz),
                                      new Vector3(0.12f, s.y * 0.22f, s.z * 0.26f), windowMat);
                            AddWindow(details, new Vector3(pos.x + s.x / 2f + 0.06f, wy, pos.z + oz),
                                      new Vector3(0.12f, s.y * 0.22f, s.z * 0.26f), windowMat);
                        }
                    }
                }
            }

            // เสาไฟตามทางเดิน
            foreach (float z in new float[] { -14f, -5f, 5f, 14f })
            {
                MakeLamp(details, new Vector3(2.4f, 0, z), lampMat, glowMat);
                MakeLamp(details, new Vector3(-2.4f, 0, z), lampMat, glowMat);
            }

            // พุ่มไม้กระจาย
            Vector3[] bushes = {
                new(-4,0,3), new(4,0,7), new(-9,0,-3), new(8,0,3),
                new(-3,0,-8), new(10,0,-6), new(-13,0,6), new(5,0,-13)
            };
            foreach (var p in bushes) MakeBush(details, p, bushMat);
        }

        private static void MakeLamp(Transform parent, Vector3 pos, Material pole, Material glow)
        {
            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(parent); lamp.position = pos;

            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Pole"; p.transform.SetParent(lamp);
            p.transform.localPosition = new Vector3(0, 1.6f, 0);
            p.transform.localScale = new Vector3(0.12f, 1.6f, 0.12f);
            p.GetComponent<Renderer>().sharedMaterial = pole;
            StripCollider(p);

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb"; bulb.transform.SetParent(lamp);
            bulb.transform.localPosition = new Vector3(0, 3.3f, 0);
            bulb.transform.localScale = Vector3.one * 0.45f;
            bulb.GetComponent<Renderer>().sharedMaterial = glow;
            StripCollider(bulb);

            // แสงจุดจริงๆ ให้เรืองรอบเสา
            var lightGo = new GameObject("PointLight");
            lightGo.transform.SetParent(lamp);
            lightGo.transform.localPosition = new Vector3(0, 3.3f, 0);
            var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point; lt.range = 8f; lt.intensity = 1.2f;
            lt.color = new Color(1f, 0.9f, 0.65f);
        }

        private static void MakeBush(Transform parent, Vector3 pos, Material mat)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush"; bush.transform.SetParent(parent);
            bush.transform.position = new Vector3(pos.x, 0.5f, pos.z);
            bush.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
            bush.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(bush);
        }

        private static void AddWindow(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Window"; w.transform.SetParent(parent);
            w.transform.position = pos; w.transform.localScale = scale;
            w.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(w);
        }

        // ---------- utility ----------
        private static Material GetOrCreateMat(string name, Color c)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
#endif
