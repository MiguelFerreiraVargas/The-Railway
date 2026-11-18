using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Teste automático do menu 3D em Play: tira fotos em momentos da abertura,
/// simula "aperte qualquer botão", abre o quadro Carregar e volta, e registra
/// erros do console. Resultado em Temp/TheRailway/playtest.txt e play_*.png.
/// Disparado pelo comando "playtest" do MenuAutoRunner.
/// </summary>
[InitializeOnLoad]
public static class MenuPlayTest
{
    private const string Key = "TheRailway.PlayTest";
    private const string Folder = "Temp/TheRailway";

    private static double startTime = -1;
    private static int step;
    private static int errorsBeforeGame;
    private static bool deleteSlotExisted;
    private static byte[] deleteSlotBackup;
    private static string DeleteSlotPath => Path.Combine(Application.persistentDataPath, "save_slot_2.json");
    private static readonly List<string> errors = new List<string>();
    private static readonly StringBuilder log = new StringBuilder();

    static MenuPlayTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
            Begin();
    }

    public static void Start()
    {
        // Nunca descarta trabalho do usuário: com cena não salva, não roda.
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var sc = EditorSceneManager.GetSceneAt(i);
            if (sc.isDirty)
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "playtest.txt"),
                    $"PARADO: a cena \"{sc.name}\" tem mudanças não salvas; salve-a e tente de novo.");
                return;
            }
        }

        EditorSceneManager.OpenScene(Menu3DBuilder.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);

        // O Play pausa com o Unity sem foco quando "Run In Background" está desligado.
        // Liga só durante o teste e restaura depois.
        SessionState.SetBool(Key + ".bg", PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;
        File.Delete(Path.Combine(Folder, "playtest.txt"));
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false))
            return;

        if (change == PlayModeStateChange.EnteredPlayMode)
            Begin();

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
        if (startTime >= 0)
            return;

        startTime = EditorApplication.timeSinceStartup;
        step = 0;
        errors.Clear();
        log.Clear();
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        log.AppendLine("play iniciado");
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add($"{type}: {message}\n{stackTrace.Split('\n').FirstOrDefault()}");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;

        double t = EditorApplication.timeSinceStartup - startTime;
        var controller = Object.FindFirstObjectByType<Menu3DController>();

        // (tempo, ação)
        switch (step)
        {
            case 0 when t > 1.0: Shot("play_1_inicio.png", controller); step++; break;
            case 1 when t > 4.0: Shot("play_2_abertura.png", controller); step++; break;
            case 2 when t > 8.5: Shot("play_3_aperte.png", controller); step++; break;
            case 3 when t > 9.0: Call(controller, "EnterMain"); step++; break;
            case 4 when t > 11.0: Shot("play_4_menu.png", controller); step++; break;
            case 5 when t > 11.5:
                // Cria um save temporário no slot 3 para testar apagar (o original é guardado)
                deleteSlotExisted = File.Exists(DeleteSlotPath);
                deleteSlotBackup = deleteSlotExisted ? File.ReadAllBytes(DeleteSlotPath) : null;
                SaveManager.Save(2, new SaveData { saveName = "Teste", playTimeSeconds = 125f });
                Call(controller, "OpenLoad");
                step++;
                break;
            case 6 when t > 14.5: Shot("play_5_carregar.png", controller); step++; break;
            case 7 when t > 14.6:
            {
                // procura um slot vazio de verdade (fora o slot 3, usado no teste de apagar)
                int empty = SaveManager.HasSave(0) ? (SaveManager.HasSave(1) ? -1 : 1) : 0;
                if (empty < 0) { log.AppendLine("slots 1 e 2 têm save: teste do slot vazio pulado"); step++; break; }
                controller?.LoadSlot(empty);
                log.AppendLine($"LoadSlot({empty}) com slot vazio={!SaveManager.HasSave(empty)} -> estado {controller?.CurrentState}");
                step++;
                break;
            }
            case 8 when t > 15.2:
                var msg = GameObject.Find("Mensagem Slot Vazio")?.GetComponent<TMPro.TMP_Text>();
                log.AppendLine(msg != null ? $"mensagem: \"{msg.text}\" visível (alpha {msg.alpha:0.00})" : "mensagem NÃO criada");
                step++;
                break;
            case 9 when t > 15.5:
            {
                var xButton = GameObject.Find("Slot 3")?.transform.Find("Apagar");
                log.AppendLine($"X de apagar no slot 3: {(xButton != null && xButton.gameObject.activeSelf ? "visível" : "não visível")}");
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(GameObject.Find("Slot 3"));
                controller.RequestDelete(2);
                var deleteMsg = GameObject.Find("Mensagem Slot Vazio")?.GetComponent<TMPro.TMP_Text>();
                log.AppendLine($"pedir para apagar: \"{deleteMsg?.text}\" | save ainda existe: {SaveManager.HasSave(2)}");
                step++;
                break;
            }
            case 10 when t > 16.0:
            {
                controller.LoadSlot(2); // Enter no mesmo slot = confirma
                var deleteMsg = GameObject.Find("Mensagem Slot Vazio")?.GetComponent<TMPro.TMP_Text>();
                log.AppendLine($"confirmar: save existe agora: {SaveManager.HasSave(2)} | mensagem \"{deleteMsg?.text}\" | estado {controller.CurrentState}");
                if (deleteSlotExisted) File.WriteAllBytes(DeleteSlotPath, deleteSlotBackup);
                else if (File.Exists(DeleteSlotPath)) File.Delete(DeleteSlotPath);
                log.AppendLine($"slot 3 restaurado ({(deleteSlotExisted ? "tinha save" : "vazio")})");
                step++;
                break;
            }
            case 11 when t > 16.5: Call(controller, "Back"); step++; break;
            case 12 when t > 19.5: Shot("play_6_voltou.png", controller); step++; break;
            case 13 when t > 20.0:
                errorsBeforeGame = errors.Count;
                Call(controller, "Play");
                step++;
                break;
            case 14 when t > 25.5:
                log.AppendLine("cena ativa depois de Jogar: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                if (errors.Count > errorsBeforeGame)
                    log.AppendLine($"(erros vindos da cena do jogo: {errors.Count - errorsBeforeGame})");
                step++;
                break;
            case 15 when t > 26.0: Finish(); step++; break;
        }
    }

    private static void Call(Menu3DController controller, string method)
    {
        if (controller == null) { log.AppendLine("sem Menu3DController na cena"); return; }
        var m = typeof(Menu3DController).GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(controller, null);
        log.AppendLine($"{method}() -> estado {controller.CurrentState}");
    }

    private static void Shot(string file, Menu3DController controller)
    {
        Directory.CreateDirectory(Folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, file));
        string sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name ?? "nada";
        var panels = new StringBuilder();
        foreach (var p in Object.FindObjectsByType<LedBoardPower>(FindObjectsSortMode.None))
        {
            var leds = typeof(LedBoardPower).GetField("leds", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(p) as CanvasGroup;
            panels.Append($" | {p.transform.parent.name}: {(p.IsOn ? "ligado" : "desligado")} (LEDs {(leds != null ? leds.alpha : -1f):0.00})");
        }
        log.AppendLine($"foto {file}: estado {(controller != null ? controller.CurrentState.ToString() : "?")}, selecionado: {sel}{panels}");
    }

    private static void Finish()
    {
        log.AppendLine(errors.Count == 0 ? "sem erros no console" : $"{errors.Count} erro(s) no console:");
        foreach (var e in errors.Distinct().Take(15))
            log.AppendLine("  " + e);

        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "playtest.txt"), log.ToString());

        startTime = -1;
        EditorApplication.ExitPlaymode();
    }
}
