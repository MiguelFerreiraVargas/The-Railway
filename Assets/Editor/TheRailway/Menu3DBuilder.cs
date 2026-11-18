using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Constrói do zero a cena 3D do menu: uma estação abandonada ao anoitecer com
/// a locomotiva parada, quadro de partidas como menu e quadro de saves.
/// A cena anterior é guardada uma vez em Assets/Scenes/Menu_Antigo.unity.
/// </summary>
public static class Menu3DBuilder
{
    public const string ScenePath = "Assets/Scenes/Menu.unity";
    private const string BackupPath = "Assets/Scenes/Menu_Antigo.unity";
    private const string Folder = "Assets/Scenes/Menu/Menu3D";
    private const string ConvertedFolder = Folder + "/Convertidos";
    private const string PreviewFolder = "Temp/TheRailway";

    private const string LocoPath = "Assets/Models/steam-train.fbx";
    private const string WagonPath = "Assets/Models/freight-train-car/source/vagon.fbx";
    private const string TrackPath = "Assets/Models/TrackStraight1m.prefab";
    private const string StreetLightPath = "Assets/Models/low poly street lights/source/street_lights.fbx";
    private const long StreetLightMeshId = 8714690333086689727;
    private const string HousePath = "Assets/Models/CASAS/old-russian-house/source/dom.fbx";
    private const string RuinsPath = "Assets/Models/city-ruins-environment/source/Sketchfab_2016_10_12_16_48_01.fbx";
    private const string SmokePath = "Assets/msVFX_Free Smoke Effects Pack/Prefabs/msVFX_Stylized Smoke 1.prefab";
    private const string SteamPath = "Assets/msVFX_Free Smoke Effects Pack/Prefabs/msVFX_Stylized Smoke 3.prefab";
    private const string LedFontPath = "Assets/Prefabs/VCR_OSD_MONO_1.001 SDF.asset";
    private const string TitleFontPath = "Assets/TextMesh Pro/Fonts/double_action/Double Action SDF.asset";
    private const string TitleMaterialPath = "Assets/Prefabs/Menu Canva/Titulo Violeta.mat";
    private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string CursorGuid = "88d4906232be1f64ea0dd1ae7bf06d8a";

    private static readonly string[] TreePaths =
    {
        "Assets/Arvores Mapa/tree01.prefab", "Assets/Arvores Mapa/tree02.prefab",
        "Assets/Arvores Mapa/tree03.prefab", "Assets/Arvores Mapa/tree04.prefab",
        "Assets/Arvores Mapa/tree05.prefab", "Assets/Arvores Mapa/tree06.prefab",
        "Assets/Arvores Mapa/tree07.prefab", "Assets/Arvores Mapa/tree08.prefab",
    };

    private static readonly string[] BushPaths =
    {
        "Assets/Arvores Mapa/bush03.prefab", "Assets/Arvores Mapa/grass_bush.prefab",
    };

    private static readonly Color Led = new Color(1f, 0.68f, 0.18f, 1f);
    private static readonly Color LedDim = new Color(1f, 0.68f, 0.18f, 0.5f);
    private static readonly Color Ivory = new Color(0.93f, 0.9f, 0.83f, 1f);
    private static readonly Color Sodium = new Color(1f, 0.62f, 0.3f);

    private static Menu3DLayout L;
    private static Random rng;
    private static StringBuilder log;
    private static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

    // Referências criadas durante a construção
    private static Camera cam;
    private static Transform rig, poseIntro, poseMain, poseLoad;
    private static CanvasGroup mainGroup, loadGroup;
    private static float trackTop;

    [MenuItem("The Railway/Menu/Construir menu 3D (substitui a cena Menu)")]
    private static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("The Railway",
                "Isto recria a cena Menu do zero como uma estação 3D.\n\nA versão anterior é guardada como Menu_Antigo.unity na primeira vez.",
                "Construir", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var sb = new StringBuilder();
        Build(sb);
        Capture(sb);
        Debug.Log("[The Railway] Menu 3D:\n" + sb);
    }

    // ===================================================================== Build

    public static void Build(StringBuilder output)
    {
        log = output;
        L = Menu3DLayout.Load();
        rng = new Random(L.seed);
        converted.Clear();

        EnsureFolder("Assets/Scenes/Menu", "Menu3D");
        EnsureFolder(Folder, "Convertidos");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            AssetDatabase.CopyAsset(ScenePath, BackupPath);
            log.AppendLine("cena antiga guardada em " + BackupPath);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        try
        {
            BuildAll(scene);
        }
        catch
        {
            // Não deixa uma cena pela metade aberta: volta para a cena salva.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            throw;
        }
    }

    private static void BuildAll(UnityEngine.SceneManagement.Scene scene)
    {
        BuildEnvironment();
        BuildGroundAndTrack();
        var locoBounds = BuildTrain();
        BuildPlatform();
        BuildHouse();
        BuildProps();
        BuildNature();
        BuildCameraRig();
        var controller = BuildController();
        BuildBoards(controller);
        BuildTitleSign();
        BuildOverlay(controller);
        BuildEffects(locoBounds);
        BuildPostProcessing();
        BuildAudio();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        log.AppendLine("cena salva em " + ScenePath);
    }

    // =============================================================== Ambiente

    private static void BuildEnvironment()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(L.skybox);
        if (sky != null) RenderSettings.skybox = sky;
        else log.AppendLine("AVISO: céu não encontrado: " + L.skybox);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = L.fogColor;
        RenderSettings.fogDensity = L.fogDensity;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = L.ambientSky;
        RenderSettings.ambientEquatorColor = L.ambientEquator;
        RenderSettings.ambientGroundColor = L.ambientGround;
        RenderSettings.reflectionIntensity = 0.5f;

        var moon = new GameObject("Lua");
        var light = moon.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = L.moonColor;
        light.intensity = L.moonIntensity;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f;
        moon.transform.rotation = Quaternion.Euler(L.moonEuler);
        RenderSettings.sun = light;
    }

    private static void BuildGroundAndTrack()
    {
        var env = new GameObject("Cenario").transform;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Chao";
        ground.transform.SetParent(env);
        ground.transform.localScale = new Vector3(L.groundSize / 10f, 1f, L.groundSize / 10f);
        ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();
        MakeStatic(ground);

        // Leito de brita sob o trilho
        var ballast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ballast.name = "Brita";
        ballast.transform.SetParent(env);
        ballast.transform.position = new Vector3((L.trackFrom + L.trackTo) / 2f, 0.05f, 0f);
        ballast.transform.localScale = new Vector3(L.trackTo - L.trackFrom, 0.1f, 3.2f);
        ballast.GetComponent<Renderer>().sharedMaterial = LitMat("Brita", new Color(0.2f, 0.19f, 0.18f), 0.05f);
        Object.DestroyImmediate(ballast.GetComponent<Collider>());
        MakeStatic(ballast);

        // Trilho montado em código: dois trilhos de aço sobre dormentes de madeira.
        var trackRoot = new GameObject("Trilho").transform;
        trackRoot.SetParent(env);
        float length = L.trackTo - L.trackFrom;
        float centerX = (L.trackFrom + L.trackTo) / 2f;
        var steel = LitMat("Aco do Trilho", new Color(0.34f, 0.32f, 0.31f), 0.6f, 0.9f);
        var wood = LitMat("Dormente", new Color(0.17f, 0.12f, 0.09f), 0.08f);

        foreach (float side in new[] { -0.72f, 0.72f })
        {
            var rail = Box("Trilho", trackRoot, new Vector3(centerX, 0.33f, side), new Vector3(length, 0.12f, 0.08f), steel);
            MakeStatic(rail);
        }

        int sleepers = 0;
        for (float x = L.trackFrom; x < L.trackTo; x += 0.7f)
        {
            float jitter = Range(-0.04f, 0.04f);
            var s = Box("Dormente", trackRoot, new Vector3(x, 0.2f, jitter), new Vector3(0.24f, 0.12f, 2.5f), wood);
            s.transform.rotation = Quaternion.Euler(0f, Range(-2f, 2f), 0f);
            MakeStatic(s);
            sleepers++;
        }

        trackTop = 0.39f;
        log.AppendLine($"trilho: {length:0}m com {sleepers} dormentes");
    }

    private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // ==================================================================== Trem

    private static Bounds BuildTrain()
    {
        // Preferência: copiar o trem já configurado (materiais e escala) da cena do jogo.
        var copies = CopyFromScene(GameScenePath, "steam-train", "vagon");

        if (copies.TryGetValue("steam-train", out var locoCopy))
        {
            var group = new GameObject("Trem").transform;
            var all = new List<GameObject> { locoCopy };
            if (copies.TryGetValue("vagon", out var wagonCopy)) all.Add(wagonCopy);

            var gb = VisibleBounds(all.ToArray());
            group.position = gb.center;
            foreach (var go in all) go.transform.SetParent(group, true);

            if (gb.size.z > gb.size.x) group.rotation = Quaternion.Euler(0f, 90f, 0f) * group.rotation;
            if (L.locoFlip) group.rotation = Quaternion.Euler(0f, 180f, 0f) * group.rotation;
            group.localScale *= L.trainScale;

            // A locomotiva fica na frente (lado -X); o vagão vai para trás dela.
            var locoB = VisibleBounds(locoCopy);
            if (copies.ContainsKey("vagon"))
            {
                var wb = VisibleBounds(copies["vagon"]);
                if (wb.center.x < locoB.center.x)
                {
                    copies["vagon"].transform.position += new Vector3(locoB.max.x + L.wagonGap - wb.min.x, 0f, 0f);
                }
            }

            gb = VisibleBounds(all.ToArray());
            locoB = VisibleBounds(locoCopy);
            group.position += new Vector3(L.locoX - L.locoLength / 2f - locoB.min.x, trackTop - 0.02f - gb.min.y, -locoB.center.z);

            locoB = VisibleBounds(locoCopy);
            log.AppendLine($"trem copiado da CenaPrincipal: locomotiva {locoB.size.x:0.0}m x {locoB.size.y:0.0}m, x {locoB.min.x:0.0}..{locoB.max.x:0.0}");
            return locoB;
        }

        log.AppendLine("AVISO: trem não achado na CenaPrincipal; usando o modelo cru");
        var train = new GameObject("Trem").transform;

        var loco = PlaceAlongX(LocoPath, L.locoLength, new Vector3(L.locoX, trackTop - 0.05f, 0f), L.locoFlip, train);
        if (loco == null)
            return new Bounds(new Vector3(L.locoX, 2f, 0f), new Vector3(L.locoLength, 4f, 3f));

        loco.name = "Locomotiva";
        var lb = BoundsOf(loco);

        float wagonX = lb.max.x + L.wagonGap + L.wagonLength / 2f;
        var wagon = PlaceAlongX(WagonPath, L.wagonLength, new Vector3(wagonX, trackTop - 0.05f, 0f), false, train);
        if (wagon != null) wagon.name = "Vagao";

        log.AppendLine($"locomotiva: x {lb.min.x:0.0}..{lb.max.x:0.0}, altura {lb.size.y:0.0}m");
        return lb;
    }

    // ============================================================== Plataforma

    private static float PlatformZ => -(L.platformGap + L.platformWidth / 2f);
    private static float PlatformBackZ => -(L.platformGap + L.platformWidth);

    private static void BuildPlatform()
    {
        var root = new GameObject("Plataforma").transform;
        float length = L.platformTo - L.platformFrom;
        float centerX = (L.platformFrom + L.platformTo) / 2f;

        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Laje";
        slab.transform.SetParent(root);
        slab.transform.position = new Vector3(centerX, L.platformHeight / 2f, PlatformZ);
        slab.transform.localScale = new Vector3(length, L.platformHeight, L.platformWidth);
        slab.GetComponent<Renderer>().sharedMaterial = LitMat("Concreto", new Color(0.3f, 0.29f, 0.28f), 0.12f);
        MakeStatic(slab);

        var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "Faixa";
        stripe.transform.SetParent(root);
        stripe.transform.position = new Vector3(centerX, L.platformHeight + 0.005f, -L.platformGap - 0.3f);
        stripe.transform.localScale = new Vector3(length, 0.02f, 0.3f);
        stripe.GetComponent<Renderer>().sharedMaterial = LitMat("Faixa Amarela", new Color(0.7f, 0.55f, 0.12f), 0.3f);
        Object.DestroyImmediate(stripe.GetComponent<Collider>());
        MakeStatic(stripe);

        // Postes
        var lampMesh = LoadSubAsset<Mesh>(StreetLightPath, StreetLightMeshId);
        var lampMats = AssetDatabase.LoadAllAssetsAtPath(StreetLightPath).OfType<Material>().ToArray();
        var lamps = new List<Light>();
        float z = PlatformBackZ + 0.7f;

        for (float x = L.platformFrom + 4f; x < L.platformTo - 2f; x += L.lampSpacing)
        {
            var lamp = new GameObject("Poste");
            lamp.transform.SetParent(root);

            float headY;

            if (lampMesh != null)
            {
                lamp.AddComponent<MeshFilter>().sharedMesh = lampMesh;
                var mr = lamp.AddComponent<MeshRenderer>();
                mr.sharedMaterials = lampMats.Length > 0 ? lampMats : new[] { LitMat("Metal", new Color(0.1f, 0.1f, 0.11f), 0.4f, 0.6f) };
                ConvertMaterials(lamp);

                float s = L.lampHeight / Mathf.Max(0.01f, lampMesh.bounds.size.y);
                lamp.transform.localScale = Vector3.one * s;
                lamp.transform.position = new Vector3(x, L.platformHeight - lampMesh.bounds.min.y * s, z);
                headY = L.platformHeight + L.lampHeight * 0.9f;
            }
            else
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.transform.SetParent(lamp.transform);
                pole.transform.localScale = new Vector3(0.15f, L.lampHeight / 2f, 0.15f);
                pole.transform.localPosition = new Vector3(0f, L.lampHeight / 2f, 0f);
                pole.GetComponent<Renderer>().sharedMaterial = LitMat("Metal", new Color(0.1f, 0.1f, 0.11f), 0.4f, 0.6f);
                lamp.transform.position = new Vector3(x, L.platformHeight, z);
                headY = L.platformHeight + L.lampHeight;
            }

            MakeStatic(lamp);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(lamp.transform, true);
            lightGo.transform.position = new Vector3(x, headY, z + 0.3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Sodium;
            light.intensity = L.lampIntensity;
            light.range = L.lampRange;
            light.shadows = LightShadows.None;
            lamps.Add(light);

            AddGlow(lightGo.transform, lightGo.transform.position, 1.6f, new Color(1f, 0.6f, 0.25f, 0.55f));
        }

        for (int i = 0; i < lamps.Count; i++)
        {
            var flicker = lamps[i].gameObject.AddComponent<LightFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = L.lampIntensity;
            so.FindProperty("igniteDelay").floatValue = 1.5f + i * 0.45f;
            bool dying = i == lamps.Count / 2;
            so.FindProperty("flickerAmount").floatValue = dying ? 0.35f : 0.08f;
            so.FindProperty("failChancePerSecond").floatValue = dying ? 0.9f : 0.03f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        log.AppendLine($"plataforma: {length:0}m com {lamps.Count} postes" + (lampMesh == null ? " (modelo do poste não achado, usei cilindro)" : ""));
    }

    private static void BuildHouse()
    {
        if (!L.useHouse)
            return;

        GameObject house;
        var copies = CopyFromScene(GameScenePath, "Dom");

        if (copies.TryGetValue("Dom", out house))
        {
            house.transform.rotation = Quaternion.Euler(0f, L.houseYaw, 0f) * house.transform.rotation;
            var hb0 = VisibleBounds(house);
            float s = L.houseWidth / Mathf.Max(0.01f, Mathf.Max(hb0.size.x, hb0.size.z));
            house.transform.localScale *= s;
            hb0 = VisibleBounds(house);
            house.transform.position += L.housePos - new Vector3(hb0.center.x, hb0.min.y, hb0.center.z);
            log.AppendLine("casa copiada da CenaPrincipal");
        }
        else
        {
            house = Place(HousePath, L.housePos, L.houseYaw, b => Mathf.Max(b.size.x, b.size.z), L.houseWidth, null);
        }

        if (house == null)
            return;

        house.name = "Casa da Estacao";
        MakeStatic(house);

        var hb = BoundsOf(house);
        var lightGo = new GameObject("Luz da Janela");
        lightGo.transform.SetParent(house.transform, true);
        lightGo.transform.position = new Vector3(hb.center.x, hb.min.y + 2f, hb.max.z + 0.4f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.25f);
        light.intensity = 6f;
        light.range = 7f;
        var flicker = lightGo.AddComponent<LightFlicker>();
        var so = new SerializedObject(flicker);
        so.FindProperty("baseIntensity").floatValue = 6f;
        so.FindProperty("flickerSpeed").floatValue = 6f;
        so.FindProperty("flickerAmount").floatValue = 0.25f;
        so.FindProperty("igniteDelay").floatValue = 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================================================================ Objetos

    /// <summary>Bancos de madeira e caixotes espalhados pela plataforma.</summary>
    private static void BuildProps()
    {
        var root = new GameObject("Objetos da Plataforma").transform;
        var wood = LitMat("Madeira Velha", new Color(0.32f, 0.22f, 0.14f), 0.12f);
        var iron = LitMat("Ferro", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
        var crateMat = LitMat("Caixote", new Color(0.36f, 0.27f, 0.17f), 0.08f);
        float top = L.platformHeight;
        float backZ = PlatformBackZ + 0.6f;

        for (int i = 0; i < L.benchCount; i++)
        {
            float x = Mathf.Lerp(L.platformFrom + 8f, L.platformTo - 6f, (i + 0.5f) / Mathf.Max(1, L.benchCount)) + Range(-2f, 2f);
            if (Mathf.Abs(x - L.mainBoardPos.x) < 2.5f || Mathf.Abs(x - L.loadBoardPos.x) < 2.5f) x += 4f;
            var bench = new GameObject("Banco").transform;
            bench.SetParent(root);
            bench.position = new Vector3(x, top, backZ);
            Box("Assento", bench, bench.position + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.06f, 0.45f), wood);
            Box("Encosto", bench, bench.position + new Vector3(0f, 0.8f, -0.2f), new Vector3(1.8f, 0.4f, 0.05f), wood);
            foreach (float side in new[] { -0.75f, 0.75f })
                Box("Pe", bench, bench.position + new Vector3(side, 0.22f, 0f), new Vector3(0.06f, 0.45f, 0.42f), iron);
            MakeStatic(bench.gameObject);
        }

        for (int i = 0; i < L.crateCount; i++)
        {
            float x = Range(L.platformFrom + 3f, L.platformTo - 3f);
            if (Mathf.Abs(x - L.mainBoardPos.x) < 3f || Mathf.Abs(x - L.loadBoardPos.x) < 3f) continue;
            float size = Range(0.5f, 0.85f);
            float z = Range(PlatformBackZ + 0.5f, PlatformBackZ + 1.6f);
            var crate = Box("Caixote", root, new Vector3(x, top + size / 2f, z), Vector3.one * size, crateMat);
            crate.transform.rotation = Quaternion.Euler(0f, Range(-25f, 25f), 0f);
            MakeStatic(crate);

            if (rng.NextDouble() < 0.5)
            {
                float s2 = size * Range(0.55f, 0.8f);
                var top2 = Box("Caixote", root, new Vector3(x + Range(-0.1f, 0.1f), top + size + s2 / 2f, z), Vector3.one * s2, crateMat);
                top2.transform.rotation = Quaternion.Euler(0f, Range(-40f, 40f), 0f);
                MakeStatic(top2);
            }
        }
    }

    // ================================================================ Natureza

    private static void BuildNature()
    {
        var root = new GameObject("Natureza").transform;
        var trees = TreePaths.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).Where(a => a != null).ToArray();
        int placed = 0;

        for (int i = 0; i < L.treeCount && trees.Length > 0; i++)
        {
            Vector3 pos;
            int guard = 0;

            do
            {
                bool farSide = rng.NextDouble() < 0.65;
                pos = farSide
                    ? new Vector3(Range(-90f, 190f), 0f, Range(9f, 75f))
                    : new Vector3(Range(-90f, 90f), 0f, Range(-70f, PlatformBackZ - 7f));
                guard++;
            }
            while (guard < 20 && L.useHouse && Mathf.Abs(pos.x - L.housePos.x) < 9f && Mathf.Abs(pos.z - L.housePos.z) < 9f);

            var asset = trees[rng.Next(trees.Length)];
            float height = Range(L.treeHeight.x, L.treeHeight.y);
            var tree = Place(asset, pos, Range(0f, 360f), b => b.size.y, height, root);
            if (tree != null) { MakeStatic(tree); placed++; }
        }

        var bushes = BushPaths.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).Where(a => a != null).ToArray();
        int grass = 0;

        for (int i = 0; i < L.grassCount && bushes.Length > 0; i++)
        {
            bool nearTrack = rng.NextDouble() < 0.6;
            var pos = nearTrack
                ? new Vector3(Range(-60f, 120f), 0f, Range(2.2f, 14f))
                : new Vector3(Range(-60f, 60f), 0f, Range(-30f, PlatformBackZ - 0.5f));

            var bush = Place(bushes[rng.Next(bushes.Length)], pos, Range(0f, 360f), b => b.size.y, Range(0.4f, 1.3f), root);
            if (bush != null) { MakeStatic(bush); grass++; }
        }

        if (L.useRuins)
        {
            var ruins = Place(RuinsPath, L.ruinsPos, 0f, b => Mathf.Max(b.size.x, b.size.z), L.ruinsWidth, root);
            if (ruins != null) { ruins.name = "Ruinas"; MakeStatic(ruins); }
        }

        log.AppendLine($"natureza: {placed} árvores, {grass} arbustos" + (L.useRuins ? ", ruínas ao fundo" : ""));
    }

    // ================================================================= Câmera

    private static void BuildCameraRig()
    {
        rig = new GameObject("CameraRig").transform;
        rig.SetPositionAndRotation(L.camMainPos, Quaternion.Euler(L.camMainEuler));

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(rig, false);
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = L.fov;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 700f;
        cam.allowHDR = true;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<MenuCameraDrift>();

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var poses = new GameObject("Poses").transform;
        poseIntro = MakePose(poses, "Pose_Abertura", L.camIntroPos, L.camIntroEuler);
        poseMain = MakePose(poses, "Pose_Menu", L.camMainPos, L.camMainEuler);

        Quaternion loadRot = BoardRotation(L.loadBoardPos, L.loadBoardYawOffset);
        Vector3 loadForward = loadRot * Vector3.forward;
        poseLoad = new GameObject("Pose_Carregar").transform;
        poseLoad.SetParent(poses);
        poseLoad.position = L.loadBoardPos - loadForward * L.loadCamDistance + Vector3.up * L.loadCamHeight;
        poseLoad.rotation = Quaternion.LookRotation(loadForward, Vector3.up);
    }

    private static Transform MakePose(Transform parent, string name, Vector3 pos, Vector3 euler)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        t.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        return t;
    }

    private static Menu3DController BuildController()
    {
        var go = new GameObject("Menu");
        var controller = go.AddComponent<Menu3DController>();
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        var module = es.AddComponent<InputSystemUIInputModule>();
        SetupUiInput(module);

        var cursorPath = AssetDatabase.GUIDToAssetPath(CursorGuid);
        var cursorTex = string.IsNullOrEmpty(cursorPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(cursorPath);
        if (cursorTex != null)
        {
            var cursor = go.AddComponent<CustomCursor>();
            var cso = new SerializedObject(cursor);
            cso.FindProperty("cursorTexture").objectReferenceValue = cursorTex;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        return controller;
    }

    /// <summary>
    /// Liga o módulo de UI às ações "UI/..." do arquivo de input do projeto.
    /// Se não achar, o módulo usa as ações padrão do Input System ao rodar.
    /// </summary>
    private static void SetupUiInput(InputSystemUIInputModule module)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");

        if (asset == null || asset.FindActionMap("UI") == null)
        {
            log.AppendLine("AVISO: ações de UI do projeto não encontradas; usando as padrão ao rodar");
            return;
        }

        UnityEngine.InputSystem.InputActionReference Ref(string name)
        {
            var action = asset.FindAction("UI/" + name);
            return action != null ? UnityEngine.InputSystem.InputActionReference.Create(action) : null;
        }

        module.actionsAsset = asset;
        module.point = Ref("Point");
        module.leftClick = Ref("Click");
        module.rightClick = Ref("RightClick");
        module.middleClick = Ref("MiddleClick");
        module.scrollWheel = Ref("ScrollWheel");
        module.move = Ref("Navigate");
        module.submit = Ref("Submit");
        module.cancel = Ref("Cancel");
        module.trackedDevicePosition = Ref("TrackedDevicePosition");
        module.trackedDeviceOrientation = Ref("TrackedDeviceOrientation");
    }

    // ================================================================ Quadros

    private static Quaternion BoardRotation(Vector3 boardPos, float yawOffset)
    {
        Vector3 dir = boardPos - L.camMainPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        return Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
    }

    private static void BuildBoards(Menu3DController controller)
    {
        var ledFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LedFontPath);
        if (ledFont == null) log.AppendLine("AVISO: fonte LED não encontrada: " + LedFontPath);

        // ---------------- Quadro de partidas (menu principal)
        var main = BuildBoard("Quadro Partidas", L.mainBoardPos, BoardRotation(L.mainBoardPos, L.mainBoardYawOffset));
        mainGroup = main.group;
        var mc = main.canvas;
        float W = main.size.x, H = main.size.y;

        Text(mc, "Titulo", "PARTIDAS", ledFont, 78, Led, new Vector2(-W / 2 + 60, H / 2 - 70), new Vector2(700, 100), TextAlignmentOptions.Left);
        var clock = Text(mc, "Relogio", "00:00", ledFont, 78, Led, new Vector2(W / 2 - 60, H / 2 - 70), new Vector2(400, 100), TextAlignmentOptions.Right);
        var clk = clock.gameObject.AddComponent<BoardClock>();
        SetRef(clk, "label", clock);
        Bar(mc, "Linha", new Vector2(0, H / 2 - 130), new Vector2(W - 100, 5), Led);
        Text(mc, "Colunas", "NO  DESTINO", ledFont, 46, LedDim, new Vector2(-W / 2 + 60, H / 2 - 175), new Vector2(W - 120, 60), TextAlignmentOptions.Left);

        var b1 = BoardLine(mc, "Jogar", "01  JOGAR", ledFont, H / 2 - 270, W);
        var b2 = BoardLine(mc, "Carregar", "02  CARREGAR", ledFont, H / 2 - 390, W);
        var b3 = BoardLine(mc, "Sair", "03  SAIR", ledFont, H / 2 - 510, W);
        Text(mc, "Rodape", "PROXIMO TREM: A ULTIMA VIAGEM", ledFont, 50, LedDim, new Vector2(0, -H / 2 + 55), new Vector2(W - 120, 60), TextAlignmentOptions.Center);

        UnityEventTools.AddPersistentListener(b1.onClick, new UnityAction(controller.Play));
        UnityEventTools.AddPersistentListener(b2.onClick, new UnityAction(controller.OpenLoad));
        UnityEventTools.AddPersistentListener(b3.onClick, new UnityAction(controller.Quit));
        Chain(b1, b2, b3);

        // ---------------- Quadro de saves
        var load = BuildBoard("Quadro Carregar", L.loadBoardPos, BoardRotation(L.loadBoardPos, L.loadBoardYawOffset));
        loadGroup = load.group;
        var lc = load.canvas;

        Text(lc, "Titulo", "CARREGAR VIAGEM", ledFont, 70, Led, new Vector2(-W / 2 + 60, H / 2 - 70), new Vector2(W - 120, 100), TextAlignmentOptions.Left);
        Bar(lc, "Linha", new Vector2(0, H / 2 - 130), new Vector2(W - 100, 5), Led);
        Text(lc, "Colunas", "NO  NOME       TEMPO  DATA", ledFont, 46, LedDim, new Vector2(-W / 2 + 60, H / 2 - 175), new Vector2(W - 120, 60), TextAlignmentOptions.Left);

        var slots = new List<Button>();
        var lines = new List<SaveBoardLine>();

        for (int i = 0; i < 3; i++)
        {
            var btn = BoardLine(lc, $"Slot {i + 1}", $"0{i + 1}  -- VAZIO --", ledFont, H / 2 - 260 - i * 105, W);
            var line = btn.gameObject.AddComponent<SaveBoardLine>();
            var so = new SerializedObject(line);
            so.FindProperty("slot").intValue = i;
            so.FindProperty("label").objectReferenceValue = btn.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Texto");
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddIntPersistentListener(btn.onClick, controller.LoadSlot, i);
            slots.Add(btn);
            lines.Add(line);
        }

        var back = BoardLine(lc, "Voltar", "<   VOLTAR", ledFont, -H / 2 + 75, W);
        UnityEventTools.AddPersistentListener(back.onClick, new UnityAction(controller.Back));
        slots.Add(back);
        Chain(slots.ToArray());

        // ---------------- Liga no controlador
        var cso = new SerializedObject(controller);
        cso.FindProperty("cameraRig").objectReferenceValue = rig;
        cso.FindProperty("introPose").objectReferenceValue = poseIntro;
        cso.FindProperty("mainPose").objectReferenceValue = poseMain;
        cso.FindProperty("loadPose").objectReferenceValue = poseLoad;
        cso.FindProperty("mainBoard").objectReferenceValue = mainGroup;
        cso.FindProperty("mainPower").objectReferenceValue = main.power;
        cso.FindProperty("loadPower").objectReferenceValue = load.power;
        cso.FindProperty("loadBoard").objectReferenceValue = loadGroup;
        cso.FindProperty("firstMainButton").objectReferenceValue = b1.gameObject;
        cso.FindProperty("firstLoadButton").objectReferenceValue = slots[0].gameObject;
        var arr = cso.FindProperty("saveLines");
        arr.arraySize = lines.Count;
        for (int i = 0; i < lines.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
        cso.FindProperty("uiAudio").objectReferenceValue = controller.GetComponent<AudioSource>();
        cso.FindProperty("confirmClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelAbrindo.mp3");
        cso.FindProperty("backClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelFechando.mp3");
        cso.ApplyModifiedPropertiesWithoutUndo();
    }

    private struct Board
    {
        public RectTransform canvas; // camada dos LEDs, onde vai o conteúdo
        public CanvasGroup group;
        public Vector2 size;
        public LedBoardPower power;
    }

    private static Board BuildBoard(string name, Vector3 pos, Quaternion rot)
    {
        const float ppm = 400f; // pixels por metro
        var root = new GameObject(name).transform;
        root.SetPositionAndRotation(pos, rot);

        float w = L.boardWidth, h = L.boardHeight;
        var metal = LitMat("Metal Escuro", new Color(0.07f, 0.07f, 0.08f), 0.35f, 0.5f);

        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Moldura";
        frame.transform.SetParent(root, false);
        frame.transform.localScale = new Vector3(w + 0.16f, h + 0.16f, 0.1f);
        frame.GetComponent<Renderer>().sharedMaterial = metal;
        Object.DestroyImmediate(frame.GetComponent<Collider>());

        var hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hood.name = "Cobertura";
        hood.transform.SetParent(root, false);
        hood.transform.localPosition = new Vector3(0f, h / 2f + 0.12f, -0.15f);
        hood.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        hood.transform.localScale = new Vector3(w + 0.4f, 0.05f, 0.45f);
        hood.GetComponent<Renderer>().sharedMaterial = metal;
        Object.DestroyImmediate(hood.GetComponent<Collider>());

        // Postes do quadro até o chão da plataforma
        float postHeight = Mathf.Max(0.2f, pos.y - h / 2f - L.platformHeight);
        foreach (float side in new[] { -1f, 1f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Poste";
            post.transform.SetParent(root, false);
            post.transform.localScale = new Vector3(0.09f, (postHeight + h) / 2f, 0.09f);
            post.transform.localPosition = new Vector3(side * (w / 2f - 0.25f), -h / 2f - postHeight / 2f + h / 2f, 0.08f);
            post.GetComponent<Renderer>().sharedMaterial = metal;
            Object.DestroyImmediate(post.GetComponent<Collider>());
        }

        // Luz do quadro (o painel ilumina o chão em âmbar)
        var glow = new GameObject("Brilho do Painel");
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = new Vector3(0f, 0f, -0.9f);
        var gl = glow.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.color = new Color(1f, 0.6f, 0.2f);
        gl.intensity = 2.2f;
        gl.range = 4.5f;

        // Canvas do painel
        var canvasGo = new GameObject("Painel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        var rt = (RectTransform)canvasGo.transform;
        rt.SetParent(root, false);
        rt.localPosition = new Vector3(0f, 0f, -0.056f);
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(w * ppm, h * ppm);
        rt.localScale = Vector3.one / ppm;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;

        var bg = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        var bgRt = (RectTransform)bg.transform;
        bgRt.SetParent(rt, false);
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.015f, 0.012f, 0.01f, 1f);
        bgImg.raycastTarget = false;

        var group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 1f;

        // Camada dos LEDs: liga e desliga separada do fundo do painel
        var ledsGo = new GameObject("LEDs", typeof(RectTransform), typeof(CanvasGroup));
        var ledsRt = (RectTransform)ledsGo.transform;
        ledsRt.SetParent(rt, false);
        ledsRt.anchorMin = Vector2.zero;
        ledsRt.anchorMax = Vector2.one;
        ledsRt.offsetMin = Vector2.zero;
        ledsRt.offsetMax = Vector2.zero;

        var audio = canvasGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.5f;
        audio.minDistance = 4f;
        audio.maxDistance = 40f;

        var power = canvasGo.AddComponent<LedBoardPower>();
        var pso = new SerializedObject(power);
        pso.FindProperty("leds").objectReferenceValue = ledsGo.GetComponent<CanvasGroup>();
        pso.FindProperty("panelLight").objectReferenceValue = gl;
        pso.FindProperty("audioSource").objectReferenceValue = audio;
        pso.ApplyModifiedPropertiesWithoutUndo();

        return new Board { canvas = ledsRt, group = group, size = rt.sizeDelta, power = power };
    }

    private static Button BoardLine(RectTransform canvas, string name, string text, TMP_FontAsset font, float y, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DepartureBoardButton));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(width - 80f, 92f);

        var img = go.GetComponent<Image>();
        img.color = new Color(Led.r, Led.g, Led.b, 0f);

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;

        float lineLeft = -(width - 80f) / 2f;
        var label = Text(rt, "Texto", text, font, 64, LedDim, new Vector2(lineLeft + 70f, 0f), new Vector2(width - 180f, 92f), TextAlignmentOptions.Left);
        var cursor = Text(rt, "Cursor", ">", font, 64, Color.black, new Vector2(lineLeft + 32f, 0f), new Vector2(40f, 92f), TextAlignmentOptions.Center);
        label.raycastTarget = false;
        cursor.raycastTarget = false;

        var dbb = go.GetComponent<DepartureBoardButton>();
        var so = new SerializedObject(dbb);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("cursor").objectReferenceValue = cursor;
        so.FindProperty("highlight").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
        dbb.ApplyVisual(0f, false);

        return button;
    }

    private static void Chain(params Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length],
                selectOnDown = buttons[(i + 1) % buttons.Length],
            };
            buttons[i].navigation = nav;
        }
    }

    // ===================================================== Placa da estação

    private static void BuildTitleSign()
    {
        var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        var titleMat = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);

        var root = new GameObject("Placa da Estacao").transform;
        root.SetPositionAndRotation(L.titleSignPos, BoardRotation(L.titleSignPos, 0f));

        float w = L.titleSignWidth, h = 1.5f;
        var wood = LitMat("Placa Esmaltada", new Color(0.1f, 0.18f, 0.16f), 0.45f);

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Placa";
        plate.transform.SetParent(root, false);
        plate.transform.localScale = new Vector3(w, h, 0.08f);
        plate.GetComponent<Renderer>().sharedMaterial = wood;
        Object.DestroyImmediate(plate.GetComponent<Collider>());

        var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trim.name = "Borda";
        trim.transform.SetParent(root, false);
        trim.transform.localScale = new Vector3(w + 0.14f, h + 0.14f, 0.06f);
        trim.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        trim.GetComponent<Renderer>().sharedMaterial = LitMat("Borda Dourada", new Color(0.55f, 0.42f, 0.2f), 0.6f, 0.8f);
        Object.DestroyImmediate(trim.GetComponent<Collider>());

        float postHeight = L.titleSignPos.y - h / 2f - L.titleSignBaseY;
        foreach (float side in new[] { -1f, 1f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Poste";
            post.transform.SetParent(root, false);
            post.transform.localScale = new Vector3(0.12f, (postHeight + h) / 2f, 0.12f);
            post.transform.localPosition = new Vector3(side * (w / 2f - 0.4f), -h / 2f - postHeight / 2f + h / 2f, 0.1f);
            post.GetComponent<Renderer>().sharedMaterial = LitMat("Metal Escuro", new Color(0.07f, 0.07f, 0.08f), 0.35f, 0.5f);
            Object.DestroyImmediate(post.GetComponent<Collider>());
        }

        const float ppm = 200f;
        var canvasGo = new GameObject("Texto", typeof(RectTransform), typeof(Canvas));
        var rt = (RectTransform)canvasGo.transform;
        rt.SetParent(root, false);
        rt.localPosition = new Vector3(0f, 0f, -0.05f);
        rt.sizeDelta = new Vector2(w * ppm, h * ppm);
        rt.localScale = Vector3.one / ppm;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;

        var title = Text(rt, "Nome", "THE RAILWAY", titleFont, 165, Ivory, new Vector2(0f, 0f), new Vector2(w * ppm - 40f, 200f), TextAlignmentOptions.Center);
        if (titleMat != null) title.fontSharedMaterial = titleMat;
        title.characterSpacing = 4f;

        // Dois refletores iluminando a placa por baixo
        foreach (float side in new[] { -0.3f, 0.3f })
        {
            var spotGo = new GameObject("Refletor");
            spotGo.transform.SetParent(root, false);
            spotGo.transform.localPosition = new Vector3(side * w, -h / 2f - 0.5f, -1.4f);
            spotGo.transform.LookAt(root.TransformPoint(new Vector3(side * w * 0.6f, 0.1f, 0f)));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.82f, 0.6f);
            spot.intensity = 22f;
            spot.range = 7f;
            spot.spotAngle = 70f;
            spot.innerSpotAngle = 30f;
        }
    }

    // =============================================================== Overlay

    private static void BuildOverlay(Menu3DController controller)
    {
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        var go = new GameObject("Tela", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        // Prompt
        var promptGo = new GameObject("Aperte", typeof(RectTransform), typeof(CanvasGroup));
        var prt = (RectTransform)promptGo.transform;
        prt.SetParent(root, false);
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 110f);
        prt.sizeDelta = new Vector2(1000f, 60f);
        Text(prt, "Texto", "APERTE QUALQUER BOTÃO", bodyFont, 26, new Color(0.92f, 0.88f, 0.8f), Vector2.zero, new Vector2(1000f, 60f), TextAlignmentOptions.Center).characterSpacing = 18f;

        // Dicas
        var hintsGo = new GameObject("Dicas", typeof(RectTransform), typeof(CanvasGroup));
        var hrt = (RectTransform)hintsGo.transform;
        hrt.SetParent(root, false);
        hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0f);
        hrt.pivot = new Vector2(1f, 0f);
        hrt.anchoredPosition = new Vector2(-50f, 40f);
        hrt.sizeDelta = new Vector2(900f, 50f);
        Text(hrt, "Texto", "ENTER  SELECIONAR      ESC  VOLTAR", bodyFont, 20, new Color(0.85f, 0.82f, 0.75f, 0.7f), new Vector2(450f, 0f), new Vector2(900f, 50f), TextAlignmentOptions.Right).characterSpacing = 8f;

        // Fade (preto); fica transparente no editor, o controlador escurece ao iniciar
        var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var frt = (RectTransform)fadeGo.transform;
        frt.SetParent(root, false);
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        fadeGo.GetComponent<Image>().color = Color.black;
        fadeGo.GetComponent<Image>().raycastTarget = false;
        var fade = fadeGo.GetComponent<CanvasGroup>();
        fade.alpha = 0f;
        fade.blocksRaycasts = false;

        var so = new SerializedObject(controller);
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("prompt").objectReferenceValue = promptGo.GetComponent<CanvasGroup>();
        so.FindProperty("hints").objectReferenceValue = hintsGo.GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================================================================ Efeitos

    private static void BuildEffects(Bounds loco)
    {
        var root = new GameObject("Efeitos").transform;
        float len = loco.size.x;

        // Farol na frente (lado -X, virado para a câmera)
        Vector3 front = new Vector3(loco.min.x - 0.15f, loco.min.y + loco.size.y * 0.62f, 0f);
        var headGo = new GameObject("Farol");
        headGo.transform.SetParent(root);
        headGo.transform.position = front;
        headGo.transform.rotation = Quaternion.Euler(4f, -90f, 0f);
        var head = headGo.AddComponent<Light>();
        head.type = LightType.Spot;
        head.color = new Color(1f, 0.86f, 0.62f);
        head.intensity = L.headlightIntensity;
        head.range = L.headlightRange;
        head.spotAngle = 48f;
        head.innerSpotAngle = 18f;
        head.shadows = LightShadows.Soft;
        AddGlow(root, front, 3.2f, new Color(1f, 0.8f, 0.55f, 0.85f));
        AddGlow(root, front, 0.9f, new Color(1f, 0.95f, 0.85f, 1f));

        // Poeira no feixe do farol
        var dust = MakeParticles("Poeira no Farol", root, front + Vector3.left * 11f, GlowMaterial());
        var main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new Color(1f, 0.85f, 0.6f, 0.55f);
        main.maxParticles = 500;
        var em = dust.emission; em.rateOverTime = 60f;
        var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(22f, 2.2f, 2.6f);
        var nz = dust.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.4f;

        // Brasa da fornalha (cabine, no fundo da locomotiva)
        var fireGo = new GameObject("Fornalha");
        fireGo.transform.SetParent(root);
        fireGo.transform.position = new Vector3(loco.max.x - len * 0.2f, loco.min.y + loco.size.y * 0.45f, -1.2f);
        var fire = fireGo.AddComponent<Light>();
        fire.type = LightType.Point;
        fire.color = new Color(1f, 0.42f, 0.12f);
        fire.intensity = 8f;
        fire.range = 6f;
        var ff = fireGo.AddComponent<LightFlicker>();
        var fso = new SerializedObject(ff);
        fso.FindProperty("baseIntensity").floatValue = 8f;
        fso.FindProperty("flickerSpeed").floatValue = 9f;
        fso.FindProperty("flickerAmount").floatValue = 0.35f;
        fso.FindProperty("igniteOnStart").boolValue = false;
        fso.ApplyModifiedPropertiesWithoutUndo();

        // Fumaça da chaminé e vapor perto das rodas
        var chimney = new Vector3(loco.min.x + len * L.chimneyFromFront, loco.max.y - 0.1f, loco.center.z);
        var smoke = MakeParticles("Fumaca da Chamine", root, chimney, AshMaterial());
        smoke.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // cone para cima
        var sm = smoke.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        sm.startSize = new ParticleSystem.MinMaxCurve(1.1f * L.smokeScale, 1.8f * L.smokeScale);
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.4f, 0.44f, 0.55f), new Color(0.3f, 0.28f, 0.32f, 0.45f));
        sm.maxParticles = 300;
        var sem = smoke.emission; sem.rateOverTime = 9f;
        var ssh = smoke.shape; ssh.shapeType = ParticleSystemShapeType.Cone; ssh.angle = 12f; ssh.radius = 0.2f;
        var ssz = smoke.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
        var svl = smoke.velocityOverLifetime; svl.enabled = true; svl.space = ParticleSystemSimulationSpace.World;
        svl.x = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); svl.y = new ParticleSystem.MinMaxCurve(0f, 0f); svl.z = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

        var steam = MakeParticles("Vapor", root, new Vector3(loco.min.x + len * 0.3f, trackTop + 0.35f, loco.min.z - 0.1f), AshMaterial());
        steam.transform.rotation = Quaternion.Euler(-20f, 180f, 0f); // sai para o lado da plataforma
        var stm = steam.main;
        stm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        stm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        stm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        stm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        stm.startColor = new Color(0.85f, 0.85f, 0.9f, 0.3f);
        stm.maxParticles = 120;
        var stem = steam.emission; stem.rateOverTime = 7f;
        var stsh = steam.shape; stsh.shapeType = ParticleSystemShapeType.Cone; stsh.angle = 25f; stsh.radius = 0.3f;
        var stsz = steam.sizeOverLifetime; stsz.enabled = true; stsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.2f));

        // Cinzas no ar, perto da câmera
        var ash = MakeParticles("Cinzas", root, L.camMainPos + new Vector3(10f, 4f, 0f), AshMaterial());
        var am = ash.main;
        am.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        am.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        am.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        am.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.78f, 0.76f, 0.6f), new Color(0.55f, 0.5f, 0.55f, 0.4f));
        am.gravityModifier = 0.01f;
        am.maxParticles = 600;
        var aem = ash.emission; aem.rateOverTime = 40f;
        var ash_sh = ash.shape; ash_sh.shapeType = ParticleSystemShapeType.Box; ash_sh.scale = new Vector3(30f, 3f, 16f);
        var vel = ash.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, -0.15f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.25f, -0.1f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var anz = ash.noise; anz.enabled = true; anz.strength = 0.3f; anz.frequency = 0.3f;
    }

    private static ParticleSystem MakeParticles(string name, Transform parent, Vector3 pos, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return ps;
    }

    private static void AddGlow(Transform parent, Vector3 pos, float size, Color color)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Brilho";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, true);
        quad.transform.position = pos;
        quad.transform.localScale = Vector3.one * size / Mathf.Max(0.001f, parent.lossyScale.x);
        quad.transform.rotation = Quaternion.LookRotation(pos - L.camMainPos, Vector3.up);
        var r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = GlowMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        r.SetPropertyBlock(block);
        quad.AddComponent<Billboard>();
    }

    // ======================================================= Pós e áudio

    private static void BuildPostProcessing()
    {
        string path = Folder + "/Menu3D Profile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

        var bloom = Get<Bloom>(profile);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(L.bloomIntensity);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(new Color(1f, 0.85f, 0.72f));

        var ca = Get<ColorAdjustments>(profile);
        ca.postExposure.Override(L.postExposure);
        ca.contrast.Override(16f);
        ca.saturation.Override(-8f);

        var split = Get<SplitToning>(profile);
        split.shadows.Override(new Color(0.38f, 0.32f, 0.62f));
        split.highlights.Override(new Color(0.95f, 0.72f, 0.45f));
        split.balance.Override(-10f);

        var vig = Get<Vignette>(profile);
        vig.color.Override(new Color(0.03f, 0.01f, 0.05f));
        vig.intensity.Override(0.38f);
        vig.smoothness.Override(0.5f);

        var grain = Get<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.25f);

        Get<ChromaticAberration>(profile).intensity.Override(0.08f);

        var dof = Get<DepthOfField>(profile);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(35f);
        dof.gaussianEnd.Override(140f);
        dof.gaussianMaxRadius.Override(1.2f);

        EditorUtility.SetDirty(profile);

        var go = new GameObject("PosProcessamento");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T c)) { c.active = true; return c; }
        c = profile.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    private static void BuildAudio()
    {
        var go = new GameObject("Som Ambiente");
        AddLoop(go, "Assets/AllSounds/SomDeVento.mp3", 0.4f);
        AddLoop(go, "Assets/AllSounds/somdegrilo.wav", 0.14f);
    }

    private static void AddLoop(GameObject go, string path, float volume)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) return;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = true;
        s.volume = volume;
        s.spatialBlend = 0f;
    }

    // ================================================================ Prévias

    public static void Capture(StringBuilder output)
    {
        log = output;
        L = Menu3DLayout.Load();

        var camera = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(c => c.CompareTag("MainCamera"));
        var rigT = camera != null ? camera.transform.parent : null;

        if (camera == null || rigT == null)
        {
            log.AppendLine("prévia: câmera do menu 3D não encontrada");
            return;
        }

        var main = GameObject.Find("Quadro Partidas")?.GetComponentInChildren<CanvasGroup>(true);
        var load = GameObject.Find("Quadro Carregar")?.GetComponentInChildren<CanvasGroup>(true);
        var pIntro = GameObject.Find("Pose_Abertura")?.transform;
        var pMain = GameObject.Find("Pose_Menu")?.transform;
        var pLoad = GameObject.Find("Pose_Carregar")?.transform;
        var firstMain = main != null ? main.GetComponentInChildren<DepartureBoardButton>(true) : null;
        var firstLoad = load != null ? load.GetComponentInChildren<DepartureBoardButton>(true) : null;

        Vector3 rigPos = rigT.position;
        Quaternion rigRot = rigT.rotation;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);

        try
        {
            camera.targetTexture = rt;

            Shot(camera, rigT, pIntro, rt, "3d_1_abertura.png", main, 0f, load, 0f);

            firstMain?.ApplyVisual(1f, true);
            Shot(camera, rigT, pMain, rt, "3d_2_menu.png", main, 1f, load, 0f);
            firstMain?.ApplyVisual(0f, false);

            firstLoad?.ApplyVisual(1f, true);
            Shot(camera, rigT, pLoad, rt, "3d_3_carregar.png", main, 0f, load, 1f);
            firstLoad?.ApplyVisual(0f, false);

            var wide = new GameObject("tmp_wide").transform;
            wide.position = new Vector3(L.camMainPos.x - 18f, 16f, L.camMainPos.z - 26f);
            wide.LookAt(new Vector3(L.locoX - 6f, 1f, 0f));
            Shot(camera, rigT, wide, rt, "3d_4_geral.png", main, 1f, load, 1f);
            wide.position = new Vector3((L.platformFrom + L.platformTo) / 2f, 95f, -6f);
            wide.rotation = Quaternion.Euler(90f, 0f, 0f);
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            Shot(camera, rigT, wide, rt, "3d_5_topo.png", main, 1f, load, 1f);
            RenderSettings.fog = fog;
            Object.DestroyImmediate(wide.gameObject);
        }
        finally
        {
            camera.targetTexture = null;
            rigT.SetPositionAndRotation(rigPos, rigRot);
            if (main != null) main.alpha = 1f;
            if (load != null) load.alpha = 1f;
            Object.DestroyImmediate(rt);
        }
    }

    private static void Shot(Camera camera, Transform rigT, Transform pose, RenderTexture rt, string file,
        CanvasGroup main, float mainAlpha, CanvasGroup load, float loadAlpha)
    {
        if (pose == null) return;
        rigT.SetPositionAndRotation(pose.position, pose.rotation);
        if (main != null) main.alpha = mainAlpha;
        if (load != null) load.alpha = loadAlpha;

        Canvas.ForceUpdateCanvases();
        camera.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        Directory.CreateDirectory(PreviewFolder);
        File.WriteAllBytes(Path.Combine(PreviewFolder, file), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        log.AppendLine("prévia: " + file);
    }

    // ============================================================ Utilidades

    private const string GameScenePath = "Assets/Scenes/CenaPrincipal.unity";

    /// <summary>
    /// Copia objetos já configurados de outra cena (só leitura: a cena é aberta
    /// em modo aditivo e fechada sem salvar). Remove scripts, luzes, áudio,
    /// física e animações das cópias, deixando só o visual.
    /// </summary>
    private static Dictionary<string, GameObject> CopyFromScene(string path, params string[] names)
    {
        var result = new Dictionary<string, GameObject>();
        var target = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        if (!File.Exists(path))
            return result;

        var source = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            var all = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

            foreach (var name in names)
            {
                var original = all.FirstOrDefault(t => t.name == name);
                if (original == null) continue;

                var copy = Object.Instantiate(original.gameObject);
                copy.name = name;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, target);
                copy.transform.SetPositionAndRotation(original.position, original.rotation);
                copy.transform.localScale = original.lossyScale;
                copy.SetActive(true);
                StripToVisual(copy);
                result[name] = copy;
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);
        }

        return result;
    }

    private static void StripToVisual(GameObject go)
    {
        // Ordem importa: componentes que dependem de outros saem primeiro.
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystemRenderer>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    /// <summary>Limites só dos renderers visíveis (ativos e ligados), sem partículas.</summary>
    private static Bounds VisibleBounds(params GameObject[] gos)
    {
        Bounds? b = null;

        foreach (var go in gos)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (b == null) b = r.bounds;
                else { var bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
            }
        }

        return b ?? new Bounds(gos.Length > 0 ? gos[0].transform.position : Vector3.zero, Vector3.zero);
    }

    private static GameObject PlaceAlongX(string path, float length, Vector3 bottomCenter, bool flip, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { log.AppendLine("AVISO: modelo não encontrado: " + path); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.position = Vector3.zero;
        var b = BoundsOf(go);
        float yaw = b.size.z > b.size.x ? 90f : 0f;
        if (flip) yaw += 180f;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;

        return Fit(go, bottomCenter, b2 => b2.size.x, length, parent);
    }

    private static GameObject Place(string path, Vector3 bottomCenter, float yaw, Func<Bounds, float> measure, float target, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { log.AppendLine("AVISO: modelo não encontrado: " + path); return null; }
        return Place(asset, bottomCenter, yaw, measure, target, parent);
    }

    private static GameObject Place(GameObject asset, Vector3 bottomCenter, float yaw, Func<Bounds, float> measure, float target, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
        return Fit(go, bottomCenter, measure, target, parent);
    }

    private static GameObject Fit(GameObject go, Vector3 bottomCenter, Func<Bounds, float> measure, float target, Transform parent)
    {
        var b = BoundsOf(go);
        float m = measure(b);

        if (m > 0.0001f)
            go.transform.localScale *= target / m;

        b = BoundsOf(go);
        go.transform.position += bottomCenter - new Vector3(b.center.x, b.min.y, b.center.z);

        if (parent != null)
            go.transform.SetParent(parent, true);

        ConvertMaterials(go);
        return go;
    }

    private static GameObject PlacePrefab(string path, Vector3 pos, Transform parent, float scale)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { log.AppendLine("AVISO: efeito não encontrado: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.position = pos;
        go.transform.localScale *= scale;
        ConvertMaterials(go);
        return go;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private static T LoadSubAsset<T>(string path, long localId) where T : Object
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is T typed && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long id) && id == localId)
                return typed;
        }
        return null;
    }

    private static void MakeStatic(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    private static TextMeshProUGUI Text(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        // Textos alinhados à esquerda começam na posição x informada
        if (align == TextAlignmentOptions.Left)
        {
            rt.pivot = new Vector2(0f, 0.5f);
        }
        else if (align == TextAlignmentOptions.Right)
        {
            rt.pivot = new Vector2(1f, 0.5f);
        }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Bar(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    // ============================================================ Materiais

    private static Material LitMat(string name, Color color, float smoothness, float metallic = 0f)
    {
        string path = $"{Folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material GroundMaterial()
    {
        var m = LitMat("Chao", new Color(0.16f, 0.15f, 0.12f), 0.05f);
        var source = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/grass-with-dirt-patches-material" })
            .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(x => x != null);

        var tex = source != null ? (source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null) ?? source.mainTexture : null;

        if (tex != null)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(L.groundSize / 6f, L.groundSize / 6f));
            m.SetColor("_BaseColor", new Color(0.55f, 0.52f, 0.48f));
        }

        return m;
    }

    private static Material glowMat, ashMat;

    private static Material GlowMaterial()
    {
        if (glowMat != null) return glowMat;

        string texPath = Folder + "/Brilho.png";

        if (!File.Exists(texPath))
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        glowMat = ParticleMaterial("Brilho Aditivo", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), additive: true);
        return glowMat;
    }

    private static Material AshMaterial()
    {
        if (ashMat != null) return ashMat;
        GlowMaterial();
        ashMat = ParticleMaterial("Cinzas 3D", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Brilho.png"), additive: false);
        return ashMat;
    }

    private static Material ParticleMaterial(string name, Texture2D tex, bool additive)
    {
        string path = $"{Folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Troca materiais de shaders que não funcionam no URP (ficariam rosa) por equivalentes URP.</summary>
    private static void ConvertMaterials(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null || src.shader == null || IsUrp(src.shader)) continue;

                if (!converted.TryGetValue(src, out var dst))
                {
                    dst = ConvertMaterial(src, r is ParticleSystemRenderer);
                    converted[src] = dst;
                }

                mats[i] = dst;
                changed = true;
            }

            if (changed) r.sharedMaterials = mats;
        }
    }

    private static bool IsUrp(Shader s)
    {
        string n = s.name;
        return n.StartsWith("Universal Render Pipeline") || n.StartsWith("Shader Graphs") || n.StartsWith("TextMeshPro") ||
               n.StartsWith("UI/") || n.StartsWith("Sprites/") || (s.isSupported && !n.StartsWith("Standard") && !n.StartsWith("Legacy") &&
               !n.StartsWith("Particles/") && !n.StartsWith("Mobile/") && n != "Hidden/InternalErrorShader");
    }

    private static Material ConvertMaterial(Material src, bool particle)
    {
        string safe = string.Join("_", src.name.Split(Path.GetInvalidFileNameChars()));
        string path = $"{ConvertedFolder}/{safe}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Texture tex = null;
        foreach (var p in new[] { "_MainTex", "_BaseMap", "_BaseColorMap" })
            if (src.HasProperty(p) && src.GetTexture(p) != null) { tex = src.GetTexture(p); break; }

        Color color = src.HasProperty("_Color") ? src.GetColor("_Color") : src.HasProperty("_TintColor") ? src.GetColor("_TintColor") : Color.white;

        Material m;

        if (particle)
        {
            m = ParticleMaterial("Convertidos/" + safe, tex as Texture2D, additive: src.shader.name.ToLowerInvariant().Contains("add"));
            m.SetColor("_BaseColor", color);
        }
        else
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null)
            {
                m.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                m.EnableKeyword("_NORMALMAP");
            }

            bool cutout = (src.HasProperty("_Mode") && Mathf.Approximately(src.GetFloat("_Mode"), 1f)) ||
                          (src.renderQueue >= 2400 && src.renderQueue < 2500) || src.IsKeywordEnabled("_ALPHATEST_ON");
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }

            m.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(m, path);
        }

        log.AppendLine($"material convertido para URP: {src.name} ({src.shader.name})");
        return m;
    }
}
