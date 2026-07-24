#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // ประกอบโมเดล Mixamo อัตโนมัติ: ตั้ง Humanoid + สร้าง Animator (ยืน/เดิน/วิ่ง) + ใส่แทนแคปซูล
    // ใช้: เมนู  Nisit -> Setup Character (M2)   หลังวางไฟล์ .fbx ใน Art/Characters/
    public static class CharacterSetup
    {
        const string CharDir = "Assets/_Project/Art/Characters";
        const string CtrlPath = CharDir + "/PlayerAnimator.controller";

        [MenuItem("Nisit/Setup Character (M2)")]
        public static void Setup()
        {
            // หา FBX ทั้งหมดในโฟลเดอร์ Characters
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { CharDir });
            if (fbxGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("Nisit",
                    "ไม่พบไฟล์ .fbx ใน Assets/_Project/Art/Characters/\n\nกรุณาโหลดจาก Mixamo แล้ววางไฟล์ก่อน (ดู SETUP_M2)", "OK");
                return;
            }
            var paths = fbxGuids.Select(AssetDatabase.GUIDToAssetPath).ToArray();

            // 1) ตั้งทุกไฟล์เป็น Humanoid
            foreach (var p in paths)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp != null && imp.animationType != ModelImporterAnimationType.Human)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.SaveAndReimport();
                }
            }

            // 2) แยกไฟล์โมเดล (มี SkinnedMeshRenderer) กับไฟล์แอนิเมชัน
            string modelPath = null, idlePath = null, walkPath = null, runPath = null;
            foreach (var p in paths)
            {
                string lower = System.IO.Path.GetFileNameWithoutExtension(p).ToLower();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                bool hasMesh = go != null && go.GetComponentInChildren<SkinnedMeshRenderer>() != null;

                if (lower.Contains("idle")) idlePath = p;
                else if (lower.Contains("walk")) walkPath = p;
                else if (lower.Contains("run")) runPath = p;
                else if (hasMesh && modelPath == null) modelPath = p;

                // เผื่อไฟล์โมเดลชื่อมี keyword — ยังจับ mesh เป็น model ด้วย
                if (hasMesh && modelPath == null) modelPath = p;
            }

            if (modelPath == null)
            {
                EditorUtility.DisplayDialog("Nisit",
                    "ไม่พบไฟล์ตัวละคร (โมเดลที่มี mesh)\nตรวจว่าโหลดตัวละครแบบ 'FBX for Unity' มาด้วย", "OK");
                return;
            }

            // 3) สร้าง Animator Controller (blend tree ยืน/เดิน/วิ่ง)
            var idle = GetClip(idlePath);
            var walk = GetClip(walkPath);
            var run = GetClip(runPath);

            if (System.IO.File.Exists(CtrlPath)) AssetDatabase.DeleteAsset(CtrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            BlendTree tree;
            ctrl.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 0.5f);
            if (run != null) tree.AddChild(run, 1f);

            // 4) ใส่โมเดลแทนแคปซูล
            var player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบ Player ในฉาก! กด Build M1 Scene ก่อน", "OK");
                return;
            }

            // ลบโมเดลเก่า (กันซ้ำ)
            var old = player.transform.Find("CharacterModel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            inst.name = "CharacterModel";
            inst.transform.SetParent(player.transform, false);

            // วางให้เท้าอยู่ก้นของ CharacterController
            var cc = player.GetComponent<CharacterController>();
            float feetY = cc != null ? cc.center.y - cc.height / 2f : 0f;
            inst.transform.localPosition = new Vector3(0, feetY, 0);
            inst.transform.localRotation = Quaternion.identity;

            // ต่อ Animator
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;   // เราขยับด้วย CharacterController
            }

            // ซ่อนแคปซูลเดิม + จุดบอกทิศ
            var mr = player.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var nose = player.transform.Find("FacingNose");
            if (nose != null) nose.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            string found = $"โมเดล: {System.IO.Path.GetFileName(modelPath)}\n" +
                           $"ยืน: {(idle != null ? "✓" : "✗")}  เดิน: {(walk != null ? "✓" : "✗")}  วิ่ง: {(run != null ? "✓" : "✗")}";
            Debug.Log("<color=lime>[Nisit] ประกอบตัวละครเสร็จ!</color> " + found);
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ประกอบตัวละครเสร็จ! 🕺\n\n" + found + "\n\nกด Ctrl+S แล้ว Play — ลองเดิน (WASD) / วิ่ง (Shift)\nตัวละครควรเล่นท่าเดิน/วิ่งตามจริง", "เยี่ยม!");
        }

        // ดึง AnimationClip จริงจากไฟล์ FBX (ข้าม preview clip)
        private static AnimationClip GetClip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                    return c;
            return null;
        }
    }
}
#endif
