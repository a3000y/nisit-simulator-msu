#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // 🎭 ตั้งค่าท่า Mixamo ใหม่ (Humanoid + Avatar Ch29 + Loop) + เพิ่ม state เข้า Animator + ต่อ station
    //   เรียน/ทำงาน → Typing · กิน → สุ่ม Drinking/Sitting Drinking · นอน → สุ่ม 2 ท่า
    // ใช้: เมนู  Nisit -> Setup Character Animations
    public static class M20CharacterAnims
    {
        const string Dir = "Assets/_Project/Art/Characters/";
        const string BaseFbx = Dir + "Ch29_nonPBR.fbx";
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        static readonly string[] Controllers = { Dir + "PlayerAnimator.controller", Dir + "NisitCharacter.controller" };

        // state → ไฟล์ท่า
        static readonly (string state, string fbx)[] Anims =
        {
            ("Typing",     "Ch29_nonPBR@Typing.fbx"),
            ("Eating_A",   "Ch29_nonPBR@Drinking.fbx"),
            ("Eating_B",   "Ch29_nonPBR@Sitting Drinking.fbx"),
            ("Sleeping_A", "Ch29_nonPBR@Laying Sleeping.fbx"),
            ("Sleeping_B", "Ch29_nonPBR@Sleeping Idle.fbx"),
            ("Cheering",   "Ch29_nonPBR@Cheering.fbx"),
            ("Talking",    "Ch29_nonPBR@Talking.fbx"),
            ("Waving",     "Ch29_nonPBR@Waving.fbx"),
            ("Coughing",   "Ch29_nonPBR@Laying Severe Cough.fbx"),
        };

        [MenuItem("Nisit/Setup Character Animations")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var avatar = GetAvatar(BaseFbx);
            if (avatar == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ Avatar ของ Ch29 (Ch29_nonPBR.fbx)", "ปิด"); return; }

            var log = new System.Text.StringBuilder();
            var clips = new Dictionary<string, AnimationClip>();

            // 1) ตั้ง import + เก็บ clip
            foreach (var (state, fbx) in Anims)
            {
                string path = Dir + fbx;
                if (AssetImporter.GetAtPath(path) == null) { log.AppendLine($"⚠ ไม่พบ {fbx}"); continue; }
                ConfigHumanoid(path, avatar);
                var clip = GetClip(path);
                if (clip != null) { clips[state] = clip; log.AppendLine($"✓ {state} ← {fbx}"); }
                else log.AppendLine($"⚠ {fbx} ไม่มีคลิป");
            }

            // 2) เพิ่ม state เข้าทุก controller ที่มี
            foreach (var cpath in Controllers)
            {
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(cpath);
                if (ac == null) continue;
                foreach (var kv in clips) AddState(ac, kv.Key, kv.Value);
                EditorUtility.SetDirty(ac);
            }
            AssetDatabase.SaveAssets();

            // 3) ต่อโรงอาหารให้สุ่มท่ากิน
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            var canteen = GameObject.Find("โรงอาหาร");
            if (canteen != null && canteen.TryGetComponent<ActivityStation>(out var act))
            {
                act.actionStates = new[] { "Eating_A", "Eating_B" };
                EditorUtility.SetDirty(act);
                log.AppendLine("✓ โรงอาหาร → สุ่มท่ากิน (Eating_A/B)");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] ตั้งค่าท่าตัวละครเสร็จ!</color>\n" + log);
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ตั้งค่าท่าตัวละครเสร็จ! 🎭\n\n" + log + "\n• เรียน/ทำงาน → Typing\n• กิน → สุ่ม 2 ท่า · นอน → สุ่ม 2 ท่า\n\nกด Play ลองเข้าเรียน/กินข้าวดู", "เยี่ยม!");
        }

        static Avatar GetAvatar(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) return a;
            return null;
        }

        static AnimationClip GetClip(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }

        static void ConfigHumanoid(string path, Avatar src)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = src;

            var clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;   // วนต่อเนื่องระหว่างทำ
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static void AddState(AnimatorController ac, string name, AnimationClip clip)
        {
            if (ac == null || clip == null) return;
            var sm = ac.layers[0].stateMachine;
            foreach (var cs in sm.states)
                if (cs.state.name == name) { cs.state.motion = clip; return; }
            var st = sm.AddState(name);
            st.motion = clip;
        }
    }
}
#endif
