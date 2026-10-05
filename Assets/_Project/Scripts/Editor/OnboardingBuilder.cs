#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.UI;
using NisitSimulator.Player;
using NisitSimulator.Stats;
using NisitSimulator.CameraRig;
using NisitSimulator.Interaction;
using NisitSimulator.Core;
using NisitSimulator.TimeSystem;
namespace NisitSimulator.EditorTools
{
    public static class OnboardingBuilder
    {
        const string Folder = "Assets/_Project/Prefabs/Tutorial";
        static TMP_FontAsset font;
        static Material cream, stone, green, gold, dark;
        static GameObject Box(string name, Vector3 p, Vector3 scale, Material m, Transform parent=null)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name;
            g.transform.SetParent(parent); g.transform.position=p; g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=m; return g;
        }
        static Material Mat(string name, Color c)
        {
            string path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.color=c; return m;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var g=new GameObject(name,typeof(RectTransform)); var r=(RectTransform)g.transform; r.SetParent(parent,false);
            r.anchorMin=min; r.anchorMax=max; r.offsetMin=offsetMin; r.offsetMax=offsetMax; return r;
        }
        static TMP_Text Text(string name, Transform parent, string value, float size, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
        {
            var r=Rect(name,parent,min,max,lo,hi); var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=font; t.text=value; t.fontSize=size; t.color=new Color(.30f,.25f,.46f);
            t.enableAutoSizing=true; t.fontSizeMin=size-4; t.fontSizeMax=size; t.raycastTarget=false;
            return t;
        }
        static Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
        {
            var r=Rect(name,parent,min,max,lo,hi); var im=r.gameObject.AddComponent<Image>(); im.color=new Color(.88f,.83f,.98f);
            var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=im;
            var t=Text("Label",r,label,22,Vector2.zero,Vector2.one,new Vector2(6,4),new Vector2(-6,-4)); t.alignment=TextAlignmentOptions.Center; return b;
        }
        static Canvas Canvas(string name,int order)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); var c=g.GetComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay; c.sortingOrder=order;
            var sc=g.GetComponent<CanvasScaler>(); sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution=new Vector2(1280,720); sc.matchWidthOrHeight=1; return c;
        }
        static void WorldLabel(string name, string value, Vector3 p, Transform parent)
        {
            var g=new GameObject(name); g.transform.SetParent(parent); g.transform.position=p; g.transform.rotation=Quaternion.Euler(0,180,0);
            var t=g.AddComponent<TextMeshPro>(); t.font=font; t.text=value; t.fontSize=3; t.alignment=TextAlignmentOptions.Center; t.color=new Color(.08f,.16f,.18f);
            t.rectTransform.sizeDelta=new Vector2(10,3);
        }
        static GameObject Clone(GameObject src, Scene scene)
        {
            var g=UnityEngine.Object.Instantiate(src); g.name=src.name; SceneManager.MoveGameObjectToScene(g,scene); return g;
        }
        public static void Build()
        {
            if(EditorApplication.isPlaying) throw new Exception("Stop play mode before building.");
            EditorSceneManager.SaveOpenScenes();
            string gameplay="Assets/_Project/Scenes/01_Gameplay.unity";
            if(SceneManager.GetActiveScene().path!=gameplay) EditorSceneManager.OpenScene(gameplay);
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.Directory.CreateDirectory("TutorialBackups/20261002");
            System.IO.File.Copy(gameplay,"TutorialBackups/20261002/01_Gameplay.unity",true);
            string tutorialPath="Assets/_Project/Scenes/00_Tutorial.unity";
            if(System.IO.File.Exists(tutorialPath)) System.IO.File.Copy(tutorialPath,"TutorialBackups/20261002/00_Tutorial.previous.unity",true);
            font=GameObject.Find("HUD Canvas").GetComponent<HUDController>().clockText.font;
            cream=Mat("Tutorial_Cream",new Color(.85f,.84f,.76f)); stone=Mat("Tutorial_Walkway",new Color(.61f,.65f,.65f));
            green=Mat("Tutorial_Grass",new Color(.29f,.46f,.29f)); gold=Mat("Tutorial_Gold",new Color(.95f,.69f,.24f)); dark=Mat("Tutorial_Teal",new Color(.10f,.25f,.27f));
            string[] names={"Player","Main Camera","Directional Light","EventSystem","HUD Canvas","Phone Canvas","Pause Canvas","MinimapCamera","Minimap Canvas","PortraitCamera","AudioManager","Quest Canvas"};
            var sources=new Dictionary<string,GameObject>(); foreach(var n in names) sources[n]=GameObject.Find(n);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var clones=new Dictionary<string,GameObject>(); foreach(var n in names) if(sources[n]!=null) clones[n]=Clone(sources[n],scene);
            SceneManager.SetActiveScene(scene);
            var player=clones["Player"]; player.transform.position=new Vector3(0, .65f,-7); player.transform.rotation=Quaternion.identity;
            UnityEngine.Object.DestroyImmediate(player.GetComponent<StatDecay>());
            UnityEngine.Object.DestroyImmediate(player.GetComponent<PlayerExhaustion>());
            var camera=clones["Main Camera"]; var rig=camera.GetComponent<IsometricCameraRig>(); rig.target=player.transform; rig.yawAngle=45; rig.orthoSize=10;
            var fader=camera.GetComponent<WallFader>(); if(fader!=null) UnityEngine.Object.DestroyImmediate(fader);
            player.GetComponent<PlayerMovement>().cameraTransform=camera.transform;
            var interaction=player.GetComponent<PlayerInteraction>(); interaction.interactableLayer=~0;
            var mini=clones["MinimapCamera"].GetComponent<Camera>(); mini.orthographicSize=22;
            var follow=mini.GetComponent<MinimapFollow>(); follow.target=player.transform; follow.cameraTransform=camera.transform;
            var map=clones["Minimap Canvas"].GetComponent<MinimapToggle>(); map.minimapCam=mini;
            var phone=clones["Phone Canvas"].GetComponent<PhoneController>(); phone.minimapCam=mini;
            if(mini.targetTexture!=null) { var rt=AssetDatabase.LoadAssetAtPath<RenderTexture>(Folder+"/TutorialMapRT.renderTexture"); if(rt==null) { rt=new RenderTexture(512,512,24); rt.name="TutorialMapRT"; AssetDatabase.CreateAsset(rt,Folder+"/TutorialMapRT.renderTexture"); } mini.targetTexture=rt; phone.mapImage.texture=rt; foreach(var im in clones["Minimap Canvas"].GetComponentsInChildren<RawImage>(true)) im.texture=rt; }
            var portrait=clones["PortraitCamera"].GetComponent<NisitSimulator.UI.PortraitCam>();
            portrait.display=clones["HUD Canvas"].GetComponentInChildren<RawImage>(true); var soPortrait=new SerializedObject(portrait); var targetProp=soPortrait.FindProperty("target"); if(targetProp!=null) {targetProp.objectReferenceValue=player.transform; soPortrait.ApplyModifiedPropertiesWithoutUndo();}
            // Keep the cloned gameplay HUD unchanged; its controller initializes the practice values.
            var gm=new GameObject("GameManager"); gm.AddComponent<GameManager>();
            var clock=gm.AddComponent<GameClock>(); clock.gameMinutesPerRealSecond=0;
            var world=new GameObject("Tutorial_Campus");
            Box("Ground",new Vector3(0,-.3f,0),new Vector3(48,.5f,40),green,world.transform);
            Box("CampusWalk",new Vector3(0,-.05f,0),new Vector3(9,.1f,24),stone,world.transform);
            Box("ActivitySquare",new Vector3(0,-.05f,3),new Vector3(30,.1f,10),stone,world.transform);
            Box("CampusRoad",new Vector3(0,-.05f,-15),new Vector3(48,.1f,6),dark,world.transform);
            for(int x=-20;x<=20;x+=5) Box("RoadMark",new Vector3(x,.01f,-15),new Vector3(2,.02f,.12f),cream,world.transform);
            Box("UniversitySign",new Vector3(0,3,8),new Vector3(12,2,.3f),cream,world.transform);
            WorldLabel("WelcomeSign","NISIT SIMULATOR\nพื้นที่ฝึกหน้ามหาวิทยาลัย",new Vector3(0,3,7.79f),world.transform);
            for(int x=-6;x<=6;x+=12) Box("GatePillar",new Vector3(x,2,8),new Vector3(.65f,4,.65f),gold,world.transform);
            for(int x=-20;x<=20;x+=8) {
                Box("TreeTrunk",new Vector3(x,1.1f,9),new Vector3(.4f,2.2f,.4f),dark,world.transform);
                var tree=GameObject.CreatePrimitive(PrimitiveType.Sphere); tree.name="TreeCrown"; tree.transform.SetParent(world.transform); tree.transform.position=new Vector3(x,3.1f,9); tree.transform.localScale=new Vector3(3,3,3); tree.GetComponent<Renderer>().sharedMaterial=green;
            }
            var waypoint=GameObject.CreatePrimitive(PrimitiveType.Cylinder); waypoint.name="WalkDestination"; waypoint.transform.position=new Vector3(0,.035f,-1); waypoint.transform.localScale=new Vector3(2.2f,.025f,2.2f); waypoint.GetComponent<Renderer>().sharedMaterial=gold; UnityEngine.Object.DestroyImmediate(waypoint.GetComponent<Collider>());
            var checkpoint=Box("TutorialCheckpoint",new Vector3(2,.8f,1),new Vector3(1,1.6f,.3f),cream); var cp=checkpoint.AddComponent<TutorialCheckpoint>();
            WorldLabel("CheckpointLabel","ป้ายต้อนรับ\n[E]",new Vector3(2,2, .75f),checkpoint.transform);
            var activities=new GameObject("PracticeActivities");
            string[] activityNames={"เรียน","กินอาหาร","พักผ่อน"}; float[] xs={-7,0,7};
            for(int i=0;i<3;i++) {
                var station=Box("Practice_"+i,new Vector3(xs[i],.5f,5),new Vector3(2,1,1),i==1?gold:cream,activities.transform);
                var a=station.AddComponent<ActivityStation>(); var so=new SerializedObject(a);
                so.FindProperty("activityName").stringValue=activityNames[i]+" (ฝึก)";
                so.FindProperty("energyChange").floatValue=i==0?-10:i==2?25:0;
                so.FindProperty("hungerChange").floatValue=i==1?30:0;
                so.FindProperty("knowledgeChange").floatValue=i==0?10:0;
                so.FindProperty("satisfactionChange").floatValue=i==2?10:0; so.ApplyModifiedPropertiesWithoutUndo();
                WorldLabel("ActivityLabel",activityNames[i]+"\n[E]",new Vector3(xs[i],2,4.4f),station.transform);
            }
            var canvas=Canvas("Onboarding Canvas",30); var tutorial=canvas.gameObject.AddComponent<OnboardingTutorial>();
            var card=Rect("TutorialCard",canvas.transform,Vector2.zero,Vector2.zero,new Vector2(24,24),new Vector2(510,424));
            card.gameObject.AddComponent<Image>().color=new Color(.955f,.93f,.985f,1);
            tutorial.progress=Text("Progress",card,"",18,new Vector2(0,1),Vector2.one,new Vector2(24,-44),new Vector2(-24,-12));
            tutorial.title=Text("Title",card,"",30,new Vector2(0,1),Vector2.one,new Vector2(24,-90),new Vector2(-24,-52));
            tutorial.body=Text("Body",card,"",23,Vector2.zero,Vector2.one,new Vector2(24,118),new Vector2(-24,-100));
            tutorial.status=Text("Status",card,"",20,new Vector2(0,0),new Vector2(1,0),new Vector2(24,62),new Vector2(-24,111));
            tutorial.back=Button("Back",card,"ย้อนกลับ",Vector2.zero,Vector2.zero,new Vector2(24,17),new Vector2(165,58));
            tutorial.next=Button("Next",card,"ถัดไป",new Vector2(1,0),new Vector2(1,0),new Vector2(-268,17),new Vector2(-24,58));
            var utility=Canvas("Tutorial Utility",1000);
            tutorial.skip=Button("Skip",utility.transform,"ข้ามการสอน",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-105,-58),new Vector2(105,-14));
            var badge=Rect("PracticeBadge",canvas.transform,new Vector2(.43f,0),new Vector2(.80f,0),new Vector2(0,20),new Vector2(0,64)); badge.gameObject.AddComponent<Image>().color=new Color(.955f,.93f,.985f,.95f); tutorial.satisfactionText=Text("PracticeInfo",badge,"",18,Vector2.zero,Vector2.one,new Vector2(10,6),new Vector2(-10,-6));
            tutorial.player=player.transform; tutorial.destination=waypoint.transform; tutorial.cameraRig=rig; tutorial.phone=phone; tutorial.map=map; tutorial.pause=clones["Pause Canvas"].GetComponent<PauseMenu>(); tutorial.checkpoint=checkpoint; tutorial.activities=activities; cp.tutorial=tutorial;
            var highlight=Rect("HUDHighlight",canvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-154),new Vector2(380,-8)); tutorial.hudHighlight=highlight.gameObject.AddComponent<Image>(); tutorial.hudHighlight.color=new Color(1,.8f,.2f,.2f); tutorial.hudHighlight.raycastTarget=false;
            // Practice components are scene-owned. No GameplayBootstrap, persistent world, inventory or save loader.
            PrefabUtility.SaveAsPrefabAsset(player,Folder+"/Tutorial_Player.prefab");
            EditorSceneManager.SaveScene(scene,tutorialPath);
            var build=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes); if(!build.Exists(s=>s.path==tutorialPath)) build.Add(new EditorBuildSettingsScene(tutorialPath,true)); EditorBuildSettings.scenes=build.ToArray();
            EditorSceneManager.OpenScene("Assets/Scenes/Scene1.unity",OpenSceneMode.Single);
            var existing=GameObject.Find("TutorialReplay Canvas"); if(existing!=null) UnityEngine.Object.DestroyImmediate(existing);
            var replayCanvas=Canvas("TutorialReplay Canvas",40); var replay= replayCanvas.gameObject.AddComponent<OnboardingReplayMenu>();
            replay.replayButton=Button("ReplayTutorial",replayCanvas.transform,"สอนเล่นอีกครั้ง",Vector2.zero,Vector2.zero,new Vector2(24,20),new Vector2(264,72));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(tutorialPath,OpenSceneMode.Single);
            Debug.Log("[Onboarding] Built 00_Tutorial and menu replay entry.");
        }
    }
}
#endif
