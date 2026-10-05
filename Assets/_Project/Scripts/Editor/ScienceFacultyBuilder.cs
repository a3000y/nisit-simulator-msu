using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using TMPro;
using NisitSimulator.CameraRig;

/// <summary>Assembles the reference storefronts from assets already in this project.</summary>
public static class ScienceFacultyBuilder
{
    const string Dir = "Assets/_Project/Prefabs/ScienceFaculty";
    const string RootName = "MSU_Science_Faculty";
    const string School = "Assets/school/Prefabs/";
    const string City = "Assets/Synty/PolygonCity/Prefabs/";
    const string Generic = "Assets/Synty/PolygonGeneric/Prefabs/";
    static Material cream, ivory, floor, terracotta, gold, brick, dark, glass, green, red, asphalt;
    static TMP_FontAsset font;
    static Material doorGlass, leaves;
    static readonly Dictionary<string, Material> brickVariants = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> brickMeshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, int> sources = new Dictionary<string, int>();
    static readonly Dictionary<Material, Material> compatibleMaterials = new Dictionary<Material, Material>();
    static int accents;

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Material CopyMaterial(string name, string source, Color color)
    {
        var original = AssetDatabase.LoadAssetAtPath<Material>(source);
        if (!original) throw new Exception("Missing source material: " + source);
        var material = new Material(original) { name = name, color = color, enableInstancing = true };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
        AssetDatabase.CreateAsset(material, Dir + "/Materials/" + name + ".mat");
        return material;
    }

    static Material CompatibleMaterial(Material original)
    {
        if (!original || original.shader.name != "Standard") return original;
        if (compatibleMaterials.TryGetValue(original, out var cached)) return cached;
        var texture = original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null;
        var color = original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
        bool transparent = original.HasProperty("_Mode") && original.GetFloat("_Mode") > 0;
        var copy = new Material(original) { name = "Science_Source_" + original.name };
        copy.shader = cream.shader;
        copy.shaderKeywords = Array.Empty<string>();
        copy.SetTexture("_BaseMap", texture);
        copy.SetColor("_BaseColor", color);
        copy.SetFloat("_Smoothness", .2f);
        if (transparent)
        {
            copy.SetFloat("_Surface", 1);
            copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            copy.SetFloat("_ZWrite", 0);
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            copy.renderQueue = (int)RenderQueue.Transparent;
            copy.SetShaderPassEnabled("ShadowCaster", false);
        }
        string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original)).Substring(0, 8);
        AssetDatabase.CreateAsset(copy, Dir + "/Materials/" + copy.name + "_" + id + ".mat");
        compatibleMaterials.Add(original, copy);
        return copy;
    }

    // Normalize source pivots, then fit by visible mesh bounds. Keep the nested prefab link.
    static Transform Fit(Transform parent, string name, string path, Vector3 center, Vector3 size,
        Material overrideMaterial = null, bool collision = true, float yaw = 0)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) throw new Exception("Missing source prefab: " + path);
        var wrapper = Group(parent, name);
        var source = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper);
        source.transform.localPosition = Vector3.zero;
        source.transform.localRotation = Quaternion.identity;
        source.transform.localScale = Vector3.one;
        var renderers = source.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new Exception("Source has no renderers: " + path);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var offset = wrapper.InverseTransformPoint(bounds.center);
        source.transform.localPosition -= offset;
        foreach (var collider in source.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        if (overrideMaterial)
            foreach (var renderer in renderers)
                renderer.sharedMaterials = Enumerable.Repeat(overrideMaterial, renderer.sharedMaterials.Length).ToArray();
        else
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select((material, index) =>
                    material ? CompatibleMaterial(material) :
                    name == "Glass_Display_Counter" && index == 0 ? glass :
                    name == "Checkout_Register" ? dark : ivory).ToArray();
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            int count = filter && filter.sharedMesh ? filter.sharedMesh.subMeshCount : renderer.sharedMaterials.Length;
            renderer.sharedMaterials = Enumerable.Range(0, count).Select(i => i < renderer.sharedMaterials.Length ? renderer.sharedMaterials[i] : ivory).ToArray();
            if (path.Contains("IT_Window"))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m && m.name.Contains("Glass") ? glass : m).ToArray();
            renderer.gameObject.isStatic = true;
        }
        if (collision)
        {
            var box = wrapper.gameObject.AddComponent<BoxCollider>();
            box.size = bounds.size;
        }
        wrapper.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
        wrapper.localRotation = Quaternion.Euler(0, yaw, 0);
        wrapper.localPosition = center;
        if (overrideMaterial && overrideMaterial.name.StartsWith("Science_Brick"))
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true)) ApplyBrickUV(filter);
        wrapper.gameObject.isStatic = true;
        if (!sources.ContainsKey(path)) sources[path] = 0;
        sources[path]++;
        return wrapper;
    }

    static Transform Wall(Transform parent, string name, Vector3 center, float width, float height,
        Material material, bool collision = true, float thickness = .18f)
    {
        return Fit(parent, name, School + "props/wall4 (1).prefab", center,
            new Vector3(thickness, height, width), material, collision, 90);
    }

    static Transform Slab(Transform parent, string name, Vector3 center, Vector3 size,
        Material material, bool collision = true)
    {
        return Fit(parent, name, School + "road/floor.prefab", center, size, material, collision);
    }

    // Basic shapes supplement only decorative details absent from the installed packs.
    static Transform Accent(Transform parent, string name, Vector3 center, Vector3 size,
        Material material, bool collision = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = true;
        accents++;
        return go.transform;
    }

    static void Line(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
    {
        var beam = Accent(parent, name, (a + b) / 2, new Vector3(width, (b - a).magnitude, .045f), material);
        beam.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
    }

    static void Label(Transform parent, string name, string text, Vector3 position, float height,
        float width, Color color)
    {
        var group = Group(parent, name);
        group.localPosition = position;
        var tmp = group.gameObject.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = height * 10;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1;
        tmp.fontSizeMax = height * 10;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(width, height * 1.8f);
        tmp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        tmp.ForceMeshUpdate(true, true);
    }

    static Material BrickSurface(float width, float height)
    {
        string key = width.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "_" + height.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        if (brickVariants.TryGetValue(key, out var material)) return material;
        material = new Material(brick) { name = "Science_Brick_" + key };
        material.SetTextureScale("_BaseMap", Vector2.one);
        AssetDatabase.CreateAsset(material, Dir + "/Materials/" + material.name + ".mat");
        brickVariants.Add(key, material);
        return material;
    }

    static void SolidFront(Transform parent, string name, float x, float y, float z, float width, float height, bool brickFace)
    {
        Wall(parent, name, new Vector3(x, y, z), width, height, brickFace ? BrickSurface(width, height) : cream);
    }

    static void ApplyBrickUV(MeshFilter filter)
    {
        var scale=filter.transform.lossyScale;
        string key=scale.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+scale.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+scale.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        if(!brickMeshes.TryGetValue(key,out var mesh))
        {
            mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Science_BrickUV_"+key;
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var v=Vector3.Scale(vertices[i]-mesh.bounds.min,scale);var n=normals[i];
                uv[i]=Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y)/3.2f:Mathf.Abs(n.z)>.5f?new Vector2(v.x,v.y)/3.2f:new Vector2(v.x,v.z)/3.2f;
            }
            mesh.uv=uv;mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,Dir+"/Meshes/"+mesh.name+".asset");brickMeshes.Add(key,mesh);
        }
        filter.sharedMesh=mesh;
    }

    static Transform Item(Transform parent, string name, string path, float x, float z, Vector3 size, float yaw = 0, float baseY = .48f, bool collision = true)
    {
        return Fit(parent, name, path, new Vector3(x, baseY + size.y / 2, z), size, null, collision, yaw);
    }

    static Transform MeshPart(Transform parent, string name, Vector3[] vertices, int[] triangles, Material material, bool collision)
    {
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Dir + "/Meshes/" + name + ".asset");
        var group = Group(parent, name);
        group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        group.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (collision) group.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        group.gameObject.isStatic = true; accents++;
        return group;
    }

    static void RingSlabs(Transform parent, string name, float y, float height, Material material, bool collision = true, float extra = 0)
    {
        foreach (float z in new[] { -9f, 9f })
            Slab(parent, name + (z < 0 ? "_Front" : "_Rear"), new Vector3(0,y,z),new Vector3(36+extra,height,6+extra),material,collision);
        foreach (float x in new[] { -14f,14f })
            Slab(parent,name+(x<0?"_Left":"_Right"),new Vector3(x,y,0),new Vector3(8+extra,height,12),material,collision);
    }

    static void Facades(Transform parent, bool upper)
    {
        int start = upper ? 1 : 0, end = upper ? 4 : 1;
        for (int index=start;index<end;index++)
        {
            float bottom=index==0?.48f:3.98f+(index-1)*3f;
            float height=index==0?3.4f:3f;
            var storey=Group(parent,"Storey_"+(index+1)+"_Exterior");
            foreach(float z in new[]{-12f,12f})
            {
                if(index==0)
                {
                    foreach(float x in new[]{-9.8f,9.8f})
                    {
                        SolidFront(storey,"Entry_Flanking_Brick_Wall",x,bottom+height/2,z,16.4f,height,true);
                        Wall(storey,"Entry_Flanking_Cream_Band",new Vector3(x,bottom+.15f,z+(z<0?-.14f:.14f)),16.4f,.4f,ivory,false,.24f);
                    }
                    SolidFront(storey,"Entrance_Lintel",0,3.62f,z,3.2f,.52f,false);
                }
                else
                {
                    SolidFront(storey,"Outer_Brick_Wall",0,bottom+height/2,z,36,height,true);
                    Wall(storey,"Outer_Cream_Band",new Vector3(0,bottom+.15f,z+(z<0?-.14f:.14f)),36.3f,.4f,ivory,false,.24f);
                }
                foreach(float x in new[]{-14.5f,-8.6f,0,8.6f,14.5f})
                {
                    if(index==0&&x==0)continue;
                    Fit(storey,"Outer_Blue_Window_Row","Assets/_Project/Prefabs/ITBuilding/IT_Window_440x180.prefab",new Vector3(x,bottom+1.72f,z+(z<0?-.14f:.14f)),new Vector3(x==0?6.8f:4.8f,1.56f,.38f),null,true,z<0?0:180);
                }
            }
            foreach(float x in new[]{-18f,18f})
            {
                if(index==0)
                {
                    foreach(float z in new[]{-6.8f,6.8f})
                    {
                        Fit(storey,"Side_Entry_Flanking_Wall",School+"props/wall4 (1).prefab",new Vector3(x,bottom+height/2,z),new Vector3(.18f,height,10.4f),BrickSurface(10.4f,height));
                        Fit(storey,"Side_Entry_Cream_Band",School+"props/wall4 (1).prefab",new Vector3(x+(x<0?-.14f:.14f),bottom+.15f,z),new Vector3(.24f,.4f,10.4f),ivory,false);
                    }
                    Fit(storey,"Side_Entrance_Lintel",School+"props/wall4 (1).prefab",new Vector3(x,3.62f,0),new Vector3(.18f,.52f,3.2f),cream);
                }
                else
                {
                    Fit(storey,"Outer_Side_Brick_Wall",School+"props/wall4 (1).prefab",new Vector3(x,bottom+height/2,0),new Vector3(.18f,height,24),BrickSurface(24,height));
                    Fit(storey,"Side_Cream_Band",School+"props/wall4 (1).prefab",new Vector3(x+(x<0?-.14f:.14f),bottom+.15f,0),new Vector3(.24f,.4f,24.3f),ivory,false);
                }
                foreach(float z in index==0?new[]{-8.6f,-4.0f,4.0f,8.6f}:new[]{-8.6f,-3.2f,3.2f,8.6f})
                    Fit(storey,"Side_Blue_Window_Row","Assets/_Project/Prefabs/ITBuilding/IT_Window_440x180.prefab",new Vector3(x+(x<0?-.14f:.14f),bottom+1.72f,z),new Vector3(index==0?4.1f:4.5f,1.56f,.38f),null,true,x<0?90:-90);
            }
            if(!upper)continue;
            // All upper storeys form a ring. The central court remains open to the sky.
            foreach(float z in new[]{-6f,6f})
            {
                SolidFront(storey,"Courtyard_Brick_Wall",0,bottom+height/2,z,20,height,true);
                Wall(storey,"Courtyard_Cream_Band",new Vector3(0,bottom+.15f,z+(z<0?.13f:-.13f)),20.3f,.4f,ivory,false,.23f);
                foreach(float x in new[]{-6.6f,0,6.6f})
                    Fit(storey,"Courtyard_Blue_Window_Row","Assets/_Project/Prefabs/ITBuilding/IT_Window_440x180.prefab",new Vector3(x,bottom+1.72f,z+(z<0?.14f:-.14f)),new Vector3(5.1f,1.56f,.38f),null,true,z<0?180:0);
            }
            foreach(float x in new[]{-10f,10f})
            {
                Fit(storey,"Courtyard_Side_Brick_Wall",School+"props/wall4 (1).prefab",new Vector3(x,bottom+height/2,0),new Vector3(.18f,height,12),BrickSurface(12,height));
                Fit(storey,"Courtyard_Side_Cream_Band",School+"props/wall4 (1).prefab",new Vector3(x+(x<0?.13f:-.13f),bottom+.15f,0),new Vector3(.23f,.4f,12.3f),ivory,false);
                foreach(float z in new[]{-3.2f,3.2f})
                    Fit(storey,"Courtyard_Side_Window","Assets/_Project/Prefabs/ITBuilding/IT_Window_440x180.prefab",new Vector3(x+(x<0?.14f:-.14f),bottom+1.72f,z),new Vector3(4.7f,1.56f,.38f),null,true,x<0?-90:90);
            }
        }
    }

    static void HippedWing(Transform parent,string name,Vector3 center,float width,float depth,float yaw)
    {
        float w=width/2,d=depth/2, ridge=Mathf.Max(0,w-d);
        Vector3 a=new Vector3(-w,12.96f,-d),b=new Vector3(w,12.96f,-d),c=new Vector3(w,12.96f,d),e=new Vector3(-w,12.96f,d);
        Vector3 f=new Vector3(-ridge,14.35f,0),g=new Vector3(ridge,14.35f,0);
        var mesh=MeshPart(parent,name,new[]{a,f,g,b,b,g,c,c,g,f,e,e,f,a},new[]{0,1,2,0,2,3,4,5,6,7,8,9,7,9,10,11,12,13},terracotta,true);
        mesh.localPosition=center;mesh.localRotation=Quaternion.Euler(0,yaw,0);
    }

    static void Roof(Transform upper)
    {
        HippedWing(upper,"Front_Wing_Hipped_Roof",new Vector3(0,0,-9),38.4f,7.2f,0);
        HippedWing(upper,"Rear_Wing_Hipped_Roof",new Vector3(0,0,9),38.4f,7.2f,0);
        HippedWing(upper,"Left_Wing_Hipped_Roof",new Vector3(-14,0,0),14.4f,9.2f,90);
        HippedWing(upper,"Right_Wing_Hipped_Roof",new Vector3(14,0,0),14.4f,9.2f,90);
        RingSlabs(upper,"Wide_Cream_Eaves",12.84f,.18f,cream,true,1.2f);
        foreach(float x in new[]{-11f,11f})
            MeshPart(upper,"Triangular_Roof_Accent_"+(x<0?"Left":"Right"),new[]{
                new Vector3(x-1,14.0f,-10.5f),new Vector3(x,15.5f,-10.5f),new Vector3(x+1,14.0f,-10.5f),
                new Vector3(x-1,14.0f,-10.5f),new Vector3(x,14.3f,-8.9f),new Vector3(x,15.5f,-10.5f),
                new Vector3(x+1,14.0f,-10.5f),new Vector3(x,15.5f,-10.5f),new Vector3(x,14.3f,-8.9f)},new[]{0,1,2,3,4,5,6,7,8},terracotta,false);
        var centers=new[]{new Vector3(0,8.45f,-9),new Vector3(0,8.45f,9),new Vector3(-14,8.45f,0),new Vector3(14,8.45f,0)};
        for(int i=0;i<4;i++)
        {
            var seal=Group(upper,"Upper_Wing_"+i+"_Inaccessible_Volume").gameObject.AddComponent<BoxCollider>();
            seal.center=centers[i];seal.size=i<2?new Vector3(36,8.8f,6):new Vector3(8,8.8f,12);
        }
    }

    static void RoomWalls(Transform structure,bool right,bool rear)
    {
        float x=right?12.5f:-12.5f,sign=rear?1:-1,doorZ=sign*3.4f;
        var room=Group(structure,(right?"Lab":"Classroom")+(rear?"_B_Walls":"_A_Walls"));
        // Open doorway in the side corridor wall; 2.2m clear width and 2.9m clear height.
        foreach(var range in rear?new[]{new Vector2(2.2f,2.3f),new Vector2(4.5f,10)}:new[]{new Vector2(-10,-4.5f),new Vector2(-2.3f,-2.2f)})
            Fit(room,"Corridor_Partition",School+"props/wall4 (1).prefab",new Vector3(x,2.18f,(range.x+range.y)/2),new Vector3(.18f,3.4f,range.y-range.x),cream);
        Fit(room,"Door_Lintel",School+"props/wall4 (1).prefab",new Vector3(x,3.63f,doorZ),new Vector3(.18f,.5f,2.2f),cream);
        foreach(float z in new[]{doorZ-1.1f,doorZ+1.1f})Fit(room,"Room_Door_Frame",School+"props/wall2.prefab",new Vector3(x,1.93f,z),new Vector3(.23f,2.9f,.08f),ivory,false);
        foreach(float z in new[]{sign*2.2f,sign*10})Wall(room,"Room_End_Wall",new Vector3(right?15.2f:-15.2f,2.18f,z),5.5f,3.4f,cream);
        var labelParent=Group(room,"Room_Sign");labelParent.localPosition=new Vector3(x+(right?-.12f:.12f),3.48f,doorZ);labelParent.localRotation=Quaternion.Euler(0,right?90:-90,0);
        Label(labelParent,"Room_Name",right?(rear?"ห้องปฏิบัติการ 104":"ห้องปฏิบัติการ 102"):(rear?"ห้องเรียน 103":"ห้องเรียน 101"),Vector3.zero,.20f,2.6f,new Color(.13f,.26f,.3f));
    }

    static void LightFixture(Transform parent,float x,float z)
    {
        Item(parent,"Existing_Ceiling_Light","Assets/_Project/Prefabs/ITBuilding/IT_LightPanel.prefab",x,z,new Vector3(1.3f,.08f,.55f),0,3.78f,false);
        var group=Group(parent,"Warm_Interior_Light");group.localPosition=new Vector3(x,3.35f,z);
        var light=group.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.96f,.88f);light.intensity=1.7f;light.range=8;light.shadows=LightShadows.None;
    }

    static void Classroom(Transform parent,bool rear)
    {
        var room=Group(parent,rear?"03_Classroom_B":"02_Classroom_A");
        float sign=rear?1:-1;
        foreach(float x in new[]{-16.1f,-13.8f})foreach(float row in new[]{5.7f,8.2f})
        {
            Item(room,"Student_Desk","Assets/_Project/Prefabs/Classroom/StudentDesk.prefab",x,sign*row,new Vector3(1.15f,.75f,.68f),rear?180:0);
            Item(room,"Student_Chair","Assets/_Project/Prefabs/Classroom/StudentChair.prefab",x,sign*(row-1),new Vector3(.5f,1.08f,.64f),rear?0:180);
        }
        Item(room,"Teaching_Whiteboard","Assets/_Project/Prefabs/ITBuilding/IT_Whiteboard.prefab",-15.2f,sign*9.83f,new Vector3(.11f,1.25f,2.6f),rear?90:-90,1.28f,false);
        Item(room,"Teacher_Desk","Assets/_Project/Prefabs/Classroom/StudentDesk.prefab",-16.4f,sign*9.15f,new Vector3(1.15f,.78f,.65f),rear?180:0);
        Item(room,"Storage_Cabinet","Assets/_Project/Prefabs/Classroom/StorageCabinet.prefab",-17.3f,sign*2.9f,new Vector3(1.4f,1.83f,.49f),90);
        LightFixture(room,-15.2f,sign*6.2f);
    }

    static void Lab(Transform parent,bool rear)
    {
        var room=Group(parent,rear?"05_Science_Lab_B":"04_Science_Lab_A");float sign=rear?1:-1;
        foreach(float row in new[]{5.5f,7.7f})
        {
            Item(room,"Science_Laboratory_Bench","Assets/_Project/Prefabs/ITBuilding/IT_GroupTable.prefab",15.3f,sign*row,new Vector3(2.8f,.8f,1.1f));
            for(int i=0;i<4;i++)
            {
                string path=Generic+"Props/SM_Gen_Prop_Bottle_0"+(i+1)+".prefab";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var rs=asset.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                Fit(room,"Sample_Bottle",path,new Vector3(14.5f+i*.5f,1.44f,sign*row),b.size*(.32f/b.size.y),null,false);
            }
            Item(room,"Laboratory_Chair","Assets/_Project/Prefabs/Classroom/StudentChair.prefab",16.2f,sign*(row-1),new Vector3(.5f,.98f,.60f),rear?0:180);
        }
        Item(room,"Laboratory_Sink","Assets/_Project/Art/Models/KayKit_Restaurant/kitchentable_sink_large.fbx",16.6f,sign*9.4f,new Vector3(1.35f,.9f,.85f),rear?0:180);
        Item(room,"Lab_Storage_Cabinet","Assets/_Project/Prefabs/Classroom/StorageCabinet.prefab",13.6f,sign*9.5f,new Vector3(1.55f,1.83f,.49f),rear?0:180);
        Item(room,"Lab_Workbench_With_Equipment","Assets/_Project/Prefabs/ITBuilding/IT_Workbench.prefab",17.25f,sign*3.55f,new Vector3(2f,1.35f,.9f),90);
        Item(room,"Lab_Whiteboard","Assets/_Project/Prefabs/ITBuilding/IT_Whiteboard.prefab",15.2f,sign*9.83f,new Vector3(.11f,1.25f,2.6f),rear?90:-90,1.30f,false);
        LightFixture(room,15.3f,sign*6.2f);
    }

    static void Ramp(Transform parent,string name,Vector3 bottom,Vector3 top,float width)
    {
        Vector3 side=Vector3.Cross(Vector3.up,new Vector3(top.x-bottom.x,0,top.z-bottom.z)).normalized*width/2;
        Vector3 a=bottom-side,b=bottom+side,c=top+side,d=top-side;
        Vector3 e=new Vector3(a.x,0,a.z),f=new Vector3(b.x,0,b.z),g=new Vector3(c.x,0,c.z),h=new Vector3(d.x,0,d.z);
        MeshPart(parent,name,new[]{a,b,c,d,e,f,g,h},new[]{0,3,2,0,2,1,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7},floor,true);
    }

    static void Palm(Transform site,int index,float x,float z,float height)
    {
        var palm=Group(site,"Low_Poly_Palm_"+index);
        var trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);trunk.name="Palm_Trunk";trunk.transform.SetParent(palm,false);trunk.transform.localPosition=new Vector3(x,height/2,z);trunk.transform.localScale=new Vector3(.34f,height/2,.34f);trunk.GetComponent<Renderer>().sharedMaterial=terracotta;
        var vs=new List<Vector3>();var ts=new List<int>();
        for(int i=0;i<9;i++)
        {
            float angle=i*Mathf.PI*2/9;Vector3 dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),side=Vector3.Cross(Vector3.up,dir)*.46f;
            Vector3 start=new Vector3(x,height,z),mid=start+dir*1.6f+Vector3.up*.38f,tip=start+dir*3-Vector3.up*.8f;
            int n=vs.Count;vs.AddRange(new[]{start,mid-side,mid+side,tip});ts.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
        }
        MeshPart(palm,"Palm_Fronds_"+index,vs.ToArray(),ts.ToArray(),leaves,false);
    }

    static void Site(Transform site)
    {
        Slab(site,"Main_Entrance_Landing",new Vector3(0,.4f,-14),new Vector3(16,.16f,4.1f),floor);
        for(int i=0;i<3;i++)
        {
            float top=.16f*(i+1);Slab(site,"Front_Step_"+(i+1),new Vector3(0,top/2,-17.05f+i*.4f),new Vector3(8.6f,top,.42f),terracotta);
        }
        Slab(site,"Front_Plaza",new Vector3(0,.035f,-18.4f),new Vector3(35,.07f,2.4f),floor);
        Slab(site,"Campus_Path_Connection",new Vector3(6,.025f,-18.25f),new Vector3(3.2f,.05f,3.8f),floor);
        Ramp(site,"Main_Accessible_Ramp",new Vector3(6.1f,.07f,-19.5f),new Vector3(6.1f,.48f,-15.9f),2.1f);
        foreach(float x in new[]{-18.8f,18.8f})
        {
            Slab(site,"Side_Exterior_Path",new Vector3(x,.025f,0),new Vector3(1.25f,.05f,27),floor);
            Ramp(site,"Side_Entry_Ramp_"+(x<0?"Left":"Right"),new Vector3(x<0?-19.45f:19.45f,.05f,0),new Vector3(x<0?-17.85f:17.85f,.48f,0),3.2f);
        }
        Slab(site,"Rear_Exterior_Path",new Vector3(0,.025f,13.15f),new Vector3(38.8f,.05f,1.25f),floor);
        Ramp(site,"Rear_Entry_Ramp",new Vector3(0,.05f,13.15f),new Vector3(0,.48f,11.85f),3.2f);
        // Courtyard grass is level with the arcade and unobstructed by furniture or roof.
        Slab(site,"Open_Central_Grass_Court",new Vector3(0,.38f,0),new Vector3(20.02f,.2f,12.02f),green);
        foreach(float z in new[]{-5.7f,5.7f})Slab(site,"Court_Paved_Edge",new Vector3(0,.49f,z),new Vector3(19.7f,.02f,.55f),floor,false);
        foreach(float x in new[]{-9.6f,9.6f})Slab(site,"Court_Paved_Edge",new Vector3(x,.49f,0),new Vector3(.55f,.02f,11.7f),floor,false);
        foreach(float x in new[]{-12.4f,12.4f})
        {
            Slab(site,"Front_Lawn",new Vector3(x,.04f,-14.7f),new Vector3(9.2f,.08f,5.0f),green,false);
            Item(site,"Synty_Low_Hedge",Generic+"Environment/SM_Gen_Env_Bush_01.prefab",x,-17.05f,new Vector3(8.8f,.65f,.75f),0,.08f);
            var tree=Item(site,"Synty_Garden_Tree",City+"Environments/SM_Env_Tree_01.prefab",x+(x<0?-2.3f:2.3f),-13.7f,new Vector3(3.2f,5.4f,3.4f),0,0,false);
            var trunk=tree.gameObject.AddComponent<CapsuleCollider>();
            trunk.center=new Vector3(0,-1.05f/tree.localScale.y,0);trunk.height=3.3f/tree.localScale.y;trunk.radius=.17f/Mathf.Max(tree.localScale.x,tree.localScale.z);
        }
        Palm(site,0,-9.5f,-14.4f,6.3f);Palm(site,1,9.5f,-14.4f,6.5f);Palm(site,2,-15.4f,-15,5.2f);Palm(site,3,15.4f,-15,5.4f);
        foreach(float x in new[]{-5.3f,10.2f})Item(site,"Synty_Outdoor_Bench",City+"Props/SM_Prop_ParkBench_01.prefab",x,-18.5f,new Vector3(2.08f,1.05f,.62f),0,.07f);
        for(int i=0;i<36;i++)
        {
            float x=-17.5f+i;if(x>3.8f&&x<8.2f)continue;
            Slab(site,"Red_White_Curb",new Vector3(x,.10f,-19.7f),new Vector3(.97f,.20f,.23f),i%2==0?red:ivory);
        }
        Wall(site,"Faculty_Monument_Sign",new Vector3(-11.5f,1.3f,-17.1f),5.8f,1.55f,dark,true,.25f);
        foreach(float x in new[]{-14.55f,-8.45f})Fit(site,"Monument_Sign_Post",School+"props/wall2.prefab",new Vector3(x,1.2f,-17.1f),new Vector3(.32f,2.4f,.4f),cream);
        Label(site,"Faculty_Name_Thai","คณะวิทยาศาสตร์",new Vector3(-11.5f,1.57f,-17.25f),.43f,5.5f,new Color(.93f,.76f,.37f));
        Label(site,"Faculty_Name_English","FACULTY OF SCIENCE",new Vector3(-11.5f,.98f,-17.25f),.18f,5.2f,new Color(.93f,.76f,.37f));
    }

    [MenuItem("Nisit/Science Faculty/Build New")]
    public static void Build()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.name!="01_Gameplay")throw new Exception("Open 01_Gameplay in Edit mode before building.");
        if(GameObject.Find(RootName)||AssetDatabase.IsValidFolder(Dir))throw new Exception("Science faculty already exists. Refusing to overwrite.");
        sources.Clear();compatibleMaterials.Clear();brickVariants.Clear();brickMeshes.Clear();accents=0;
        System.IO.Directory.CreateDirectory(Dir+"/Materials");System.IO.Directory.CreateDirectory(Dir+"/Meshes");System.IO.Directory.CreateDirectory("Assets/_Project/Scenes/Backups");
        string backup="Assets/_Project/Scenes/Backups/ScienceFaculty_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        EditorSceneManager.SaveScene(scene,backup,true);AssetDatabase.Refresh();
        const string baseMaterial="Assets/_Project/Materials/MSU/MSU_KR_WallCream.mat";
        cream=CopyMaterial("Science_Cream",baseMaterial,new Color(.9f,.865f,.78f));ivory=CopyMaterial("Science_Ivory",baseMaterial,new Color(.97f,.95f,.87f));
        floor=CopyMaterial("Science_Tile","Assets/_Project/Materials/MSU/MSU_Walkway.mat",new Color(.77f,.75f,.68f));terracotta=CopyMaterial("Science_Terracotta",baseMaterial,new Color(.62f,.30f,.19f));terracotta.SetFloat("_Cull",0);
        brick=CopyMaterial("Science_Brick",baseMaterial,new Color(1.35f,1.04f,.93f));brick.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Synty/PolygonGeneric/Textures/Generic_Brick.png"));
        gold=CopyMaterial("Science_Gold",baseMaterial,new Color(.83f,.65f,.29f));dark=CopyMaterial("Science_Sign_Dark",baseMaterial,new Color(.10f,.15f,.15f));glass=CopyMaterial("Science_Blue_Window",baseMaterial,new Color(.28f,.52f,.64f));glass.SetFloat("_Smoothness",.5f);
        green=CopyMaterial("Science_Garden_Green",baseMaterial,new Color(.24f,.43f,.20f));red=CopyMaterial("Science_Curb_Red",baseMaterial,new Color(.72f,.22f,.18f));
        leaves=CopyMaterial("Science_Palm_Leaf",baseMaterial,new Color(.24f,.43f,.20f));leaves.SetFloat("_Cull",0);
        doorGlass=CopyMaterial("Science_Entrance_Glass",baseMaterial,new Color(.44f,.69f,.75f,.25f));doorGlass.SetFloat("_Surface",1);doorGlass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);doorGlass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        doorGlass.SetFloat("_ZWrite",0);doorGlass.SetOverrideTag("RenderType","Transparent");doorGlass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");doorGlass.renderQueue=3000;doorGlass.SetShaderPassEnabled("ShadowCaster",false);
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Mitr SDF.asset");if(!font)throw new Exception("Existing Thai font missing.");
        var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Build MSU Science Faculty courtyard plan");
        var structure=Group(root.transform,"01_Ground_Floor_Structure");var rooms=Group(root.transform,"02_Ground_Floor_Interiors");var upper=Group(root.transform,"03_Upper_Floors_Exterior_Only");var cover=Group(root.transform,"04_Ceiling_And_Portico_Cover");var site=Group(root.transform,"05_Landscape_And_Access");
        RingSlabs(structure,"Ring_Foundation",.2f,.4f,cream,true,.2f);RingSlabs(structure,"Ground_Floor_Surface",.39f,.18f,floor);
        Facades(structure,false);Facades(upper,true);Roof(upper);
        foreach(bool right in new[]{false,true})foreach(bool rear in new[]{false,true})RoomWalls(structure,right,rear);
        Classroom(rooms,false);Classroom(rooms,true);Lab(rooms,false);Lab(rooms,true);
        var lobby=Group(rooms,"01_Main_Reception_And_Waiting_Hall");
        Item(lobby,"Reception_Desk","Assets/_Project/Prefabs/ITBuilding/IT_ReceptionDesk.prefab",3.4f,-9.5f,new Vector3(3.1f,1.28f,1.57f),90);
        foreach(float z in new[]{-10.5f,-8.6f})Item(lobby,"Waiting_Bench","Assets/_Project/Prefabs/GEBuilding/GE_Bench.prefab",-3.7f,z,new Vector3(1.6f,.98f,.51f),90);
        Label(lobby,"Reception_Label","ประชาสัมพันธ์",new Vector3(3.4f,2.65f,-11.1f),.25f,3.3f,new Color(.13f,.27f,.31f));
        LightFixture(lobby,0,-9.5f);LightFixture(lobby,0,9.5f);LightFixture(lobby,-14,0);LightFixture(lobby,14,0);
        // Open ground floor arcade marks the lawn boundary without restricting courtyard access.
        foreach(float z in new[]{-6f,6f})foreach(float x in new[]{-9.8f,-5f,5f,9.8f})Fit(structure,"Courtyard_Arcade_Column",School+"props/wall2.prefab",new Vector3(x,2.18f,z),new Vector3(.3f,3.4f,.3f),ivory);
        foreach(float x in new[]{-10f,10f})foreach(float z in new[]{-3f,3f})Fit(structure,"Side_Courtyard_Column",School+"props/wall2.prefab",new Vector3(x,2.18f,z),new Vector3(.3f,3.4f,.3f),ivory);
        RingSlabs(cover,"Ground_Floor_Ceiling",3.98f,.2f,ivory,true,.2f);
        foreach(float x in new[]{-7.1f,-2.7f,2.7f,7.1f})Fit(structure,"School_Portico_Column",School+"props/wall2.prefab",new Vector3(x,2.23f,-15.3f),new Vector3(.42f,3.5f,.42f),ivory);
        Slab(cover,"Main_Entrance_Portico",new Vector3(0,4.1f,-14.2f),new Vector3(16.5f,.3f,4.8f),ivory);
        Wall(cover,"Faculty_Entrance_Sign_Band",new Vector3(0,4.05f,-16.62f),16.5f,.82f,cream,false,.15f);
        Label(cover,"Faculty_Name_Thai","คณะวิทยาศาสตร์",new Vector3(0,4.17f,-16.71f),.48f,11,new Color(.13f,.27f,.31f));
        Label(cover,"Faculty_Name_English","FACULTY OF SCIENCE",new Vector3(0,3.73f,-16.71f),.19f,9,new Color(.13f,.27f,.31f));
        foreach(float x in new[]{-2.45f,2.45f})Wall(structure,"Parked_Open_Entrance_Glass",new Vector3(x,1.88f,-12.18f),1.55f,2.8f,doorGlass,false,.04f);
        Site(site);
        var hider=root.AddComponent<BuildingRoofHider>();hider.roofGroups=new[]{upper,cover};hider.localMin=new Vector3(-18,.15f,-12.2f);hider.localMax=new Vector3(18,3.75f,12.2f);
        root.transform.position=new Vector3(78,0,-64);root.transform.rotation=Quaternion.Euler(0,180,0);
        var retained=new List<string>();var campus=GameObject.Find("KR_Campus");
        if(campus)
        {
            var old=campus.transform.Find("Zone_SouthEast/คณะวิทยาศาสตร์");
            if(old){Undo.RecordObject(old.gameObject,"Preserve original science building");old.gameObject.SetActive(false);retained.Add("Original science building retained inactive.");}
            foreach(var t in campus.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("KR_Tree_")&&t.gameObject.activeInHierarchy).ToArray())
            {
                var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);var p=root.transform.InverseTransformPoint(bounds.center);
                if((Mathf.Abs(p.x)<18.6f&&p.z>-12.8f&&p.z<12.8f)||(Mathf.Abs(p.x)<9.5f&&p.z>=-20&&p.z<=-12.8f))
                {Undo.RecordObject(t.gameObject,"Preserve tree in science construction footprint");t.gameObject.SetActive(false);retained.Add("Retained inactive tree: "+t.parent.name+"/"+t.name+" at "+t.position);}
            }
        }
        var label=GameObject.Find("KR_Labels/Label_SC1 · อาคารวิทยาศาสตร์");if(label){Undo.RecordObject(label.transform,"Update science label");label.transform.position=new Vector3(78,17,-64);}
        var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,Dir+"/MSU_Science_Faculty.prefab",InteractionMode.AutomatedAction);if(!prefab)throw new Exception("Failed to save science prefab.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        System.IO.File.WriteAllText(Dir+"/AssetSources.txt","Existing prefab/model instances: "+sources.Values.Sum()+"\nSupplementary parts: "+accents+"\nScene backup: "+backup+"\n"+string.Join("\n",retained)+"\n"+string.Join("\n",sources.OrderBy(x=>x.Key).Select(x=>x.Value+" x "+x.Key)));
        AssetDatabase.ImportAsset(Dir+"/AssetSources.txt");Selection.activeGameObject=root;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(root.transform.TransformPoint(new Vector3(0,6,-2)),Quaternion.Euler(25,190,0),40,false,true);
        Debug.Log("SCIENCE_FACULTY_BUILD_OK: 4 entrances; open grass courtyard; 2 classrooms; 2 laboratories; upper floors exterior only; "+sources.Values.Sum()+" existing asset instances. Backup: "+backup);
    }
}


