using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Teste curto das armas na CenaPrincipal que grava vídeos (GIF) das animações: roda de armas,
/// machado (sacar, golpe, andar), faca, rifle (tiro e recarga) e soco. Comando "armastest";
/// relatório em Temp/TheRailway/armastest.txt e os GIFs em Temp/TheRailway/videos.
/// </summary>
[InitializeOnLoad]
public static class ArmasPlayTest
{
    private const string Key = "TheRailway.ArmasTest";
    private const string Folder = "Temp/TheRailway";
    private static readonly string Videos = Path.Combine(Folder, "videos");
    private static readonly StringBuilder log = new StringBuilder();
    private static double inicio = -1;
    private static int passo, falhas, erros;
    private static float marca;            // Time.time do começo do passo
    private static float marcaReal;        // tempo real do começo do passo
    private static string clipe;           // gravando
    private static int quadro;
    private static float ultimoQuadro;
    private static readonly List<float> tempos = new List<float>();
    private static int feito;              // ações já feitas no passo

    static ArmasPlayTest()
    {
        EditorApplication.playModeStateChanged += Mudou;
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying) Comecar();
    }

    public static void Start()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText(Path.Combine(Folder, "armastest.txt"), "PARADO: há cena com mudanças não salvas.");
                return;
            }
        // limpa os vídeos antigos (o que estiver preso fica, sem parar o teste)
        if (Directory.Exists(Videos))
            foreach (var f in Directory.GetFiles(Videos, "*", SearchOption.AllDirectories)) { try { File.Delete(f); } catch { } }
        Directory.CreateDirectory(Videos);
        EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".bg", PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;
        EditorApplication.EnterPlaymode();
    }

    private static void Mudou(PlayModeStateChange c)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (c == PlayModeStateChange.EnteredPlayMode) Comecar();
        if (c == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            PlayerSettings.runInBackground = SessionState.GetBool(Key + ".bg", false);
            Application.logMessageReceived -= NoLog;
            EditorApplication.update -= Tick;
            inicio = -1;
            FazerGifs();
        }
    }

    private static void Comecar()
    {
        if (inicio >= 0) return;
        inicio = EditorApplication.timeSinceStartup;
        passo = 0; falhas = 0; erros = 0; clipe = null;
        log.Clear();
        SaveGameLoader.selectedSlot = -1;
        Application.logMessageReceived += NoLog;
        EditorApplication.update += Tick;
        log.AppendLine($"play iniciado ({DateTime.Now:HH:mm:ss})");
    }

    private static void NoLog(string msg, string stack, LogType t)
    {
        if (t != LogType.Error && t != LogType.Exception && t != LogType.Assert) return;
        erros++;
        if (erros <= 10) log.AppendLine($"  !! {t}: {msg}");
    }

    private static void Check(bool ok, string oque)
    {
        if (!ok) falhas++;
        log.AppendLine($"  [{(ok ? "OK" : "FALHOU")}] {oque}");
    }

    // ------------------------------------------------------------ gravação

    private static void Gravar(string nome)
    {
        clipe = nome; quadro = 0; tempos.Clear(); ultimoQuadro = -99f;
        Directory.CreateDirectory(Path.Combine(Videos, nome));
    }

    private static void Parar()
    {
        if (clipe == null) return;
        File.WriteAllText(Path.Combine(Videos, clipe, "tempos.txt"), string.Join("\n", tempos.Select(t => t.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))));
        log.AppendLine($"  vídeo: {clipe} ({quadro} quadros)");
        clipe = null;
    }

    private static void GravarQuadro()
    {
        if (clipe == null) return;
        float agora = Time.realtimeSinceStartup;
        if (agora - ultimoQuadro < 1f / 15f) return;
        ultimoQuadro = agora;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Videos, clipe, $"f{quadro:000}.png")));
        tempos.Add(agora);
        quadro++;
    }

    public static void FazerGifs()
    {
        if (!Directory.Exists(Videos)) return;
        var rel = new StringBuilder(File.Exists(Path.Combine(Folder, "armastest.txt")) ? File.ReadAllText(Path.Combine(Folder, "armastest.txt")) : "");
        foreach (var dir in Directory.GetDirectories(Videos).OrderBy(d => d))
        {
            var pngs = Directory.GetFiles(dir, "f*.png").OrderBy(f => f).ToList();
            var ts = File.Exists(Path.Combine(dir, "tempos.txt"))
                ? File.ReadAllLines(Path.Combine(dir, "tempos.txt")).Where(l => l.Length > 0).Select(l => float.Parse(l, System.Globalization.CultureInfo.InvariantCulture)).ToList()
                : new List<float>();
            var atrasos = new List<float>();
            for (int i = 0; i < pngs.Count; i++) atrasos.Add(i + 1 < ts.Count ? ts[i + 1] - ts[i] : 0.5f);
            string gif = dir + ".gif";
            try { GravadorGif.Salvar(gif, pngs, atrasos); rel.AppendLine($"gif: {Path.GetFullPath(gif)} ({pngs.Count} quadros)"); }
            catch (Exception e) { rel.AppendLine($"ERRO no gif {dir}: {e.Message}"); }
        }
        rel.AppendLine("gifs prontos");
        File.WriteAllText(Path.Combine(Folder, "armastest.txt"), rel.ToString());
    }

    // ------------------------------------------------------------ roteiro

    private static float T => Time.time - marca;
    private static float TReal => Time.realtimeSinceStartup - marcaReal;
    private static void Proximo() { passo++; marca = Time.time; marcaReal = Time.realtimeSinceStartup; feito = 0; }
    // faz a ação uma vez, quando o tempo do passo passar de t
    private static bool Em(float t, bool real = false)
    {
        if ((real ? TReal : T) < t) return false;
        feito++;
        return true;
    }

    private static int acoes;
    private static bool Uma(float t, bool real = false)
    {
        // a n-ésima ação do passo só roda uma vez
        int n = ++acoes;
        if (n <= feito) return false;
        if (n != feito + 1) return false;
        return Em(t, real);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - inicio > 240) { log.AppendLine($"TEMPO ESGOTADO no passo {passo}"); Fim(); return; }
        if (passo == 0 && EditorApplication.timeSinceStartup - inicio > 25 && Time.time < 0.5f) { log.AppendLine($"TRAVADO NO COMEÇO: Time.time {Time.time:0.00}"); Fim(); return; }
        GravarQuadro();
        acoes = 0;

        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        var armas = ArmasDoJogador.Instance;
        var roda = RodaDeArmas.Instance;
        if (fpc == null || armas == null) { if (Time.time > 4f) { Check(false, "jogador e armas na cena"); Fim(); } return; }
        var inv = PlayerInventory.Instance;

        switch (passo)
        {
            case 0:
                if (Time.time < 2f) return;
                log.AppendLine("== preparação ==");
                inv.AddItem("mochila", 1);
                foreach (var id in new[] { "lanterna", "faca", "machado", "rifle" }) if (inv.GetQuantity(id) == 0) inv.AddItem(id, 1);
                inv.AddItem("municao", 10); inv.AddItem("pilha", 1);
                if (!LanternaNaMao.NaMaoAgora) LanternaNaMao.Guardar(false);
                Mirar(fpc, fpc.transform.position + fpc.transform.forward * 20f + Vector3.up * 1.0f);
                // só para o vídeo: uma luz fraca junto da câmera (de noite, sem a lanterna, não se vê nada)
                var luz = new GameObject("Luz do teste").AddComponent<Light>();
                luz.type = LightType.Point; luz.range = 3f; luz.intensity = 1.6f; luz.color = new Color(1f, 0.9f, 0.75f);
                luz.transform.SetParent(Camera.main.transform, false); luz.transform.localPosition = new Vector3(0f, 0.3f, 0.1f);
                Check(roda != null, "roda de armas criada");
                Proximo();
                break;

            case 1: // roda: abre, passa por todas, escolhe o machado
                if (Uma(0.3f, true)) { Gravar("1_roda_de_armas"); }
                if (Uma(0.6f, true)) { roda.Abrir(); Check(RodaDeArmas.Aberta && Time.timeScale < 0.5f, $"Tab segurado: roda aberta e tempo lento ({Time.timeScale:0.00})"); }
                if (Uma(1.2f, true)) roda.Escolher(1);
                if (Uma(1.8f, true)) roda.Escolher(2);
                if (Uma(2.4f, true)) roda.Escolher(3);
                if (Uma(3.0f, true)) roda.Escolher(4);
                if (Uma(3.6f, true)) roda.Escolher(0);
                if (Uma(4.2f, true)) roda.Escolher(2);
                if (Uma(5.0f, true))
                {
                    roda.Fechar(true);
                    Check(!RodaDeArmas.Aberta && Mathf.Approximately(Time.timeScale, 1f), "soltou o Tab: roda fecha e o tempo volta");
                    Check(armas.Equipada == "machado" && LanternaNaMao.Guardada, "escolheu o machado: lanterna para a mochila");
                }
                if (Uma(6.4f, true)) { Parar(); Proximo(); }
                break;

            case 2: // machado: parado, dois golpes, andando
                if (Uma(0f)) Gravar("2_machado");
                if (Uma(0.45f)) Detalhe("machado_parado");
                if (Uma(0.5f)) { fpc.staminaAtual = fpc.staminaMax; armas.Atacar(); }
                if (Uma(0.62f)) Detalhe("machado_armado");
                if (Uma(0.82f)) Detalhe("machado_golpe");
                if (Uma(1.6f)) { fpc.staminaAtual = fpc.staminaMax; armas.Atacar(); }
                if (Uma(2.7f)) ModoCamera.MoverTeste = new Vector2(0f, 1f);
                if (Uma(4.2f)) ModoCamera.MoverTeste = null;
                if (Uma(4.6f)) { Parar(); Proximo(); }
                break;

            case 3: // faca: a lanterna volta; duas estocadas
                if (Uma(0f)) { Gravar("3_faca"); armas.Equipar("faca"); Check(LanternaNaMao.NaMaoAgora, "faca: lanterna de volta na mão"); }
                if (Uma(0.8f)) Detalhe("faca_parada");
                if (Uma(0.9f)) { fpc.staminaAtual = fpc.staminaMax; armas.Atacar(); }
                if (Uma(1.6f)) { fpc.staminaAtual = fpc.staminaMax; armas.Atacar(); }
                if (Uma(2.4f)) { Parar(); Proximo(); }
                break;

            case 4: // rifle: saca, atira, recarrega
                if (Uma(0f)) { Gravar("4_rifle"); armas.Equipar("rifle"); }
                if (Uma(0.1f)) typeof(ArmasDoJogador).GetField("fimRecarga", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(armas, Time.time); // pente cheio já
                if (Uma(0.9f)) Detalhe("rifle_parado");
                if (Uma(1.0f)) { int antes = armas.NoPente; armas.Atacar(); Check(armas.NoPente == antes - 1, $"tiro ({antes} → {armas.NoPente})"); }
                if (Uma(2.0f)) { typeof(ArmasDoJogador).GetMethod("Recarregar", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(armas, null); Check(armas.Recarregando, "R recarrega"); }
                if (Uma(3.0f)) Detalhe("rifle_recarga");
                if (Uma(4.6f)) { Check(armas.NoPente == armas.capacidadePente, $"recarregou ({armas.NoPente}/{armas.capacidadePente})"); Parar(); Proximo(); }
                break;

            case 5: // mãos: soco
                if (Uma(0f)) { Gravar("5_maos"); armas.Equipar(""); }
                if (Uma(0.7f)) { fpc.staminaAtual = fpc.staminaMax; armas.Atacar(); }
                if (Uma(1.5f)) { fpc.staminaAtual = fpc.staminaMax; armas.Atacar(); }
                if (Uma(2.4f)) { Parar(); Proximo(); }
                break;

            case 6:
                if (Uma(0.4f)) Fim();
                break;
        }
    }

    // foto de perto das mãos (só a camada dos braços), de frente e de lado
    private static void Detalhe(string nome)
    {
        var rig = Object.FindFirstObjectByType<MaoPrimeiraPessoa>()?.GetComponentsInChildren<Transform>();
        var cm = Camera.main != null ? Camera.main.transform : null;
        if (rig == null || cm == null) return;
        Vector3 e = rig.First(t => t.name == "hand.L").position, d = rig.First(t => t.name == "hand.R").position;
        string arma = ArmasDoJogador.Instance.Equipada;
        Vector3 foco = arma == "faca" ? e : (e + d) * 0.5f;
        float longe = arma == "faca" ? 0.32f : 0.45f + Vector3.Distance(e, d) * 0.6f;
        Directory.CreateDirectory(Path.Combine(Videos, "detalhes"));
        foreach (var (lado, de) in new[] { ("frente", cm.forward * longe + cm.up * 0.05f), ("lado", (cm.right * 0.8f + cm.forward * 0.35f).normalized * longe), ("cima", (cm.up * 0.9f - cm.forward * 0.2f).normalized * longe) })
        {
            var go = new GameObject("Câmera do detalhe");
            var c = go.AddComponent<Camera>();
            c.cullingMask = 1 << LayerMask.NameToLayer(CameraDosBracos.NomeCamada);
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.22f, 0.22f, 0.25f);
            c.fieldOfView = 45f; c.nearClipPlane = 0.01f; c.farClipPlane = 5f;
            go.transform.position = foco + de;
            go.transform.LookAt(foco, cm.up);
            var rt = new RenderTexture(640, 640, 24);
            c.targetTexture = rt; c.Render();
            var antes = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(Path.Combine(Videos, "detalhes", $"{nome}_{lado}.png"), tex.EncodeToPNG());
            c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }

    private static void Mirar(FirstPersonController p, Vector3 ponto)
    {
        var olho = p.CinemachineCameraTarget.transform.position;
        Vector3 d = ponto - olho;
        Vector3 h = new Vector3(d.x, 0f, d.z);
        if (h.sqrMagnitude > 0.0001f) p.transform.rotation = Quaternion.LookRotation(h.normalized);
        float pitch = Mathf.Atan2(-d.y, h.magnitude) * Mathf.Rad2Deg;
        typeof(FirstPersonController).GetField("_cinemachineTargetPitch", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(p, pitch);
        p.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static void Fim()
    {
        Parar();
        ModoCamera.MoverTeste = null;
        if (RodaDeArmas.Aberta) RodaDeArmas.Instance?.Fechar(false);
        log.AppendLine($"== fim: {falhas} falha(s), {erros} erro(s) no console ==");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "armastest.txt"), log.ToString());
        passo = 99;
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }
}
