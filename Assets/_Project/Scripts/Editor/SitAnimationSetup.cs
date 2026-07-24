#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using NisitSimulator.Player;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // เพิ่มท่า "นั่ง" เข้า Animator + ใส่ PlayerActivity ให้ Player + วางเก้าอี้ทดสอบใกล้จุดเกิด
    // ใช้: เมนู  Nisit -> Add Sit Animation
    public static class SitAnimationSetup
    {
        private const string CharFolder = "Assets/_Project/Art/Characters";
        private const string ControllerPath = CharFolder + "/NisitCharacter.controller";

        [MenuItem("Nisit/Add Sit Animation", false, 9)]
        public static void Setup()
        {
            // ---- หาไฟล์ท่านั่ง (เลือกตัวที่ไม่ใช่ "idle" ก่อน = ตัวใหม่) ----
            string sitPath = null, sitFallback = null;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { CharFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var f = System.IO.Path.GetFileNameWithoutExtension(p).ToLower();
                if (f.Contains("@") && f.Contains("sit"))
                {
                    if (!f.Contains("idle")) { sitPath = p; break; }   // ชอบตัวใหม่ (@Sitting)
                    else if (sitFallback == null) sitFallback = p;
                }
            }
            if (sitPath == null) sitPath = sitFallback;
            if (sitPath == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบไฟล์ท่านั่ง (@Sitting) ใน Art/Characters", "OK"); return; }

            Debug.Log($"[Nisit] ใช้ท่านั่ง: {System.IO.Path.GetFileName(sitPath)}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ Animator (NisitCharacter.controller)\nรัน Setup M2 Character ก่อน", "OK"); return; }

            // ---- ตั้งท่านั่งเป็น Humanoid + loop ----
            var imp = (ModelImporter)AssetImporter.GetAtPath(sitPath);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = imp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();

            AnimationClip sitClip = null;
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(sitPath))
                if (o is AnimationClip c && !o.name.StartsWith("__preview")) { sitClip = c; break; }
            if (sitClip == null) { EditorUtility.DisplayDialog("Nisit", "อ่าน AnimationClip ท่านั่งไม่ได้", "OK"); return; }

            // ---- เพิ่มพารามิเตอร์ + state + เส้นเชื่อม ----
            bool hasParam = false;
            foreach (var pp in controller.parameters) if (pp.name == "Sitting") hasParam = true;
            if (!hasParam) controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            AnimatorState loco = null, sit = null;
            foreach (var s in sm.states)
            {
                if (s.state.name == "Locomotion") loco = s.state;
                if (s.state.name == "Sit") sit = s.state;
            }
            if (sit != null) sm.RemoveState(sit);           // ลบของเก่า (กดซ้ำได้)
            sit = sm.AddState("Sit");
            sit.motion = sitClip;

            if (loco != null)
            {
                var toSit = loco.AddTransition(sit);
                toSit.hasExitTime = false; toSit.duration = 0.15f;
                toSit.AddCondition(AnimatorConditionMode.If, 0f, "Sitting");

                var toLoco = sit.AddTransition(loco);
                toLoco.hasExitTime = false; toLoco.duration = 0.15f;
                toLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // ---- ใส่ PlayerActivity ให้ Player ----
            var player = GameObject.Find("Player");
            if (player != null && player.GetComponent<PlayerActivity>() == null)
                player.AddComponent<PlayerActivity>();

            // ---- วางเก้าอี้ทดสอบใกล้จุดเกิด ----
            int layer = LayerMask.NameToLayer("Interactable");
            var old = GameObject.Find("SitTest");
            if (old != null) Object.DestroyImmediate(old);
            var spotGo = new GameObject("SitTest");
            Undo.RegisterCreatedObjectUndo(spotGo, "Add Sit Test");
            spotGo.transform.position = new Vector3(0f, 0f, 4f);
            if (layer >= 0) spotGo.layer = layer;

            var col = spotGo.AddComponent<BoxCollider>();
            col.isTrigger = true; col.size = new Vector3(2f, 2f, 2f); col.center = new Vector3(0f, 1f, 0f);

            var spot = spotGo.AddComponent<ActivitySpot>();
            spot.activityName = "นั่งพัก";
            spot.satisfactionChange = 5f;
            spot.energyChange = 3f;
            // ยกตัวขึ้นกันจมพื้น ~0.35 เท่าส่วนสูงตัวละคร
            float ph = 1.3f;
            if (player != null) { var r = player.GetComponentInChildren<Renderer>(); if (r != null) ph = player.GetComponentInChildren<Renderer>().bounds.size.y; }
            spot.sitYOffset = Mathf.Max(0.2f, ph * 0.35f);
            // จุดนั่ง = ตรงนี้ หันกลับไปทางจุดเกิด
            var seat = new GameObject("Seat").transform;
            seat.SetParent(spotGo.transform);
            seat.localPosition = Vector3.zero;
            seat.rotation = Quaternion.Euler(0f, 180f, 0f);
            spot.seat = seat;

            Selection.activeGameObject = spotGo;
            Debug.Log("<color=lime>[Nisit] ✅ เพิ่มท่านั่งเสร็จ! กด Play → เดินไปที่ SitTest (หน้าจุดเกิด) → กด E นั่ง / กด E อีกที ลุก\n" +
                      "ถ้าท่านั่งลอย/จม หรือหันผิดทาง บอกได้ ปรับ seat anchor ให้</color>");
        }
    }
}
#endif
