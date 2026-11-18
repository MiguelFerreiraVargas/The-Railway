using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Teste automático da pausa na cena Teste save: pausa, confere tempo/jogador/mouse,
/// salva num slot (e depois devolve o arquivo como estava), volta e despausa.
/// Resultado em Temp/TheRailway/pausetest.txt. Disparado pelo comando "pausetest".
/// </summary>
[InitializeOnLoad]
public static class PausePlayTest
{
    private const string Key = "TheRailway.PauseTest";
    private const string Folder = "Temp/TheRailway";
    private const int TestSlot = 2;

    private static double startTime = -1;
    private static int step;
    private static readonly List<string> errors = new List<string>();
    private static readonly StringBuilder log = new StringBuilder();
    private static byte[] slotBackup;
    private static bool slotExisted;

    static PausePlayTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
            Begin();
    }

    public static void Start()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var sc = EditorSceneManager.GetSceneAt(i);
            if (sc.isDirty)
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "pausetest.txt"), $"PARADO: a cena \"{sc.name}\" tem mudanças não salvas.");
                return;
            }
        }

        EditorSceneManager.OpenScene(PauseMenuBuilder.TestScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".bg", PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;
        File.Delete(Path.Combine(Folder, "pausetest.txt"));
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode) Begin();
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            int autoSlot = SessionState.GetInt(Key + ".autoslot", -1);
            if (autoSlot >= 0)
            {
                string path = Path.Combine(Application.persistentDataPath, $"save_slot_{autoSlot}.json");
                bool created = File.Exists(path);
                File.AppendAllText(Path.Combine(Folder, "pausetest.txt"),
                    $"salvamento automático ao fechar: {(created ? "CRIADO" : "NÃO criado")} no slot {autoSlot + 1}" +
                    (created ? " | " + File.ReadAllText(path).Replace("\n", " ").Replace("  ", "") : "") + "\n");
                if (created) File.Delete(path); // remove o arquivo do teste
                File.AppendAllText(Path.Combine(Folder, "pausetest.txt"), "arquivo do teste removido\n");
                SessionState.SetInt(Key + ".autoslot", -1);
            }
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
        step = 0;
        errors.Clear();
        log.Clear();
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        log.AppendLine("play iniciado na cena Teste save");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add($"{type}: {message} | {stack.Split('\n').FirstOrDefault()}");
    }

    private static string SlotPath => Path.Combine(Application.persistentDataPath, $"save_slot_{TestSlot}.json");

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double t = EditorApplication.timeSinceStartup - startTime;
        var pause = PauseMenu.Instance;

        switch (step)
        {
            case 0 when t > 2.0:
                State("antes de pausar");
                if (pause == null) { log.AppendLine("ERRO: PauseMenu não está na cena"); Finish(); return; }
                pause.Pause();
                step++;
                break;
            case 1 when t > 3.2:
                State("pausado");
                pause.OpenSave();
                step++;
                break;
            case 2 when t > 4.0:
                slotExisted = File.Exists(SlotPath);
                slotBackup = slotExisted ? File.ReadAllBytes(SlotPath) : null;
                if (slotExisted) File.Delete(SlotPath);
                pause.SaveToSlot(TestSlot);
                log.AppendLine($"SaveToSlot({TestSlot}) -> arquivo criado: {File.Exists(SlotPath)}");
                if (File.Exists(SlotPath))
                    log.AppendLine("  conteúdo: " + File.ReadAllText(SlotPath).Replace("\n", " ").Replace("  ", ""));
                var msg = GameObject.Find("Mensagem")?.GetComponent<TMPro.TMP_Text>();
                log.AppendLine($"  mensagem: \"{msg?.text}\"");
                step++;
                break;
            case 3 when t > 4.6:
                // devolve o slot como estava antes do teste
                if (slotExisted) File.WriteAllBytes(SlotPath, slotBackup);
                else if (File.Exists(SlotPath)) File.Delete(SlotPath);
                log.AppendLine($"slot {TestSlot + 1} restaurado ao estado anterior ({(slotExisted ? "tinha save" : "vazio")})");
                pause.BackToMain();
                step++;
                break;
            case 4 when t > 5.2:
                State("de volta à lista");
                pause.OpenOptions();
                step++;
                break;
            case 5 when t > 5.8:
            {
                State("opções");
                float before = GameSettings.Volume;
                var volumeLine = Object.FindObjectsByType<OptionLine>(FindObjectsSortMode.None).FirstOrDefault(o => o.name == "Volume");
                var stepMethod = typeof(OptionLine).GetMethod("Step", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                stepMethod?.Invoke(volumeLine, new object[] { -1 });
                float after = GameSettings.Volume;
                string text = volumeLine?.GetComponentInChildren<TMPro.TMP_Text>()?.text;
                log.AppendLine($"volume: {before:0.0} -> {after:0.0} (AudioListener {AudioListener.volume:0.0}) texto \"{text}\"");
                GameSettings.Volume = before; // devolve
                log.AppendLine($"volume restaurado: {GameSettings.Volume:0.0}");
                log.AppendLine("linhas de opção: " + string.Join(", ", Object.FindObjectsByType<OptionLine>(FindObjectsSortMode.None).Select(o => o.GetComponentInChildren<TMPro.TMP_Text>()?.text.Trim())));
                pause.BackToMain();
                step++;
                break;
            }
            case 6 when t > 6.4:
                pause.OpenQuit();
                step++;
                break;
            case 7 when t > 6.9:
                State("sair para");
                pause.BackToMain();
                step++;
                break;
            case 8 when t > 7.4:
                State("de volta à lista");
                log.AppendLine("efeito de LED na pausa: " + (PauseMenu.Instance.GetComponentInChildren<LedBoardPower>(true) != null ? "SIM (errado)" : "não (certo)"));
                pause.Resume();
                step++;
                break;
            case 9 when t > 8.2:
            {
                State("despausado");
                // Prepara o teste do salvamento automático ao fechar (parar o Play = fechar)
                var saver = Object.FindFirstObjectByType<GameSaveController>();
                int expected = -1;
                for (int i = 0; i < 3 && expected < 0; i++) if (!SaveManager.HasSave(i)) expected = i;
                if (saver != null && expected >= 0)
                {
                    GameSaveController.CurrentSlot = -1;
                    saver.autoSaveInEditor = true;
                    SessionState.SetInt(Key + ".autoslot", expected);
                    log.AppendLine($"salvamento automático: esperado no slot {expected + 1} (primeiro vazio) ao parar o Play");
                }
                else
                {
                    SessionState.SetInt(Key + ".autoslot", -1);
                    log.AppendLine("salvamento automático: sem slot vazio, teste pulado para não sobrescrever seus saves");
                }
                step++;
                break;
            }
            case 10 when t > 8.4:
                Finish();
                step++;
                break;
        }
    }

    private static void State(string label)
    {
        var fpc = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        var input = Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
        var power = PauseMenu.Instance != null ? PauseMenu.Instance.GetComponentInChildren<LedBoardPower>(true) : null;
        string sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name ?? "nada";
        log.AppendLine($"[{label}] pausado={PauseMenu.IsPaused} timeScale={Time.timeScale} jogador ativo={(fpc != null ? fpc.enabled.ToString() : "sem jogador")} " +
                       $"olhar com mouse={(input != null ? input.cursorInputForLook.ToString() : "-")} cursor={Cursor.lockState} painel={(power != null && power.IsOn ? "ligado" : "desligado")} selecionado={sel}");
    }

    private static void Finish()
    {
        log.AppendLine(errors.Count == 0 ? "sem erros no console" : $"{errors.Count} erro(s) no console:");
        foreach (var e in errors.Distinct().Take(15)) log.AppendLine("  " + e);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "pausetest.txt"), log.ToString());
        startTime = -1;
        EditorApplication.ExitPlaymode();
    }
}
