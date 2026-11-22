using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Executa a configuração do menu sem precisar clicar em nada: quando o arquivo
/// Temp/TheRailway/comando.txt aparece, aplica a atmosfera na cena Menu, salva
/// e gera prévias em PNG em Temp/TheRailway/. Usado para iterar no visual.
/// Comandos: "apply" (aplica, salva e gera prévias) ou "preview" (só prévias).
/// </summary>
[InitializeOnLoad]
public static class MenuAutoRunner
{
    private const string Folder = "Temp/TheRailway";
    private const string CommandFile = Folder + "/comando.txt";
    private const string StatusFile = Folder + "/status.txt";
    private const string MenuScenePath = "Assets/Scenes/Menu.unity";

    private static double nextCheck;

    static MenuAutoRunner()
    {
        EditorApplication.update += Poll;
    }

    private static DateTime lastScriptWrite = DateTime.MinValue;

    /// <summary>
    /// Recompila sozinho quando um script do menu muda no disco, sem precisar
    /// trazer o Unity para frente (o Unity só atualiza ao ganhar foco).
    /// </summary>
    private static void RefreshIfScriptsChanged()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        DateTime newest = DateTime.MinValue;

        foreach (var dir in new[] { "Assets/Editor/TheRailway", "Assets/Scripts/Menu", "Assets/Scripts/Pause", "Assets/Scripts/Loading", "Assets/Scripts/Sobrevivencia", "Assets/Scripts/Prologo", "Assets/Scripts/SystemSave Menu" })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTimeUtc(file);
                if (t > newest) newest = t;
            }
        }

        if (lastScriptWrite == DateTime.MinValue)
        {
            lastScriptWrite = newest;
            return;
        }

        if (newest > lastScriptWrite)
        {
            lastScriptWrite = newest;
            AssetDatabase.Refresh();
        }
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck)
            return;

        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        RefreshIfScriptsChanged();

        if (!File.Exists(CommandFile))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        string command = File.ReadAllText(CommandFile).Trim().ToLowerInvariant();

        if (command == "refresh")
        {
            File.Delete(CommandFile);
            AssetDatabase.Refresh();
            return;
        }

        if (command == "playtest")
        {
            File.Delete(CommandFile);
            MenuPlayTest.Start();
            return;
        }

        if (command == "murmurio")
        {
            File.Delete(CommandFile);
            var mlog = new StringBuilder();
            mlog.AppendLine($"comando: murmurio  ({DateTime.Now:HH:mm:ss})");
            try { GravarMurmurio(mlog); mlog.AppendLine("ok"); }
            catch (Exception e) { mlog.AppendLine("ERRO: " + e); Debug.LogException(e); }
            WriteStatus(mlog);
            return;
        }

        if (command == "movepause")
        {
            File.Delete(CommandFile);
            var mlog = new StringBuilder();
            const string oldPath = "Assets/Prefabs/Menu de Pausa.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuBuilder.PrefabPath) == null)
            {
                string error = AssetDatabase.MoveAsset(oldPath, PauseMenuBuilder.PrefabPath);
                mlog.AppendLine(string.IsNullOrEmpty(error) ? "movido para " + PauseMenuBuilder.PrefabPath : "ERRO ao mover: " + error);
            }
            else
            {
                mlog.AppendLine("nada a mover (já está em " + PauseMenuBuilder.PrefabPath + "?) existe: " +
                                (AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuBuilder.PrefabPath) != null));
            }
            mlog.AppendLine("ok");
            WriteStatus(mlog);
            return;
        }

        if (command == "prefabtest")
        {
            File.Delete(CommandFile);
            PausePrefabTest.Start();
            return;
        }

        if (command == "jogador" || command.StartsWith("jogadorinstall"))
        {
            File.Delete(CommandFile);
            var jlog = new StringBuilder();
            jlog.AppendLine($"comando: {command}  ({DateTime.Now:HH:mm:ss})");
            bool sujo = false;
            if (command != "jogador") // criar o prefab não mexe em cenas; instalar sim
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    if (EditorSceneManager.GetSceneAt(i).isDirty) { jlog.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas."); sujo = true; }
            if (!sujo)
            {
                try
                {
                    if (command == "jogador") { SistemaJogadorBuilder.Build(jlog); SistemaJogadorBuilder.Capture(jlog); }
                    else
                    {
                        SistemaJogadorBuilder.InstallInScene(PauseMenuBuilder.MainScenePath, jlog);
                        SistemaJogadorBuilder.InstallInScene(PauseMenuBuilder.TestScenePath, jlog);
                    }
                    jlog.AppendLine("ok");
                }
                catch (Exception e) { jlog.AppendLine("ERRO: " + e); Debug.LogException(e); }
            }
            WriteStatus(jlog);
            return;
        }

        if (command == "arte")
        {
            File.Delete(CommandFile);
            var alog = new StringBuilder();
            try { ArteInspetor.Gerar(alog); alog.AppendLine("ok"); }
            catch (Exception e) { alog.AppendLine("ERRO: " + e); }
            WriteStatus(alog);
            return;
        }

        if (command == "prologotest")
        {
            File.Delete(CommandFile);
            PrologoPlayTest.Start();
            return;
        }

        if (command == "carvaomonte")
        {
            File.Delete(CommandFile);
            var clog = new StringBuilder();
            clog.AppendLine($"comando: carvaomonte  ({DateTime.Now:HH:mm:ss})");
            bool sujo = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { clog.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas."); sujo = true; }
            if (!sujo)
            {
                string voltar = EditorSceneManager.GetActiveScene().path;
                try { PrologoBuilder.ConverterMonteDeCarvao(clog); clog.AppendLine("ok"); }
                catch (Exception e) { clog.AppendLine("ERRO: " + e); Debug.LogException(e); }
                finally
                {
                    if (!string.IsNullOrEmpty(voltar) && voltar != PrologoBuilder.ScenePath && File.Exists(voltar))
                        EditorSceneManager.OpenScene(voltar, OpenSceneMode.Single);
                }
            }
            WriteStatus(clog);
            return;
        }

        if (command == "prologo" || command == "prologocaptura")
        {
            File.Delete(CommandFile);
            var plog = new StringBuilder();
            plog.AppendLine($"comando: {command}  ({DateTime.Now:HH:mm:ss})");
            bool sujo = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { plog.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas."); sujo = true; }
            if (!sujo)
            {
                string voltar = EditorSceneManager.GetActiveScene().path;
                try
                {
                    if (command == "prologo") PrologoBuilder.Build(plog);
                    else EditorSceneManager.OpenScene(PrologoBuilder.ScenePath, OpenSceneMode.Single);
                    PrologoBuilder.Capture(plog);
                    plog.AppendLine("ok");
                }
                catch (Exception e) { plog.AppendLine("ERRO: " + e); Debug.LogException(e); }
                finally
                {
                    if (!string.IsNullOrEmpty(voltar) && voltar != PrologoBuilder.ScenePath && File.Exists(voltar))
                        EditorSceneManager.OpenScene(voltar, OpenSceneMode.Single);
                }
            }
            WriteStatus(plog);
            return;
        }

        if (command == "loading")
        {
            File.Delete(CommandFile);
            var llog = new StringBuilder();
            llog.AppendLine($"comando: loading  ({DateTime.Now:HH:mm:ss})");
            bool dirtyScene = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { llog.AppendLine("PARADO: cena com mudanças não salvas."); dirtyScene = true; }
            if (!dirtyScene)
            {
                try { LoadingScreenBuilder.Build(llog); LoadingScreenBuilder.Capture(llog); llog.AppendLine("ok"); }
                catch (Exception e) { llog.AppendLine("ERRO: " + e); Debug.LogException(e); }
            }
            WriteStatus(llog);
            return;
        }

        if (command == "loadtest")
        {
            File.Delete(CommandFile);
            LoadingPlayTest.Start();
            return;
        }

        if (command == "pausemain")
        {
            File.Delete(CommandFile);
            var ilog = new StringBuilder();
            ilog.AppendLine($"comando: pausemain  ({DateTime.Now:HH:mm:ss})");
            bool dirty = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { ilog.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas."); dirty = true; }
            if (!dirty)
            {
                try { PauseMenuBuilder.InstallPrefabInScene(PauseMenuBuilder.MainScenePath, ilog); ilog.AppendLine("ok"); }
                catch (Exception e) { ilog.AppendLine("ERRO: " + e); Debug.LogException(e); }
            }
            WriteStatus(ilog);
            return;
        }

        if (command == "pausetest:main")
        {
            File.Delete(CommandFile);
            PausePlayTest.Start(PauseMenuBuilder.MainScenePath);
            return;
        }

        if (command == "pausetest")
        {
            File.Delete(CommandFile);
            PausePlayTest.Start();
            return;
        }

        if (command == "pause")
        {
            File.Delete(CommandFile);
            var plog = new StringBuilder();
            plog.AppendLine($"comando: pause  ({DateTime.Now:HH:mm:ss})");
            try
            {
                bool dirty = false;
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    if (EditorSceneManager.GetSceneAt(i).isDirty) { plog.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas."); dirty = true; }
                if (!dirty) { PauseMenuBuilder.BuildForTestScene(plog); plog.AppendLine("ok"); }
            }
            catch (Exception e) { plog.AppendLine("ERRO: " + e); Debug.LogException(e); }
            WriteStatus(plog);
            return;
        }

        File.Delete(CommandFile);

        var log = new StringBuilder();
        log.AppendLine($"comando: {command}  ({DateTime.Now:HH:mm:ss})");

        try
        {
            if (command.Contains("build3d") || command.Contains("preview3d"))
            {
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var sc = EditorSceneManager.GetSceneAt(i);

                    // Cena sem nome deixada por uma construção nossa que falhou: pode descartar.
                    bool ours = string.IsNullOrEmpty(sc.path) && sc.GetRootGameObjects().Any(g => g.name == "CameraRig" || g.name == "Cenario");

                    if (sc.isDirty && !ours)
                    {
                        log.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas; salve-a e tente de novo.");
                        WriteStatus(log);
                        return;
                    }
                }

                if (command.Contains("build3d"))
                    Menu3DBuilder.Build(log);
                else
                    EditorSceneManager.OpenScene(Menu3DBuilder.ScenePath, OpenSceneMode.Single);

                Menu3DBuilder.Capture(log);
                log.AppendLine("ok");
                WriteStatus(log);
                return;
            }

            if (!EnsureMenuSceneOpen(log))
            {
                WriteStatus(log);
                return;
            }

            if (command.Contains("apply"))
            {
                var report = MenuAtmosphereSetup.ApplyAll();
                foreach (var line in report)
                    log.AppendLine("- " + line);

                bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                log.AppendLine(saved ? "cena salva" : "FALHA ao salvar a cena");
            }

            if (command.Contains("info"))
                WriteInfo();

            CapturePreviews(log);
            log.AppendLine("ok");
        }
        catch (Exception e)
        {
            log.AppendLine("ERRO: " + e);
            Debug.LogException(e);
        }

        WriteStatus(log);
    }

    private static bool EnsureMenuSceneOpen(StringBuilder log)
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                log.AppendLine($"PARADO: a cena \"{EditorSceneManager.GetSceneAt(i).name}\" tem mudanças não salvas; salve-a e tente de novo.");
                return false;
            }
        }

        // Sempre recarrega do disco, para nunca trabalhar sobre uma versão velha da cena.
        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        log.AppendLine("cena Menu recarregada do disco");

        // Trava de segurança: a cena do menu precisa ter o trem.
        bool hasTrain = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(t => t.name.Contains("steam-train"));

        if (!hasTrain)
        {
            log.AppendLine("PARADO: a cena Menu não tem o trem (steam-train). Nada foi alterado nem salvo.");
            return false;
        }

        return true;
    }

    /// <summary>Despeja posições e medidas úteis da cena em info.txt.</summary>
    private static void WriteInfo()
    {
        var sb = new StringBuilder();
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();

        if (cam != null)
            sb.AppendLine($"CAMERA {cam.name} pos={cam.transform.position} rot={cam.transform.eulerAngles} fov={cam.fieldOfView} near={cam.nearClipPlane} far={cam.farClipPlane} hasAnimator={cam.GetComponent<Animator>() != null}");

        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.transform.parent != null && r.transform.parent.GetComponent<Renderer>() != null)
                continue;

            string dist = cam != null ? Vector3.Distance(cam.transform.position, r.bounds.center).ToString("0.0") : "?";
            sb.AppendLine($"RENDERER {GetPath(r.transform)} center={r.bounds.center} size={r.bounds.size} dist={dist} type={r.GetType().Name}");
        }

        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string dist = cam != null ? Vector3.Distance(cam.transform.position, l.transform.position).ToString("0.0") : "?";
            sb.AppendLine($"LIGHT {GetPath(l.transform)} type={l.type} pos={l.transform.position} rot={l.transform.eulerAngles} int={l.intensity} range={l.range} angle={l.spotAngle} dist={dist} active={l.isActiveAndEnabled}");
        }

        sb.AppendLine($"FOG on={RenderSettings.fog} mode={RenderSettings.fogMode} density={RenderSettings.fogDensity}");

        var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.transform.Find("ButtonController") != null);

        if (canvas != null)
        {
            foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                var lg = rt.GetComponent<UnityEngine.UI.LayoutGroup>();
                var mask = rt.GetComponent<UnityEngine.UI.RectMask2D>() != null || rt.GetComponent<UnityEngine.UI.Mask>() != null;
                sb.AppendLine($"UI {GetPath(rt)} active={rt.gameObject.activeSelf} aMin={rt.anchorMin} aMax={rt.anchorMax} pivot={rt.pivot} pos={rt.anchoredPosition} size={rt.sizeDelta} rect={rt.rect.size} scale={rt.localScale} layout={(lg != null ? lg.GetType().Name : "-")} mask={mask}");
            }
        }

        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "info.txt"), sb.ToString());
    }

    private static string GetPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // Grava em WAV algumas frases murmuradas, com o mesmo ritmo das legendas, para ouvir fora do jogo.
    private static void GravarMurmurio(StringBuilder log)
    {
        var frases = new[]
        {
            (VozMurmurio.Tipo.Candido, "Onde... onde eu tô?"),
            (VozMurmurio.Tipo.Candido, "Nossa... que dor de cabeça."),
            (VozMurmurio.Tipo.Candido, "Que sede... minha boca tá seca. Preciso achar água."),
            (VozMurmurio.Tipo.Radio, "...aqui é a estação de Vila Serena, alguém na escuta?"),
        };
        const int sr = 44100;
        const float letra = 1f / 30f, volume = 0.32f;
        var mix = new System.Collections.Generic.List<float>();
        foreach (var (tipo, frase) in frases)
        {
            int baseIdx = mix.Count;
            float t = 0f, ultima = -1f;
            float total = 0f;
            for (int i = 0; i < frase.Length; i++) total += letra * PausaDe(frase, i);
            int tamanho = Mathf.CeilToInt((total + 1f) * sr);
            mix.AddRange(new float[tamanho]);
            for (int i = 0; i < frase.Length; i++)
            {
                bool vogal = Vogal(frase[i]);
                if (vogal && (i == 0 || !Vogal(frase[i - 1])) && t - ultima > 0.085f)
                {
                    var clip = VozMurmurio.Silaba(tipo);
                    var d = new float[clip.samples];
                    clip.GetData(d, 0);
                    float pitch = UnityEngine.Random.Range(0.93f, 1.08f), vol = volume * UnityEngine.Random.Range(0.75f, 1f);
                    int ini = baseIdx + Mathf.RoundToInt(t * sr);
                    for (int k = 0; ini + k < mix.Count; k++)
                    {
                        float src = k * pitch;
                        int j = (int)src;
                        if (j + 1 >= d.Length) break;
                        mix[ini + k] += Mathf.Lerp(d[j], d[j + 1], src - j) * vol;
                    }
                    ultima = t;
                }
                t += letra * PausaDe(frase, i);
            }
        }

        float pico = 0f;
        foreach (var v in mix) pico = Mathf.Max(pico, Mathf.Abs(v));
        string caminho = Path.Combine(Folder, "murmurio.wav");
        using (var w = new BinaryWriter(File.Create(caminho)))
        {
            int bytes = mix.Count * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (var v in mix) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
        log.AppendLine($"gravado {caminho}: {mix.Count / (float)sr:0.0}s, pico {pico:0.00}");
    }

    private static bool Vogal(char c) => "aeiouáéíóúâêôãõàAEIOUÁÉÍÓÚÂÊÔÃÕÀ".IndexOf(c) >= 0;

    private static float PausaDe(string s, int i)
    {
        bool fim = i + 1 >= s.Length || s[i + 1] == ' ';
        switch (s[i])
        {
            case ',': case ';': return 6f;
            case '.': return fim ? 10f : 4f;
            case '!': case '?': return 10f;
            case ' ': return 1.4f;
            default: return 1f;
        }
    }

    private static void WriteStatus(StringBuilder log)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(StatusFile, log.ToString());
    }

    // ----------------------------------------------------------------- Prévias

    private static void CapturePreviews(StringBuilder log)
    {
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();

        if (cam == null)
        {
            log.AppendLine("sem câmera para a prévia");
            return;
        }

        var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.transform.Find("ButtonController") != null);

        Transform root = canvas != null ? canvas.transform : null;
        var buttons = root?.Find("ButtonController")?.GetComponent<CanvasGroup>();
        var press = root?.Find("Press")?.gameObject;
        var save = root?.Find("Save")?.gameObject;
        var startButton = root?.Find("ButtonController/Start");
        var titleRect = root?.Find("Ttile") as RectTransform;
        var tuning = MenuTuning.Load();
        Vector2 prevTitlePos = titleRect != null ? titleRect.anchoredPosition : Vector2.zero;

        // Guarda o estado para restaurar depois.
        var prevMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
        var prevCam = canvas != null ? canvas.worldCamera : null;
        float prevPlane = canvas != null ? canvas.planeDistance : 100f;
        float prevButtonsAlpha = buttons != null ? buttons.alpha : 1f;
        bool prevPress = press != null && press.activeSelf;
        bool prevSave = save != null && save.activeSelf;

        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;

        try
        {
            cam.targetTexture = rt;

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.1f, 0.5f);
            }

            // 1) Abertura
            SetUi(buttons, 0f, press, true, save, false);
            Shot(cam, rt, "1_abertura.png", log);

            // 2) Menu com o primeiro botão selecionado
            // (com o título na posição final da animação "Anim title")
            SetUi(buttons, 1f, press, false, save, false);
            if (titleRect != null)
                titleRect.anchoredPosition = new Vector2(prevTitlePos.x, tuning.previewTitleY);
            using (new FakeSelection(startButton))
                Shot(cam, rt, "2_menu.png", log);
            if (titleRect != null)
                titleRect.anchoredPosition = prevTitlePos;

            // 3) Painel de saves
            SetUi(buttons, 0f, press, false, save, true);
            Shot(cam, rt, "3_saves.png", log);

            // 4) Trem passando (meio da animação "Trem anim", x ≈ 0)
            var train = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.parent == null && t.name.Contains("steam-train"));

            if (train != null)
            {
                Vector3 trainPos = train.localPosition;
                try
                {
                    SetUi(buttons, 1f, press, false, save, false);
                    if (titleRect != null)
                        titleRect.anchoredPosition = new Vector2(prevTitlePos.x, tuning.previewTitleY);
                    train.localPosition = new Vector3(2f, trainPos.y, trainPos.z);
                    Shot(cam, rt, "4_trem_passando.png", log);
                }
                finally
                {
                    train.localPosition = trainPos;
                    if (titleRect != null)
                        titleRect.anchoredPosition = prevTitlePos;
                }
            }
        }
        finally
        {
            cam.targetTexture = prevTarget;

            if (canvas != null)
            {
                canvas.renderMode = prevMode;
                canvas.worldCamera = prevCam;
                canvas.planeDistance = prevPlane;
            }

            SetUi(buttons, prevButtonsAlpha, press, prevPress, save, prevSave);

            if (titleRect != null)
                titleRect.anchoredPosition = prevTitlePos;
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    private static void SetUi(CanvasGroup buttons, float alpha, GameObject press, bool pressOn, GameObject save, bool saveOn)
    {
        if (buttons != null) buttons.alpha = alpha;
        if (press != null) press.SetActive(pressOn);
        if (save != null)
        {
            save.SetActive(saveOn);
            var group = save.GetComponent<CanvasGroup>();
            if (group != null && saveOn) group.alpha = 1f;
            if (saveOn) save.transform.localScale = Vector3.one;
        }
    }

    private static void Shot(Camera cam, RenderTexture rt, string fileName, StringBuilder log)
    {
        Canvas.ForceUpdateCanvases();
        cam.Render();

        var previous = RenderTexture.active;
        RenderTexture.active = rt;

        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();

        RenderTexture.active = previous;

        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(Path.Combine(Folder, fileName), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        log.AppendLine("prévia: " + fileName);
    }

    /// <summary>Mostra um botão como selecionado só durante a captura.</summary>
    private sealed class FakeSelection : IDisposable
    {
        private readonly TMP_Text label;
        private readonly CanvasGroup marker;
        private readonly Color labelColor;
        private readonly float spacing;
        private readonly float markerAlpha;

        public FakeSelection(Transform button)
        {
            if (button == null)
                return;

            label = button.GetComponentInChildren<TMP_Text>(true);
            marker = button.Find("Marcador")?.GetComponent<CanvasGroup>();

            if (label != null)
            {
                labelColor = label.color;
                spacing = label.characterSpacing;
                label.color = new Color(1f, 0.74f, 0.38f, 1f);
                label.characterSpacing = spacing + 8f;
            }

            if (marker != null)
            {
                markerAlpha = marker.alpha;
                marker.alpha = 1f;
            }
        }

        public void Dispose()
        {
            if (label != null)
            {
                label.color = labelColor;
                label.characterSpacing = spacing;
            }

            if (marker != null)
                marker.alpha = markerAlpha;
        }
    }
}
