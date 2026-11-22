using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Constrói a cena "Prologo": a ferrovia abandonada onde o Cândido acorda,
/// com jornais, rádio e o puzzle do trem (água, carvão, fogo e alavanca).
/// </summary>
public static class PrologoBuilder
{
    public const string ScenePath = "Assets/Scenes/Prologo.unity";
    private const string GameScenePath = "Assets/Scenes/CenaPrincipal.unity";
    private const string Folder = "Assets/Scenes/Prologo";
    private const string StreetLightPath = "Assets/Models/low poly street lights/source/street_lights.fbx";
    private const long StreetLightMeshId = 8714690333086689727;
    private const string Skybox = "Assets/AllSkyFree/Overcast Low/AllSky_Overcast4_Low.mat";

    private static readonly string[] Trees =
    {
        "Assets/Arvores Mapa/tree01.prefab", "Assets/Arvores Mapa/tree02.prefab", "Assets/Arvores Mapa/tree03.prefab",
        "Assets/Arvores Mapa/tree04.prefab", "Assets/Arvores Mapa/tree05.prefab", "Assets/Arvores Mapa/tree06.prefab",
        "Assets/Arvores Mapa/tree07.prefab", "Assets/Arvores Mapa/tree08.prefab",
    };
    private static readonly string[] Bushes = { "Assets/Arvores Mapa/bush03.prefab", "Assets/Arvores Mapa/grass_bush.prefab" };

    // Medidas (metros). Trilho em z = 0, plataforma do lado -Z.
    private const float PlatH = 0.9f, PlatFrom = -32f, PlatTo = 30f, PlatGap = 1.6f, PlatW = 4.6f;
    private static float PlatBack => -(PlatGap + PlatW);
    private const float TrackTop = 0.39f;

    private static Random rng;
    private static StringBuilder log;
    private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    private static Transform root;
    private static PrologoController controller;
    private static Fornalha fornalha;

    [MenuItem("The Railway/Prólogo/Construir cena da ferrovia")]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sb = new StringBuilder();
        Build(sb);
        Debug.Log("[The Railway] Prólogo:\n" + sb);
    }

    // ===================================================================== Build

    public static void Build(StringBuilder output)
    {
        log = output;
        rng = new Random(2021);
        mats.Clear();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Prologo");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            root = new GameObject("Ferrovia Abandonada").transform;
            var copies = CopyFromScene(GameScenePath,
                keepScripts: new[] { "PlayerCapsule", "MainCamera 1", "PlayerFollowCamera" },
                visualOnly: new[] { "steam-train", "Dom" });

            Environment();
            GroundAndTrack();
            Platform();
            var loco = Train(copies);
            OverturnedWagon(copies);
            House(copies);
            WaterTower();
            CoalShed();
            Office();
            Outhouse();
            BenchAndPapers();
            Dressing();
            Nature();
            Systems(copies, loco);

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name.StartsWith("Hidden/InternalError") || !m.shader.name.Contains("Universal") && !m.shader.name.Contains("Shader Graphs") && !m.shader.name.StartsWith("Sprites") && !m.shader.name.StartsWith("UI") && !m.shader.name.StartsWith("TextMeshPro"))
                        log.AppendLine($"material suspeito: {AnimationUtility.CalculateTransformPath(r.transform, null)} -> {(m == null ? "null" : m.name + " / " + m.shader?.name)}");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            log.AppendLine("cena salva: " + ScenePath);
        }
        catch
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            throw;
        }
    }

    // ================================================================ Ambiente

    private static void Environment()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(Skybox);
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.5f, 0.52f, 0.56f);
        RenderSettings.fogDensity = 0.02f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.64f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.13f);

        var sun = new GameObject("Sol Nublado").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.86f, 0.84f, 0.8f);
        sun.intensity = 0.95f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        sun.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
        RenderSettings.sun = sun;

        // Pós-processamento: desbotado, frio, granulado
        var profile = Profile("Prologo Profile");
        Get<Tonemapping>(profile).mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
        var ca = Get<ColorAdjustments>(profile);
        ca.postExposure.Override(0.55f); ca.contrast.Override(14f); ca.saturation.Override(-38f);
        var st = Get<SplitToning>(profile);
        st.shadows.Override(new Color(0.42f, 0.5f, 0.55f)); st.highlights.Override(new Color(0.85f, 0.78f, 0.62f)); st.balance.Override(-15f);
        var vg = Get<Vignette>(profile); vg.intensity.Override(0.32f); vg.smoothness.Override(0.5f);
        var fg = Get<FilmGrain>(profile); fg.type.Override(FilmGrainLookup.Medium4); fg.intensity.Override(0.38f);
        var bl = Get<Bloom>(profile); bl.intensity.Override(0.6f); bl.threshold.Override(1f);
        var vol = new GameObject("PosProcessamento").AddComponent<Volume>();
        vol.isGlobal = true; vol.sharedProfile = profile;
        EditorUtility.SetDirty(profile);

        // Som do vento
        var amb = new GameObject("Som Ambiente");
        var wind = amb.AddComponent<AudioSource>();
        wind.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/SomDeVento.mp3");
        wind.loop = true; wind.playOnAwake = true; wind.volume = 0.45f; wind.spatialBlend = 0f;
    }

    private static void GroundAndTrack()
    {
        var env = Group("Terreno e Trilho");

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Chao";
        ground.transform.SetParent(env);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
        ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();
        Static(ground);

        Box("Brita", env, new Vector3(20f, 0.05f, 0f), new Vector3(240f, 0.1f, 3.4f), Mat("Brita Suja", new Color(0.24f, 0.22f, 0.2f), 0.05f), collider: false);

        var rust = Mat("Trilho Enferrujado", new Color(0.36f, 0.22f, 0.14f), 0.3f, 0.45f);
        var wood = Mat("Dormente Podre", new Color(0.2f, 0.15f, 0.11f), 0.05f);
        foreach (float side in new[] { -0.72f, 0.72f })
            Box("Trilho", env, new Vector3(20f, 0.33f, side), new Vector3(240f, 0.12f, 0.08f), rust, collider: false);

        int count = 0;
        for (float x = -100f; x < 140f; x += 0.7f)
        {
            if (rng.NextDouble() < 0.12) continue; // dormentes faltando
            var s = Box("Dormente", env, new Vector3(x + R(-0.08f, 0.08f), 0.18f + R(-0.03f, 0.02f), R(-0.1f, 0.1f)), new Vector3(0.24f, 0.12f, 2.4f + R(-0.2f, 0.1f)), wood, collider: false);
            s.transform.rotation = Quaternion.Euler(R(-3f, 3f), R(-6f, 6f), R(-2f, 2f));
            Static(s); count++;
        }
        log.AppendLine($"trilho com {count} dormentes (12% faltando)");
    }

    private static void Platform()
    {
        var p = Group("Plataforma Rachada");
        var concrete = Mat("Concreto Sujo", new Color(0.36f, 0.35f, 0.33f), 0.1f);
        float z = -(PlatGap + PlatW / 2f);

        for (float x = PlatFrom; x < PlatTo; x += 3f)
        {
            float w = Mathf.Min(3f, PlatTo - x) - 0.06f;
            // só visual: o piso de verdade é um colisor plano único (abaixo)
            var slab = Box("Laje", p, new Vector3(x + w / 2f, PlatH / 2f + R(-0.012f, 0.004f), z), new Vector3(w, PlatH, PlatW), concrete, collider: false);
            slab.transform.rotation = Quaternion.Euler(R(-0.25f, 0.25f), R(-0.5f, 0.5f), R(-0.25f, 0.25f));
            Static(slab);

            if (rng.NextDouble() < 0.7)
            {
                var stripe = Box("Faixa Desbotada", p, new Vector3(x + w / 2f, PlatH + 0.006f, -PlatGap - 0.3f), new Vector3(w * R(0.5f, 0.95f), 0.02f, 0.3f), Mat("Faixa Desbotada", new Color(0.55f, 0.48f, 0.22f), 0.2f), collider: false);
                Static(stripe);
            }
        }

        // Piso da plataforma: um colisor só, plano, na altura exata (sem degrauzinhos entre lajes)
        var piso = new GameObject("Piso da Plataforma (colisor)");
        piso.transform.SetParent(p, false);
        piso.transform.position = new Vector3((PlatFrom + PlatTo) / 2f, PlatH / 2f, z);
        piso.AddComponent<BoxCollider>().size = new Vector3(PlatTo - PlatFrom, PlatH, PlatW);
        Static(piso);

        // Rampas nas pontas para descer/subir
        Ramp(p, new Vector3(PlatFrom - 1.6f, 0f, z), true, concrete);
        Ramp(p, new Vector3(PlatTo + 1.6f, 0f, z), false, concrete);

        // Placa da estação (dois postes)
        var iron = Mat("Ferro Velho", new Color(0.2f, 0.17f, 0.15f), 0.3f, 0.6f);
        foreach (float sx in new[] { -1.4f, 1.4f }) Static(Cyl("Poste Placa", p, new Vector3(-2f + sx, PlatH + 1.25f, PlatBack + 0.4f), 0.05f, 2.5f, iron));
        var station = Sign(p, "ESTAÇÃO VILA ESPERANÇA", new Vector3(-2f, PlatH + 2.3f, PlatBack + 0.4f), 0f, 3.2f);
        station.transform.rotation = Quaternion.Euler(0f, 0f, -2.5f); // torta

        // Escadas na parte de trás (galpão e banheiro)
        Stairs(p, 14f, concrete, frente: false);
        Stairs(p, -20f, concrete, frente: false);
        Stairs(p, 25f, concrete, frente: true);

        // Postes: apagados, um piscando
        var mesh = LoadSub<Mesh>(StreetLightPath, StreetLightMeshId);
        var lampMats = AssetDatabase.LoadAllAssetsAtPath(StreetLightPath).OfType<Material>().ToArray();
        int i = 0;
        for (float x = PlatFrom + 5f; x < PlatTo - 2f; x += 12f, i++)
        {
            var lamp = new GameObject("Poste Apagado");
            lamp.transform.SetParent(p);
            if (mesh != null)
            {
                lamp.AddComponent<MeshFilter>().sharedMesh = mesh;
                lamp.AddComponent<MeshRenderer>().sharedMaterials = lampMats;
                float s = 5f / Mathf.Max(0.01f, mesh.bounds.size.y);
                lamp.transform.localScale = Vector3.one * s;
                lamp.transform.position = new Vector3(x, PlatH - mesh.bounds.min.y * s, PlatBack + 0.7f);
                lamp.transform.rotation = Quaternion.Euler(R(-4f, 4f), 0f, R(-4f, 4f)); // tortos
            }
            Static(lamp);

            if (i == 2)
            {
                var l = new GameObject("Luz Falhando");
                l.transform.SetParent(lamp.transform, true);
                l.transform.position = new Vector3(x, PlatH + 4.5f, PlatBack + 1f);
                var light = l.AddComponent<Light>();
                light.type = LightType.Point; light.color = new Color(1f, 0.65f, 0.35f); light.intensity = 5f; light.range = 9f;
                var f = l.AddComponent<LightFlicker>();
                SetF(f, "baseIntensity", 5f); SetF(f, "failChancePerSecond", 1.6f); SetF(f, "flickerAmount", 0.5f);
            }
        }
    }

    private static void Stairs(Transform parent, float x, Material m, bool frente)
    {
        const int steps = 4;
        float h = PlatH / steps, d = 0.32f;
        float borda = frente ? -PlatGap : PlatBack, dir = frente ? 1f : -1f;
        for (int i = 0; i < steps; i++)
        {
            float top = PlatH - h * (i + 1);
            var st = Box("Degrau", parent, new Vector3(x, (top + h) / 2f, borda + dir * d * (i + 0.5f)), new Vector3(1.6f, top + h, d), m);
            Static(st);
        }
    }

    /// <summary>Placa esmaltada azul com letras brancas desbotadas (de frente para +Z).</summary>
    private static GameObject Sign(Transform parent, string text, Vector3 pos, float yaw, float width)
    {
        var plate = Box("Placa " + text, parent, pos, new Vector3(width, 0.42f, 0.04f), Mat("Placa Esmalte", new Color(0.13f, 0.19f, 0.3f), 0.45f), collider: false);
        plate.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (float face in new[] { 1f, -1f })
        {
            var t = new GameObject("Letras", typeof(RectTransform)).AddComponent<TextMeshPro>();
            t.transform.SetParent(plate.transform, false);
            var rt = (RectTransform)t.transform;
            rt.localPosition = new Vector3(0f, 0f, face * 0.6f);
            rt.localRotation = Quaternion.Euler(0f, face > 0 ? 180f : 0f, 0f);
            rt.sizeDelta = new Vector2(width * 0.94f, 0.36f);
            rt.localScale = new Vector3(1f / width, 1f / 0.42f, 1f / 0.04f); // compensa a escala da placa
            t.text = text;
            t.fontSize = 2.2f;
            t.enableAutoSizing = true; t.fontSizeMin = 0.5f; t.fontSizeMax = 2.6f;
            t.characterSpacing = 6f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.86f, 0.84f, 0.78f);
            t.fontStyle = FontStyles.Bold;
        }
        Static(plate);
        return plate;
    }

    private static void Ramp(Transform parent, Vector3 basePos, bool left, Material m)
    {
        var r = Box("Rampa", parent, basePos + new Vector3(0f, PlatH / 2f - 0.15f, 0f), new Vector3(3.4f, 0.3f, PlatW), m);
        float ang = Mathf.Atan2(PlatH, 3.2f) * Mathf.Rad2Deg;
        r.transform.rotation = Quaternion.Euler(0f, 0f, left ? ang : -ang); // alta do lado da plataforma
        Static(r);
    }

    // =================================================================== Trem

    private static Bounds Train(Dictionary<string, GameObject> copies)
    {
        var trainRoot = Group("Trem Parado");
        if (!copies.TryGetValue("steam-train", out var loco))
        {
            log.AppendLine("AVISO: locomotiva não encontrada na CenaPrincipal");
            return new Bounds(new Vector3(10f, 2f, 0f), new Vector3(10f, 4f, 3f));
        }

        // Na CenaPrincipal o vagão é filho da locomotiva
        var wagon = loco.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "vagon")?.gameObject;
        Bounds LocoBounds() => Visible(loco.GetComponentsInChildren<Renderer>(false).Where(r => (wagon == null || !r.transform.IsChildOf(wagon.transform)) && !r.name.StartsWith("Modular_SciFi"))); // escadas de embarque ficam de fora

        var gb = Visible(loco);
        var g = new GameObject("Trem");
        g.transform.SetParent(trainRoot);
        g.transform.position = gb.center;
        loco.transform.SetParent(g.transform, true);
        if (gb.size.z > gb.size.x) g.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * g.transform.rotation;

        // locomotiva na frente (lado -X, de onde o jogador vem), vagão atrás
        var lb = LocoBounds();
        if (wagon != null && Visible(wagon).center.x < lb.center.x) g.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * g.transform.rotation;

        gb = Visible(loco);
        lb = LocoBounds();
        g.transform.position += new Vector3(4f - lb.min.x, TrackTop - 0.02f - gb.min.y, -lb.center.z);
        lb = LocoBounds();

        // Colisores simples para não atravessar o trem
        AddBoxCollider(loco, lb);
        foreach (var mf in loco.GetComponentsInChildren<MeshFilter>(true))
            if (mf.name.StartsWith("Modular_SciFi") && mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; // escada de embarque
        if (wagon != null) AddBoxCollider(wagon, Visible(wagon));
        log.AppendLine($"vagão: {(wagon != null ? $"x {Visible(wagon).min.x:0.0}..{Visible(wagon).max.x:0.0}" : "não achado")}");

        // ---- Pontos do puzzle na cabine (lado da plataforma, alcançável) ----
        float cabX = lb.max.x - lb.size.x * 0.18f;
        float side = lb.min.z - 0.05f;
        float y = lb.min.y + lb.size.y * 0.42f;

        var exam = Point("Examinar Locomotiva", trainRoot, new Vector3(lb.min.x + lb.size.x * 0.35f, y, side), new Vector3(lb.size.x * 0.45f, lb.size.y * 0.6f, 0.4f));
        exam.AddComponent<ExaminarLocomotiva>();

        var fornGo = Point("Fornalha", trainRoot, new Vector3(cabX - 1.2f, lb.min.y + 1.4f, side), new Vector3(0.9f, 0.9f, 0.5f));
        fornalha = fornGo.AddComponent<Fornalha>();
        var doorMat = Mat("Ferro Fornalha", new Color(0.12f, 0.11f, 0.1f), 0.4f, 0.7f);
        Box("Porta da Fornalha", fornGo.transform, fornGo.transform.position + new Vector3(0f, 0f, -0.06f), new Vector3(0.7f, 0.6f, 0.06f), doorMat, collider: false);
        var brasas = Box("Brasas", fornGo.transform, fornGo.transform.position + new Vector3(0f, -0.15f, 0.2f), new Vector3(0.6f, 0.15f, 0.3f), Emissive("Brasa", new Color(1f, 0.35f, 0.08f), 4f), collider: false);
        var fireLightGo = new GameObject("Luz do Fogo");
        fireLightGo.transform.SetParent(fornGo.transform, false);
        fireLightGo.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        var fireLight = fireLightGo.AddComponent<Light>();
        fireLight.type = LightType.Point; fireLight.color = new Color(1f, 0.45f, 0.15f); fireLight.intensity = 6f; fireLight.range = 6f; fireLight.enabled = false;
        var ff = fireLightGo.AddComponent<LightFlicker>();
        SetF(ff, "baseIntensity", 6f); SetF(ff, "flickerSpeed", 9f); SetF(ff, "flickerAmount", 0.35f); SetB(ff, "igniteOnStart", false);

        var smoke = Smoke(trainRoot, new Vector3(lb.min.x + lb.size.x * 0.2f, lb.max.y - 0.1f, lb.center.z));
        var fogoSom = fornGo.AddComponent<AudioSource>();
        fogoSom.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/SomDeVento.mp3");
        fogoSom.loop = true; fogoSom.playOnAwake = false; fogoSom.spatialBlend = 1f; fogoSom.volume = 0.25f; fogoSom.pitch = 0.6f;

        SetO(fornalha, "luzDoFogo", fireLight);
        SetO(fornalha, "fumacaChamine", smoke);
        SetO(fornalha, "brasas", brasas);
        SetO(fornalha, "somFogo", fogoSom);
        SetO(fornalha, "somCarvao", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/SomDePassosNaGrama.mp3"));

        var regGo = Point("Regulador", trainRoot, new Vector3(cabX, lb.min.y + 2.0f, side), new Vector3(0.6f, 0.8f, 0.5f));
        var reg = regGo.AddComponent<Regulador>();
        Box("Suporte do Regulador", regGo.transform, regGo.transform.position + new Vector3(0f, -0.2f, 0f), new Vector3(0.18f, 0.18f, 0.18f), doorMat, collider: false);
        var lever = Box("Alavanca Encaixada", regGo.transform, regGo.transform.position + new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.55f, 0.06f), Mat("Latão", new Color(0.65f, 0.5f, 0.25f), 0.6f, 0.9f), collider: false);
        SetO(reg, "alavancaVisual", lever);

        var radioGo = Point("Radio", trainRoot, new Vector3(cabX - 0.6f, lb.min.y + 2.6f, side), new Vector3(0.5f, 0.35f, 0.35f)); // acima, entre a fornalha e o regulador
        Box("Radio Caixa", radioGo.transform, radioGo.transform.position, new Vector3(0.45f, 0.28f, 0.25f), Mat("Radio Baquelite", new Color(0.25f, 0.16f, 0.1f), 0.5f), collider: false);
        Box("Radio Painel", radioGo.transform, radioGo.transform.position + new Vector3(0f, 0.02f, -0.13f), new Vector3(0.3f, 0.12f, 0.02f), Emissive("Radio Luz", new Color(1f, 0.6f, 0.2f), 1.5f), collider: false);
        radioGo.AddComponent<RadioDoTrem>();

        log.AppendLine($"trem: locomotiva x {lb.min.x:0.0}..{lb.max.x:0.0}; cabine em x={cabX:0.0}");
        return lb;
    }

    private static void OverturnedWagon(Dictionary<string, GameObject> copies)
    {
        // Um segundo vagão tombado ao lado do trilho
        var vag = CopyFromScene(GameScenePath, null, new[] { "vagon" });
        if (!vag.TryGetValue("vagon", out var w)) return;
        w.name = "Vagao Tombado";
        w.transform.SetParent(Group("Vagao Tombado"), true);
        w.transform.rotation = Quaternion.Euler(0f, 12f, 82f) * w.transform.rotation;
        var b = Visible(w);
        w.transform.position += new Vector3(-16f - b.center.x, -b.min.y - 0.15f, 7.5f - b.center.z);
        AddBoxCollider(w);
    }

    private static void House(Dictionary<string, GameObject> copies)
    {
        if (!copies.TryGetValue("Dom", out var house)) return;
        house.name = "Casa da Estacao";
        house.transform.SetParent(Group("Casa da Estacao"), true);
        var b = Visible(house);
        float s = 10f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
        house.transform.localScale *= s;
        b = Visible(house);
        house.transform.position += new Vector3(-6f - b.center.x, -b.min.y, -15.5f - b.center.z);
        AddBoxCollider(house);
        Static(house);
    }

    // =========================================================== Caixa-d'água

    private static void WaterTower()
    {
        var t = Group("Caixa d'Agua");
        var pos = new Vector3(22f, 0f, 5.5f);
        var wood = Mat("Madeira Velha", new Color(0.3f, 0.22f, 0.15f), 0.08f);
        var iron = Mat("Ferro Velho", new Color(0.2f, 0.17f, 0.15f), 0.3f, 0.6f);

        foreach (var off in new[] { new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(-1.5f, 1.5f), new Vector2(1.5f, 1.5f) })
        {
            var leg = Cyl("Perna", t, pos + new Vector3(off.x, 3f, off.y), 0.25f, 6f, wood);
            Static(leg);
        }
        var tank = Cyl("Tanque", t, pos + new Vector3(0f, 7.6f, 0f), 2.6f, 3.2f, wood);
        Static(tank);
        var roof = Cyl("Teto", t, pos + new Vector3(0f, 9.4f, 0f), 2.9f, 0.3f, iron);
        Static(roof);

        // bica sobre o trilho
        var spout = Cyl("Bica", t, pos + new Vector3(0f, 6.2f, -2.6f), 0.18f, 2.6f, iron, collider: false);
        spout.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

        // válvula na perna da frente (lado do trilho)
        var valvePos = pos + new Vector3(-1.5f, 1.3f, -1.85f);
        var valveGo = Point("Valvula", t, valvePos, new Vector3(0.7f, 0.7f, 0.5f));
        var wheel = Cyl("Volante", valveGo.transform, valvePos, 0.32f, 0.08f, Mat("Volante Vermelho", new Color(0.45f, 0.08f, 0.06f), 0.4f, 0.5f), collider: false);
        wheel.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var jet = Water(t, pos + new Vector3(0f, 5.2f, -3.4f));
        var water = valveGo.AddComponent<AudioSource>();
        water.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/SomDeVento.mp3");
        water.loop = true; water.playOnAwake = false; water.spatialBlend = 1f; water.pitch = 1.8f; water.volume = 0.6f;
        var valve = valveGo.AddComponent<ValvulaCaixaDagua>();
        SetO(valve, "volante", wheel.transform);
        SetO(valve, "jatoDeAgua", jet);
        SetO(valve, "somAgua", water);

        // torneira e balde
        var tapGo = Point("Torneira", t, pos + new Vector3(1.5f, 0.9f, -1.9f), new Vector3(0.5f, 0.5f, 0.5f));
        Cyl("Cano", tapGo.transform, tapGo.transform.position, 0.06f, 0.4f, iron, collider: false).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var tap = tapGo.AddComponent<Torneira>();
        SetS(tap, "flagAoUsar", PrologoController.FlagBebeu);
        SetF(tap, "quantidade", 45f);
        SetS(tap, "falaSemAgua", "Seca. A água deve vir lá de cima... da caixa-d'água.");
        SetS(tap, "falaPrimeiraVez", "Ferrugem e tudo... mas é água. Que alívio.");

        var bucketGo = Point("Balde", t, pos + new Vector3(0.4f, 0.25f, -2.1f), new Vector3(0.6f, 0.6f, 0.6f));
        Cyl("Balde", bucketGo.transform, bucketGo.transform.position, 0.25f, 0.45f, iron, collider: false);
        var bucket = bucketGo.AddComponent<Torneira>();
        SetE(bucket, "uso", 1); // Lavar
        SetF(bucket, "quantidade", 45f);
        SetS(bucket, "flagAoUsar", "prologo.lavou");
        SetS(bucket, "falaSemAgua", "Vazio. Se a caixa-d'água funcionar, dá pra encher.");
        SetS(bucket, "falaPrimeiraVez", "Faz quanto tempo que eu não lavo esse rosto...");
    }

    // ============================================================ Galpão

    private static void CoalShed()
    {
        var s = Group("Galpao de Carvao");
        var o = new Vector3(18f, 0f, -12.5f);
        var wood = Mat("Tabuas", new Color(0.27f, 0.2f, 0.14f), 0.06f);
        Room(s, o, new Vector3(7f, 3.4f, 5f), wood, openFront: true, door: false);

        // Monte de carvão: um objeto só; cada E dá uma pedra (uma por vez, ela fica na mão)
        var coalMat = Mat("Carvao", new Color(0.05f, 0.05f, 0.055f), 0.35f);
        var monte = new GameObject("Carvão").transform;
        monte.SetParent(s, false);
        monte.position = o + new Vector3(-1.4f, 0f, -0.6f);
        for (int i = 0; i < 9; i++)
        {
            var lump = Box("Monte", monte, o + new Vector3(R(-2.5f, -0.5f), R(0.1f, 0.5f), R(-1.6f, 0.4f)), Vector3.one * R(0.5f, 1.1f), coalMat, collider: false);
            lump.transform.rotation = Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360));
        }
        var mc = monte.gameObject.AddComponent<MonteDeCarvao>();
        SetS(mc, "acao", "PEGAR CARVÃO");
        ColisorDoMonte(monte);

        // bancada com a chave inglesa e comida
        var bench = Box("Bancada", s, o + new Vector3(2.2f, 0.45f, -1.5f), new Vector3(2.2f, 0.9f, 0.8f), wood);
        var wrench = Point("Chave Inglesa", s, o + new Vector3(2.0f, 0.97f, -1.5f), new Vector3(0.5f, 0.2f, 0.3f));
        Box("Ferramenta", wrench.transform, wrench.transform.position, new Vector3(0.38f, 0.04f, 0.08f), Mat("Aço Velho", new Color(0.45f, 0.45f, 0.47f), 0.6f, 0.9f), collider: false);
        Item(wrench, "chave_inglesa", 1, "prologo.item.chave_inglesa", "PEGAR CHAVE INGLESA", "Uma chave inglesa. Isso solta muita coisa emperrada.");

        var can = Point("Lata de Feijao", s, o + new Vector3(2.7f, 1.0f, -1.4f), new Vector3(0.3f, 0.3f, 0.3f));
        if (Modelo(SopaFbx, null, can.transform, can.transform.position, o.y + 0.9f, 0.14f, 200f) == null)
            Cyl("Lata", can.transform, can.transform.position, 0.07f, 0.16f, Mat("Lata", new Color(0.55f, 0.35f, 0.25f), 0.5f, 0.6f), collider: false);
        Item(can, "lata_feijao", 1, "prologo.item.feijao1", "PEGAR LATA DE FEIJÃO", "Feijão em lata. Ainda tá fechada. [F] pra comer.");

        Paper(s, o + new Vector3(0.8f, 0.03f, 1.6f), 4);

        // lampião velho pendurado (ainda com um resto de querosene)
        var lampPos = o + new Vector3(0.3f, 2.7f, -0.5f);
        var lamp = Modelo(SurvivalFbx, "Lantern", s, lampPos, lampPos.y - 0.2f, 0.4f, 30f)
            ?? Box("Lampiao", s, lampPos, new Vector3(0.18f, 0.28f, 0.18f), Emissive("Vidro Lampiao", new Color(1f, 0.62f, 0.3f), 2.5f), collider: false);
        var ll = new GameObject("Luz do Lampiao").AddComponent<Light>();
        ll.transform.SetParent(lamp.transform, false);
        ll.type = LightType.Point; ll.color = new Color(1f, 0.6f, 0.3f); ll.intensity = 3f; ll.range = 7f; ll.shadows = LightShadows.Soft;
        var lf = ll.gameObject.AddComponent<LightFlicker>();
        SetF(lf, "baseIntensity", 3f); SetF(lf, "flickerAmount", 0.25f); SetF(lf, "flickerSpeed", 5f); SetB(lf, "igniteOnStart", false);
        Sign(s, "GALPÃO DE CARVÃO", o + new Vector3(0f, 3.0f, 2.56f), 0f, 2.6f);
    }

    // =========================================================== Escritório

    private static void Office()
    {
        var s = Group("Escritorio da Estacao");
        var o = new Vector3(8f, PlatH, PlatBack - 1.8f);
        var wall = Mat("Reboco Velho", new Color(0.55f, 0.5f, 0.43f), 0.05f);
        var room = Room(s, o, new Vector3(4.4f, 3f, 3.6f), wall, openFront: false, door: true);
        Static(Box("Fundacao", s, new Vector3(o.x, PlatH / 2f - 0.05f, o.z), new Vector3(4.6f, PlatH - 0.1f, 3.8f), Mat("Concreto Sujo", new Color(0.36f, 0.35f, 0.33f), 0.1f)));

        // porta trancada
        var hinge = new GameObject("Dobradiça").transform;
        hinge.SetParent(room, false);
        hinge.position = o + new Vector3(-0.55f, 0f, 1.8f);
        var leaf = Box("Folha da Porta", hinge, hinge.position + new Vector3(0.55f, 1.05f, 0f), new Vector3(1.1f, 2.1f, 0.08f), Mat("Porta Verde", new Color(0.18f, 0.28f, 0.22f), 0.2f));
        var door = leaf.AddComponent<PortaInteragivel>();
        SetO(door, "folha", hinge);
        SetF(door, "angulo", 105f);
        SetS(door, "chaveItemId", "chave_escritorio");
        SetS(door, "flagAberta", "prologo.escritorio_aberto");
        SetS(door, "falaTrancada", "Trancada. \"ESCRITÓRIO DA ESTAÇÃO\"... a chave deve estar por aí.");
        SetS(door, "flagTentouTrancada", "prologo.escritorio_trancado");
        Sign(room, "ESCRITÓRIO", o + new Vector3(0f, 2.55f, 1.87f), 0f, 1.5f);

        // dentro
        var deskMat = Mat("Mesa", new Color(0.32f, 0.22f, 0.14f), 0.15f);
        Box("Mesa", room, o + new Vector3(0.8f, 0.4f, -0.9f), new Vector3(1.8f, 0.8f, 0.9f), deskMat);
        var lever = Point("Alavanca do Regulador", room, o + new Vector3(0.5f, 0.86f, -0.9f), new Vector3(0.7f, 0.2f, 0.3f));
        Box("Haste", lever.transform, lever.transform.position, new Vector3(0.6f, 0.06f, 0.06f), Mat("Latão", new Color(0.65f, 0.5f, 0.25f), 0.6f, 0.9f), collider: false);
        Item(lever, "alavanca", 1, "prologo.item.alavanca", "PEGAR ALAVANCA", "A alavanca do regulador! Alguém guardou aqui.");

        var match = Point("Fosforos", room, o + new Vector3(1.3f, 0.84f, -0.7f), new Vector3(0.25f, 0.15f, 0.25f));
        if (Modelo(SurvivalFbx, "match", match.transform, match.transform.position, o.y + 0.8f, 0.15f, 25f) == null)
            Box("Caixa de Fosforo", match.transform, match.transform.position, new Vector3(0.12f, 0.04f, 0.07f), Mat("Fosforo", new Color(0.7f, 0.2f, 0.12f), 0.2f), collider: false);
        Item(match, "fosforos", 1, "prologo.item.fosforos", "PEGAR FÓSFOROS", "Fósforos. Tomara que ainda acendam.");

        var soda = Point("Soda", room, o + new Vector3(-1.4f, 0.12f, -1.2f), new Vector3(0.25f, 0.3f, 0.25f));
        if (Modelo(SurvivalFbx, "Can_Soda", soda.transform, soda.transform.position, o.y, 0.13f, 140f) == null)
            Cyl("Garrafa", soda.transform, soda.transform.position, 0.05f, 0.24f, Mat("Vidro Verde", new Color(0.2f, 0.4f, 0.25f), 0.8f), collider: false);
        Item(soda, "soda", 1, "prologo.item.soda", "PEGAR SODA", "Uma soda! [G] pra beber.");

        var cal = Point("Calendario", room, o + new Vector3(-1.2f, 1.7f, -1.74f), new Vector3(0.5f, 0.6f, 0.15f));
        Box("Folha", cal.transform, cal.transform.position, new Vector3(0.4f, 0.5f, 0.02f), Mat("Papel", new Color(0.82f, 0.78f, 0.68f), 0.05f), collider: false);
        var com = cal.AddComponent<Comentario>();
        SetS(com, "acao", "OLHAR O CALENDÁRIO");
        SetS(com, "fala", "Novembro de 2020... e ninguém virou a folha desde então. Quanto tempo se passou?");

        Paper(room, o + new Vector3(-0.5f, 0.03f, 0.2f), 5);

        var luz = new GameObject("Luz pela Janela").AddComponent<Light>();
        luz.transform.SetParent(room, false);
        luz.transform.position = o + new Vector3(0f, 2.4f, 0f);
        luz.type = LightType.Point; luz.intensity = 2.2f; luz.range = 5f; luz.color = new Color(0.85f, 0.85f, 0.9f);
    }

    private static void Outhouse()
    {
        var s = Group("Banheiro");
        var o = new Vector3(-26f, 0f, -11f);
        var wood = Mat("Tabuas", new Color(0.27f, 0.2f, 0.14f), 0.06f);
        Room(s, o, new Vector3(1.6f, 2.5f, 1.6f), wood, openFront: false, door: true);
        var g = Point("Usar Banheiro", s, o + new Vector3(0f, 1.1f, 0.85f), new Vector3(0.9f, 2f, 0.2f));
        g.AddComponent<Banheiro>();
        Box("Porta Velha", s, o + new Vector3(0.62f, 1.05f, 1.2f), new Vector3(0.06f, 2f, 0.85f), wood, collider: false).transform.rotation = Quaternion.Euler(0f, -40f, 0f);
    }

    private static void BenchAndPapers()
    {
        var g = Group("Banco e Jornais");
        var wood = Mat("Madeira Banco", new Color(0.33f, 0.23f, 0.15f), 0.12f);
        var iron = Mat("Ferro Velho", new Color(0.2f, 0.17f, 0.15f), 0.3f, 0.6f);
        var b = new Vector3(-12f, PlatH, PlatBack + 0.7f);
        Box("Assento", g, b + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.06f, 0.45f), wood);
        Box("Encosto", g, b + new Vector3(0f, 0.8f, -0.2f), new Vector3(1.8f, 0.4f, 0.05f), wood, collider: false);
        foreach (float sx in new[] { -0.75f, 0.75f }) Box("Pe", g, b + new Vector3(sx, 0.22f, 0f), new Vector3(0.06f, 0.45f, 0.42f), iron, collider: false);

        var key = Point("Chave do Escritorio", g, b + new Vector3(0.45f, 0.5f, 0.05f), new Vector3(0.25f, 0.12f, 0.25f));
        Box("Chave", key.transform, key.transform.position, new Vector3(0.1f, 0.015f, 0.03f), Mat("Latão", new Color(0.65f, 0.5f, 0.25f), 0.6f, 0.9f), collider: false);
        Item(key, "chave_escritorio", 1, "prologo.item.chave_escritorio", "PEGAR CHAVE", "Uma chave com uma etiqueta: \"ESCRITÓRIO\".");

        Paper(g, b + new Vector3(-0.4f, 0.5f, 0.05f), 1);
        Paper(g, b + new Vector3(1.6f, 0.03f, 1.1f), 2);
        Paper(g, new Vector3(-6f, 0.05f, 4.2f), 3); // perto do vagão tombado, do outro lado do trilho

        var bottle = Point("Garrafa d'Agua", g, b + new Vector3(-0.7f, 0.12f, 0.6f), new Vector3(0.25f, 0.35f, 0.25f));
        if (Modelo(SurvivalFbx, "Water", bottle.transform, bottle.transform.position, PlatH, 0.32f, 60f) == null)
            Cyl("Garrafa", bottle.transform, bottle.transform.position, 0.06f, 0.28f, Mat("Plastico", new Color(0.55f, 0.65f, 0.75f), 0.7f), collider: false);
        Item(bottle, "agua", 1, "prologo.item.agua1", "PEGAR GARRAFA", "Meia garrafa d'água... já é alguma coisa. [G] pra beber.");
    }

    // ============================================================= Jornais

    private static readonly string[][] Jornais =
    {
        new[] { "Folha da Estação", "Terça-feira, 14 de abril de 2020", "Doença misteriosa deixa o dedo mínimo roxo",
            "Médicos do hospital regional registraram o terceiro caso de uma doença ainda sem nome. Os pacientes chegam com o dedo mínimo arroxeado e tremores nas mãos.\n\nA Secretaria de Saúde pede calma: \"Não há motivo para pânico\", disse o secretário. Os casos estão sendo investigados.",
            "Dedo mínimo roxo... por que isso me parece familiar?" },
        new[] { "Folha da Estação", "Sábado, 2 de maio de 2020", "\"Vírus Violeta\": casos dobram em uma semana",
            "Batizada de Violeta por causa da cor que deixa nas mãos, a doença já soma centenas de casos no estado. Hospitais estão lotados e cirurgias foram suspensas.\n\nCientistas confirmam: o vírus só atinge seres humanos. Os animais não adoecem.",
            "Violeta. Era assim que todo mundo chamava..." },
        new[] { "Correio Nacional", "Domingo, 21 de junho de 2020", "Emergência mundial: fronteiras fechadas",
            "A Organização Mundial da Saúde declarou emergência global. Governos fecham fronteiras e aeroportos. A ferrovia passa a transportar apenas mantimentos e militares.\n\nEm várias capitais, multidões cercam os mercados.",
            "Os trens de mantimentos... eu via eles passando por aqui." },
        new[] { "Correio Nacional", "Domingo, 30 de agosto de 2020", "Governo confirma bombardeio de cidades infectadas",
            "Em pronunciamento, o governo admitiu que cidades com alto índice de contaminação foram bombardeadas \"para conter o avanço do Violeta\". A população foi orientada a seguir para os pontos de evacuação.\n\nNão há informações sobre sobreviventes.",
            "Bombardearam as cidades... com as pessoas dentro." },
        new[] { "Folha da Estação", "Quarta-feira, 11 de novembro de 2020 — ÚLTIMA EDIÇÃO", "A ferrovia para. Que Deus nos ajude.",
            "Esta é a última edição. Não há mais papel, nem tinta, nem quem entregue. A linha férrea está oficialmente suspensa.\n\nAos que ficarem: há rumores de um grupo reunindo sobreviventes no fim da linha. Se for verdade, sigam os trilhos.",
            "\"Sigam os trilhos\"... é exatamente o que eu vou fazer." },
    };

    private static void Paper(Transform parent, Vector3 pos, int numero)
    {
        var j = Jornais[numero - 1];
        var go = Point($"Jornal {numero}", parent, pos + new Vector3(0f, 0.04f, 0f), new Vector3(0.55f, 0.15f, 0.45f));
        var sheet = Box("Folha", go.transform, pos + new Vector3(0f, 0.012f, 0f), new Vector3(0.48f, 0.012f, 0.36f), Mat("Jornal", new Color(0.78f, 0.75f, 0.66f), 0.05f), collider: false);
        sheet.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), R(-3f, 3f));
        var c = go.AddComponent<Jornal>();
        SetS(c, "acao", "LER JORNAL");
        SetS(c, "nomeJornal", j[0]);
        SetS(c, "data", j[1]);
        SetS(c, "manchete", j[2]);
        SetS(c, "corpo", j[3]);
        SetS(c, "falaDepois", j[4]);
        SetS(c, "flag", "prologo.jornal" + numero);
    }

    // ========================================================== Decoração

    private static void Dressing()
    {
        var d = Group("Abandono");
        var crate = Mat("Caixote Podre", new Color(0.3f, 0.23f, 0.15f), 0.05f);
        var rust = Mat("Barril Enferrujado", new Color(0.35f, 0.18f, 0.1f), 0.3f, 0.5f);
        var leather = Mat("Mala", new Color(0.25f, 0.16f, 0.1f), 0.2f);

        for (int i = 0; i < 14; i++)
        {
            float x = R(PlatFrom + 2f, PlatTo - 2f);
            if (Mathf.Abs(x - 8f) < 3f || Mathf.Abs(x + 12f) < 2.5f) continue; // escritório e banco livres
            float z = R(PlatBack + 0.5f, PlatBack + 1.6f);
            float r = (float)rng.NextDouble();
            if (r < 0.45f) Box("Caixote", d, new Vector3(x, PlatH + 0.3f, z), Vector3.one * R(0.5f, 0.8f), crate).transform.rotation = Quaternion.Euler(0f, R(-30f, 30f), 0f);
            else if (r < 0.75f) Cyl("Barril", d, new Vector3(x, PlatH + 0.45f, z), 0.32f, 0.9f, rust);
            else Box("Mala", d, new Vector3(x, PlatH + 0.15f, z - 0.4f), new Vector3(0.6f, 0.3f, 0.4f), leather).transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        }

        // um barril tombado e caixotes no trilho
        Cyl("Barril Tombado", d, new Vector3(-4f, 0.35f, 1.3f), 0.32f, 0.9f, rust).transform.rotation = Quaternion.Euler(0f, 30f, 90f);

        // papéis soltos (só enfeite)
        var paperMat = Mat("Papel Sujo", new Color(0.7f, 0.68f, 0.6f), 0.05f);
        for (int i = 0; i < 40; i++)
        {
            bool plat = rng.NextDouble() < 0.6;
            var p = new Vector3(R(PlatFrom, PlatTo), plat ? PlatH + 0.01f : 0.02f, plat ? R(PlatBack + 0.3f, -PlatGap - 0.2f) : R(-14f, 10f));
            var q = Box("Papel", d, p, new Vector3(R(0.15f, 0.3f), 0.005f, R(0.2f, 0.35f)), paperMat, collider: false);
            q.transform.rotation = Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f));
        }

        // folhas secas voando
        var leaves = new GameObject("Folhas ao Vento");
        leaves.transform.SetParent(d);
        leaves.transform.position = new Vector3(0f, 3f, -3f);
        var ps = leaves.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 9f; main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.32f, 0.15f), new Color(0.3f, 0.25f, 0.12f));
        main.gravityModifier = 0.02f; main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 14f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60f, 4f, 20f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); vel.y = new ParticleSystem.MinMaxCurve(-0.3f, -0.1f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("Folha", new Color(0.4f, 0.3f, 0.15f), 0f);
    }

    private static void Nature()
    {
        var n = Group("Natureza");
        var trees = Trees.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).Where(a => a != null).ToArray();
        var bushes = Bushes.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).Where(a => a != null).ToArray();
        int nt = 0, nb = 0;

        for (int i = 0; i < 110 && trees.Length > 0; i++)
        {
            Vector3 p;
            int guard = 0;
            do { p = rng.NextDouble() < 0.55 ? new Vector3(R(-90f, 120f), 0f, R(11f, 70f)) : new Vector3(R(-90f, 90f), 0f, R(-70f, -18f)); }
            while (++guard < 20 && Blocked(p, 7f));
            var t = Place(trees[rng.Next(trees.Length)], p, R(7f, 13f), n);
            if (t != null) { Static(t); nt++; }
        }

        for (int i = 0; i < 220 && bushes.Length > 0; i++)
        {
            float r = (float)rng.NextDouble();
            var p = r < 0.35f ? new Vector3(R(-60f, 90f), 0f, R(-1.3f, 1.3f) + (rng.NextDouble() < 0.5 ? 0f : 2.6f)) // mato no trilho
                  : r < 0.7f ? new Vector3(R(-60f, 90f), 0f, R(2f, 12f))
                  : new Vector3(R(-50f, 50f), 0f, R(-30f, PlatBack - 0.5f));
            if (Blocked(p, 1.5f)) continue;
            var b = Place(bushes[rng.Next(bushes.Length)], p, R(0.3f, 1.2f), n);
            if (b != null) { Static(b); nb++; }
        }
        log.AppendLine($"natureza: {nt} árvores, {nb} arbustos/mato");
    }

    private static bool Blocked(Vector3 p, float margin)
    {
        var areas = new[]
        {
            new Rect(18f - 4.5f, -12.5f - 3.5f, 9f, 7f),   // galpão
            new Rect(22f - 4f, 5.5f - 4f, 8f, 8f),         // caixa-d'água
            new Rect(-6f - 6f, -15.5f - 6f, 12f, 12f),     // casa
            new Rect(-26f - 2f, -11f - 2f, 4f, 4f),        // banheiro
            new Rect(-16f - 6f, 7.5f - 4f, 12f, 8f),       // vagão tombado
            new Rect(8f - 3f, -8f - 3f, 6f, 6f),         // escritório
        };
        foreach (var a in areas)
        {
            var big = new Rect(a.x - margin, a.y - margin, a.width + margin * 2, a.height + margin * 2);
            if (big.Contains(new Vector2(p.x, p.z))) return true;
        }
        return false;
    }

    // ============================================================ Sistemas

    private static void Systems(Dictionary<string, GameObject> copies, Bounds loco)
    {
        // Jogador (copiado da CenaPrincipal), deitado na plataforma
        GameObject player = null;
        if (copies.TryGetValue("PlayerCapsule", out player))
        {
            player.transform.position = new Vector3(-20f, PlatH + 0.05f, -3.6f);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // O detector de chão padrão (raio 0,5 a 0,36 m abaixo dos pés) deixa o jogador
            // "flutuar" ao sair de degraus. Aqui ele só conta como chão até 8 cm abaixo.
            var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
            if (fpc != null) { SetF(fpc, "GroundedRadius", 0.3f); SetF(fpc, "GroundedOffset", -0.22f); }
            if (player.GetComponent<AderirAoChao>() == null) player.AddComponent<AderirAoChao>();
        }
        else log.AppendLine("AVISO: jogador não encontrado na CenaPrincipal");

        if (copies.TryGetValue("PlayerFollowCamera", out var follow) && player != null)
        {
            var target = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PlayerCameraRoot");
            foreach (var mb in follow.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var so = new SerializedObject(mb);
                var f = so.FindProperty("m_Follow");
                if (f != null) { f.objectReferenceValue = target; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            follow.transform.position = target != null ? target.position : player.transform.position;
        }
        if (copies.TryGetValue("MainCamera 1", out var mainCam) && player != null)
            mainCam.transform.position = player.transform.position + Vector3.up * 1.4f;

        // Prefabs prontos: necessidades/HUD e pausa
        InstanciarPrefab(SistemaJogadorBuilder.PrefabPath);
        InstanciarPrefab(PauseMenuBuilder.PrefabPath);

        // Save
        var gm = new GameObject("GameManager");
        var tracker = gm.AddComponent<PlayTimeTracker>();
        var saver = gm.AddComponent<GameSaveController>();
        saver.player = player != null ? player.transform : null;
        saver.playTimeTracker = tracker;

        // Controlador do prólogo
        var ctrlGo = new GameObject("Prologo");
        controller = ctrlGo.AddComponent<PrologoController>();
        SetO(controller, "fornalha", fornalha);
        var departure = ctrlGo.AddComponent<AudioSource>();
        departure.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/SomDeTrilho.wav");
        departure.loop = true; departure.playOnAwake = false; departure.volume = 0.9f;
        SetO(controller, "somPartida", departure);
        foreach (var r in Object.FindObjectsByType<Regulador>(FindObjectsInactive.Include, FindObjectsSortMode.None)) SetO(r, "controller", controller);

        // Acordar: pálpebras + volume da tontura
        var wake = new GameObject("Acordar", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var wc = wake.GetComponent<Canvas>(); wc.renderMode = RenderMode.ScreenSpaceOverlay; wc.sortingOrder = 80;
        var lid = new GameObject("Palpebras", typeof(RectTransform), typeof(Image));
        var lrt = (RectTransform)lid.transform; lrt.SetParent(wake.transform, false);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        lid.GetComponent<Image>().color = Color.black; lid.GetComponent<Image>().raycastTarget = false;
        var wcg = wake.GetComponent<CanvasGroup>(); wcg.blocksRaycasts = false; wcg.interactable = false; wcg.alpha = 0f;

        var dizzy = Profile("Tontura Profile");
        var dof = Get<DepthOfField>(dizzy); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(0.2f); dof.gaussianEnd.Override(4f); dof.gaussianMaxRadius.Override(1.5f);
        Get<ChromaticAberration>(dizzy).intensity.Override(1f);
        Get<LensDistortion>(dizzy).intensity.Override(-0.35f);
        var dv = Get<Vignette>(dizzy); dv.intensity.Override(0.55f); dv.color.Override(new Color(0.05f, 0.02f, 0.05f));
        var dca = Get<ColorAdjustments>(dizzy); dca.saturation.Override(-50f); dca.postExposure.Override(-0.3f);
        EditorUtility.SetDirty(dizzy);
        var dvol = new GameObject("Volume Tontura").AddComponent<Volume>();
        dvol.isGlobal = true; dvol.priority = 20f; dvol.weight = 0f; dvol.sharedProfile = dizzy;

        var seq = wake.AddComponent<AcordarSequencia>();
        SetO(seq, "olhos", wcg);
        SetO(seq, "volumeTontura", dvol);
    }

    private static GameObject InstanciarPrefab(string path)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p == null) { log.AppendLine("AVISO: prefab não encontrado: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
        go.name = p.name;
        return go;
    }

    /// <summary>
    /// Converte o monte montado à mão na cena (objeto com ItemColetavel de carvão) em MonteDeCarvao,
    /// com colisor. Não mexe em mais nada da cena.
    /// </summary>
    public static void ConverterMonteDeCarvao(StringBuilder output)
    {
        log = output;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int n = 0;
        foreach (var item in Object.FindObjectsByType<ItemColetavel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(item);
            if (so.FindProperty("itemId").stringValue != "carvao") continue;
            var go = item.gameObject;
            log.AppendLine($"convertendo \"{go.name}\" ({go.transform.childCount} pedras filhas)");
            Object.DestroyImmediate(item);
            var mc = go.GetComponent<MonteDeCarvao>() ?? go.AddComponent<MonteDeCarvao>();
            SetS(mc, "acao", "PEGAR CARVÃO");
            ColisorDoMonte(go.transform);
            n++;
        }
        foreach (var mc in Object.FindObjectsByType<MonteDeCarvao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (n == 0) { log.AppendLine($"já era MonteDeCarvao: {mc.name}"); ColisorDoMonte(mc.transform); }
        log.AppendLine($"montes convertidos: {n}");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva: " + ScenePath);
    }

    // Gatilho alinhado ao mundo cobrindo todas as pedras (a mira precisa de um colisor).
    private static void ColisorDoMonte(Transform monte)
    {
        var velho = monte.Find("Colisor do Monte");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var rends = monte.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        var go = new GameObject("Colisor do Monte");
        go.transform.SetParent(monte, false);
        go.transform.position = b.center;
        go.transform.rotation = Quaternion.identity;
        var sc = monte.lossyScale;
        go.transform.localScale = new Vector3(1f / sc.x, 1f / sc.y, 1f / sc.z);
        var bc = go.AddComponent<BoxCollider>();
        bc.size = b.size;
        bc.isTrigger = true;
        log?.AppendLine($"colisor do monte: centro {b.center}, tamanho {b.size}");
    }

    // ============================================================ Prévias

    public static void Capture(StringBuilder output)
    {
        log = output;
        var camGo = new GameObject("Camera Previa");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 62f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
        var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var dizzy = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v.name == "Volume Tontura");
        var lids = Object.FindObjectsByType<AcordarSequencia>(FindObjectsSortMode.None).FirstOrDefault()?.GetComponent<CanvasGroup>();
        try
        {
            void Shot(string file, Vector3 pos, Vector3 look, float tontura = 0f)
            {
                if (dizzy != null) dizzy.weight = tontura;
                camGo.transform.position = pos;
                camGo.transform.LookAt(look);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory("Temp/TheRailway");
                File.WriteAllBytes(Path.Combine("Temp/TheRailway", file), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                log.AppendLine("prévia: " + file);
            }

            var start = new Vector3(-20f, PlatH + 1.6f, -3.6f);
            Shot("prologo_1_acordando.png", start + new Vector3(0f, -1f, 0f), start + new Vector3(10f, -0.4f, 1.5f), 0.9f);
            Shot("prologo_2_inicio.png", start, start + new Vector3(20f, 0f, 3f));
            var forn = Object.FindFirstObjectByType<Fornalha>();
            var fp = forn != null ? forn.transform.position : new Vector3(14f, 1.5f, -1.5f);
            Shot("prologo_3_cabine.png", new Vector3(fp.x - 1.5f, PlatH + 1.45f, -4.4f), fp + new Vector3(0.6f, 0.1f, 0f));
            Shot("prologo_4_caixa_dagua.png", new Vector3(14f, PlatH + 1.7f, -4f), new Vector3(22f, 4f, 5.5f));
            Shot("prologo_5_galpao.png", new Vector3(14f, 1.7f, -7f), new Vector3(18f, 0.8f, -13f));
            Shot("prologo_6_escritorio.png", new Vector3(6f, PlatH + 1.6f, -3.8f), new Vector3(8f, PlatH + 1.2f, -8f));
            Shot("prologo_7_banco.png", new Vector3(-15f, PlatH + 1.6f, -3f), new Vector3(-12f, PlatH + 0.4f, -5.4f));
            Shot("prologo_9_garrafa.png", new Vector3(-12.2f, PlatH + 0.75f, -3.9f), new Vector3(-12.7f, PlatH + 0.12f, -4.9f));
            Shot("prologo_10_galpao_bancada.png", new Vector3(19.6f, 1.45f, -12.6f), new Vector3(20.5f, 0.95f, -13.9f));
            Shot("prologo_11_escritorio_mesa.png", new Vector3(8.2f, PlatH + 1.45f, -7.9f), new Vector3(8.6f, PlatH + 0.6f, -9.6f));
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            Shot("prologo_8_mapa.png", new Vector3(-2f, 70f, -10f), new Vector3(-2f, 0f, -9.99f));
            RenderSettings.fog = fog;
        }
        finally
        {
            if (dizzy != null) dizzy.weight = 0f;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }
    }

    // ============================================================ Ajudantes

    private static Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root);
        return t;
    }

    private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private const string SurvivalFbx = "Assets/Models/Survival/Models/Survival.fbx";
    private const string SopaFbx = "Assets/Packages/PSX - Doomsday Delights - Soup Can/Prefab/Soup Can.fbx";

    /// <summary>
    /// Copia um modelo (ou uma peça de dentro de um .fbx, pelo nome) e o encaixa: maior lado = tamanho,
    /// centro em x/z do ponto e a base apoiada na altura "chao". Sem colisor (quem interage é o Point).
    /// Se o modelo não for achado, devolve null e quem chamou usa o primitivo antigo.
    /// </summary>
    private static GameObject Modelo(string fbx, string peca, Transform parent, Vector3 pos, float chao, float tamanho, float giro = 0f, Vector3? inclinar = null)
    {
        var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (raiz == null) { log?.AppendLine($"AVISO: modelo não achado: {fbx}"); return null; }
        var src = peca == null ? raiz.transform : raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == peca);
        if (src == null) { log?.AppendLine($"AVISO: peça \"{peca}\" não achada em {fbx}"); return null; }

        var go = Object.Instantiate(src.gameObject);
        go.name = "Modelo " + (peca ?? raiz.name);
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(inclinar ?? Vector3.zero) * Quaternion.Euler(0f, giro, 0f) * src.rotation;
        go.transform.localScale = src.lossyScale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // O material embutido do Survival.fbx vem sem textura: usa a textura com o nome da peça
        if (fbx == SurvivalFbx && peca != null)
        {
            var mat = MatTextura("Survival " + peca, $"Assets/Models/Survival/Textures/{peca}.png",
                peca == "Lantern" ? "Assets/Models/Survival/Textures/Lantern_Emissor.jpg" : null);
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        }

        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { Object.DestroyImmediate(go); return null; }
        Bounds Caixa() { var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds); return b; }

        var bb = Caixa();
        float maior = Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z));
        if (maior > 1e-5f) go.transform.localScale *= tamanho / maior;
        bb = Caixa();
        go.transform.position += new Vector3(pos.x - bb.center.x, chao - bb.min.y, pos.z - bb.center.z);
        return go;
    }

    /// <summary>Ponto interativo: objeto vazio com um colisor-gatilho do tamanho dado.</summary>
    private static GameObject Point(string name, Transform parent, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.isTrigger = true;
        return go;
    }

    private static Transform Room(Transform parent, Vector3 origin, Vector3 size, Material m, bool openFront, bool door)
    {
        var r = new GameObject("Sala").transform;
        r.SetParent(parent, true);
        float t = 0.12f;
        Box("Piso", r, origin + new Vector3(0f, -0.05f, 0f), new Vector3(size.x, 0.1f, size.z), m);
        Box("Teto", r, origin + new Vector3(0f, size.y, 0f), new Vector3(size.x + 0.3f, 0.12f, size.z + 0.3f), m);
        Box("Parede Fundo", r, origin + new Vector3(0f, size.y / 2f, -size.z / 2f), new Vector3(size.x, size.y, t), m);
        Box("Parede Esq", r, origin + new Vector3(-size.x / 2f, size.y / 2f, 0f), new Vector3(t, size.y, size.z), m);
        Box("Parede Dir", r, origin + new Vector3(size.x / 2f, size.y / 2f, 0f), new Vector3(t, size.y, size.z), m);
        if (!openFront)
        {
            float gap = door ? 1.15f : 0f;
            float seg = (size.x - gap) / 2f;
            Box("Frente A", r, origin + new Vector3(-size.x / 2f + seg / 2f, size.y / 2f, size.z / 2f), new Vector3(seg, size.y, t), m);
            Box("Frente B", r, origin + new Vector3(size.x / 2f - seg / 2f, size.y / 2f, size.z / 2f), new Vector3(seg, size.y, t), m);
            if (door) Box("Verga", r, origin + new Vector3(0f, size.y - 0.4f, size.z / 2f), new Vector3(gap, 0.8f, t), m);
        }
        foreach (Transform c in r) Static(c.gameObject);
        return r;
    }

    private static void Item(GameObject go, string id, int qty, string flag, string acao, string fala)
    {
        var item = go.AddComponent<ItemColetavel>();
        SetS(item, "itemId", id);
        SetI(item, "quantidade", qty);
        SetS(item, "flag", flag);
        SetS(item, "acao", acao);
        SetS(item, "fala", fala);
    }

    private static GameObject Smoke(Transform parent, Vector3 pos)
    {
        var go = new GameObject("Fumaca da Chamine");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.34f, 0.36f, 0.6f), new Color(0.25f, 0.24f, 0.26f, 0.45f));
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 250;
        var em = ps.emission; em.rateOverTime = 9f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.2f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMaterial();
        go.SetActive(false);
        return go;
    }

    private static GameObject Water(Transform parent, Vector3 pos)
    {
        var go = new GameObject("Jato de Agua");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.startLifetime = 1.1f; main.startSpeed = 4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); main.gravityModifier = 1f;
        main.startColor = new Color(0.7f, 0.8f, 0.9f, 0.6f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600;
        var em = ps.emission; em.rateOverTime = 160f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.12f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMaterial();
        return go;
    }

    private static Material soft;
    private static Material SoftMaterial()
    {
        if (soft != null) return soft;
        soft = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Menu/Menu3D/Cinzas 3D.mat");
        if (soft == null) soft = Mat("Particula", Color.white, 0f);
        return soft;
    }

    private static Material Mat(string name, Color color, float smooth, float metal = 0f)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{Folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    private static Material MatTextura(string name, string texPath, string emissaoPath = null)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { log?.AppendLine($"AVISO: textura não achada: {texPath}"); return null; }
        var m = Mat(name, Color.white, 0.35f);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        var em = emissaoPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(emissaoPath) : null;
        if (em != null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", em);
            m.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.3f) * 2.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Emissive(string name, Color color, float intensity)
    {
        var m = Mat(name, color, 0.2f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    }

    private static Material GroundMaterial()
    {
        var m = Mat("Chao Mato Seco", new Color(0.36f, 0.34f, 0.26f), 0.03f);
        var src = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/grass-with-dirt-patches-material" })
            .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x != null);
        var tex = src != null ? (src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null) ?? src.mainTexture : null;
        if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", new Vector2(70f, 70f)); m.SetColor("_BaseColor", new Color(0.62f, 0.6f, 0.5f)); }
        return m;
    }

    private static VolumeProfile Profile(string name)
    {
        string path = $"{Folder}/{name}.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
        return p;
    }

    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T c)) { c.active = true; return c; }
        c = profile.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    private static GameObject Place(GameObject asset, Vector3 pos, float height, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * go.transform.rotation;
        var b = Visible(go);
        if (b.size.y > 0.001f) go.transform.localScale *= height / b.size.y;
        b = Visible(go);
        go.transform.position += pos - new Vector3(b.center.x, b.min.y, b.center.z);
        go.transform.SetParent(parent, true);
        return go;
    }

    private static Bounds Visible(IEnumerable<Renderer> renderers)
    {
        Bounds? b = null;
        foreach (var r in renderers)
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (b == null) b = r.bounds; else { var x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        return b ?? new Bounds();
    }

    private static Bounds Visible(params GameObject[] gos)
    {
        Bounds? b = null;
        foreach (var go in gos)
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (b == null) b = r.bounds; else { var x = b.Value; x.Encapsulate(r.bounds); b = x; }
            }
        return b ?? new Bounds(gos.Length > 0 ? gos[0].transform.position : Vector3.zero, Vector3.zero);
    }

    private static void AddBoxCollider(GameObject go) => AddBoxCollider(go, Visible(go));

    private static void AddBoxCollider(GameObject go, Bounds b)
    {
        var holder = new GameObject("Colisor");
        holder.transform.SetParent(go.transform, false);
        holder.transform.position = b.center;
        holder.transform.rotation = Quaternion.identity;
        var bc = holder.AddComponent<BoxCollider>();
        var ls = holder.transform.lossyScale;
        bc.size = new Vector3(b.size.x / Mathf.Max(0.0001f, ls.x), b.size.y / Mathf.Max(0.0001f, ls.y), b.size.z / Mathf.Max(0.0001f, ls.z));
    }

    private static T LoadSub<T>(string path, long id) where T : Object
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is T t && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long lid) && lid == id) return t;
        return null;
    }

    private static Dictionary<string, GameObject> CopyFromScene(string path, string[] keepScripts, string[] visualOnly)
    {
        var result = new Dictionary<string, GameObject>();
        var target = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var source = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var all = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            void Copy(string name, bool strip)
            {
                var original = all.FirstOrDefault(t => t.name == name);
                if (original == null) return;
                var copy = Object.Instantiate(original.gameObject);
                copy.name = name;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, target);
                copy.transform.SetPositionAndRotation(original.position, original.rotation);
                copy.transform.localScale = original.lossyScale;
                copy.SetActive(true);
                if (strip) StripToVisual(copy); else StripTrainScripts(copy);
                result[name] = copy;
            }
            if (keepScripts != null) foreach (var n in keepScripts) Copy(n, false);
            if (visualOnly != null) foreach (var n in visualOnly) Copy(n, true);
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);
        }
        return result;
    }

    // Do jogador/câmeras, tira só o que depende do trem/minimapa da CenaPrincipal
    private static void StripTrainScripts(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterials.Any(m => m == null)) r.enabled = false; // cápsula sem material (fica rosa)
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "TrainClick" || n == "TrainCameraView" || n == "TrainInteraction" || n == "MinimapCameraFollow" || n == "BarraVida" || n == "BarraFome")
                Object.DestroyImmediate(mb);
        }
    }

    private static void StripToVisual(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystemRenderer>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.AppendLine("cena adicionada ao Build Settings");
    }

    private static void Static(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    private static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    private static void SetS(Object o, string f, string v) { var so = new SerializedObject(o); var p = so.FindProperty(f); if (p == null) { log.AppendLine($"AVISO: campo {f} não existe em {o.GetType().Name}"); return; } p.stringValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetF(Object o, string f, float v) { var so = new SerializedObject(o); var p = so.FindProperty(f); if (p == null) { log.AppendLine($"AVISO: campo {f} não existe em {o.GetType().Name}"); return; } p.floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetI(Object o, string f, int v) { var so = new SerializedObject(o); var p = so.FindProperty(f); if (p == null) { log.AppendLine($"AVISO: campo {f} não existe em {o.GetType().Name}"); return; } p.intValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetE(Object o, string f, int v) { var so = new SerializedObject(o); var p = so.FindProperty(f); if (p == null) { log.AppendLine($"AVISO: campo {f} não existe em {o.GetType().Name}"); return; } p.enumValueIndex = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetB(Object o, string f, bool v) { var so = new SerializedObject(o); var p = so.FindProperty(f); if (p == null) { log.AppendLine($"AVISO: campo {f} não existe em {o.GetType().Name}"); return; } p.boolValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetO(Object o, string f, Object v) { var so = new SerializedObject(o); var p = so.FindProperty(f); if (p == null) { log.AppendLine($"AVISO: campo {f} não existe em {o.GetType().Name}"); return; } p.objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
}
