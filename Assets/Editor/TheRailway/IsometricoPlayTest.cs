using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Teste em Play da câmera isométrica na CenaPrincipal: começa na isométrica, anda e corre
/// (movendo o CharacterController), confere a animação do corpo, troca para a primeira pessoa
/// e volta. Gera capturas em Temp/TheRailway/iso_*.png e o relatório isotest.txt.
/// </summary>
[InitializeOnLoad]
public static class IsometricoPlayTest
{
    private const string Key = "TheRailway.IsoTest";
    private const string Folder = "Temp/TheRailway";
    private static readonly StringBuilder log = new StringBuilder();
    private static double inicio = -1;
    private static int passo, falhas, erros;
    private static float marca;
    private static Estrutura casa;
    private static bool portaAbriu;
    private static TrainInteraction trem;
    private static float antesCarvao;
    private static System.Collections.Generic.List<Estrutura> visitar;
    private static int visita;
    private static Animal alvoTeste;
    private static float vidaTeste, sanidadeTeste;
    private static EventoFerrovia eventoTeste;
    private static bool chegou;
    private static void MarcarChegada() => chegou = true;

    static IsometricoPlayTest()
    {
        EditorApplication.playModeStateChanged += Mudou;
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying) Comecar();
    }

    public static void Start()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText(Path.Combine(Folder, "isotest.txt"), "PARADO: há cena com mudanças não salvas.");
                return;
            }
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
        }
    }

    private static void Comecar()
    {
        if (inicio >= 0) return;
        inicio = EditorApplication.timeSinceStartup;
        passo = 0; falhas = 0; erros = 0; portaAbriu = false; casa = null; trem = null;
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

    private static T Campo<T>(object o, string nome)
    {
        var f = o?.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return f != null ? (T)f.GetValue(o) : default;
    }

    // itens no inventário (os que as estruturas dão)
    private static int Contar()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return 0;
        return new[] { "lata_feijao", "agua", "soda", "biscoito", "fosforos", "municao" }.Sum(id => inv.GetQuantity(id));
    }

    private static void Captura(string nome)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var rt = new RenderTexture(1280, 720, 24);
        var antes = cam.targetTexture;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(Folder, nome), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine("  captura: " + nome);
        }
        finally { cam.targetTexture = antes; Object.DestroyImmediate(rt); }
    }

    // anda pelo próprio controle do jogo (como se apertasse WASD), na direção dada em relação à tela
    private static void Andar(Vector2 tela, float velocidade)
    {
        var p = Object.FindFirstObjectByType<FirstPersonController>();
        var inputs = p != null ? p.GetComponent<StarterAssetsInputs>() : null;
        if (inputs != null) inputs.sprint = velocidade > 5f;
        ModoCamera.MoverTeste = tela;
    }

    // anda até um ponto do mundo, em qualquer modo de câmera
    private static float AndarAte(FirstPersonController p, Vector3 alvo)
    {
        Vector3 d = alvo - p.transform.position; d.y = 0f;
        if (d.magnitude < 0.2f) { ModoCamera.MoverTeste = Vector2.zero; return d.magnitude; }
        var inputs = p.GetComponent<StarterAssetsInputs>();
        if (inputs != null) inputs.sprint = false;
        if (ModoCamera.Instance != null && ModoCamera.Instance.Isometrico)
        {
            var cam = Camera.main.transform;
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector3 n = d.normalized;
            ModoCamera.MoverTeste = new Vector2(Vector3.Dot(n, r), Vector3.Dot(n, f)).normalized;
        }
        else
        {
            p.transform.rotation = Quaternion.LookRotation(d.normalized);
            ModoCamera.MoverTeste = new Vector2(0f, 1f);
        }
        return d.magnitude;
    }

    private static void Teleportar(FirstPersonController p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.position = pos;
        cc.enabled = true;
    }

    // primeira pessoa: vira o corpo e a cabeça para olhar um ponto (a mira do centro passa por ele)
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

    // pega uma pedra como o E faria (o monte e depois o aviso que anima os braços)
    private static void PegarCarvao(MonteDeCarvao monte)
    {
        if (monte == null) return;
        monte.Interagir();
        var mao = Object.FindFirstObjectByType<MaoPrimeiraPessoa>();
        typeof(MaoPrimeiraPessoa).GetMethod("AoInteragir", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(mao, new object[] { monte });
    }

    private static bool? AnimMaoDireita()
    {
        var a = Object.FindFirstObjectByType<MaoPrimeiraPessoa>()?.GetComponentInChildren<Animator>();
        return a != null ? a.GetBool("UseRightHand") : (bool?)null;
    }

    private static float DistPedra(string pedra, string osso)
    {
        var p = GameObject.Find(pedra);
        var m = Object.FindFirstObjectByType<MaoPrimeiraPessoa>()?.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == osso);
        var r = p != null ? p.GetComponent<Renderer>() : null;
        return r != null && m != null && p.activeInHierarchy ? Vector3.Distance(r.bounds.center, m.position) : 99f;
    }

    private static float AlfaBarra()
    {
        var b = GameObject.Find("Barra do Carvão");
        var g = b != null ? b.GetComponent<CanvasGroup>() : null;
        return g != null ? g.alpha : -1f;
    }

    private static string Usaria()
    {
        var a = PlayerInteractor.Atual;
        return a != null ? $"{a.name} \"{a.Texto}\"" : "nada";
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - inicio > 1300) { log.AppendLine($"TEMPO ESGOTADO no passo {passo}"); Fim(); return; }
        // travou logo no começo: diz quem está segurando o tempo do jogo
        if (passo == 0 && EditorApplication.timeSinceStartup - inicio > 20)
        {
            log.AppendLine($"TRAVADO NO COMEÇO: Time.time {Time.time:0.00}, timeScale {Time.timeScale}, pausado {EditorApplication.isPaused}, carregando {LoadingScreen.IsLoading}, pausa {PauseMenu.IsPaused}, leitura {Leitura.Aberta}, desmaio {Desmaio.Ativo}, morto {(PlayerStats.Instance != null && PlayerStats.Instance.Morto)}, mochila {TelaMochila.Aberta}, mapa {MapaDoMundo.Aberto}, fpc {(Object.FindFirstObjectByType<FirstPersonController>() != null)}, modo {(ModoCamera.Instance != null)}");
            Fim(); return;
        }

        var modo = ModoCamera.Instance;
        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        var corpo = Object.FindFirstObjectByType<CorpoAnimado>();
        var iso = Object.FindFirstObjectByType<CameraIsometrica>();
        if (modo == null || fpc == null) { if (Time.time > 3f) { Check(false, "ModoCamera e jogador na cena"); Fim(); } return; }

        switch (passo)
        {
            // ---------------------------------------------------------------- lá fora: primeira pessoa
            case 0 when Time.time > 2f:
            {
                log.AppendLine("== mochila ==");
                {
                    var inv = PlayerInventory.Instance;
                    Check(inv != null && !inv.TemMochila && inv.Capacidade <= 4.01f, $"sem mochila: só os bolsos ({(inv != null ? inv.Capacidade : 0):0} kg)");
                    var mo = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Backpack");
                    Check(mo != null, "a mochila do kit dá para pegar");
                    if (mo != null) mo.Interagir();
                    Check(inv != null && inv.TemMochila && inv.Capacidade > 10f, $"pegou a mochila: cabe {(inv != null ? inv.Capacidade : 0):0} kg");
                    // a lanterna é achada: começa sem, pega a da casa do começo
                    Check(!LanternaNaMao.TemLanterna && !LanternaNaMao.MaoDireitaOcupada, "começa sem lanterna (mão direita livre)");
                    var lt = Object.FindObjectsByType<ItemColetavel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Achado: lanterna" && i.GetComponentInParent<Estrutura>() != null && i.GetComponentInParent<Estrutura>().name == "Dom");
                    Check(lt != null, "tem lanterna na casa do começo");
                    if (lt != null) lt.Interagir();
                    Check(LanternaNaMao.TemLanterna, "pegou a lanterna");
                    Check(GameObject.Find("Barra do Carvão") != null, "barra do carvão na tela");
                }
                marca = Time.time; passo = 200; // um instante para a lanterna ir para a mão
                break;
            }
            case 200 when Time.time - marca > 0.6f:
            {
                log.AppendLine("== lá fora: primeira pessoa ==");
                Check(!modo.Isometrico, "começa em primeira pessoa");
                var raiz = fpc.CinemachineCameraTarget.transform;
                float d = Vector3.Distance(raiz.position, Camera.main.transform.position);
                Check(d < 0.3f, $"câmera nos olhos ({d:0.00} m)");
                Check(Cursor.lockState == CursorLockMode.Locked, "mouse travado");
                var mao = Object.FindFirstObjectByType<MaoPrimeiraPessoa>();
                var rm = mao != null ? mao.GetComponentInChildren<Renderer>() : null;
                Check(rm != null && rm.enabled, "mão aparece");
                var lant = Object.FindFirstObjectByType<LanternaNaMao>();
                Check(lant != null && lant.Mao != null && lant.Mao.name == "hand.R", "lanterna segue a mão direita");
                if (lant != null && lant.Mao != null)
                {
                    var cl = Camera.main.transform;
                    var lr = lant.GetComponentInChildren<Renderer>();
                    float dMao = Vector3.Distance(lr.bounds.center, lant.Mao.position);
                    var mfl = lant.GetComponentInChildren<MeshFilter>();
                    Vector3 lente = mfl.transform.right * (lant.lenteNoPositivo ? 1f : -1f);
                    float aponta = Vector3.Dot(lente.normalized, cl.forward);
                    Check(dMao < 0.15f && aponta > 0.8f, $"lanterna no punho ({dMao:0.00} m da mão) apontando para a frente ({aponta:0.00})");
                    var br = Object.FindFirstObjectByType<MaoPrimeiraPessoa>().transform;
                    var esc = br.localScale;
                    Check(Mathf.Abs(esc.x - esc.z) < 0.01f && Mathf.Abs(esc.y - esc.z) < 0.01f, $"braços sem esticar ({esc:F3})");
                    var ombro = br.GetComponentsInChildren<Transform>().First(t => t.name == "upper_arm.R");
                    Check(cl.InverseTransformPoint(ombro.position).z < 0.2f, $"ombro perto dos olhos (z {cl.InverseTransformPoint(ombro.position).z:0.00} m)");
                    var luz = lant.GetComponent<LuzDaLanterna>();
                    Check(luz != null && luz.Luz != null && luz.ligada && LanternaNaMao.TemLanterna && luz.Carga > 0f, "lanterna acesa");
                    if (luz != null && luz.Luz != null)
                    {
                        float para = Vector3.Dot(luz.Luz.transform.forward, cl.forward);
                        Check(para > 0.8f, $"a luz sai da lanterna para a frente ({para:0.00})");
                        luz.Alternar(); bool apagou = !luz.ligada; luz.Alternar();
                        Check(apagou && luz.ligada, "L apaga e acende");
                    }
                }
                {
                    // braços numa câmera própria, por cima do mundo (não atravessam paredes)
                    int cfp = LayerMask.NameToLayer(CameraDosBracos.NomeCamada);
                    var dadosCam = Camera.main.GetUniversalAdditionalCameraData();
                    var sobre = dadosCam.cameraStack.FirstOrDefault(c => c != null && c.name == "Câmera dos Braços");
                    Check(sobre != null && sobre.cullingMask == 1 << cfp, "câmera dos braços (overlay) na pilha da câmera principal");
                    Check((Camera.main.cullingMask & (1 << cfp)) == 0, "a câmera principal não desenha os braços");
                    var rb = mao != null ? mao.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                    var rl = lant != null ? lant.GetComponentInChildren<Renderer>(true) : null;
                    Check(rb != null && rb.gameObject.layer == cfp && rl != null && rl.gameObject.layer == cfp, "braços e lanterna na camada dos braços");
                }
                Captura("fp_1_fora.png");
                marca = Time.time; passo = 1;
                break;
            }
            case 1:
                ModoCamera.MoverTeste = new Vector2(0f, 1f);
                if (Time.time - marca > 1.3f)
                {
                    float mistura = Campo<float>(corpo, "mistura");
                    Check(mistura > 0.6f, $"anda em primeira pessoa (corpo anima na sombra: {mistura:0.00})");
                    ModoCamera.MoverTeste = Vector2.zero;
                    // isométrica à mão (tecla V) para conferir o visual dela
                    modo.Definir(ModoCamera.Modo.Isometrico);
                    if (iso != null) iso.distancia = 11f;
                    marca = Time.time; passo++;
                }
                break;
            case 2 when Time.time - marca > 1.4f:
            {
                log.AppendLine("== isométrica (V) ==");
                Check(modo.Isometrico, "V troca para a isométrica");
                var dc = Camera.main.transform.position - fpc.transform.position;
                Check(dc.y > 5f, $"câmera no alto ({dc.y:0.0} m)");
                Check(Cursor.lockState == CursorLockMode.None, "mouse livre");
                marca = Time.time; passo++;
                break;
            }
            case 3:
                ModoCamera.MoverTeste = new Vector2(1f, 0f);
                if (Time.time - marca > 1.3f)
                {
                    float mistura = Campo<float>(corpo, "mistura");
                    Check(mistura > 0.6f, $"animação de andar ({mistura:0.00})");
                    Captura("iso_2_andando.png");
                    ModoCamera.MoverTeste = Vector2.zero;
                    modo.Definir(ModoCamera.Modo.PrimeiraPessoa);
                    marca = Time.time; passo++;
                }
                break;
            case 4 when Time.time - marca > 1.4f:
            {
                Check(!modo.Isometrico, "V volta para a primeira pessoa");
                casa = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).FirstOrDefault(e => e.name == "Dom");
                if (casa == null) { Check(false, "casa com Estrutura na cena"); Fim(); break; }
                log.AppendLine("== casa: dentro vira isométrica ==");
                Teleportar(fpc, casa.transform.TransformPoint(new Vector3(-4.4f, 0.1f, 3.2f)));
                Mirar(fpc, casa.transform.TransformPoint(new Vector3(-4.4f, 1.4f, 0f)));
                marca = Time.time; passo++;
                break;
            }
            case 5 when Time.time - marca > 1.0f:
                Check(!modo.Isometrico && !casa.JogadorDentro, "na frente da casa, primeira pessoa");
                Captura("casa_1_porta_fp.png");
                marca = Time.time; passo++;
                break;
            case 6:
            {
                AndarAte(fpc, casa.transform.TransformPoint(new Vector3(-4.4f, 0.4f, -1.4f)));
                var porta = Object.FindObjectsByType<PortaAutomatica>(FindObjectsSortMode.None).OrderBy(p => Vector3.Distance(p.transform.position, fpc.transform.position)).FirstOrDefault();
                if (porta != null && porta.Aberta) portaAbriu = true;
                if (Time.time - marca > 4.5f)
                {
                    ModoCamera.MoverTeste = null;
                    Check(portaAbriu, "porta abriu sozinha");
                    Check(casa.JogadorDentro, "dentro da casa (piso embaixo)");
                    marca = Time.time; passo++;
                }
                break;
            }
            case 7 when Time.time - marca > 1.6f:
            {
                Check(modo.Isometrico, "dentro da casa: isométrica");
                var dc = Camera.main.transform.position - fpc.transform.position;
                log.AppendLine($"  câmera a {dc.magnitude:0.0} m do Cândido (interior)");
                var oc = Object.FindFirstObjectByType<OcultarObstaculos>();
                Check(oc != null && oc.QuantosEscondidos > 0, $"telhado/paredes da frente somem ({(oc != null ? oc.QuantosEscondidos : 0)} peças)");
                var mira = Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Mira");
                Check(mira == null || !mira.enabled, "mira escondida na isométrica");
                Captura("casa_2_dentro_iso.png");
                marca = Time.time; passo++;
                break;
            }
            case 8:
            {
                // anda até o documento do outro canto e confere o E por proximidade
                var doc = casa.transform.Find("Documento Stephenson");
                var entrada = casa.transform.TransformPoint(new Vector3(-4.4f, 0.4f, -1.4f));
                if (doc != null) AndarAte(fpc, doc.position + (entrada - doc.position).normalized * 0.9f);
                if (Time.time - marca > 3f)
                {
                    ModoCamera.MoverTeste = null;
                    log.AppendLine($"  E usaria: {Usaria()}");
                    Check(PlayerInteractor.Atual is Jornal, "perto do documento, o E é LER");
                    Captura("casa_3_documento.png");
                    marca = Time.time; passo++;
                }
                break;
            }
            case 9:
                AndarAte(fpc, casa.transform.TransformPoint(new Vector3(-4.4f, 0f, 3.6f)));
                if (Time.time - marca > 6f) { ModoCamera.MoverTeste = null; marca = Time.time; passo++; }
                break;
            case 10 when Time.time - marca > 1.5f:
            {
                Check(!casa.JogadorDentro && !modo.Isometrico, $"saiu: volta para a primeira pessoa (posição {casa.transform.InverseTransformPoint(fpc.transform.position):F1})");
                trem = Object.FindFirstObjectByType<TrainInteraction>();
                if (trem == null) { Check(false, "trem na cena"); Fim(); break; }
                log.AppendLine("== trem (primeira pessoa) ==");
                var assento = trem.trainSeat.position;
                var saida = trem.exitPoint.position;
                Vector3 fora = saida - assento; fora.y = 0f;
                var lado = saida + fora.normalized * 0.8f;
                Teleportar(fpc, new Vector3(lado.x, trem.transform.position.y + 0.1f, lado.z));
                Mirar(fpc, assento + Vector3.up * 0.6f);
                marca = Time.time; passo++;
                break;
            }
            case 11 when Time.time - marca > 1.0f:
                log.AppendLine($"  E usaria: {Usaria()}");
                Check(PlayerInteractor.Atual is TremInteragivel, "mirando o trem: \"SUBIR NO TREM\"");
                Captura("trem_1_perto.png");
                PlayerInteractor.Atual?.Interagir();
                marca = Time.time; passo++;
                break;
            case 12 when Time.time - marca > 4f:
            {
                Check(trem.IsInsideTrain, "subiu e o trem anda");
                Check(AlfaBarra() > 0.9f, $"no trem: barra do carvão aparece ({AlfaBarra():0.00})");
                // câmera de fora da locomotiva (o mesmo que apertar V)
                var tc = Object.FindObjectsByType<TrainCameraView>(FindObjectsSortMode.None).FirstOrDefault(c => c.trainCamera != null && c.enabled);
                if (tc == null) { Check(false, "câmera de fora do trem na cena"); passo = 61; break; }
                typeof(TrainCameraView).GetField("alternada", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tc, true);
                marca = Time.time; passo = 60;
                break;
            }
            case 60 when Time.time - marca > 1.2f:
            {
                var tc = Object.FindObjectsByType<TrainCameraView>(FindObjectsSortMode.None).First(c => c.trainCamera != null && c.enabled);
                Check(TrainCameraView.Ativa && Camera.main == tc.trainCamera, $"V no trem: câmera de fora da locomotiva (ativa {TrainCameraView.Ativa}, Camera.main '{(Camera.main != null ? Camera.main.name : "nenhuma")}')");
                var ouvidos = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled);
                Check(ouvidos == 1, $"um ouvido só ligado ({ouvidos})");
                Captura("trem_3_camera_de_fora.png");
                typeof(TrainCameraView).GetField("alternada", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tc, false);
                marca = Time.time; passo = 61;
                break;
            }
            case 61 when Time.time - marca > 0.5f:
                Check(!TrainCameraView.Ativa, "V de novo: volta para a cabine");
                // olha pela janela: mesmo sem mirar o trem, o E desce
                Mirar(fpc, fpc.transform.position + fpc.transform.right * 10f + Vector3.up * 1.4f);
                marca = Time.time; passo = 13;
                break;
            case 13 when Time.time - marca > 0.4f:
                log.AppendLine($"  E usaria: {Usaria()}");
                Check(PlayerInteractor.Atual is TremInteragivel && PlayerInteractor.Atual.Texto == "DESCER DO TREM", "sentado, olhando para fora: o E é \"DESCER DO TREM\"");
                Captura("trem_2_dirigindo.png");
                PlayerInteractor.Atual?.Interagir();
                marca = Time.time; passo++;
                break;
            case 14 when !trem.IsInsideTrain || Time.time - marca > 12f:
                Check(!trem.IsInsideTrain, $"desceu depois de frear ({Time.time - marca:0.0}s)");
                marca = Time.time; passo++;
                break;
            case 15 when Time.time - marca > 1f:
            {
                log.AppendLine("== carvão e itens (primeira pessoa) ==");
                var monte = GameObject.Find("Monte de Carvao 1");
                if (monte == null) { Check(false, "Monte de Carvao 1 na cena"); Fim(); break; }
                var lado = monte.transform.position + new Vector3(1.8f, 0.2f, 0f);
                Teleportar(fpc, lado);
                Mirar(fpc, monte.transform.position + Vector3.up * 0.4f);
                marca = Time.time; passo++;
                break;
            }
            case 16 when Time.time - marca > 1.0f:
            {
                log.AppendLine($"  E usaria: {Usaria()}");
                Check(AlfaBarra() < 0.1f, $"fora do trem: barra do carvão some ({AlfaBarra():0.00})");
                Check(PlayerInteractor.Atual is MonteDeCarvao, "mirando o monte: PEGAR CARVÃO");
                Captura("mundo_1_monte.png");
                PlayerInteractor.Atual?.Interagir();
                Check(PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity("carvao") == 1, "uma pedra na mão");
                marca = Time.time; passo = 30;
                break;
            }
            case 30 when Time.time - marca > 1.0f:
            {
                // a pedra fica no punho esquerdo dos braços
                var pedra = GameObject.Find("Carvao na Mao");
                var maoE = Object.FindFirstObjectByType<MaoPrimeiraPessoa>()?.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "hand.L");
                var pr = pedra != null ? pedra.GetComponentInChildren<Renderer>() : null;
                float dPedra = pr != null && maoE != null ? Vector3.Distance(pr.bounds.center, maoE.position) : 99f;
                Check(pr != null && pr.gameObject.activeInHierarchy && dPedra < 0.15f, $"pedra no punho esquerdo ({dPedra:0.00} m da mão)");
                Captura("mundo_1b_carvao_na_mao.png");
                // Q larga a pedra no chão; dá para pegar de volta
                ArmasDoJogador.Instance.Largar();
                var largada = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Carvão largado");
                Check(PlayerInventory.Instance.GetQuantity("carvao") == 0 && largada != null, "Q larga a pedra no chão (mãos livres)");
                if (largada != null) largada.Interagir();
                Check(PlayerInventory.Instance.GetQuantity("carvao") == 1, "pegou a pedra de volta");
                // com a lanterna na direita, só cabe uma pedra
                Check(CarvaoNaMao.Capacidade == 1, $"com a lanterna na mão, cabe 1 pedra ({CarvaoNaMao.Capacidade})");
                // H guarda a lanterna: as duas mãos livres para o carvão
                ArmasDoJogador.Instance.Largar();
                LanternaNaMao.Guardar(true);
                Check(LanternaNaMao.Guardada && !LanternaNaMao.MaoDireitaOcupada && LanternaNaMao.TemLanterna, "H guarda a lanterna (continua na mochila)");
                Check(CarvaoNaMao.Capacidade == 2, $"lanterna guardada: cabem 2 pedras ({CarvaoNaMao.Capacidade})");
                PegarCarvao(GameObject.Find("Monte de Carvao 1").GetComponent<MonteDeCarvao>());
                Check(PlayerInventory.Instance.GetQuantity("carvao") == 1 && AnimMaoDireita() == true, "primeira pedra: pega com a mão direita");
                LanternaNaMao.Guardar(false);
                Check(LanternaNaMao.Guardada, "com pedra na direita, a lanterna não volta para a mão");
                marca = Time.time; passo = 31;
                break;
            }
            case 31 when Time.time - marca > 0.8f:
            {
                Check(LuzDaLanterna.Instance == null || !LuzDaLanterna.Instance.Luz.enabled, "lanterna guardada: luz apagada");
                var lr = Object.FindFirstObjectByType<LanternaNaMao>()?.GetComponentInChildren<Renderer>(true);
                Check(lr != null && !lr.enabled, "lanterna guardada: some da mão");
                Check(DistPedra("Pedra (direita)", "hand.R") < 0.15f, $"pedra na mão direita ({DistPedra("Pedra (direita)", "hand.R"):0.00} m)");
                var monte = GameObject.Find("Monte de Carvao 1").GetComponent<MonteDeCarvao>();
                PegarCarvao(monte);
                Check(PlayerInventory.Instance.GetQuantity("carvao") == 2 && AnimMaoDireita() == false, "segunda pedra: pega com a mão esquerda");
                Check(monte.Texto == "MÃOS OCUPADAS", $"com as duas mãos cheias: \"{monte.Texto}\"");
                monte.Interagir();
                Check(PlayerInventory.Instance.GetQuantity("carvao") == 2, "não pega a terceira");
                marca = Time.time; passo = 32;
                break;
            }
            case 32 when Time.time - marca > 0.8f:
            {
                float dd = DistPedra("Pedra (direita)", "hand.R"), de = DistPedra("Pedra (esquerda)", "hand.L");
                Check(dd < 0.15f && de < 0.15f, $"uma pedra em cada mão (direita {dd:0.00} m, esquerda {de:0.00} m)");
                int cfp = LayerMask.NameToLayer(CameraDosBracos.NomeCamada);
                var pd = GameObject.Find("Pedra (direita)");
                Check(pd != null && pd.layer == cfp, "pedras na camada dos braços");
                Captura("mundo_1c_duas_pedras.png");
                ArmasDoJogador.Instance.Largar(); ArmasDoJogador.Instance.Largar();
                Check(PlayerInventory.Instance.GetQuantity("carvao") == 0, "Q duas vezes larga as duas pedras");
                foreach (var it in Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).Where(i => i.name == "Carvão largado")) Object.Destroy(it.gameObject);
                LanternaNaMao.Guardar(false);
                Check(LanternaNaMao.NaMaoAgora && LanternaNaMao.MaoDireitaOcupada, "H de novo: lanterna volta para a mão");
                // Q sem carvão nem arma larga a lanterna
                if (ArmasDoJogador.Instance.Equipada != "") ArmasDoJogador.Instance.Equipar("");
                ArmasDoJogador.Instance.Largar();
                var lg = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Lanterna largada");
                Check(!LanternaNaMao.TemLanterna && lg != null, "Q larga a lanterna no chão");
                if (lg != null) lg.Interagir();
                Check(LanternaNaMao.NaMaoAgora, "pegou a lanterna de volta (na mão)");
                PegarCarvao(GameObject.Find("Monte de Carvao 1").GetComponent<MonteDeCarvao>());
                // pilha: acabando, troca sozinho pela da mochila
                var luz = LuzDaLanterna.Instance;
                if (luz != null)
                {
                    PlayerInventory.Instance.AddItem("pilha", 1);
                    luz.Carga = 0.0001f;
                }
                var tender = Object.FindFirstObjectByType<CombustivelTrem>();
                antesCarvao = tender != null ? tender.Carvao : -1f;
                if (tender != null)
                {
                    var t2 = tender.transform.position + (tender.transform.position - trem.trainSeat.position).normalized * 1.6f;
                    Teleportar(fpc, new Vector3(t2.x, trem.transform.position.y + 0.1f, t2.z));
                    Mirar(fpc, tender.transform.position);
                }
                marca = Time.time; passo = 17;
                break;
            }
            case 17 when Time.time - marca > 1.0f:
            {
                if (LuzDaLanterna.Instance != null)
                    Check(LuzDaLanterna.Instance.Carga > 0.9f && PlayerInventory.Instance.GetQuantity("pilha") == 0, $"pilha acabou: trocou sozinho pela da mochila ({LuzDaLanterna.Instance.Carga * 100f:0}%)");
                log.AppendLine($"  E usaria: {Usaria()}");
                {
                    var cm = Camera.main.transform;
                    var tdr = Object.FindFirstObjectByType<CombustivelTrem>();
                    var tcol = tdr.GetComponent<Collider>(); log.AppendLine($"  colisor do tênder: {(tcol != null ? tcol.bounds.ToString() + " ativo=" + tcol.enabled + " gatilho=" + tcol.isTrigger + " escala " + tdr.transform.lossyScale.ToString("F2") : "NENHUM")}");
                    log.AppendLine($"  jogador {fpc.transform.position:F2}, câmera {cm.position:F2} olhando {cm.forward:F2}, tênder {tdr.transform.position:F2} (dist {Vector3.Distance(cm.position, tdr.transform.position):0.00})");
                    foreach (var h in Physics.RaycastAll(cm.position, cm.forward, 6f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                        log.AppendLine($"    raio: {h.collider.name} ({h.collider.GetType().Name}{(h.collider.isTrigger ? ", gatilho" : "")}) a {h.distance:0.00} m");
                }
                Check(PlayerInteractor.Atual is CombustivelTrem, "mirando o tênder: COLOCAR CARVÃO");
                Captura("mundo_2_tender.png");
                PlayerInteractor.Atual?.Interagir();
                var tender = Object.FindFirstObjectByType<CombustivelTrem>();
                Check(tender != null && tender.Carvao > antesCarvao + 0.5f && PlayerInventory.Instance.GetQuantity("carvao") == 0,
                    $"abasteceu (tênder {antesCarvao:0.0} → {tender?.Carvao:0.0})");
                var agua = GameObject.Find("Survival")?.transform.Find("Water");
                if (agua != null)
                {
                    var t3 = agua.position + new Vector3(1.2f, 0.1f, 0f);
                    Teleportar(fpc, t3);
                    var ra = agua.GetComponent<Renderer>();
                    Mirar(fpc, ra != null ? ra.bounds.center : agua.position);
                }
                marca = Time.time; passo++;
                break;
            }
            case 18 when Time.time - marca > 1.0f:
            {
                log.AppendLine($"  E usaria: {Usaria()}");
                Check(PlayerInteractor.Atual is ItemColetavel, "mirando o kit: PEGAR");
                Captura("mundo_3_kit.png");
                PlayerInteractor.Atual?.Interagir();
                int total = 0;
                foreach (var id in new[] { "agua", "soda", "lata_feijao", "fosforos", "municao", "galho" }) total += PlayerInventory.Instance.GetQuantity(id);
                Check(total > 0, $"item foi para o inventário ({total})");
                visitar = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).OrderBy(e => e.name).ToList();
                visita = 0;
                log.AppendLine($"== estruturas pelo mapa: {visitar.Count} ==");
                marca = Time.time; passo = 19;
                break;
            }
            // ---------------------------------------------------------------- cada estrutura: chega, entra, sem árvore dentro
            case 19:
            {
                if (visita >= visitar.Count) { marca = Time.time; passo = 70; break; }
                var e = visitar[visita];
                var ent = e.transform.Find("Entrada");
                if (ent == null) { Check(false, $"{e.name}: tem ponto de entrada"); visita++; break; }
                var pe = ent.position;
                var ter = Terrain.activeTerrain;
                if (ter != null) pe.y = Mathf.Max(pe.y, ter.SampleHeight(pe) + ter.transform.position.y);
                Teleportar(fpc, pe + Vector3.up * 0.15f);
                marca = Time.time; passo = 20;
                break;
            }
            case 20:
            {
                // espera a floresta em volta ser gerada
                var sp = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
                if ((sp == null || sp.IsLoaded) && Time.time - marca > 1.5f || Time.time - marca > 12f) { marca = Time.time; passo = 21; }
                break;
            }
            case 21:
            {
                var e = visitar[visita];
                var den = e.transform.Find("Dentro");
                float falta = den != null ? AndarAte(fpc, den.position) : 0f;
                if (falta > 0.35f && Time.time - marca < 9f) break;
                ModoCamera.MoverTeste = Vector2.zero;
                marca = Time.time; passo = 22;
                break;
            }
            // ---------------------------------------------------------------- casa velha: escada para o andar de cima
            case 70:
            {
                log.AppendLine("== casa velha: dois andares ==");
                var cv = visitar.FirstOrDefault(e => e.name == "Casa Velha 1");
                if (cv == null) { Check(false, "Casa Velha 1 na cena"); passo = 80; break; }
                Teleportar(fpc, cv.transform.TransformPoint(new Vector3(5.65f, 0.15f, -2.7f)));
                marca = Time.time; passo = 71;
                break;
            }
            case 71 when Time.time - marca > 1.2f:
            {
                var cv = visitar.First(e => e.name == "Casa Velha 1");
                Check(cv.JogadorDentro && modo.Isometrico, "no pé da escada, dentro e na isométrica");
                var oc = Object.FindFirstObjectByType<OcultarObstaculos>();
                var esc = Campo<System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>>(oc, "escondidos");
                var cima = cv.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Panels andar de cima")).ToList();
                var terreo = cv.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Panels terreo")).ToList();
                Check(cima.Count > 0 && cima.All(r => esc != null && esc.ContainsKey(r)), $"no térreo, as paredes do andar de cima somem ({cima.Count})");
                Check(terreo.Count(r => esc == null || !esc.ContainsKey(r)) >= 2, $"no térreo, as paredes do térreo ficam ({terreo.Count(r => esc == null || !esc.ContainsKey(r))} de {terreo.Count})");
                Captura("casavelha_1_terreo.png");
                marca = Time.time; passo = 72;
                break;
            }
            case 72:
            {
                var cv = visitar.First(e => e.name == "Casa Velha 1");
                float falta = AndarAte(fpc, cv.transform.TransformPoint(new Vector3(5.5f, 0f, -3.55f)));
                if (falta > 0.25f && Time.time - marca < 5f) break;
                marca = Time.time; passo = 73;
                break;
            }
            case 73:
            {
                var cv = visitar.First(e => e.name == "Casa Velha 1");
                float falta = AndarAte(fpc, cv.transform.TransformPoint(new Vector3(0.4f, 0f, -3.55f)));
                if (falta > 0.3f && Time.time - marca < 10f) break;
                ModoCamera.MoverTeste = Vector2.zero;
                marca = Time.time; passo = 74;
                break;
            }
            case 74 when Time.time - marca > 1.2f:
            {
                var cv = visitar.First(e => e.name == "Casa Velha 1");
                float altura = cv.transform.InverseTransformPoint(fpc.transform.position).y;
                Check(altura > 2.8f, $"subiu a escada ({altura:0.00} m acima do térreo)");
                Check(cv.PisoAtual != null && cv.PisoAtual.name.StartsWith("Piso de Cima"), $"pisando no andar de cima ({(cv.PisoAtual != null ? cv.PisoAtual.name : "nada")})");
                var oc = Object.FindFirstObjectByType<OcultarObstaculos>();
                var esc = Campo<System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>>(oc, "escondidos");
                var cima = cv.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Panels andar de cima")).ToList();
                Check(cima.Count(r => esc == null || !esc.ContainsKey(r)) >= 2, $"lá em cima, as paredes do andar aparecem ({cima.Count(r => esc == null || !esc.ContainsKey(r))} de {cima.Count})");
                int itensCima = cv.GetComponentsInChildren<ItemColetavel>().Count(i => cv.transform.InverseTransformPoint(i.transform.position).y > 2.9f);
                Check(itensCima > 0, $"tem coisas no andar de cima ({itensCima} suprimentos)");
                Captura("casavelha_2_andar_de_cima.png");
                passo = 80;
                break;
            }
            // ---------------------------------------------------------------- cômodo trancado: forçar a porta
            case 80:
            {
                log.AppendLine("== cômodo trancado ==");
                var tr = Object.FindObjectsByType<PortaTrancada>(FindObjectsSortMode.None).FirstOrDefault(t => t.GetComponentInParent<Estrutura>().name == "Casa da Curva");
                if (tr == null) { Check(false, "porta trancada na Casa da Curva"); passo = 90; break; }
                Check(tr.Trancada && tr.cobertura != null && tr.cobertura.activeSelf, "cômodo trancado e coberto (não dá para ver dentro)");
                // fica do lado de fora do cômodo, de frente para a porta
                var b = tr.GetComponent<Renderer>().bounds;
                var cb = tr.cobertura.GetComponent<Renderer>().bounds;
                var casaT = tr.GetComponentInParent<Estrutura>().transform;
                var bl = casaT.InverseTransformVector(b.size);
                Vector3 normal = casaT.TransformDirection(Mathf.Abs(bl.x) < Mathf.Abs(bl.z) ? Vector3.right : Vector3.forward);
                if (Vector3.Dot(normal, cb.center - b.center) > 0f) normal = -normal;
                Vector3 onde = b.center + normal * 0.95f;
                Teleportar(fpc, new Vector3(onde.x, tr.GetComponentInParent<Estrutura>().pisos[0].bounds.max.y + 0.1f, onde.z));
                fpc.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(-normal, Vector3.up));
                marca = Time.time; passo = 81;
                break;
            }
            case 81 when Time.time - marca > 1.5f:
            {
                var tr = Object.FindObjectsByType<PortaTrancada>(FindObjectsSortMode.None).First(t => t.GetComponentInParent<Estrutura>().name == "Casa da Curva");
                log.AppendLine($"  E usaria: {Usaria()}");
                Check(PlayerInteractor.Atual == tr, "de frente para a porta trancada: FORÇAR A PORTA");
                var dentroDoComodo = tr.cobertura.GetComponent<Renderer>().bounds;
                bool pegaDeFora = PlayerInteractor.Atual is ItemColetavel ic && dentroDoComodo.Contains(new Vector3(ic.transform.position.x, dentroDoComodo.center.y, ic.transform.position.z));
                Check(!pegaDeFora, "não pega item do cômodo trancado de fora");
                Check(!tr.porta.Aberta, "trancada não abre sozinha");
                Captura("trancado_1_porta.png");
                tr.Interagir();
                marca = Time.time; passo = 82;
                break;
            }
            case 82 when Time.time - marca > 0.6f:
            {
                var tr = Object.FindObjectsByType<PortaTrancada>(FindObjectsSortMode.None).First(t => t.GetComponentInParent<Estrutura>().name == "Casa da Curva");
                if (tr.Trancada) { tr.Interagir(); marca = Time.time; break; }
                Check(!tr.cobertura.activeSelf, "forçou a porta: o cômodo aparece");
                marca = Time.time; passo = 83;
                break;
            }
            case 83 when Time.time - marca > 1.2f:
            {
                var tr = Object.FindObjectsByType<PortaTrancada>(FindObjectsSortMode.None).First(t => t.GetComponentInParent<Estrutura>().name == "Casa da Curva");
                Check(tr.porta.Aberta, "a porta abriu");
                Captura("trancado_2_aberto.png");
                passo = 90;
                break;
            }
            // ---------------------------------------------------------------- mercadinho: vasculhar o estoque
            case 90:
            {
                log.AppendLine("== vasculhar ==");
                var rc = Object.FindObjectsByType<Recipiente>(FindObjectsSortMode.None).FirstOrDefault(r => r.tipo == Recipiente.Tipo.Estoque);
                int nRec = Object.FindObjectsByType<Recipiente>(FindObjectsSortMode.None).Length;
                Check(rc != null, $"móveis para vasculhar na cena ({nRec})");
                if (rc != null)
                {
                    int antes = Contar();
                    rc.Interagir();
                    Check(!rc.Disponivel && GameFlags.Has(rc.flag), "estoque vasculhado uma vez (o save lembra)");
                    Check(Contar() > antes, $"vasculhar o estoque deu itens ({Contar() - antes})");
                }
                marca = Time.time; passo = 40;
                break;
            }
            // ---------------------------------------------------------------- mapa (M) e pausa
            case 40:
            {
                log.AppendLine("== mapa e pausa ==");
                Teleportar(fpc, visitar.Count > 0 ? visitar[0].transform.Find("Entrada").position + Vector3.up * 0.2f : fpc.transform.position);
                var mapa = MapaDoMundo.Instance;
                Check(mapa != null && mapa.imagem != null, "mapa na cena com a imagem do terreno");
                if (mapa != null) mapa.Mostrar(true);
                marca = Time.time; passo = 41;
                break;
            }
            case 41 when Time.time - marca > 0.8f:
            {
                int montes = MonteDeCarvao.Todos.Count, comCarvao = MonteDeCarvao.Todos.Count(m => m != null && m.TemCarvao);
                Check(MapaDoMundo.Aberto, "M abre o mapa");
                Check(montes >= 7, $"{montes} montes de carvão no mapa ({comCarvao} com carvão)");
                var obj = Object.FindFirstObjectByType<Objetivos>();
                var txt = obj != null ? new SerializedObject(obj).FindProperty("texto").objectReferenceValue as TMPro.TMP_Text : null;
                Check(txt != null && txt.text.Contains("monte de carvão mais perto"), "objetivo mostra o monte mais perto");
                // materiais quebrados (aparecem roxos)
                int quebrados = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var mats = r.sharedMaterials;
                    if (mats.Length == 0 || mats.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported))
                    {
                        quebrados++;
                        if (quebrados <= 12) log.AppendLine($"  roxo? {r.GetType().Name} '{r.name}' (pai '{(r.transform.parent ? r.transform.parent.name : "-")}') em {r.transform.position:F0}, a {Vector3.Distance(r.transform.position, Camera.main.transform.position):0} m, mats [{string.Join(", ", mats.Select(m => m == null ? "null" : m.name + "/" + (m.shader ? m.shader.name : "?")))}]");
                    }
                }
                log.AppendLine($"  renderers com material quebrado: {quebrados}");
                // foto da tela do jogo (com a interface por cima), sai no fim do quadro
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "mapa_aberto.png")));
                log.AppendLine("  captura: mapa_aberto.png (tela do jogo)");
                marca = Time.time; passo = 42;
                break;
            }
            case 42 when Time.time - marca > 1f:
            {
                MapaDoMundo.Instance.Mostrar(false);
                if (PauseMenu.Instance == null) { Check(false, "menu de pausa na cena"); Fim(); break; }
                PauseMenu.Instance.Pause();
                Check(AudioListener.pause, "pausar silencia o som do jogo (trem incluso)");
                PauseMenu.Instance.Resume();
                Check(!AudioListener.pause, "voltar do pause devolve o som");
                marca = Time.time; passo = 100;
                break;
            }
            // ---------------------------------------------------------------- mochila (tela) e armas
            case 100:
            {
                log.AppendLine("== armas ==");
                var inv = PlayerInventory.Instance;
                int armasNoMapa = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).Count(i => i.name.StartsWith("Arma: "));
                Check(armasNoMapa >= 3, $"armas espalhadas pelas estruturas ({armasNoMapa})");
                var rifle = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Arma: rifle");
                if (rifle != null) rifle.Interagir();
                var faca = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Arma: faca");
                if (faca != null) faca.Interagir();
                Check(inv.GetQuantity("rifle") == 1 && inv.GetQuantity("faca") == 1, "pegou o rifle e a faca");
                inv.AddItem("municao", 10);
                // num campo aberto, longe das casas
                var ter = Terrain.activeTerrain;
                var campo = new Vector3(500f, 0f, 500f);
                campo.y = ter.SampleHeight(campo) + ter.transform.position.y + 0.2f;
                Teleportar(fpc, campo);
                modo.Definir(ModoCamera.Modo.PrimeiraPessoa);
                TelaMochila.Instance.Mostrar(true);
                marca = Time.time; passo = 101;
                break;
            }
            case 101 when Time.time - marca > 0.8f:
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "mochila_tela.png")));
                log.AppendLine("  captura: mochila_tela.png (tela do jogo)");
                marca = Time.time; passo = 102;
                break;
            }
            case 102 when Time.time - marca > 0.6f:
            {
                TelaMochila.Instance.Mostrar(false);
                var armas = ArmasDoJogador.Instance;
                armas.Equipar("rifle");
                typeof(ArmasDoJogador).GetField("fimRecarga", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(armas, 0.01f); // recarrega já
                // um cervo a 14 m, na frente
                var sp = Object.FindFirstObjectByType<SpawnerDeAnimais>();
                var p = fpc.transform.position + fpc.transform.forward * 14f;
                var ter = Terrain.activeTerrain; p.y = ter.SampleHeight(p) + ter.transform.position.y;
                var cervo = sp.Criar("cervo", p, Quaternion.LookRotation(fpc.transform.right));
                Check(cervo != null, "nasceu um cervo para o teste");
                if (cervo != null) cervo.GetComponent<Animal>().distVer = 2f;
                alvoTeste = cervo != null ? cervo.GetComponent<Animal>() : null;
                marca = Time.time; passo = 103;
                break;
            }
            case 103 when Time.time - marca > 0.6f:
            {
                var armas = ArmasDoJogador.Instance;
                Check(armas.NoPente == armas.capacidadePente, $"rifle recarregado ({armas.NoPente}/{armas.capacidadePente})");
                if (alvoTeste != null) Mirar(fpc, alvoTeste.Centro);
                marca = Time.time; passo = 104;
                break;
            }
            case 104 when Time.time - marca > 0.4f:
            {
                var armas = ArmasDoJogador.Instance;
                int antes = armas.NoPente;
                {
                    var cm = Camera.main.transform;
                    log.AppendLine($"  cervo: vida {(alvoTeste != null ? alvoTeste.vida : -1):0}, a {(alvoTeste != null ? Vector3.Distance(cm.position, alvoTeste.Centro) : -1):0.0} m");
                    foreach (var h in Physics.RaycastAll(cm.position, cm.forward, 60f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Take(4))
                        log.AppendLine($"    raio do tiro: {h.collider.name} ({h.collider.GetType().Name}) a {h.distance:0.0} m");
                }
                armas.Atacar();
                Captura("arma_1_rifle.png");
                Check(armas.NoPente == antes - 1, "o tiro gasta uma bala");
                marca = Time.time; passo = 105;
                break;
            }
            case 105 when Time.time - marca > 0.8f:
            {
                Check(alvoTeste != null && !alvoTeste.Vivo, $"um tiro de rifle derruba o cervo (vida {(alvoTeste != null ? alvoTeste.vida : -1):0})");
                int sois = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled && l.type == LightType.Directional);
                Check(sois == 1, $"com cervos no mapa, só a lua ilumina tudo ({sois} luz direcional)");
                var carc = alvoTeste != null ? alvoTeste.GetComponent<Carcaca>() : null;
                Check(carc != null, "o cervo morto vira carcaça");
                int antes = PlayerInventory.Instance.GetQuantity("carne_crua");
                if (carc != null) carc.Interagir();
                Check(PlayerInventory.Instance.GetQuantity("carne_crua") > antes, "pegou carne do cervo");
                // faca num lobo colado
                ArmasDoJogador.Instance.Equipar("faca");
                var sp = Object.FindFirstObjectByType<SpawnerDeAnimais>();
                var p = fpc.transform.position + fpc.transform.forward * 1.3f;
                var ter = Terrain.activeTerrain; p.y = ter.SampleHeight(p) + ter.transform.position.y;
                var lobo = sp.Criar("lobo", p, Quaternion.LookRotation(-fpc.transform.forward));
                alvoTeste = lobo != null ? lobo.GetComponent<Animal>() : null;
                Check(alvoTeste != null, "nasceu um lobo (modelo do urso, menor)");
                if (alvoTeste != null) Mirar(fpc, alvoTeste.Centro);
                fpc.staminaAtual = fpc.staminaMax;
                marca = Time.time; passo = 106;
                break;
            }
            case 106 when Time.time - marca > 0.5f:
            {
                if (alvoTeste != null) Mirar(fpc, alvoTeste.Centro);
                float vidaAntes = alvoTeste != null ? alvoTeste.vida : 0f;
                float folego = fpc.staminaAtual;
                ArmasDoJogador.Instance.Atacar();
                Captura("arma_2_faca.png");
                Check(alvoTeste != null && alvoTeste.vida < vidaAntes, $"facada acerta o lobo ({vidaAntes:0} → {(alvoTeste != null ? alvoTeste.vida : 0):0})");
                Check(fpc.staminaAtual < folego, "golpe gasta estamina");
                var faca = GameObject.Find("Arma na Mão (faca)");
                Check(faca != null && faca.layer == LayerMask.NameToLayer(CameraDosBracos.NomeCamada), "faca na camada dos braços (não entra na parede)");
                var mm = ArmasDoJogador.Instance.modeloMachado;
                var mmf = mm != null ? mm.GetComponentInChildren<MeshFilter>(true) : null;
                Check(mmf != null && AssetDatabase.GetAssetPath(mmf.sharedMesh).EndsWith("Machado/Machado.obj"), $"machado novo ({(mmf != null ? AssetDatabase.GetAssetPath(mmf.sharedMesh) : "nenhum")})");
                vidaTeste = PlayerStats.Instance.vida.atual;
                marca = Time.time; passo = 107;
                break;
            }
            case 107 when Time.time - marca > 3.5f:
            {
                Check(PlayerStats.Instance.vida.atual < vidaTeste, $"o lobo ataca ({vidaTeste:0} → {PlayerStats.Instance.vida.atual:0} de vida)");
                Check(ArmasDoJogador.EmCombate, "em combate");
                Captura("ameaca_1_lobo.png");
                if (alvoTeste != null) alvoTeste.Atingir(999f, fpc.transform.position, "faca");
                foreach (var a in Animal.Todos.ToArray()) if (a != null && a.Vivo) Object.Destroy(a.gameObject);
                sanidadeTeste = PlayerStats.Instance.sanidade.atual;
                marca = Time.time; passo = 108;
                break;
            }
            case 108 when Time.time - marca > ArmasDoJogador.Instance.fimDoCombate + 1.5f:
            {
                Check(!ArmasDoJogador.EmCombate, "o combate acabou");
                Check(PlayerStats.Instance.sanidade.atual < sanidadeTeste - 3f, $"depois do combate a sanidade cai ({sanidadeTeste:0} → {PlayerStats.Instance.sanidade.atual:0})");
                // um urso a 16 m vem atrás
                var sp = Object.FindFirstObjectByType<SpawnerDeAnimais>();
                var p = fpc.transform.position + fpc.transform.forward * 16f;
                var ter = Terrain.activeTerrain; p.y = ter.SampleHeight(p) + ter.transform.position.y;
                var urso = sp.Criar("urso", p, Quaternion.LookRotation(-fpc.transform.forward));
                alvoTeste = urso != null ? urso.GetComponent<Animal>() : null;
                PlayerStats.Instance.Curar(100f);
                vidaTeste = PlayerStats.Instance.vida.atual;
                modo.Definir(ModoCamera.Modo.Isometrico);
                marca = Time.time; passo = 109;
                break;
            }
            case 109 when Time.time - marca > 5f:
            {
                Check(alvoTeste != null && (alvoTeste.transform.position - fpc.transform.position).magnitude < 4f, $"o urso veio atrás ({(alvoTeste != null ? (alvoTeste.transform.position - fpc.transform.position).magnitude : 0):0.0} m)");
                Check(PlayerStats.Instance.vida.atual < vidaTeste, $"o urso machuca ({vidaTeste:0} → {PlayerStats.Instance.vida.atual:0})");
                Captura("ameaca_2_urso.png");
                foreach (var a in Animal.Todos.ToArray()) if (a != null) Object.Destroy(a.gameObject);
                PlayerStats.Instance.Curar(100f);
                modo.Definir(ModoCamera.Modo.PrimeiraPessoa);
                marca = Time.time; passo = 110;
                break;
            }
            // ---------------------------------------------------------------- trilhos: o trem para na árvore caída
            case 110:
            {
                log.AppendLine("== eventos na ferrovia ==");
                var ev = EventoFerrovia.Todos.OrderBy(e => e.t).FirstOrDefault(e => !e.Resolvido);
                Check(EventoFerrovia.Todos.Count >= 4, $"obstáculos nos trilhos ({EventoFerrovia.Todos.Count})");
                if (ev == null) { passo = 120; break; }
                eventoTeste = ev;
                float comp = (float)typeof(TrainInteraction).GetField("splineLength", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trem);
                typeof(TrainInteraction).GetField("splinePosition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(trem, ev.t - 90f / comp);
                typeof(TrainInteraction).GetMethod("SetTrainPosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trem, null);
                Physics.SyncTransforms();
                typeof(TrainInteraction).GetMethod("EnterTrain", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trem, null);
                marca = Time.time; passo = 111;
                break;
            }
            case 111 when Time.time - marca > 14f || (Time.time - marca > 3f && trem.VelocidadeAtual < 0.01f):
            {
                float comp = (float)typeof(TrainInteraction).GetField("splineLength", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trem);
                float falta = (eventoTeste.t - trem.PosicaoNoTrilho) * comp;
                Check(trem.VelocidadeAtual < 0.01f && falta > 5f && falta < 30f, $"o trem freou antes do obstáculo ({falta:0} m antes, {eventoTeste.tipo})");
                Captura("ferrovia_1_obstaculo.png");
                PlayerInteractor.Atual?.Interagir(); // desce
                marca = Time.time; passo = 112;
                break;
            }
            case 112 when !trem.IsInsideTrain || Time.time - marca > 8f:
            {
                Check(!trem.IsInsideTrain, "desceu para resolver");
                // resolve como o jogador (E várias vezes, com fôlego)
                for (int i = 0; i < 12 && !eventoTeste.Resolvido; i++) { fpc.staminaAtual = fpc.staminaMax; eventoTeste.Interagir(); typeof(EventoFerrovia).GetField("mexendo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eventoTeste, false); }
                Check(eventoTeste.Resolvido && GameFlags.Has(eventoTeste.flag), "tirou o obstáculo (o save lembra)");
                float prox = EventoFerrovia.ProximoBloqueio(trem.PosicaoNoTrilho, out _);
                Check(prox > eventoTeste.t, "o caminho ficou livre");
                marca = Time.time; passo = 120;
                break;
            }
            // ---------------------------------------------------------------- fim da linha: a base
            case 120 when Time.time - marca > 2f:
            {
                log.AppendLine("== chegada à base ==");
                Check(ChegadaBase.Instance != null, "a base do rádio existe no fim da linha");
                foreach (var e in EventoFerrovia.Todos) GameFlags.Set(e.flag);
                typeof(TrainInteraction).GetField("splinePosition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(trem, 0.995f);
                typeof(TrainInteraction).GetMethod("SetTrainPosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trem, null);
                Physics.SyncTransforms();
                chegou = false;
                TrainInteraction.ChegouAoFim += MarcarChegada;
                typeof(TrainInteraction).GetMethod("EnterTrain", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trem, null);
                marca = Time.time; passo = 121;
                break;
            }
            case 121 when chegou || Time.time - marca > 15f:
            {
                TrainInteraction.ChegouAoFim -= MarcarChegada;
                Check(chegou, "o trem chegou ao fim da linha");
                var b = ChegadaBase.Instance;
                Teleportar(fpc, b.transform.position - b.transform.forward * 0f + b.transform.forward * 3f + Vector3.up * 0.3f);
                Mirar(fpc, b.transform.position);
                marca = Time.time; passo = 122;
                break;
            }
            case 122 when Time.time - marca > 1.5f:
            {
                Captura("base_1_portao.png");
                ChegadaBase.Instance.Interagir();
                marca = Time.time; passo = 123;
                break;
            }
            case 123 when Time.time - marca > 5f:
            {
                Check(GameObject.Find("Final do Jogo") != null, "entrou na base: o final começa");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "base_2_final.png")));
                PlayerStats.Instance.enabled = true;
                fpc.enabled = true;
                Object.Destroy(GameObject.Find("Final do Jogo"));
                Object.FindFirstObjectByType<ChegadaBase>().StopAllCoroutines();
                marca = Time.time; passo = 130;
                break;
            }
            // ---------------------------------------------------------------- derrota: desmaio de fome
            case 130 when Time.time - marca > 1f:
            {
                log.AppendLine("== desmaio ==");
                PlayerStats.Instance.Curar(100f);
                vidaTeste = PlayerStats.Instance.vida.atual;
                PlayerStats.Instance.Comer(-1000f);
                marca = Time.time; passo = 131;
                break;
            }
            case 131 when Desmaio.Ativo || Time.time - marca > 4f:
            {
                Check(Desmaio.Ativo, "fome zerada: o Cândido desmaia");
                marca = Time.time; passo = 132;
                break;
            }
            case 132 when Time.time - marca > 3.5f:
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "desmaio_1.png")));
                marca = Time.time; passo = 133;
                break;
            }
            case 133 when !Desmaio.Ativo || Time.time - marca > 10f:
            {
                Check(!PlayerStats.Instance.Morto && PlayerStats.Instance.vida.atual < vidaTeste && PlayerStats.Instance.fome.atual > 0f, $"acordou fraco ({vidaTeste:0} → {PlayerStats.Instance.vida.atual:0} de vida, fome {PlayerStats.Instance.fome.atual:0})");
                PlayerStats.Instance.Comer(-1000f);
                marca = Time.time; passo = 134;
                break;
            }
            case 134 when PlayerStats.Instance.Morto || Time.time - marca > 4f:
            {
                Check(PlayerStats.Instance.Morto, "fome zerada de novo logo depois: morre");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "desmaio_2_morte.png")));
                marca = Time.time; passo = 135;
                break;
            }
            case 135:
                Fim();
                break;
            case 22 when Time.time - marca > 0.8f:
            {
                var e = visitar[visita];
                var rs = e.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                b.Expand(new Vector3(-0.6f, 0f, -0.6f));
                var sp = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
                int arvores = 0, perto = 0;
                if (sp != null)
                    foreach (Transform v in sp.transform)
                    {
                        var p = v.position;
                        if (Mathf.Abs(p.x - e.transform.position.x) < 60f && Mathf.Abs(p.z - e.transform.position.z) < 60f) perto++;
                        if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z) arvores++;
                    }
                int itens = e.GetComponentsInChildren<ItemColetavel>().Length;
                int moveis = e.transform.Find("Moveis") != null ? e.transform.Find("Moveis").childCount : 0;
                Check(e.JogadorDentro, $"{e.name}: entrou pela porta (posição {e.transform.InverseTransformPoint(fpc.transform.position):F1})");
                Check(modo.Isometrico, $"{e.name}: dentro vira isométrica");
                Check(arvores == 0, $"{e.name}: nenhuma árvore dentro ({perto} plantas num raio de 60 m)");
                Check(itens > 0, $"{e.name}: {moveis} móveis e {itens} suprimentos");
                if (e.name == "Mercadinho" || e.name == "Casa Velha 1" || e.name == "Casa da Curva") Captura($"visita_{new string(e.name.Where(char.IsLetterOrDigit).ToArray())}.png");
                visita++;
                passo = 19;
                break;
            }
        }
    }

    private static void Fim()
    {
        ModoCamera.MoverTeste = null;
        log.AppendLine($"== fim: {falhas} falha(s), {erros} erro(s) no console ==");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "isotest.txt"), log.ToString());
        passo = 99;
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }
}
