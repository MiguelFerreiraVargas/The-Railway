using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Teste: abre o Menu em Play, aperta Jogar e acompanha a tela de carregamento
/// até a CenaPrincipal ficar pronta. Resultado em Temp/TheRailway/loadtest.txt.
/// </summary>
[InitializeOnLoad]
public static class LoadingPlayTest
{
    private const string Key = "TheRailway.LoadTest";
    private const string Folder = "Temp/TheRailway";

    private static double startTime = -1, loadStart = -1, loadEnd = -1;
    private static int step, errors;
    private static double lastDiag, menuAt = -1;
    private static string lastStatus = "";
    private static readonly StringBuilder log = new StringBuilder();

    static LoadingPlayTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying) Begin();
    }

    public static void Start()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText(Path.Combine(Folder, "loadtest.txt"), "PARADO: há cena com mudanças não salvas.");
                return;
            }
        }

        EditorSceneManager.OpenScene(Menu3DBuilder.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".bg", PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode) Begin();
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            PlayerSettings.runInBackground = SessionState.GetBool(Key + ".bg", false);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
        }
    }

    private static void Begin()
    {
        if (startTime >= 0) return;
        startTime = EditorApplication.timeSinceStartup;
        loadStart = loadEnd = -1; menuAt = -1;
        step = 0; errors = 0; lastStatus = "";
        log.Clear();
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        log.AppendLine("play iniciado no Menu");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            if (errors <= 10) log.AppendLine($"  {type}: {message}");
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        double t = now - startTime;

        // Acompanha a tela de carregamento
        var etapa = GameObject.Find("Tela de Carregamento/Etapa")?.GetComponent<TMPro.TMP_Text>();
        var pct = GameObject.Find("Tela de Carregamento/Porcentagem")?.GetComponent<TMPro.TMP_Text>();
        if (LoadingScreen.IsLoading)
        {
            if (loadStart < 0) { loadStart = now; log.AppendLine($"tela de carregamento apareceu em t={t:0.0}s"); }
            string s = etapa != null ? etapa.text.TrimEnd('.') : "?";
            if (s != lastStatus)
            {
                lastStatus = s;
                log.AppendLine($"  +{now - loadStart:0.0}s  etapa: {s}  ({pct?.text})  timeScale={Time.timeScale}");
            }
        }
        else if (loadStart >= 0 && loadEnd < 0)
        {
            loadEnd = now;
            log.AppendLine($"tela de carregamento terminou após {loadEnd - loadStart:0.0}s");
        }

        // Diagnóstico a cada 2s depois do Jogar
        if (step >= 2 && loadEnd < 0 && t - lastDiag > 2.0)
        {
            lastDiag = t;
            var mc = Object.FindFirstObjectByType<Menu3DController>();
            log.AppendLine($"  [t={t:0}s] cena={SceneManager.GetActiveScene().name} menu={(mc != null ? mc.CurrentState.ToString() : "-")} " +
                           $"IsLoading={LoadingScreen.IsLoading} tela={(Object.FindFirstObjectByType<LoadingScreen>() != null)} timeScale={Time.timeScale} dt={Time.unscaledDeltaTime:0.000}");
        }

        switch (step)
        {
            case 0 when t > 1.0:
            {
                var c = Object.FindFirstObjectByType<Menu3DController>();
                typeof(Menu3DController).GetMethod("EnterMain", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);
                step++;
                break;
            }
            case 1 when t > 3.0:
                Object.FindFirstObjectByType<Menu3DController>()?.Play();
                log.AppendLine("apertou JOGAR");
                step++;
                break;
            case 2 when loadEnd > 0 && now - loadEnd > 1.5:
            {
                var fpc = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
                var spawners = Object.FindObjectsByType<ProceduralVegetationSpawner>(FindObjectsSortMode.None);
                log.AppendLine($"cena: {SceneManager.GetActiveScene().name} | timeScale={Time.timeScale} | jogador ativo={(fpc != null ? fpc.enabled.ToString() : "sem jogador")}");
                foreach (var sp in spawners)
                {
                    int chunks = sp.camadas.Sum(c => c.chunksAtivos.Count);
                    log.AppendLine($"vegetação \"{sp.name}\": pronta={sp.IsLoaded}, chunks gerados={chunks}, por frame agora={sp.maxChunksGeradosPorFrame}");
                }
                log.AppendLine($"Esc liberado (IsLoading={LoadingScreen.IsLoading})");
                log.AppendLine($"mouse no jogo: {Cursor.lockState}, visível={Cursor.visible}");
                step++;
                break;
            }
            case 3 when now - loadEnd > 3.5:
            {
                var pause = PauseMenu.Instance;
                if (pause == null) { log.AppendLine("ERRO: sem pausa na cena"); Finish(); step = 99; break; }
                pause.Pause();
                pause.OpenQuit();
                log.AppendLine("pausou e abriu SAIR PARA; mouse: " + Cursor.lockState);
                step++;
                break;
            }
            case 4 when now - loadEnd > 4.2:
                PauseMenu.Instance?.GoToMainMenu();
                log.AppendLine("apertou VOLTAR AO MENU");
                menuAt = now;
                step++;
                break;
            case 5 when menuAt > 0 && now - menuAt > 3.0:
            {
                var mc = Object.FindFirstObjectByType<Menu3DController>();
                log.AppendLine($"no menu: cena={SceneManager.GetActiveScene().name} estado={(mc != null ? mc.CurrentState.ToString() : "-")} " +
                               $"mouse={Cursor.lockState} visível={Cursor.visible} timeScale={Time.timeScale} pausado={PauseMenu.IsPaused}");
                Finish();
                step++;
                break;
            }
            case 2 when t > 90:
            case 5 when t > 120:
                log.AppendLine("TEMPO ESGOTADO esperando o carregamento");
                Finish();
                step++;
                break;
        }
    }

    private static void Finish()
    {
        log.AppendLine(errors == 0 ? "sem erros no console" : $"{errors} erro(s) no console");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "loadtest.txt"), log.ToString());
        startTime = -1;
        EditorApplication.ExitPlaymode();
    }
}
