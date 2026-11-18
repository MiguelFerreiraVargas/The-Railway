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

        foreach (var dir in new[] { "Assets/Editor/TheRailway", "Assets/Scripts/Menu", "Assets/Scripts/Pause" })
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
