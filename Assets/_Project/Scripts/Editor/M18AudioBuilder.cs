#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.Core;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // 🔊 สร้างระบบเสียง: สังเคราะห์เสียง (WAV) เอง + วาง SFXManager + แปะ ButtonSound ทุกปุ่ม + เพลงพื้นหลัง
    // ใช้: เมนู  Nisit -> Build Audio
    public static class M18AudioBuilder
    {
        const int RATE = 44100;
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string AudioDir = "Assets/_Project/Audio";

        class Clips { public AudioClip click, notify, coin, success, error, footstep, jump, sleep, whoosh, music, fanfare, eat, page, ambient; }

        [MenuItem("Nisit/Build Audio")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var c = Generate();
            int b1 = BuildScene(GameplayPath, c);
            int b2 = BuildScene(MenuPath, c);

            Debug.Log("<color=lime>[Nisit] สร้างระบบเสียงเสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างระบบเสียงเสร็จแล้ว! 🔊\n\n• สังเคราะห์เสียงเอง 9 แบบ + เพลงพื้นหลัง\n• คลิกปุ่มมีเสียง (แปะ " + (b1 + b2) + " ปุ่ม)\n• เดิน/กระโดด/ได้เงิน/สอบผ่าน-ตก/แจ้งเตือน/นอน มีเสียง\n• เพลง ambient วนพื้นหลัง\n\nปรับความดังได้ที่ ตั้งค่า (Master/Music/SFX)\nกด Play ฟังได้เลย", "เยี่ยม!");
        }

        static Clips Generate() => new Clips
        {
            click    = Pick("click",    () => Tone(760f, 0.05f, true, 40f)),
            notify   = Pick("notify",   () => Seq(new[] { 784f, 1047f }, 0.09f, 10f)),
            coin     = Pick("coin",     () => Seq(new[] { 988f, 1319f }, 0.07f, 12f)),
            success  = Pick("success",  () => Seq(new[] { 523f, 659f, 784f, 1047f }, 0.11f, 6f)),
            error    = Pick("error",    () => Sweep(420f, 170f, 0.32f, 5f)),
            footstep = Pick("footstep", () => Noise(0.09f, 26f, 0.7f)),
            jump     = Pick("jump",     () => Sweep(300f, 640f, 0.15f, 7f)),
            sleep    = Pick("sleep",    () => Sweep(540f, 200f, 0.5f, 3.5f)),
            whoosh   = Pick("whoosh",   () => Noise(0.22f, 7f, 0.6f)),
            music    = Pick("music",    () => Music()),
            fanfare  = Pick("fanfare",  () => Fanfare()),                     // ดีใจ/จบการศึกษา
            eat      = Pick("eat",      () => Eat()),                         // กินข้าว
            page     = Pick("page",     () => Noise(0.13f, 15f, 0.4f)),       // เปิดหน้า/เปลี่ยนแอป (เสียงกระดาษ)
            ambient  = Pick("ambient",  () => Ambient()),                    // บรรยากาศ (นก/ลม)
        };

        // ใช้ไฟล์ที่โหลดมาก่อน (Audio/custom/<ชื่อ>.wav/.mp3/.ogg) ถ้าไม่มีค่อยสังเคราะห์เอง
        static AudioClip Pick(string name, System.Func<float[]> synth)
        {
            foreach (var ext in new[] { ".wav", ".ogg", ".mp3" })
            {
                string p = AudioDir + "/custom/" + name + ext;
                if (File.Exists(p))
                {
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    if (clip != null) return clip;
                }
            }
            return MakeClip("sfx_" + name, synth());
        }

        static int BuildScene(string path, Clips c)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var old = GameObject.Find("AudioManager");
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject("AudioManager");
            var sfx = go.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f;
            var mus = go.AddComponent<AudioSource>(); mus.playOnAwake = false; mus.loop = true; mus.spatialBlend = 0f;
            var amb = go.AddComponent<AudioSource>(); amb.playOnAwake = false; amb.loop = true; amb.spatialBlend = 0f;

            var m = go.AddComponent<SFXManager>();
            m.sfxSource = sfx; m.musicSource = mus; m.ambientSource = amb;
            m.click = c.click; m.notify = c.notify; m.coin = c.coin; m.success = c.success; m.error = c.error;
            m.footstep = c.footstep; m.jump = c.jump; m.sleep = c.sleep; m.whoosh = c.whoosh; m.music = c.music;
            m.fanfare = c.fanfare; m.eat = c.eat; m.page = c.page; m.ambient = c.ambient;

            int added = 0;
            foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (btn.GetComponent<ButtonSound>() == null) { btn.gameObject.AddComponent<ButtonSound>(); added++; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return added;
        }

        // ---------- สังเคราะห์เสียง ----------
        static float[] Tone(float freq, float dur, bool square, float decay)
        {
            int n = (int)(dur * RATE); var s = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE; ph += freq / RATE;
                float w = square ? (Mathf.Repeat(ph, 1f) < 0.5f ? 0.6f : -0.6f) : Mathf.Sin(ph * 2f * Mathf.PI);
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay);
                s[i] = w * env;
            }
            return s;
        }

        static float[] Sweep(float f0, float f1, float dur, float decay)
        {
            int n = (int)(dur * RATE); var s = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE; float f = Mathf.Lerp(f0, f1, t / dur); ph += f / RATE;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay);
                s[i] = Mathf.Sin(ph * 2f * Mathf.PI) * env;
            }
            return s;
        }

        static float[] Seq(float[] freqs, float noteDur, float decay)
        {
            var list = new List<float>();
            foreach (var f in freqs) list.AddRange(Tone(f, noteDur, false, decay));
            return list.ToArray();
        }

        static float[] Noise(float dur, float decay, float amp)
        {
            int n = (int)(dur * RATE); var s = new float[n]; float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE;
                float w = Random.value * 2f - 1f;
                last = Mathf.Lerp(last, w, 0.5f);   // low-pass ให้ทึบขึ้น
                s[i] = last * Mathf.Exp(-t * decay) * amp;
            }
            return s;
        }

        static float[] Music()
        {
            float dur = 8f; int n = (int)(dur * RATE); var s = new float[n];
            float[] ch = { 261.63f, 329.63f, 392f, 523.25f };   // C major
            for (int k = 0; k < ch.Length; k++) ch[k] = Mathf.Round(ch[k] * 8f) / 8f;   // สแนปให้วนลูปเนียน
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE; float v = 0f;
                for (int k = 0; k < ch.Length; k++) v += Mathf.Sin(t * ch[k] * 2f * Mathf.PI);
                v /= ch.Length;
                float lfo = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 0.25f);
                s[i] = v * lfo * 0.5f;
            }
            return s;
        }

        // อารเพจจิโอขึ้น + คอร์ดจบยาว (ดีใจ/จบการศึกษา)
        static float[] Fanfare()
        {
            var notes = new[] { 392f, 523f, 659f, 784f, 1047f };
            var list = new List<float>();
            foreach (var f in notes) list.AddRange(Tone(f, 0.12f, false, 6f));
            list.AddRange(Chord(new[] { 523.25f, 659.25f, 784f }, 0.7f, 2.8f));   // C major ยาว
            return list.ToArray();
        }

        static float[] Chord(float[] freqs, float dur, float decay)
        {
            int n = (int)(dur * RATE); var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE; float v = 0f;
                foreach (var f in freqs) v += Mathf.Sin(t * f * 2f * Mathf.PI);
                v /= freqs.Length;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * decay);
                s[i] = v * env * 0.85f;
            }
            return s;
        }

        // เสียงเคี้ยว 2 คำ (noise สั้น ๆ เว้นช่วง)
        static float[] Eat()
        {
            var list = new List<float>();
            list.AddRange(Noise(0.09f, 30f, 0.5f));
            list.AddRange(new float[(int)(0.05f * RATE)]);
            list.AddRange(Noise(0.09f, 30f, 0.5f));
            return list.ToArray();
        }

        // บรรยากาศมหาลัย: ลมเบา (filtered noise) + นกร้องเป็นระยะ (วนลูปได้)
        static float[] Ambient()
        {
            float dur = 12f; int n = (int)(dur * RATE); var s = new float[n];
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = Random.value * 2f - 1f;
                last = Mathf.Lerp(last, w, 0.02f);   // low-pass มาก = เสียงลมทึบ
                float lfo = 0.55f + 0.45f * Mathf.Sin(i / (float)RATE * 2f * Mathf.PI * 0.1f);
                s[i] = last * 0.22f * lfo;
            }
            var rng = new System.Random(20260806);
            for (float t = 1f; t < dur - 1f; t += 1.4f + (float)rng.NextDouble() * 2.2f)
            {
                int start = (int)(t * RATE);
                float bf = 2200f + (float)rng.NextDouble() * 1300f;
                int cn = (int)(0.12f * RATE);
                for (int i = 0; i < cn && start + i < n; i++)
                {
                    float tt = i / (float)RATE;
                    float chirp = Mathf.Sin(tt * (bf + Mathf.Sin(tt * 60f) * 320f) * 2f * Mathf.PI);
                    float env = Mathf.Sin(tt / 0.12f * Mathf.PI);   // เข้า-ออกนุ่ม
                    s[start + i] += chirp * env * 0.11f;
                }
            }
            return s;
        }

        // ---------- เขียน WAV → AudioClip ----------
        static AudioClip MakeClip(string name, float[] samples)
        {
            if (!Directory.Exists(AudioDir)) Directory.CreateDirectory(AudioDir);
            string path = AudioDir + "/" + name + ".wav";

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                int n = samples.Length; short ch = 1; short bits = 16; int dataSize = n * 2;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + dataSize); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write(ch);
                bw.Write(RATE); bw.Write(RATE * ch * bits / 8); bw.Write((short)(ch * bits / 8)); bw.Write(bits);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(dataSize);
                for (int i = 0; i < n; i++) bw.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
                File.WriteAllBytes(path, ms.ToArray());
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
#endif
