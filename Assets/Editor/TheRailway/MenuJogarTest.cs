using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Teste do caminho Menu → JOGAR → Prólogo: no Prólogo o mouse tem que estar travado e escondido.
/// Comando "menutest"; relatório em Temp/TheRailway/menutest.txt.
/// </summary>
[InitializeOnLoad]
public static class MenuJogarTest
{
    private const string Key = "TheRailway.MenuJogarTest";
    private const string Folder = "Temp/TheRailway";
    private static double inicio = -1, marca;
    private static int passo;
    private static readonly StringBuilder log = new StringBuilder();

    static MenuJogarTest()
    {
        EditorApplication.playModeStateChanged += Mudou;
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying) Comecar();
    }

    public static void Start()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Mudou(PlayModeStateChange c)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (c == PlayModeStateChange.EnteredPlayMode) Comecar();
        if (c == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; inicio = -1; }
    }

    private static void Comecar()
    {
        if (inicio >= 0) return;
        inicio = EditorApplication.timeSinceStartup; marca = inicio; passo = 0;
        log.Clear();
        log.AppendLine($"menu iniciado ({DateTime.Now:HH:mm:ss})");
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double agora = EditorApplication.timeSinceStartup;
        if (agora - inicio > 90) { log.AppendLine($"TEMPO ESGOTADO no passo {passo} (cena {SceneManager.GetActiveScene().name})"); Fim(); return; }
        switch (passo)
        {
            case 0 when agora - marca > 6:
            {
                var menu = Menu3DController.Instance != null ? Menu3DController.Instance : UnityEngine.Object.FindFirstObjectByType<Menu3DController>(FindObjectsInactive.Include);
                if (menu == null) { log.AppendLine("  [FALHOU] menu na cena"); Fim(); return; }
                log.AppendLine($"  no menu: mouse {(Cursor.visible ? "visível" : "escondido")}, {Cursor.lockState}");
                typeof(Menu3DController).GetMethod("EnterMain", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(menu, null);
                menu.Play();
                log.AppendLine("  JOGAR");
                marca = agora; passo = 1;
                break;
            }
            case 1 when SceneManager.GetActiveScene().name == "Prologo" && !LoadingScreen.IsLoading:
                marca = agora; passo = 2;
                break;
            case 2 when agora - marca > 3:
            {
                bool ok = !Cursor.visible && Cursor.lockState == CursorLockMode.Locked;
                log.AppendLine($"  [{(ok ? "OK" : "FALHOU")}] no Prólogo o mouse fica escondido e travado (visível {Cursor.visible}, {Cursor.lockState})");
                Fim();
                break;
            }
        }
    }

    private static void Fim()
    {
        log.AppendLine("== fim ==");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "menutest.txt"), log.ToString());
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }
}
