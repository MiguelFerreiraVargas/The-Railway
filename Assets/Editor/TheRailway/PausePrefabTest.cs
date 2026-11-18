using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Testa o prefab da pausa "jogado" numa cena vazia (só câmera e luz):
/// sem jogador, sem EventSystem, sem GameSaveController. Resultado em
/// Temp/TheRailway/prefabtest.txt. Depois reabre a cena que estava aberta.
/// </summary>
[InitializeOnLoad]
public static class PausePrefabTest
{
    private const string Key = "TheRailway.PrefabTest";
    private const string Folder = "Temp/TheRailway";

    private static double startTime = -1;
    private static int step;
    private static readonly StringBuilder log = new StringBuilder();
    private static int errors;

    static PausePrefabTest()
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
                File.WriteAllText(Path.Combine(Folder, "prefabtest.txt"), "PARADO: há cena com mudanças não salvas.");
                return;
            }
        }

        SessionState.SetString(Key + ".scene", EditorSceneManager.GetActiveScene().path);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuBuilder.PrefabPath);
        if (prefab == null)
        {
            File.WriteAllText(Path.Combine(Folder, "prefabtest.txt"), "ERRO: prefab não encontrado em " + PauseMenuBuilder.PrefabPath);
            Restore();
            return;
        }

        PrefabUtility.InstantiatePrefab(prefab);
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
            Restore();
        }
    }

    private static void Restore()
    {
        string path = SessionState.GetString(Key + ".scene", "");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single); // descarta a cena vazia do teste
    }

    private static void Begin()
    {
        if (startTime >= 0) return;
        startTime = EditorApplication.timeSinceStartup;
        step = 0;
        errors = 0;
        log.Clear();
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        log.AppendLine("play iniciado numa cena vazia (câmera + luz + prefab da pausa)");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            log.AppendLine($"  {type}: {message}");
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double t = EditorApplication.timeSinceStartup - startTime;
        var pause = PauseMenu.Instance;

        switch (step)
        {
            case 0 when t > 1.5:
                log.AppendLine($"EventSystem na cena: {Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length} (criado pela pausa)");
                if (pause == null) { log.AppendLine("ERRO: PauseMenu.Instance vazio"); Finish(); return; }
                pause.Pause();
                step++;
                break;
            case 1 when t > 2.2:
                log.AppendLine($"pausado={PauseMenu.IsPaused} timeScale={Time.timeScale} cursor={Cursor.lockState} selecionado={EventSystem.current?.currentSelectedGameObject?.name}");
                pause.OpenSave();
                step++;
                break;
            case 2 when t > 2.8:
            {
                pause.SaveToSlot(0); // sem jogador: deve recusar sem criar arquivo
                var msg = GameObject.Find("Mensagem")?.GetComponent<TMPro.TMP_Text>();
                log.AppendLine($"salvar sem jogador na cena: mensagem \"{msg?.text}\"");
                pause.BackToMain();
                step++;
                break;
            }
            case 3 when t > 3.3:
                pause.OpenOptions();
                log.AppendLine($"opções: selecionado={EventSystem.current?.currentSelectedGameObject?.name}");
                pause.BackToMain();
                step++;
                break;
            case 4 when t > 3.8:
                pause.OpenQuit();
                log.AppendLine($"sair para: selecionado={EventSystem.current?.currentSelectedGameObject?.name}");
                pause.BackToMain();
                pause.Resume();
                step++;
                break;
            case 5 when t > 4.6:
                log.AppendLine($"despausado: pausado={PauseMenu.IsPaused} timeScale={Time.timeScale}");
                Finish();
                step++;
                break;
        }
    }

    private static void Finish()
    {
        log.AppendLine(errors == 0 ? "sem erros no console" : $"{errors} erro(s) no console");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "prefabtest.txt"), log.ToString());
        startTime = -1;
        EditorApplication.ExitPlaymode();
    }
}
