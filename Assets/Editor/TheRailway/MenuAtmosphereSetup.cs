using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Aplica a atmosfera noturna do menu do The Railway na cena aberta:
/// luar frio, neblina, postes de sódio piscando, cinzas no ar, som ambiente
/// e um perfil de pós-processamento cinematográfico.
/// Tudo é registrado no Undo e só vai para o disco quando a cena for salva.
/// </summary>
public static class MenuAtmosphereSetup
{
    private const string MenuPath = "The Railway/Menu/Aplicar atmosfera noturna";
    private const string ProfilePath = "Assets/Scenes/Menu/Menu Atmosfera Profile.asset";
    private const string AshMaterialPath = "Assets/Scenes/Menu/Cinzas.mat";
    private const string SkyboxPath = "Assets/AllSkyFree/Cold Night/Cold Night.mat";
    private const string WindClipPath = "Assets/AllSounds/SomDeVento.mp3";
    private const string CricketClipPath = "Assets/AllSounds/somdegrilo.wav";

    // Paleta
    private static readonly Color MoonColor = new Color(0.56f, 0.66f, 0.92f);
    private static readonly Color SodiumColor = new Color(1f, 0.62f, 0.3f);
    private static readonly Color LanternColor = new Color(1f, 0.7f, 0.42f);
    private static readonly Color FogColor = new Color(0.055f, 0.07f, 0.1f);

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();

        if (scene.name != "Menu" &&
            !EditorUtility.DisplayDialog(
                "The Railway",
                $"A cena aberta é \"{scene.name}\", não \"Menu\". Aplicar a atmosfera mesmo assim?",
                "Aplicar",
                "Cancelar"))
        {
            return;
        }

        var report = ApplyAll();

        EditorUtility.DisplayDialog(
            "The Railway",
            "Atmosfera aplicada.\n\n- " + string.Join("\n- ", report) +
            "\n\nDê Play para ver os postes acendendo. Salve a cena (Ctrl+S) para manter, ou use Ctrl+Z para desfazer.",
            "OK");
    }

    /// <summary>Aplica atmosfera e estilo da UI na cena aberta, sem diálogos.</summary>
    public static List<string> ApplyAll()
    {
        var scene = EditorSceneManager.GetActiveScene();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Atmosfera noturna do menu");

        var report = new List<string>();
        T = MenuTuning.Load();
        MenuUiStyle.T = T;

        ApplyRenderSettings(report);
        ApplyLights(report);
        ApplyCamera(report);
        ApplyVolume(report);
        CreateAsh(report);
        CreateAmbientAudio(report);
        MenuUiStyle.Apply(report);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[The Railway] Atmosfera aplicada:\n- " + string.Join("\n- ", report));
        return report;
    }

    // ---------------------------------------------------------------- Ambiente

    private static MenuTuning T = new MenuTuning();

    private static void ApplyRenderSettings(List<string> report)
    {
        // RenderSettings não tem suporte a Undo; a cena só muda no disco ao salvar.
        var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);

        if (skybox != null)
            RenderSettings.skybox = skybox;

        float density = T.fogDensity;
        string fogNote = "";

        if (density < 0f)
        {
            // Calcula a densidade para o trem continuar visível através da neblina.
            float distance = DistanceToTrain();
            float visibility = Mathf.Clamp(T.fogVisibilityAtTrain, 0.05f, 0.99f);
            density = Mathf.Sqrt(-Mathf.Log(visibility)) / Mathf.Max(distance, 1f);
            fogNote = $" (trem a {distance:0}m, densidade {density:0.0000})";
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = T.fogColor;
        RenderSettings.fogDensity = density;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = T.ambientSky * T.ambientMultiplier;
        RenderSettings.ambientEquatorColor = T.ambientEquator * T.ambientMultiplier;
        RenderSettings.ambientGroundColor = T.ambientGround * T.ambientMultiplier;
        RenderSettings.reflectionIntensity = 0.45f;

        report.Add((skybox != null
            ? "Céu \"Cold Night\", neblina azulada e luz ambiente noturna"
            : "Neblina azulada e luz ambiente noturna (céu Cold Night não encontrado)") + fogNote);
    }

    private static float DistanceToTrain()
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

        if (cam == null)
            return 30f;

        float best = 0f;

        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = r.transform.root.name.ToLowerInvariant();

            if (n.Contains("train") || n.Contains("trem") || n.Contains("vagon") || n.Contains("vagao"))
                best = Mathf.Max(best, Vector3.Distance(cam.transform.position, r.bounds.center));
        }

        return best > 0f ? best : 30f;
    }

    private static void ApplyLights(List<string> report)
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int moon = 0;
        var lamps = new List<Light>();

        foreach (var light in lights)
        {
            Undo.RecordObject(light, "Atmosfera noturna do menu");

            switch (light.type)
            {
                case LightType.Directional:
                    if (T.enableMoon && !light.gameObject.activeSelf)
                    {
                        Undo.RecordObject(light.gameObject, "Atmosfera noturna do menu");
                        light.gameObject.SetActive(true);
                    }

                    light.enabled = true;
                    light.color = MoonColor;
                    light.intensity = T.moonIntensity;

                    if (T.moonEuler.x >= 0f)
                        light.transform.eulerAngles = T.moonEuler;

                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.75f;
                    moon++;
                    break;

                case LightType.Spot:
                    light.color = LanternColor;
                    light.intensity = T.spotIntensity;
                    light.range = T.spotRange;
                    light.spotAngle = T.spotAngle;
                    light.innerSpotAngle = light.spotAngle * 0.45f;
                    light.shadows = LightShadows.Soft;
                    lamps.Add(light);
                    break;

                case LightType.Point:
                    light.color = SodiumColor;
                    light.intensity = T.pointIntensity;
                    light.range = T.pointRange;
                    lamps.Add(light);
                    break;
            }
        }

        // Acendem da esquerda para a direita, como se a energia voltasse aos poucos.
        var ordered = lamps.OrderBy(l => l.transform.position.x).ToList();
        int dyingLamp = ordered.Count > 2 ? ordered.Count / 2 : -1;

        for (int i = 0; i < ordered.Count; i++)
        {
            var light = ordered[i];
            var flicker = light.GetComponent<LightFlicker>();

            if (flicker == null)
                flicker = Undo.AddComponent<LightFlicker>(light.gameObject);

            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = light.intensity;
            so.FindProperty("igniteDelay").floatValue = 0.8f + i * 0.35f;

            bool dying = i == dyingLamp;
            so.FindProperty("flickerAmount").floatValue = dying ? 0.3f : 0.1f;
            so.FindProperty("failChancePerSecond").floatValue = dying ? 0.8f : 0.04f;
            so.ApplyModifiedProperties();
        }

        report.Add($"{moon} luz(es) de luar azul, {ordered.Count} postes/lampiões âmbar com oscilação e acendimento em sequência" +
                   (dyingLamp >= 0 ? " (um deles falhando)" : ""));
    }

    private static void ApplyCamera(List<string> report)
    {
        var cam = Camera.main != null
            ? Camera.main
            : Object.FindFirstObjectByType<Camera>();

        if (cam == null)
        {
            report.Add("Nenhuma câmera encontrada");
            return;
        }

        Undo.RecordObject(cam, "Atmosfera noturna do menu");
        cam.allowHDR = true;

        var data = cam.GetUniversalAdditionalCameraData();

        if (data != null)
        {
            Undo.RecordObject(data, "Atmosfera noturna do menu");
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        if (cam.GetComponent<Animator>() != null)
        {
            report.Add("Câmera com HDR e pós-processamento (balanço de câmera não adicionado: ela tem Animator)");
            return;
        }

        if (cam.GetComponent<MenuCameraDrift>() == null)
            Undo.AddComponent<MenuCameraDrift>(cam.gameObject);

        report.Add("Câmera com HDR, pós-processamento, SMAA e balanço sutil com paralaxe do mouse");
    }

    // -------------------------------------------------------- Pós-processamento

    private static void ApplyVolume(List<string> report)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        var tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(T.bloomThreshold);
        bloom.intensity.Override(T.bloomIntensity);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.86f, 0.72f));

        var colors = GetOrAdd<ColorAdjustments>(profile);
        colors.postExposure.Override(T.postExposure);
        colors.contrast.Override(T.contrast);
        colors.saturation.Override(T.saturation);
        colors.colorFilter.Override(new Color(0.93f, 0.96f, 1f));

        // Sombras frias, luzes quentes: o contraste entre o luar e as lâmpadas.
        var split = GetOrAdd<SplitToning>(profile);
        split.shadows.Override(new Color(0.3f, 0.38f, 0.58f));
        split.highlights.Override(new Color(0.95f, 0.72f, 0.46f));
        split.balance.Override(-15f);

        // Um toque violeta nas sombras: a identidade do vírus Violeta.
        var lgg = GetOrAdd<LiftGammaGain>(profile);
        lgg.lift.Override(new Vector4(1f, 0.96f, 1.05f, -0.03f));
        lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
        lgg.gain.Override(new Vector4(1f, 0.99f, 0.97f, 0.02f));

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.color.Override(new Color(0.02f, 0.01f, 0.03f));
        vignette.intensity.Override(T.vignette);
        vignette.smoothness.Override(0.45f);

        var grain = GetOrAdd<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.3f);
        grain.response.Override(0.8f);

        var aberration = GetOrAdd<ChromaticAberration>(profile);
        aberration.intensity.Override(0.12f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var volume = volumes.FirstOrDefault(v => v.isGlobal) ?? volumes.FirstOrDefault();

        if (volume == null)
        {
            var go = new GameObject("Atmosfera Volume");
            Undo.RegisterCreatedObjectUndo(go, "Atmosfera noturna do menu");
            volume = go.AddComponent<Volume>();
        }

        Undo.RecordObject(volume, "Atmosfera noturna do menu");
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.priority = Mathf.Max(volume.priority, 10f);
        volume.sharedProfile = profile;

        report.Add($"Pós-processamento \"{System.IO.Path.GetFileNameWithoutExtension(ProfilePath)}\" no volume \"{volume.name}\" (o perfil antigo não foi alterado)");
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
        {
            component.active = true;
            return component;
        }

        component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    // ------------------------------------------------------------------ Cinzas

    private static void CreateAsh(List<string> report)
    {
        if (GameObject.Find("Cinzas") != null)
        {
            report.Add("Cinzas já existiam na cena (mantidas)");
            return;
        }

        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        Vector3 origin = cam != null
            ? cam.transform.position + cam.transform.forward * 9f + Vector3.up * 5f
            : Vector3.up * 5f;

        var go = new GameObject("Cinzas");
        Undo.RegisterCreatedObjectUndo(go, "Atmosfera noturna do menu");
        go.transform.position = origin;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.7f, 0.68f, 0.55f),
            new Color(0.5f, 0.48f, 0.5f, 0.35f));
        main.gravityModifier = 0.015f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 700;
        main.useUnscaledTime = true;

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 2f, 16f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.15f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); // vento leve
        velocity.y = new ParticleSystem.MinMaxCurve(-0.25f, -0.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetAshMaterial();

        ps.Play();

        report.Add("Cinzas caindo com vento leve (objeto \"Cinzas\")");
    }

    private static Material GetAshMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(AshMaterialPath);

        if (material != null)
            return material;

        var urpParticles = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");

        if (urpParticles != null)
        {
            material = new Material(urpParticles);
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
                return null;

            material = new Material(shader);
        }

        material.name = "Cinzas";
        AssetDatabase.CreateAsset(material, AshMaterialPath);
        return material;
    }

    // ------------------------------------------------------------------- Áudio

    private static void CreateAmbientAudio(List<string> report)
    {
        if (GameObject.Find("Som Ambiente") != null)
        {
            report.Add("Som ambiente já existia (mantido)");
            return;
        }

        var wind = AssetDatabase.LoadAssetAtPath<AudioClip>(WindClipPath);
        var crickets = AssetDatabase.LoadAssetAtPath<AudioClip>(CricketClipPath);

        if (wind == null && crickets == null)
        {
            report.Add("Sons de vento/grilos não encontrados em Assets/AllSounds");
            return;
        }

        var go = new GameObject("Som Ambiente");
        Undo.RegisterCreatedObjectUndo(go, "Atmosfera noturna do menu");

        AddLoop(go, wind, 0.35f);
        AddLoop(go, crickets, 0.12f);

        report.Add("Som ambiente de vento e grilos em loop (objeto \"Som Ambiente\")");
    }

    private static void AddLoop(GameObject go, AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = volume;
        source.spatialBlend = 0f;
    }
}
