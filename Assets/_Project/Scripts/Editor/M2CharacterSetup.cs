#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace NisitSimulator.EditorTools
{
    // ตั้งค่าตัวละครจริง (Mixamo) แทนแคปซูล — กดปุ่มเดียวจบ
    //  1) ตั้งโมเดล+แอนิเมชันเป็น Humanoid  2) สร้าง Animator (Idle/Walk/Run blend)
    //  3) เอาโมเดลใส่แทนแคปซูลที่ Player  4) ต่อกับ PlayerMovement
    // ใช้: เมนู  Nisit -> Setup M2 Character
    public static class M2CharacterSetup
    {
        private const string CharFolder = "Assets/_Project/Art/Characters";
        private const string ControllerPath = CharFolder + "/NisitCharacter.controller";

        [MenuItem("Nisit/Setup M2 Character", false, 4)]
        public static void Setup()
        {
            // ---- หาไฟล์: โมเดล (ไม่มี @) + แอนิเมชัน (มี @) ----
            string modelPath = null, idlePath = null, walkPath = null, runPath = null;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { CharFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var f = System.IO.Path.GetFileNameWithoutExtension(p).ToLower();
                if (!f.Contains("@")) modelPath = p;
                else if (f.Contains("idle")) idlePath = p;
                else if (f.Contains("walk")) walkPath = p;
                else if (f.Contains("run")) runPath = p;
            }
            if (modelPath == null || idlePath == null || walkPath == null || runPath == null)
            {
                EditorUtility.DisplayDialog("Nisit",
                    "หาไฟล์ไม่ครบใน Art/Characters\nต้องมี: โมเดล + @Idle + @Walking + @Running", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Nisit — Setup M2 Character",
                "จะตั้งค่าตัวละคร Humanoid + Animator + ใส่แทนแคปซูล\n\nทำต่อไหม?", "ทำเลย", "ยกเลิก"))
                return;

            // ---- 1) โมเดลหลัก = Humanoid (สร้าง Avatar จากตัวเอง) ----
            var mImp = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            mImp.animationType = ModelImporterAnimationType.Human;
            mImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mImp.SaveAndReimport();

            var avatar = LoadOfType<Avatar>(modelPath);
            if (avatar == null) { EditorUtility.DisplayDialog("Nisit", "สร้าง Avatar ไม่สำเร็จ", "OK"); return; }

            // ---- 2) แอนิเมชัน = Humanoid + loop (คลิป Humanoid รีทาร์เก็ตข้าม avatar ได้เอง) ----
            var idle = SetupAnim(idlePath, loop: true);
            var walk = SetupAnim(walkPath, loop: true);
            var run  = SetupAnim(runPath,  loop: true);
            if (idle == null || walk == null || run == null)
            { EditorUtility.DisplayDialog("Nisit", "อ่าน AnimationClip ไม่ครบ", "OK"); return; }

            // ---- 3) Animator Controller (Blend Tree 1D: Speed) ----
            // PlayerMovement ส่ง Speed = 0 (ยืน) / 0.5 (เดิน) / 1 (วิ่ง) -> threshold ต้องตรงกัน
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            BlendTree bt;
            var state = controller.CreateBlendTreeInController("Locomotion", out bt, 0);
            bt.blendType = BlendTreeType.Simple1D;
            bt.blendParameter = "Speed";
            bt.useAutomaticThresholds = false;
            bt.AddChild(idle, 0f);
            bt.AddChild(walk, 0.5f);
            bt.AddChild(run, 1f);
            // ลบพารามิเตอร์ "Blend" ที่ระบบเติมอัตโนมัติ
            var ps = new List<AnimatorControllerParameter>(controller.parameters);
            ps.RemoveAll(pp => pp.name == "Blend");
            controller.parameters = ps.ToArray();

            // ---- 4) ประกอบร่างที่ Player ----
            var player = GameObject.Find("Player");
            if (player == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ Player ในฉาก", "OK"); return; }

            // เอา mesh แคปซูลออก
            var mf = player.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf);
            var mr = player.GetComponent<MeshRenderer>(); if (mr != null) Object.DestroyImmediate(mr);
            var old = player.transform.Find("CharacterModel"); if (old != null) Object.DestroyImmediate(old.gameObject);

            // ขนาดโมเดล native
            float charH = MeasureModelHeight(modelPath);
            if (charH < 0.01f) charH = 1.8f;

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var charGo = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            Undo.RegisterCreatedObjectUndo(charGo, "Setup Character");
            charGo.name = "CharacterModel";
            charGo.transform.SetParent(player.transform, false);
            charGo.transform.localPosition = Vector3.zero;
            charGo.transform.localRotation = Quaternion.identity;
            charGo.transform.localScale = Vector3.one * (2f / charH);   // ให้สูงเท่าแคปซูล (2 หน่วย)

            // จัดเท้าให้แตะฐาน CharacterController
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                float bottom = player.transform.position.y + (cc.center.y - cc.height * 0.5f) * player.transform.lossyScale.y;
                var b = MeasureBounds(charGo);
                charGo.transform.position += Vector3.up * (bottom - b.min.y);
            }

            var anim = charGo.GetComponent<Animator>(); if (anim == null) anim = charGo.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.avatar = avatar;
            anim.applyRootMotion = false;

            EditorUtility.SetDirty(player);
            Selection.activeGameObject = player;
            Debug.Log("<color=lime>[Nisit] ✅ ใส่ตัวละครจริงเสร็จ! กด Play แล้วเดิน — ควรเล่นแอนิเมชัน ยืน/เดิน/วิ่ง\n" +
                      "ถ้าตัวละครเป็นสีเทา (ไม่มี texture) บอกได้ เดี๋ยว extract material ให้</color>");
        }

        private static AnimationClip SetupAnim(string path, bool loop)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = imp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = loop;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
            return LoadOfType<AnimationClip>(path);
        }

        private static T LoadOfType<T>(string path) where T : Object
        {
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is T t && !o.name.StartsWith("__preview")) return t;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is T t && !o.name.StartsWith("__preview")) return t;
            return null;
        }

        private static float MeasureModelHeight(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return 0f;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.position = Vector3.zero; tmp.transform.localScale = Vector3.one;
            float h = MeasureBounds(tmp).size.y;
            Object.DestroyImmediate(tmp);
            return h;
        }

        private static Bounds MeasureBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
