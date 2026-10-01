using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using NisitSimulator.Interaction;

public static class CentralCanteenBuilder
{
    const string Dir="Assets/_Project/Prefabs/CentralCanteen";
    static TMP_FontAsset font;
    static Material cream,gold,steel,floor,yellow,white,glass,red,green;
    static int meshId;
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static Material Mat(string n,Color c){string path=Dir+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=c;m.enableInstancing=true;return m;}
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material m,bool collision=true){
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
        if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static void Beam(Transform p,string n,Vector3 a,Vector3 b,float w,Material m){var g=Box(p,n,(a+b)/2,new Vector3(w,(b-a).magnitude,w),m,false);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Text(Transform p,string n,string s,Vector3 pos,float size,Color color,float width=5){
        var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,180,0);
        var t=g.AddComponent<TextMeshPro>();t.font=font;t.text=s;t.fontSize=size*10;t.enableAutoSizing=true;t.fontSizeMin=.8f;t.fontSizeMax=size*10;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,2);t.enableWordWrapping=false;
    }
    static void Panel(Transform p,string n,Vector3[] vs,Material m){
        var g=new GameObject(n);g.transform.SetParent(p,false);var mesh=new Mesh();mesh.name=n;
        var verts=new List<Vector3>();var inds=new List<int>();
        for(int i=1;i<vs.Length-1;i++){int k=verts.Count;verts.Add(vs[0]);verts.Add(vs[i]);verts.Add(vs[i+1]);verts.Add(vs[0]);verts.Add(vs[i+1]);verts.Add(vs[i]);for(int j=0;j<6;j++)inds.Add(k+j);}
        mesh.SetVertices(verts);mesh.SetTriangles(inds,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Dir+"/Surface_"+(meshId++)+".asset");
        g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=m;
    }
    [MenuItem("Nisit/Central Canteen/Build")]
    public static void Build(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="01_Gameplay")throw new Exception("Open 01_Gameplay before building.");
        string backup="Assets/_Project/Scenes/Backups/Canteen_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        System.IO.Directory.CreateDirectory("Assets/_Project/Scenes/Backups");EditorSceneManager.SaveScene(scene,backup,true);
        System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Mitr SDF.asset");
        if(!font)throw new Exception("Thai font missing");
        font.TryAddCharacters("โรงอาหารกลางตลาดน้อยร้านข้าวแกงก๋วยเตี๋ยวตามสั่งส้มตำไก่ย่างข้าวมันไก่ข้าวหมูแดงราดหน้าขนมจีนอาหารเจน้ำผลไม้กาแฟขนมหวานคืนภาชนะล้างมือบริการน้ำแยกขยะ0123456789บาท",out string missing);
        cream=Mat("Cream",new Color(.88f,.86f,.75f));gold=Mat("Gold",new Color(.56f,.44f,.26f));steel=Mat("Steel",new Color(.12f,.16f,.18f));floor=Mat("Floor",new Color(.57f,.6f,.59f));yellow=Mat("Yellow",new Color(1,.72f,.035f));white=Mat("White",new Color(.88f,.91f,.9f));glass=Mat("Glass",new Color(.18f,.37f,.39f));red=Mat("Red",new Color(.68f,.11f,.13f));green=Mat("Green",new Color(.22f,.48f,.18f));
        // Keep old objects and their gameplay references, hide their old shell.
        foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))){
            if(t.name=="โรงอาหาร"){foreach(var r in t.GetComponentsInChildren<Renderer>(true))r.enabled=false;foreach(var c in t.GetComponentsInChildren<Collider>(true))c.enabled=false;}
            if(t.name=="Door_โรงอาหาร"){foreach(var b in t.GetComponents<Behaviour>())b.enabled=false;foreach(var c in t.GetComponents<Collider>())c.enabled=false;}
            if(t.name.StartsWith("KR_Tree")&&t.position.x>-66&&t.position.x<-32&&t.position.z>79&&t.position.z<113)t.gameObject.SetActive(false);
        }
        var old=GameObject.Find("Central_Canteen");if(old)throw new Exception("Central_Canteen already exists; use backup before rebuilding.");
        meshId=0;var root=new GameObject("Central_Canteen");Undo.RegisterCreatedObjectUndo(root,"Build central canteen");
        root.transform.position=new Vector3(-49,0,93);
        var shell=Group(root.transform,"Structure");var roof=Group(root.transform,"Roof_Trusses");var shops=Group(root.transform,"Food_Shops");var seats=Group(root.transform,"Dining_Tables");var lights=Group(root.transform,"Lighting");var site=Group(root.transform,"Exterior");var utility=Group(root.transform,"Services");
        Box(shell,"Floor",new Vector3(0,-.13f,0),new Vector3(32,.3f,24),floor);
        Box(shell,"BackWall",new Vector3(0,2.3f,-12),new Vector3(32,4.6f,.18f),cream);
        Box(shell,"WestWall",new Vector3(-16,2.3f,0),new Vector3(.18f,4.6f,24),cream);
        Box(shell,"EastWall",new Vector3(16,2.3f,0),new Vector3(.18f,4.6f,24),cream);
        for(int x=-16;x<=16;x+=4)Box(shell,"FrontColumn",new Vector3(x,2.3f,12),new Vector3(.24f,4.6f,.24f),cream);
        for(int z=-12;z<=12;z+=6){Box(shell,"SideColumn",new Vector3(-15.7f,2.3f,z),new Vector3(.3f,4.6f,.3f),gold);Box(shell,"SideColumn",new Vector3(15.7f,2.3f,z),new Vector3(.3f,4.6f,.3f),gold);}
        // Sloping stepped roof, front glazing and vertical cream cladding.
        for(int z=-12;z<=12;z+=24){
            Panel(roof,"FrontGable",new[]{new Vector3(-16,4.6f,z),new Vector3(-4,7,z),new Vector3(4,7,z),new Vector3(16,4.6f,z)},glass);
            for(int x=-16;x<=16;x++){float top=7-Mathf.Max(0,Mathf.Abs(x)-4)*.2f;Box(roof,"FacadeRib",new Vector3(x,(top+4.6f)/2,z+.035f),new Vector3(.065f,top-4.6f,.1f),gold,false);}
        }
        Panel(roof,"LeftSlope",new[]{new Vector3(-16.5f,4.6f,-12.5f),new Vector3(-4,7,-12.5f),new Vector3(-4,7,12.5f),new Vector3(-16.5f,4.6f,12.5f)},cream);
        Panel(roof,"RightSlope",new[]{new Vector3(4,7,-12.5f),new Vector3(16.5f,4.6f,-12.5f),new Vector3(16.5f,4.6f,12.5f),new Vector3(4,7,12.5f)},cream);
        Box(roof,"RaisedClerestory",new Vector3(0,7.45f,0),new Vector3(8,.9f,24),glass,false);
        Box(roof,"RaisedRoof",new Vector3(0,7.95f,0),new Vector3(8.6f,.15f,25),cream,false);
        for(int x=-4;x<=4;x++)Box(roof,"UpperVerticalPanel",new Vector3(x,7.45f,12.08f),new Vector3(.09f,.9f,.1f),gold,false);
        for(int z=-12;z<=12;z+=4){
            Beam(roof,"TrussChord",new Vector3(-16,4.5f,z),new Vector3(16,4.5f,z),.1f,steel);
            Beam(roof,"TrussSlope",new Vector3(-16,4.5f,z),new Vector3(0,7,z),.1f,steel);Beam(roof,"TrussSlope",new Vector3(0,7,z),new Vector3(16,4.5f,z),.1f,steel);
            for(int x=-12;x<=12;x+=4){float h=7-Mathf.Abs(x)*.15625f;Beam(roof,"TrussVertical",new Vector3(x,4.5f,z),new Vector3(x,h,z),.055f,steel);Beam(roof,"TrussDiagonal",new Vector3(x-4,4.5f,z),new Vector3(x,h,z),.055f,steel);}
        }
        for(int x=-12;x<=12;x+=4)Beam(roof,"Purlin",new Vector3(x,6.9f-Mathf.Max(0,Mathf.Abs(x)-4)*.2f,-12),new Vector3(x,6.9f-Mathf.Max(0,Mathf.Abs(x)-4)*.2f,12),.07f,steel);
        Box(site,"FrontCanopy",new Vector3(0,3.4f,13.3f),new Vector3(33,.15f,3.2f),gold,false);
        Text(site,"MainThaiSign","โรงอาหารกลาง",new Vector3(3.5f,4.15f,14.97f),2,Color.white,22);
        Text(site,"SecondarySign","(ตลาดน้อย)",new Vector3(10,5.05f,12.15f),.85f,Color.white,8);
        Box(site,"Apron",new Vector3(0,-.035f,15.5f),new Vector3(34,.1f,7),floor);
        Box(site,"AccessWalk",new Vector3(0,-.04f,20.8f),new Vector3(4,.08f,4),white);
        for(int x=-14;x<=14;x+=3){if(Mathf.Abs(x)<4)continue;Box(site,"ParkingLine",new Vector3(x,.026f,16.5f),new Vector3(.07f,.015f,4),white,false);}
        string[] names={"ข้าวแกง","ก๋วยเตี๋ยว","อาหารตามสั่ง","ส้มตำไก่ย่าง","ข้าวมันไก่","ข้าวหมูแดง","ราดหน้า","ขนมจีน","อาหารเจ","น้ำผลไม้","กาแฟ","ขนมหวาน"};
        for(int i=0;i<12;i++){
            var s=Group(shops,"Shop_"+(i+1).ToString("00"));bool east=i>=6;float z=-9.25f+(i%6)*3.5f;
            s.localPosition=new Vector3(east?13.5f:-13.5f,0,z);s.localRotation=Quaternion.Euler(0,east?-90:90,0);
            Box(s,"Counter",new Vector3(0,.55f,.6f),new Vector3(3.25f,1.1f,.9f),white);
            Box(s,"Accent",new Vector3(0,.55f,1.06f),new Vector3(3.25f,.5f,.03f),i%2==0?green:yellow,false);
            Box(s,"ShopSign",new Vector3(0,2.85f,0),new Vector3(3.4f,.65f,.1f),red,false);
            Text(s,"ThaiShopName",names[i],new Vector3(0,2.87f,.07f),.45f,Color.white,3.25f);
            Text(s,"Menu","เมนู "+names[i]+"\n35–50 บาท",new Vector3(0,1.95f,0),.3f,Color.black,3);
            Box(s,"PrepTable",new Vector3(0,.85f,-1),new Vector3(3.1f,.13f,.7f),steel);
            var trigger=Box(s,"BuyFood_E",new Vector3(0,1,1.3f),new Vector3(3,1.8f,.4f),white);
            UnityEngine.Object.DestroyImmediate(trigger.GetComponent<Renderer>());UnityEngine.Object.DestroyImmediate(trigger.GetComponent<MeshFilter>());
            int layer=LayerMask.NameToLayer("Interactable");if(layer>=0)trigger.layer=layer;
            trigger.GetComponent<Collider>().isTrigger=true;var vendor=trigger.AddComponent<TalkNPC>();vendor.npcName=names[i];vendor.isVendor=true;vendor.vendorIsShop=false;
            if(i==0){var tmp=Group(seats,"TableTemplate");MakeTable(tmp);PrefabUtility.SaveAsPrefabAsset(tmp.gameObject,Dir+"/Canteen_Table.prefab");UnityEngine.Object.DestroyImmediate(tmp.gameObject);}
        }
        var tablePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Canteen_Table.prefab");
        for(int row=0;row<6;row++)for(int col=0;col<5;col++){
            var g=(GameObject)PrefabUtility.InstantiatePrefab(tablePrefab,seats);g.name="Dining_"+row+"_"+col;
            float x=col<2?-8+col*3.3f:3+(col-2)*3.3f;g.transform.localPosition=new Vector3(x,0,-8.5f+row*3.4f);
        }
        for(int z=-8;z<=8;z+=8)for(int x=-8;x<=8;x+=8){
            var l=Group(lights,"Pendant");l.localPosition=new Vector3(x,4.35f,z);
            Box(l,"Fixture",Vector3.zero,new Vector3(1.2f,.08f,.2f),white,false);
            var light=l.gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=9;light.intensity=1.6f;light.color=new Color(1,.94f,.82f);light.shadows=LightShadows.None;
        }
        string[] labels={"คืนภาชนะ","ล้างมือ","บริการน้ำ","แยกขยะ"};
        for(int i=0;i<4;i++){var u=Group(utility,labels[i]);u.localPosition=new Vector3(-7.5f+i*5,0,-10.8f);Box(u,"Station",new Vector3(0,.45f,0),new Vector3(1.6f,.9f,.6f),i==3?green:white);Text(u,"Label",labels[i],new Vector3(0,1.35f,.35f),.36f,Color.black,3);
        }
        var door=GameObject.Find("Door_โรงอาหาร");if(door)door.transform.position=root.transform.TransformPoint(new Vector3(0,0,13));
        var label=GameObject.Find("KR_Campus/KR_Labels/Label_ตลาดน้อย (โรงอาหาร)");if(label)label.transform.position=root.transform.TransformPoint(new Vector3(0,8,0));
        PrefabUtility.SaveAsPrefabAssetAndConnect(root,Dir+"/Central_Canteen.prefab",InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=root;
        Debug.Log("CANTEEN_BUILD_OK: 12 shops, 30 tables, existing food shop integration. Backup: "+backup+" Missing Thai glyphs: "+missing);
    }
    static void MakeTable(Transform p){
        Box(p,"TableTop",new Vector3(0,.78f,0),new Vector3(2.4f,.09f,.8f),white);
        foreach(float x in new[]{-.9f,.9f}){Box(p,"TableLeg",new Vector3(x,.38f,0),new Vector3(.08f,.72f,.65f),steel);foreach(float z in new[]{-.8f,.8f})Box(p,"BenchLeg",new Vector3(x,.23f,z),new Vector3(.07f,.44f,.36f),steel);}
        foreach(float z in new[]{-.8f,.8f})Box(p,"YellowBench",new Vector3(0,.48f,z),new Vector3(2.4f,.08f,.38f),yellow);
    }
}
