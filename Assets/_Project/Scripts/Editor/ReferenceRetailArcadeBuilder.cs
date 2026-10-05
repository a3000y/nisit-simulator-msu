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
public static class ReferenceRetailArcadeBuilder
{
    const string Dir = "Assets/_Project/Prefabs/ReferenceRetailArcade";
    const string RootName = "Reference_Retail_Arcade";
    const string School = "Assets/school/Prefabs/";
    const string City = "Assets/Synty/PolygonCity/Prefabs/";
    const string Generic = "Assets/Synty/PolygonGeneric/Prefabs/";
    static Material cream, ivory, floor, terracotta, gold, pink, yellow, dark, glass, green, red, orange;
    static TMP_FontAsset font;
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
        var copy = new Material(original) { name = "Arcade_Source_" + original.name };
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
        foreach (var renderer in renderers) renderer.gameObject.isStatic = true;
        if (collision)
        {
            var box = wrapper.gameObject.AddComponent<BoxCollider>();
            box.size = bounds.size;
        }
        wrapper.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
        wrapper.localRotation = Quaternion.Euler(0, yaw, 0);
        wrapper.localPosition = center;
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

    static void GlassPanel(Transform parent, string name, float x, float width)
    {
        Wall(parent, "Storefront_Base_Sill", new Vector3(x, .58f, -4.53f), width, .2f, cream);
        Wall(parent, name, new Vector3(x, 1.98f, -4.53f), width, 2.6f, glass, true, .055f);
        foreach (float edge in new[] { x - width / 2, x + width / 2 })
            Accent(parent, "Storefront_Mullion", new Vector3(edge, 1.98f, -4.58f), new Vector3(.055f, 2.68f, .075f), dark);
        Accent(parent, "Glass_Base_Frame", new Vector3(x, .67f, -4.57f), new Vector3(width, .065f, .075f), dark);
        Accent(parent, "Glass_Top_Frame", new Vector3(x, 3.29f, -4.57f), new Vector3(width, .065f, .075f), dark);
        Accent(parent, "Window_Safety_Stripe", new Vector3(x, 1.75f, -4.575f), new Vector3(width, .045f, .025f), ivory);
    }

    static void Storefront(Transform parent, float center, float width, string name, Material signColor)
    {
        var facade = Group(parent, "Storefront_" + name);
        float panelWidth = (width - 2.7f) / 2;
        GlassPanel(facade, "Left_Glazing", center - 1.35f - panelWidth / 2, panelWidth);
        GlassPanel(facade, "Right_Glazing", center + 1.35f + panelWidth / 2, panelWidth);
        Wall(facade, "Solid_Lintel", new Vector3(center, 3.71f, -4.5f), width, .8f, cream);
        Wall(facade, "Shop_Sign_Panel", new Vector3(center, 3.82f, -7.79f), width - .22f, .69f, signColor, false, .065f);
        Label(facade, "Shop_Name", name, new Vector3(center, 3.86f, -7.85f), .43f, width - .9f, Color.white);
        // Sliding leaves are parked against adjacent fixed glazing, keeping a 2.7m opening.
        foreach (float side in new[] { -1f, 1f })
        {
            Wall(facade, "Parked_Sliding_Door", new Vector3(center + side * 2.06f, 1.95f, -4.64f),
                1.32f, 2.5f, glass, false, .035f);
            Accent(facade, "Door_Handle", new Vector3(center + side * 1.51f, 1.78f, -4.7f),
                new Vector3(.035f, .42f, .055f), ivory);
        }
        Accent(facade, "Door_Track", new Vector3(center, 3.27f, -4.66f), new Vector3(5.4f, .055f, .09f), dark);
        Label(facade, "Entrance_Label", "ยินดีต้อนรับ", new Vector3(center, 3.1f, -4.71f), .17f, 2.4f, new Color(.23f, .26f, .23f));
    }

    static void Shelf(Transform parent, string name, Vector3 position, Vector3 size, float yaw = 180,
        string variant = "01")
    {
        Fit(parent, name, City + "Props/SM_Prop_ShopInterior_Shelf_" + variant + ".prefab",
            position + Vector3.up * size.y / 2, size, null, true, yaw);
    }

    static void Product(Transform parent, string path, Vector3 center, float height, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) return;
        var rs = asset.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var size = b.size * (height / Mathf.Max(.001f, b.size.y));
        Fit(parent, name, path, center + Vector3.up * height / 2, size, null, false);
    }

    static void CeilingLight(Transform parent, float x, float z)
    {
        Slab(parent, "Ceiling_Light_Fixture", new Vector3(x, 3.92f, z), new Vector3(1.5f, .055f, .26f), ivory, false);
        var glow = Group(parent, "Warm_Interior_Light");
        glow.localPosition = new Vector3(x, 3.45f, z);
        var light = glow.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .96f, .87f);
        light.intensity = 2.1f;
        light.range = 8;
        light.shadows = LightShadows.None;
    }

    static void Shop(Transform parent, string name, float center, float width, int type)
    {
        var shop = Group(parent, name);
        Slab(shop, "Shop_Tile_Floor", new Vector3(center, .38f, 0), new Vector3(width - .18f, .2f, 8.82f), floor);
        var fixtures = Group(shop, "Fixtures_And_Merchandise");
        float rear = 3.76f;
        for (float x = center - width / 2 + 1.2f; x < center + width / 2 - .8f; x += 1.85f)
        {
            if (type == 1 && x < -.5f) continue; // Rear fridge bank occupies this wall.
            if (type == 0)
                Fit(fixtures, "School_Stationery_Bookshelf", School + "props/rack.prefab",
                    new Vector3(x, 1.44f, rear), new Vector3(1.55f, 1.92f, .6f));
            else
                Shelf(fixtures, "Rear_Stocked_Shelf", new Vector3(x, .48f, rear), new Vector3(1.55f, 1.82f, .54f), 180, "02");
        }
        if (type == 1)
        {
            // Four gondolas, in two rows. Central and cross aisles remain at least 1.5m wide.
            foreach (float x in new[] { -4.4f, 4.4f })
                foreach (float z in new[] { -1.35f, 1.6f })
                    Shelf(fixtures, "MiniMart_Stocked_Gondola", new Vector3(x, .48f, z), new Vector3(2.65f, 1.7f, .82f));
            foreach (float x in new[] { -5.55f, -2.75f })
                Fit(fixtures, "Drinks_Display_Fridge", "Assets/CoffeeShopStarterPack/Prefabs/PW_fridge.prefab",
                    new Vector3(x, 1.48f, 3.48f), new Vector3(2.4f, 2f, .95f));
        }
        else
        {
            Shelf(fixtures, "Side_Gondola_Left", new Vector3(center - 2.15f, .48f, .15f), new Vector3(1.7f, 1.65f, .74f));
            Shelf(fixtures, "Side_Gondola_Right", new Vector3(center + 2.15f, .48f, .15f), new Vector3(1.7f, 1.65f, .74f));
        }
        float counterX = center + width / 2 - 2.15f;
        Fit(fixtures, "Checkout_Counter", City + "Props/SM_Prop_ShopInterior_Desk_01.prefab",
            new Vector3(counterX, 1.02f, -2.7f), new Vector3(2.4f, 1.08f, .7f));
        Fit(fixtures, "Checkout_Register", School + "props/computer1.prefab",
            new Vector3(counterX + .65f, 1.77f, -2.7f), new Vector3(.28f, .42f, .45f), null, false, 180);
        Fit(fixtures, "Glass_Display_Counter", School + "props/showcase.prefab",
            new Vector3(center - width / 2 + 1.68f, 1.04f, -2.78f), new Vector3(2.25f, 1.12f, .72f));
        if (type == 0)
        {
            for (int i = 0; i < 9; i++)
                Product(fixtures, School + "props/book" + (i % 8 + 1) + ".prefab",
                    new Vector3(center - 3.2f + i * .65f, 1.2f, 3.45f), .25f, "Stationery_Book");
        }
        else
        {
            string[] goods = { "Soda", "Bread", "Apple", "Banana", "CoffeCup" };
            for (int i = 0; i < 10; i++)
                Product(fixtures, "Assets/LowPolyFoodLite/Prefabs/" + goods[i % goods.Length] + ".prefab",
                    new Vector3(center - width / 2 + 1.05f + (i % 5) * .38f, 1.61f, -2.7f + (i / 5) * .18f),
                    .19f, "Counter_Product_" + goods[i % goods.Length]);
            if (type == 2)
                Fit(fixtures, "Household_Cabinet", "Assets/CoffeeShopStarterPack/Prefabs/PW_cupboard01.prefab",
                    new Vector3(center + 3.2f, 1.21f, 2.5f), new Vector3(1.5f, 1.46f, .66f));
        }
        foreach (float x in new[] { center - width / 4, center + width / 4 }) CeilingLight(shop, x, .4f);
    }

    static void UpperFacade(Transform upper)
    {
        Wall(upper, "Upper_Front_Closed_Wall", new Vector3(0, 6.08f, -4.5f), 34, 3.92f, cream);
        Wall(upper, "Upper_Back_Closed_Wall", new Vector3(0, 6.08f, 4.5f), 34, 3.92f, cream);
        foreach (float x in new[] { -16.9f, 16.9f })
            Fit(upper, "Upper_Side_Closed_Wall", School + "props/wall4 (1).prefab", new Vector3(x, 6.08f, 0), new Vector3(.2f, 3.92f, 9f), cream);
        Slab(upper, "Flat_Roof_Cap", new Vector3(0, 8.1f, 0), new Vector3(34.5f, .16f, 9.45f), ivory);
        Wall(upper, "Continuous_Upper_Cornice", new Vector3(0, 7.93f, -4.66f), 34.35f, .2f, ivory, false, .16f);
        Wall(upper, "Continuous_Facade_Base", new Vector3(0, 4.25f, -4.69f), 34.3f, .21f, ivory, false, .16f);
        // Opaque backing seals decorative lattice and ventilation recesses.
        Wall(upper, "Lattice_Dark_Recess", new Vector3(-12.5f, 6.28f, -4.64f), 8.4f, 2.95f, dark, false, .07f);
        var lattice = Group(upper, "Gold_Diamond_Lattice");
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 12; col++)
            {
                float x = -16.35f + col * .70f, y = 5.17f + row * .64f;
                var a = new Vector3(x, y, -4.72f);
                var b = new Vector3(x + .35f, y + .32f, -4.72f);
                var c = new Vector3(x + .7f, y, -4.72f);
                var d = new Vector3(x + .35f, y - .32f, -4.72f);
                Line(lattice, "Diamond", a, b, .04f, gold);
                Line(lattice, "Diamond", b, c, .04f, gold);
                Line(lattice, "Diamond", c, d, .04f, gold);
                Line(lattice, "Diamond", d, a, .04f, gold);
            }
        // Long pastel panels and stepped linework echo the reference mural.
        Wall(upper, "Pastel_Yellow_Field", new Vector3(4.3f, 6.39f, -4.64f), 24.1f, 2.64f, yellow, false, .06f);
        Wall(upper, "Pastel_Pink_Field_Left", new Vector3(-1.65f, 6.89f, -4.7f), 9.5f, 1.25f, pink, false, .05f);
        Wall(upper, "Pastel_Pink_Field_Right", new Vector3(11.4f, 6.89f, -4.7f), 7.8f, 1.25f, pink, false, .05f);
        Wall(upper, "Cream_Mural_Band", new Vector3(4.3f, 5.65f, -4.71f), 23.7f, .48f, ivory, false, .025f);
        var motifs = Group(upper, "Geometric_Facade_Motifs");
        for (int i = 0; i < 5; i++)
        {
            float y = 5.11f + i * .1f;
            Line(motifs, "Circuit_Line", new Vector3(-7f, y, -4.76f), new Vector3(-3.8f, y, -4.76f), .025f, gold);
            Line(motifs, "Circuit_Angle", new Vector3(-3.8f, y, -4.76f), new Vector3(-2.3f, y + 1f, -4.76f), .025f, gold);
            Line(motifs, "Circuit_Line", new Vector3(-2.3f, y + 1f, -4.76f), new Vector3(3.9f, y + 1f, -4.76f), .025f, gold);
            Line(motifs, "Circuit_Angle", new Vector3(5.4f, y + 1.1f, -4.76f), new Vector3(7.1f, y, -4.76f), .025f, gold);
            Line(motifs, "Circuit_Line", new Vector3(7.1f, y, -4.76f), new Vector3(16.2f, y, -4.76f), .025f, gold);
        }
        for (float x = -7.6f; x < 16.7f; x += .53f)
        {
            Wall(upper, "Ventilation_Dark_Recess", new Vector3(x, 4.65f, -4.64f), .29f, .48f, dark, false, .025f);
            Accent(upper, "Ventilation_Divider", new Vector3(x + .21f, 4.65f, -4.72f), new Vector3(.048f, .54f, .08f), ivory);
        }
        var block = Group(upper, "Upper_Storey_Inaccessible_Volume").gameObject.AddComponent<BoxCollider>();
        block.center = new Vector3(0, 6.18f, 0);
        block.size = new Vector3(33.9f, 4.02f, 8.92f);
    }

    [MenuItem("Nisit/Reference Retail Arcade/Build New")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "01_Gameplay") throw new Exception("Open 01_Gameplay before building.");
        if (GameObject.Find(RootName) || AssetDatabase.IsValidFolder(Dir))
            throw new Exception("Retail arcade already exists. Refusing to overwrite.");
        sources.Clear();
        compatibleMaterials.Clear();
        accents = 0;
        System.IO.Directory.CreateDirectory(Dir + "/Materials");
        System.IO.Directory.CreateDirectory("Assets/_Project/Scenes/Backups");
        string backup = "Assets/_Project/Scenes/Backups/RetailArcade_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        EditorSceneManager.SaveScene(scene, backup, true);
        AssetDatabase.Refresh();
        const string baseMaterial = "Assets/_Project/Materials/MSU/MSU_KR_WallCream.mat";
        cream = CopyMaterial("Arcade_Cream", baseMaterial, new Color(.9f, .865f, .78f));
        ivory = CopyMaterial("Arcade_Ivory", baseMaterial, new Color(.98f, .96f, .88f));
        floor = CopyMaterial("Arcade_Tile", "Assets/_Project/Materials/MSU/MSU_Walkway.mat", new Color(.78f, .76f, .7f));
        terracotta = CopyMaterial("Arcade_Brown_Steps", baseMaterial, new Color(.59f, .35f, .24f));
        gold = CopyMaterial("Arcade_Motif_Gold", baseMaterial, new Color(.55f, .48f, .31f));
        pink = CopyMaterial("Arcade_Pastel_Pink", baseMaterial, new Color(.84f, .64f, .64f));
        yellow = CopyMaterial("Arcade_Pastel_Yellow", baseMaterial, new Color(.91f, .79f, .48f));
        dark = CopyMaterial("Arcade_Dark_Frame", "Assets/_Project/Materials/MSU/MSU_KR_WindowDark.mat", new Color(.19f, .24f, .23f));
        green = CopyMaterial("Arcade_Shop_Green", baseMaterial, new Color(.09f, .38f, .25f));
        red = CopyMaterial("Arcade_Shop_Red", baseMaterial, new Color(.75f, .19f, .18f));
        orange = CopyMaterial("Arcade_Shop_Orange", baseMaterial, new Color(.93f, .51f, .15f));
        glass = CopyMaterial("Arcade_Shop_Glass", "Assets/_Project/Materials/MSU/MSU_KR_WindowDark.mat", new Color(.51f, .72f, .7f, .21f));
        glass.SetFloat("_Surface", 1);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0);
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.renderQueue = (int)RenderQueue.Transparent;
        glass.SetShaderPassEnabled("ShadowCaster", false);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Mitr SDF.asset");
        if (!font) throw new Exception("Existing Thai font missing.");

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create reference retail arcade");
        // Build at the origin so source bounds stay independent of scene placement.
        var structure = Group(root.transform, "Ground_Floor_Structure");
        var stores = Group(root.transform, "Ground_Floor_Shops");
        var glazing = Group(root.transform, "Storefronts");
        var upper = Group(root.transform, "Upper_Storey_Exterior_Only");
        var cover = Group(root.transform, "Ceiling_And_Canopy");
        var site = Group(root.transform, "Front_Walkway_And_Landscape");
        Slab(structure, "Continuous_Foundation", new Vector3(0, .20f, -1.5f), new Vector3(34.6f, .4f, 12.1f), terracotta);
        Wall(structure, "Ground_Floor_Back_Wall", new Vector3(0, 2.24f, 4.5f), 34f, 3.52f, cream);
        foreach (float x in new[] { -16.9f, -8f, 8f, 16.9f })
            Fit(structure, "Ground_Floor_Side_Or_Partition", School + "props/wall4 (1).prefab",
                new Vector3(x, 2.24f, 0), new Vector3(.18f, 3.52f, 9f), cream);
        foreach (float x in new[] { -16.9f, -8f, 8f, 16.9f })
            Fit(structure, "School_Square_Arcade_Column", School + "props/wall2.prefab",
                new Vector3(x, 2.25f, -7.18f), new Vector3(.40f, 3.54f, .40f), ivory);
        foreach (float x in new[] { -16.9f, -8f, 8f, 16.9f })
            Fit(structure, "Storefront_Pier", School + "props/wall2.prefab",
                new Vector3(x, 2.3f, -4.51f), new Vector3(.32f, 3.64f, .32f), ivory);
        Slab(cover, "Ground_Floor_Ceiling", new Vector3(0, 4.08f, 0), new Vector3(34.3f, .16f, 9.15f), ivory);
        Slab(cover, "Covered_Arcade_Canopy", new Vector3(0, 4.0f, -6.05f), new Vector3(34.65f, .18f, 3.28f), ivory);
        Wall(cover, "Canopy_Fascia", new Vector3(0, 3.91f, -7.69f), 34.65f, .3f, cream, false, .12f);
        Slab(site, "Raised_Covered_Walkway", new Vector3(0, .4f, -6.05f), new Vector3(34.5f, .16f, 3.1f), floor);
        // Three 0.16m risers: lower than the existing scaled player's 0.198m step offset.
        for (int i = 0; i < 3; i++)
        {
            float top = .16f * (i + 1);
            Slab(site, "Front_Step_" + (i + 1), new Vector3(0, top / 2, -8.45f + i * .4f),
                new Vector3(34.6f, top, .42f), terracotta);
            Slab(site, "Step_Light_Nosing", new Vector3(0, top + .002f, -8.63f + i * .4f),
                new Vector3(34.6f, .012f, .045f), floor, false);
        }
        Slab(site, "Front_Paved_Apron", new Vector3(0, .04f, -9.48f), new Vector3(35f, .08f, 1.65f), floor);
        Slab(site, "Access_Path_To_Campus", new Vector3(0, .025f, -13.05f), new Vector3(3.2f, .05f, 5.6f), floor);
        Storefront(glazing, -12.5f, 8.82f, "ร้านเครื่องเขียน", green);
        Storefront(glazing, 0, 15.82f, "นิสิตมาร์ท", green);
        Storefront(glazing, 12.5f, 8.82f, "ร้านของใช้", green);
        foreach (var stripe in new[] { new { y = 3.55f, m = orange }, new { y = 4.10f, m = red } })
            Wall(glazing, "MiniMart_Sign_Stripe", new Vector3(0, stripe.y, -7.86f), 15.6f, .07f, stripe.m, false, .022f);
        Label(glazing, "MiniMart_English", "NISIT MART", new Vector3(0, 3.60f, -7.89f), .13f, 7, Color.white);
        Shop(stores, "01_Stationery_Shop", -12.5f, 8.82f, 0);
        Shop(stores, "02_Convenience_Store", 0, 15.82f, 1);
        Shop(stores, "03_Household_Goods", 12.5f, 8.82f, 2);
        UpperFacade(upper);
        // Preserve clear access paths between the planted beds.
        foreach (float x in new[] { -13.1f, 12.4f })
        {
            Slab(site, "Planter_Base", new Vector3(x, .15f, -11.35f), new Vector3(5.2f, .3f, 1.05f), terracotta);
            Fit(site, "Synty_Low_Hedge", Generic + "Environment/SM_Gen_Env_Bush_01.prefab",
                new Vector3(x, .7f, -11.35f), new Vector3(5f, .88f, .95f), null, true);
        }
        foreach (float x in new[] { -20.3f, 20.3f })
        {
            var tree = Fit(site, "Synty_Street_Tree", City + "Environments/SM_Env_Tree_01.prefab",
                new Vector3(x, 2.4f, -8.9f), new Vector3(2.6f, 4.8f, 3f), null, false);
            var trunk = tree.gameObject.AddComponent<CapsuleCollider>();
            trunk.center = new Vector3(0, -1.4f, 0);
            trunk.height = 1.45f;
            trunk.radius = .14f;
        }
        var cutaway = root.AddComponent<BuildingRoofHider>();
        cutaway.roofGroups = new[] { upper, cover };
        cutaway.localMin = new Vector3(-17f, .15f, -4.75f);
        cutaway.localMax = new Vector3(17f, 3.7f, 4.4f);
        root.transform.position = new Vector3(-52, 0, 90);
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, Dir + "/Reference_Retail_Arcade.prefab", InteractionMode.AutomatedAction);
        if (!prefab) throw new Exception("Failed to save reusable retail arcade prefab.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        System.IO.File.WriteAllText(Dir + "/AssetSources.txt", "Source prefab instances: " + sources.Values.Sum() +
            "\nSupplementary decorative shapes: " + accents + "\nScene backup: " + backup + "\n" +
            string.Join("\n", sources.OrderBy(x => x.Key).Select(x => x.Value + " x " + x.Key)));
        AssetDatabase.ImportAsset(Dir + "/AssetSources.txt");
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.LookAt(root.transform.TransformPoint(new Vector3(0, 3.7f, -1)),
                Quaternion.Euler(15, 8, 0), 27, false, true);
        Debug.Log("RETAIL_ARCADE_BUILD_OK: 3 accessible shops; exterior-only upper storey; " +
            sources.Values.Sum() + " existing prefab instances. Backup: " + backup);
    }
}
