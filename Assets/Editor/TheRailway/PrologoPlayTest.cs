using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Teste do prólogo: abre a cena Prologo em Play, acompanha o despertar,
/// confere se cada ponto interativo é alcançável, resolve o puzzle inteiro
/// (rádio, água, carvão, fogo, alavanca, jornais), testa o save e faz o trem
/// partir para a CenaPrincipal. Resultado em Temp/TheRailway/prologotest.txt.
/// </summary>
[InitializeOnLoad]
public static class PrologoPlayTest
{
    private const string Key = "TheRailway.PrologoTest";
    private const string Folder = "Temp/TheRailway";
    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static double startTime = -1;
    private static int nextFrame;
    private static Func<bool> esperar;
    private static float esperarAte;
    private static int step, errors, falhas;
    private static string ultimaFala = "", ultimoObjetivo = "";
    private static int maiorParcial; // maior nº de letras visíveis numa legenda ainda incompleta
    private static readonly StringBuilder log = new StringBuilder();
    private static readonly Queue<Action> acoes = new Queue<Action>();
    private static double partidaEm = -1;

    static PrologoPlayTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying) Begin();
    }

    public static void Start()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText(Path.Combine(Folder, "prologotest.txt"), "PARADO: há cena com mudanças não salvas.");
                return;
            }

        EditorSceneManager.OpenScene(PrologoBuilder.ScenePath, OpenSceneMode.Single);
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
            startTime = -1;
        }
    }

    private static void Begin()
    {
        if (startTime >= 0) return;
        startTime = EditorApplication.timeSinceStartup;
        step = 0; errors = 0; falhas = 0; partidaEm = -1;
        ultimaFala = ultimoObjetivo = "";
        maiorParcial = 0;
        acoes.Clear();
        log.Clear();
        SaveGameLoader.selectedSlot = -1;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        log.AppendLine($"play iniciado na cena {SceneManager.GetActiveScene().name} ({DateTime.Now:HH:mm:ss})");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            if (errors <= 12) log.AppendLine($"  !! {type}: {message}\n     {stack.Split('\n').FirstOrDefault()}");
        }
    }

    // ------------------------------------------------------------------ util

    private static void Check(bool ok, string oque)
    {
        if (!ok) falhas++;
        log.AppendLine($"  [{(ok ? "OK" : "FALHOU")}] {oque}");
    }

    private static T Campo<T>(object o, string nome)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(nome, Priv);
            if (f != null) return (T)f.GetValue(o);
        }
        return default;
    }

    private static string Texto(MonoBehaviour mb) => mb == null ? "" : Campo<TMP_Text>(mb, "texto")?.text ?? "";

    private static ItemColetavel Item(string id) =>
        Object.FindObjectsByType<ItemColetavel>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(i => Campo<string>(i, "itemId") == id && i.gameObject.activeInHierarchy);

    private static T Um<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    private static int Qtd(string id) => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(id) : -1;

    private static string Stats()
    {
        var s = PlayerStats.Instance;
        if (s == null) return "sem PlayerStats";
        return $"vida {s.vida.Fracao:0.00} fome {s.fome.Fracao:0.00} sede {s.sede.Fracao:0.00} higiene {s.higiene.Fracao:0.00} sanidade {s.sanidade.Fracao:0.00}";
    }

    private static void Usar(Interagivel alvo, string nome)
    {
        if (alvo == null) { Check(false, $"existe: {nome}"); return; }
        log.AppendLine($"> {nome}: \"{alvo.Texto}\"");
        alvo.Interagir();
    }

    private static void Teleportar(Vector3 pos)
    {
        var p = GameObject.FindWithTag("Player");
        if (p == null) return;
        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Procura um lugar onde o jogador consiga ficar em pé e, olhando dali,
    /// o raio do PlayerInteractor (2,8 m) acerte este objeto antes de qualquer outro.
    /// </summary>
    private static bool Alcancavel(Interagivel alvo, out Vector3 ondeFicar)
    {
        ondeFicar = Vector3.zero;
        Collider col = alvo.GetComponent<Collider>();
        if (col == null) col = alvo.GetComponentInChildren<Collider>(); // "??" não enxerga o nulo do Unity
        if (col == null) return false;
        var c = col.bounds.center;
        foreach (float raio in new[] { 1.0f, 1.5f, 2.0f })
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI * 2f / 16f;
                var xz = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * raio;
                foreach (float alto in new[] { 1.2f, 2.5f })
                {
                    if (!Physics.Raycast(new Vector3(xz.x, c.y + alto, xz.z), Vector3.down, out var chao, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (chao.normal.y < 0.7f) continue;
                    var pe = chao.point;
                    if (Physics.CheckCapsule(pe + Vector3.up * 0.45f, pe + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var olho = pe + Vector3.up * 1.45f;
                    var dir = c - olho;
                    if (dir.magnitude > 2.7f) continue;
                    if (Physics.Raycast(olho, dir.normalized, out var hit, 2.8f, ~0, QueryTriggerInteraction.Collide) &&
                        hit.collider.GetComponentInParent<Interagivel>() == alvo)
                    {
                        ondeFicar = pe;
                        return true;
                    }
                }
            }
        return false;
    }

    // ------------------------------------------------------------------ passos

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate(); // continua rodando mesmo com o Unity minimizado ou sem foco
        double now = EditorApplication.timeSinceStartup;
        double t = now - startTime;

        // registra falas e mudanças nos objetivos
        string fala = Texto(Narrador.Instance);
        if (fala != ultimaFala) { ultimaFala = fala; if (!string.IsNullOrWhiteSpace(fala)) log.AppendLine($"    fala: {fala.Replace("\n", " ")}"); }
        string obj = Texto(Objetivos.Instance);
        var textoFala = Narrador.Instance != null ? Campo<TMP_Text>(Narrador.Instance, "texto") : null;
        if (textoFala != null && textoFala.maxVisibleCharacters > 0 && textoFala.maxVisibleCharacters < textoFala.GetParsedText().Length)
            maiorParcial = Math.Max(maiorParcial, textoFala.maxVisibleCharacters);

        if (t > 600) { log.AppendLine($"TEMPO ESGOTADO (passo {step}, pausado={EditorApplication.isPaused}, Time.time={Time.time:0.0}, timeScale={Time.timeScale}, quadros={Time.frameCount}, carregando={LoadingScreen.IsLoading}, pausa={PauseMenu.IsPaused})"); Finish(); return; }

        switch (step)
        {
            case 0 when t > 0.6:
            {
                log.AppendLine("== cena ==");
                var player = GameObject.FindWithTag("Player");
                Check(player != null, "jogador com tag Player");
                Check(Um<FirstPersonController>() != null, "FirstPersonController");
                Check(Camera.main != null, "Camera.main");
                Check(PlayerStats.Instance != null, "PlayerStats (Sistema do Jogador)");
                Check(PlayerInventory.Instance != null, "PlayerInventory");
                Check(Um<PlayerInteractor>() != null, "PlayerInteractor");
                Check(Um<PauseMenu>() != null, "Menu de Pausa");
                Check(Um<GameSaveController>() != null && Um<GameSaveController>().player != null, "GameSaveController com jogador");
                Check(Um<PrologoController>() != null, "PrologoController");
                var acordar = Um<AcordarSequencia>();
                Check(acordar != null, "AcordarSequencia");
                var olhos = acordar != null ? Campo<CanvasGroup>(acordar, "olhos") : null;
                Check(olhos != null && olhos.alpha > 0.5f, $"pálpebras fechadas no início (alpha={olhos?.alpha:0.00})");
                Check(AcordarSequencia.Tontura > 0.9f, "tontura no início");
                if (player != null) log.AppendLine($"  jogador em {player.transform.position}");

                log.AppendLine("== alcance de cada ponto (raio do E, 2,8 m) ==");
                foreach (var i in Object.FindObjectsByType<Interagivel>(FindObjectsSortMode.None).OrderBy(i => i.name))
                {
                    bool ok = Alcancavel(i, out var onde);
                    Check(ok, $"{i.GetType().Name} \"{i.name}\"" + (ok ? $" (de pé em {onde.x:0.0}, {onde.y:0.00}, {onde.z:0.0})" : " NÃO ALCANÇÁVEL"));
                }
                step++;
                break;
            }
            case 1 when Time.time > 2.5f:
                log.AppendLine("== acordando ==");
                log.AppendLine("  " + Stats());
                Check(PlayerStats.Instance != null && PlayerStats.Instance.sede.Fracao < 0.3f, "acorda com sede");
                Check(Um<FirstPersonController>() != null && Um<FirstPersonController>().enabled, "câmera livre durante o despertar");
                Check(Um<FirstPersonController>() != null && Um<FirstPersonController>().MoveSpeed < 2.5f, "andando devagar (tonto)");
                Check(Camera.main != null && Mathf.Abs(Camera.main.fieldOfView - GameSettings.FieldOfView) < 0.5f,
                    $"campo de visão da opção aplicado ({Camera.main?.fieldOfView:0} / opção {GameSettings.FieldOfView:0})");
                step++;
                break;
            case 2 when GameFlags.Has(AcordarSequencia.Flag) || Time.time > 25f:
            {
                var fpc = Um<FirstPersonController>();
                Check(GameFlags.Has(AcordarSequencia.Flag), $"levantou (tempo de jogo {Time.time:0.0}s, real {t:0.0}s, {Time.frameCount / Math.Max(0.1, t):0} fps)");
                Check(fpc != null && fpc.enabled, "controle liberado");
                log.AppendLine($"  velocidade tonto: {fpc?.MoveSpeed:0.0}  tontura={AcordarSequencia.Tontura:0.00}");
                Check(obj.Contains("água"), "objetivo de achar água apareceu");
                Check(maiorParcial >= 5, $"legendas escritas letra a letra (vista parcial com {maiorParcial} letras)");
                Captura("prologo_play_1.png");
                MontarPuzzle();
                step++;
                nextFrame = Time.frameCount + 30;
                break;
            }
            case 3 when Time.frameCount >= nextFrame && (esperar == null || esperar() || Time.time > esperarAte):
                if (acoes.Count > 0)
                {
                    esperar = null;
                    try { acoes.Dequeue()(); }
                    catch (Exception e) { log.AppendLine("  !! exceção no teste: " + e.Message); falhas++; }
                    nextFrame = Time.frameCount + 12;
                }
                else step++;
                break;
            case 4:
                log.AppendLine("== objetivos no fim ==");
                foreach (var l in obj.Split('\n')) log.AppendLine("  | " + System.Text.RegularExpressions.Regex.Replace(l, "<.*?>", ""));
                Captura("prologo_play_2.png");
                log.AppendLine("== partida ==");
                Usar(Um<Regulador>(), "Regulador (puxar)");
                Check(GameFlags.Has(PrologoController.FlagPartiu), "trem partindo");
                partidaEm = now;
                step++;
                break;
            case 5 when SceneManager.GetActiveScene().name == "CenaPrincipal" && !LoadingScreen.IsLoading:
                Check(true, $"chegou na CenaPrincipal ({now - partidaEm:0.0}s depois de puxar a alavanca)");
                Check(GameFlags.Has("prologo.concluido"), "prólogo concluído");
                Check(PlayerStats.Instance != null, "necessidades continuam na CenaPrincipal");
                log.AppendLine("  " + Stats());
                Finish();
                break;
            case 5 when now - partidaEm > 120:
                Check(false, "chegar na CenaPrincipal em 2 min");
                Finish();
                break;
        }
    }

    private static void MontarPuzzle()
    {
        var radio = Um<RadioDoTrem>();
        var exam = Um<ExaminarLocomotiva>();
        var valv = Um<ValvulaCaixaDagua>();
        var torneiras = Object.FindObjectsByType<Torneira>(FindObjectsSortMode.None);
        var beber = torneiras.FirstOrDefault(x => Campo<Torneira.Uso>(x, "uso") == Torneira.Uso.Beber);
        var lavar = torneiras.FirstOrDefault(x => Campo<Torneira.Uso>(x, "uso") == Torneira.Uso.Lavar);
        var forn = Um<Fornalha>();
        var reg = Um<Regulador>();
        var porta = Um<PortaInteragivel>();

        // ---- física do jogador (anda de verdade, pela entrada de movimento) ----
        const float plat = 0.9f;
        const float pe = 0.12f;      // o pivô do jogador fica ~12 cm acima dos pés
        const float chao = pe + 0.06f, naPlataforma = plat + pe;
        // contorno dos itens: de longe some, de perto aparece, mirando fica forte
        acoes.Enqueue(() =>
        {
            log.AppendLine("== contorno dos itens ==");
            var destaques = Object.FindObjectsByType<DestaqueItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int itens = Object.FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(i => i is ItemColetavel || i is Jornal || i is MonteDeCarvao);
            Check(destaques.Length == itens && itens > 0, $"destaque em todo item e jornal ({destaques.Length}/{itens})");
            Check(destaques.All(d => d.GetComponent<Outline>() != null), "todos com Outline");
            Teleportar(new Vector3(-12.3f, 0.9f + 0.05f, -3.5f));
            var p = GameObject.FindWithTag("Player");
            if (p != null) p.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            var fpc = Um<FirstPersonController>();
            typeof(FirstPersonController).GetField("_cinemachineTargetPitch", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(fpc, 32f);
            if (fpc != null && fpc.CinemachineCameraTarget != null) fpc.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);
            float fim = Time.time + 1.2f;
            Esperar(() => Time.time > fim, 4f);
        });
        acoes.Enqueue(() =>
        {
            var garrafa = Object.FindObjectsByType<ItemColetavel>(FindObjectsSortMode.None).FirstOrDefault(i => i.name == "Garrafa d'Agua");
            var o = garrafa != null ? garrafa.GetComponent<Outline>() : null;
            var longe = Um<MonteDeCarvao>();
            var ol = longe != null ? longe.GetComponent<Outline>() : null;
            log.AppendLine($"  mirando: {(PlayerInteractor.Atual != null ? PlayerInteractor.Atual.name : "nada")}");
            Check(o != null && o.OutlineColor.a > 0.4f && o.OutlineWidth > 0f, $"garrafa perto com contorno (alfa {o?.OutlineColor.a:0.00}, largura {o?.OutlineWidth:0.0})");
            Check(ol != null && ol.OutlineColor.a < 0.01f, $"carvão longe sem contorno (alfa {ol?.OutlineColor.a:0.00})");
            CapturaCamera("prologo_play_contorno.png");
            typeof(FirstPersonController).GetField("_cinemachineTargetPitch", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(Um<FirstPersonController>(), 0f);
            if (Um<FirstPersonController>()?.CinemachineCameraTarget != null) Um<FirstPersonController>().CinemachineCameraTarget.transform.localRotation = Quaternion.identity;
            float fim = Time.time + 0.3f;
            Esperar(() => Time.time > fim, 2f);
        });

        acoes.Enqueue(() => log.AppendLine("== física =="));
        Andar("cair do alto até o chão", new Vector3(16f, 3f, -9f), 0f, Vector2.zero, 1.5f,
            (a, b) => b.y < chao, (a, b) => $"y {a.y:0.00} → {b.y:0.00}");
        Andar("andar na plataforma sem travar", new Vector3(-28f, plat + 0.05f, -3f), 90f, Vector2.up, 2.5f,
            (a, b) => b.x - a.x > velocidadeTeste * 2.5f * 0.7f && Mathf.Abs(b.y - naPlataforma) < 0.04f,
            (a, b) => $"andou {b.x - a.x:0.00} m (esperado ~{velocidadeTeste * 2.5f:0.0}), altura {b.y:0.00}");
        Andar("descer a escada de trás", new Vector3(14f, plat + 0.05f, -5.4f), 180f, Vector2.up, 2.5f,
            (a, b) => b.y < chao && b.z < -7.3f, (a, b) => $"z {a.z:0.0} → {b.z:0.0}, y {a.y:0.00} → {b.y:0.00}");
        Andar("entrar no galpão", new Vector3(18f, 0.05f, -8.5f), 180f, Vector2.up, 2.5f,
            (a, b) => b.z < -10.8f && b.y < chao, (a, b) => $"z {a.z:0.0} → {b.z:0.0}, y {b.y:0.00}");
        Andar("descer da plataforma pela beirada", new Vector3(-6f, plat + 0.05f, -5.6f), 180f, Vector2.up, 1.5f,
            (a, b) => b.y < chao, (a, b) => $"y {a.y:0.00} → {b.y:0.00}");
        Andar("subir a escada da frente", new Vector3(25f, 0.05f, 0.4f), 180f, Vector2.up, 4f,
            (a, b) => Mathf.Abs(b.y - naPlataforma) < 0.04f && b.z < -2.2f, (a, b) => $"z {a.z:0.0} → {b.z:0.0}, y {b.y:0.00}",
            parar: q => q.z < -2.6f);

        acoes.Enqueue(() => { log.AppendLine("== rádio =="); if (radio != null) Teleportar(radio.transform.position + new Vector3(0f, -1f, -2.2f)); });
        acoes.Enqueue(() => Esperar(() => GameFlags.Has("prologo.radio_perto"), 30f));
        acoes.Enqueue(() => Check(GameFlags.Has("prologo.radio_perto"), "rádio ouvido de perto"));
        acoes.Enqueue(() => { Usar(radio, "Rádio"); Esperar(() => GameFlags.Has("prologo.radio"), 40f); });
        acoes.Enqueue(() => Check(GameFlags.Has("prologo.radio"), "rádio escutado até o fim"));
        acoes.Enqueue(() => { Usar(exam, "Examinar locomotiva"); Check(GameFlags.Has(ExaminarLocomotiva.Flag), "locomotiva examinada"); Esperar(() => Texto(Objetivos.Instance).Contains("caldeira"), 3f); });
        acoes.Enqueue(() => Check(Texto(Objetivos.Instance).Contains("caldeira"), "objetivos do trem apareceram"));

        acoes.Enqueue(() => { log.AppendLine("== água =="); float s0 = PlayerStats.Instance.sede.Fracao; Usar(beber, "Torneira (seca)"); Check(Mathf.Approximately(s0, PlayerStats.Instance.sede.Fracao), "torneira seca não dá água"); });
        acoes.Enqueue(() => { Usar(valv, "Válvula sem ferramenta"); Check(!GameFlags.Has(ValvulaCaixaDagua.FlagAberta) && GameFlags.Has("prologo.valvula_emperrada"), "válvula emperrada"); });
        acoes.Enqueue(() => { Usar(Item("chave_inglesa"), "Chave inglesa"); Check(Qtd("chave_inglesa") == 1, "pegou a chave inglesa"); });
        acoes.Enqueue(() => { Usar(valv, "Válvula com a chave"); Check(GameFlags.Has(ValvulaCaixaDagua.FlagAberta) && GameFlags.Has(ValvulaCaixaDagua.FlagCaldeira), "válvula aberta e caldeira cheia"); });
        acoes.Enqueue(() => { float s0 = PlayerStats.Instance.sede.Fracao; Usar(beber, "Torneira"); Check(PlayerStats.Instance.sede.Fracao > s0 + 0.2f && GameFlags.Has(PrologoController.FlagBebeu), $"bebeu (sede {s0:0.00} → {PlayerStats.Instance.sede.Fracao:0.00})"); });
        acoes.Enqueue(() => { float h0 = PlayerStats.Instance.higiene.Fracao; Usar(lavar, "Balde"); Check(PlayerStats.Instance.higiene.Fracao > h0, $"lavou o rosto (higiene {h0:0.00} → {PlayerStats.Instance.higiene.Fracao:0.00})"); });

        acoes.Enqueue(() => { log.AppendLine("== carvão =="); Usar(forn, "Fornalha vazia"); Check(Fornalha.CarvaoColocado == 0, "fornalha continua vazia"); });
        var monte = Um<MonteDeCarvao>();
        Check(monte != null, "monte de carvão na cena");
        string[] esperado = { "mais dois", "mais um", "Finalmente" };
        for (int k = 0; k < 3; k++)
        {
            int n = k + 1;
            string fala = esperado[k];
            acoes.Enqueue(() => { Usar(monte, $"Monte de carvão (pedra {n})"); Esperar(() => CarvaoNaMao.Visivel, 2f); });
            acoes.Enqueue(() => Check(Qtd("carvao") == 1 && CarvaoNaMao.Visivel, $"pedra {n} na mão (inventário {Qtd("carvao")}, visível {CarvaoNaMao.Visivel})"));
            if (k == 0)
            {
                acoes.Enqueue(() =>
                {
                    // olha para o monte, de dentro do galpão, para a captura da pedra na mão
                    Teleportar(new Vector3(20.4f, 0.05f, -10.6f));
                    var pl = GameObject.FindWithTag("Player");
                    if (pl != null) pl.transform.rotation = Quaternion.Euler(0f, 237f, 0f);
                    var f = Um<FirstPersonController>();
                    if (f != null && f.CinemachineCameraTarget != null) f.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
                    float fim = Time.time + 0.8f;
                    Esperar(() => Time.time > fim, 3f);
                });
                acoes.Enqueue(() =>
                {
                    CapturaCamera("prologo_play_carvao_na_mao.png");
                    var f = Um<FirstPersonController>();
                    if (f != null && f.CinemachineCameraTarget != null) f.CinemachineCameraTarget.transform.localRotation = Quaternion.identity;
                });
                acoes.Enqueue(() => { Usar(monte, "Monte de novo, com a mão cheia"); Check(Qtd("carvao") == 1, "só uma pedra por vez"); Esperar(() => Texto(Objetivos.Instance).Contains("na mão"), 3f); });
                acoes.Enqueue(() => Check(Texto(Objetivos.Instance).Contains("na mão"), "pista: pedra na mão"));
            }
            acoes.Enqueue(() => { Narrador.Calar(); Usar(forn, $"Fornalha ({n}/3)"); Check(Fornalha.CarvaoColocado == n && Qtd("carvao") == 0, $"{n}/3 na fornalha"); Esperar(() => Texto(Narrador.Instance).Contains(fala), 15f); });
            acoes.Enqueue(() => Check(Texto(Narrador.Instance).Contains(fala), $"fala: \"{fala}\""));
        }
        acoes.Enqueue(() => { Esperar(() => !CarvaoNaMao.Visivel, 2f); });
        acoes.Enqueue(() => Check(GameFlags.Has(Fornalha.FlagCarvao) && !CarvaoNaMao.Visivel && !monte.Disponivel, "carvão completo, mão vazia e monte encerrado"));
        acoes.Enqueue(() => { Usar(forn, "Fornalha sem fósforo"); Check(!GameFlags.Has(Fornalha.FlagFogo), "sem fósforo não acende"); });

        acoes.Enqueue(() => { log.AppendLine("== escritório =="); Usar(reg, "Regulador sem alavanca"); Check(!GameFlags.Has(Regulador.FlagAlavanca), "regulador sem alavanca"); });
        acoes.Enqueue(() => { Usar(porta, "Porta trancada"); Check(GameFlags.Has("prologo.escritorio_trancado") && !GameFlags.Has("prologo.escritorio_aberto"), "porta trancada"); Esperar(() => Texto(Objetivos.Instance).Contains("chave"), 3f); });
        acoes.Enqueue(() => Check(Texto(Objetivos.Instance).Contains("chave"), "pista: procurar a chave"));
        acoes.Enqueue(() => { Usar(Item("chave_escritorio"), "Chave do escritório"); Usar(porta, "Porta"); Check(GameFlags.Has("prologo.escritorio_aberto"), "porta aberta"); float fim = Time.time + 1.5f; Esperar(() => Time.time > fim, 5f); });
        acoes.Enqueue(() => { var f = Campo<Transform>(porta, "folha"); Check(f != null && Quaternion.Angle(f.localRotation, Quaternion.identity) > 80f, "folha da porta girou"); });
        foreach (var id in new[] { "alavanca", "fosforos", "soda", "lata_feijao", "agua" })
            acoes.Enqueue(() => { Usar(Item(id), "Item " + id); Check(Qtd(id) >= 1, "pegou " + id); });

        acoes.Enqueue(() => { log.AppendLine("== trem pronto =="); Usar(reg, "Regulador"); Check(GameFlags.Has(Regulador.FlagAlavanca) && Qtd("alavanca") == 0, "alavanca encaixada"); });
        acoes.Enqueue(() =>
        {
            Usar(forn, "Fornalha");
            Check(GameFlags.Has(Fornalha.FlagFogo), "fornalha acesa");
            var luz = Campo<Light>(forn, "luzDoFogo"); var fumaca = Campo<GameObject>(forn, "fumacaChamine");
            Check(luz != null && luz.enabled && fumaca != null && fumaca.activeSelf, "fogo e fumaça visíveis");
        });
        acoes.Enqueue(() => Check(PrologoController.TremPronto, "trem pronto"));

        acoes.Enqueue(() => log.AppendLine("== jornais =="));
        foreach (var j in Object.FindObjectsByType<Jornal>(FindObjectsSortMode.None).OrderBy(j => j.name))
        {
            acoes.Enqueue(() => { Usar(j, j.name); Check(Leitura.Aberta && Time.timeScale == 0f, "jornal aberto (jogo parado)"); });
            acoes.Enqueue(() => { Leitura.Instance.Fechar(); });
            acoes.Enqueue(() => Check(!Leitura.Aberta && Time.timeScale > 0f, "jornal fechado"));
        }
        acoes.Enqueue(() => Esperar(() => GameFlags.Has(PrologoController.FlagJornais), 2f));
        acoes.Enqueue(() => Check(GameFlags.Has(PrologoController.FlagJornais), "leu os 5 jornais"));

        acoes.Enqueue(() =>
        {
            log.AppendLine("== necessidades ==");
            float f0 = PlayerStats.Instance.fome.Fracao;
            var c = PlayerStats.Instance.ConsumirDoInventario(false);
            Check(c != null && PlayerStats.Instance.fome.Fracao > f0, $"comeu {c?.itemId} (fome {f0:0.00} → {PlayerStats.Instance.fome.Fracao:0.00})");
            float s0 = PlayerStats.Instance.sede.Fracao;
            var b = PlayerStats.Instance.ConsumirDoInventario(true);
            Check(b != null, $"bebeu {b?.itemId} (sede {s0:0.00} → {PlayerStats.Instance.sede.Fracao:0.00})");
            float h0 = PlayerStats.Instance.higiene.Fracao;
            Usar(Um<Banheiro>(), "Banheiro");
            Check(PlayerStats.Instance.higiene.Fracao > h0, "usou o banheiro");
            log.AppendLine("  " + Stats());
        });

        acoes.Enqueue(() =>
        {
            log.AppendLine("== save (em memória) ==");
            var d = new SaveData();
            SaveSnapshot.Capturar(d);
            var antes = GameFlags.ToList().OrderBy(x => x).ToList();
            log.AppendLine($"  cena={d.cena} flags={d.flags?.Count} itens={d.inventario?.Count} sede={d.sede:0.00}");
            GameFlags.Clear();
            SaveSnapshot.Aplicar(d);
            var depois = GameFlags.ToList().OrderBy(x => x).ToList();
            Check(d.cena == "Prologo", "save guarda a cena do prólogo");
            Check(antes.SequenceEqual(depois), $"marcas restauradas ({depois.Count})");
        });
    }

    private static Vector3 inicioAndar;
    private static float velocidadeTeste;

    /// <summary>Coloca o jogador num ponto, segura a direção de movimento por um tempo e confere onde ele parou.</summary>
    private static void Andar(string nome, Vector3 ondeComeca, float yaw, Vector2 direcao, float segundos,
        Func<Vector3, Vector3, bool> passou, Func<Vector3, Vector3, string> detalhe, Func<Vector3, bool> parar = null)
    {
        acoes.Enqueue(() =>
        {
            var p = GameObject.FindWithTag("Player");
            Teleportar(ondeComeca);
            if (p != null) p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float fim = Time.time + 0.4f; // assenta no chão
            Esperar(() => Time.time > fim, 2f);
        });
        acoes.Enqueue(() =>
        {
            var p = GameObject.FindWithTag("Player");
            var input = p != null ? p.GetComponent<StarterAssetsInputs>() : null;
            var fpc = p != null ? p.GetComponent<FirstPersonController>() : null;
            velocidadeTeste = fpc != null ? fpc.MoveSpeed : 4f;
            inicioAndar = p != null ? p.transform.position : Vector3.zero;
            if (input != null) { input.move = direcao; input.sprint = false; }
            float fim = Time.time + segundos;
            Esperar(() => Time.time > fim || (parar != null && p != null && parar(p.transform.position)), segundos + 2f);
        });
        acoes.Enqueue(() =>
        {
            var p = GameObject.FindWithTag("Player");
            var input = p != null ? p.GetComponent<StarterAssetsInputs>() : null;
            if (input != null) input.move = Vector2.zero;
            var agora = p != null ? p.transform.position : Vector3.zero;
            Check(p != null && passou(inicioAndar, agora), $"{nome}: {detalhe(inicioAndar, agora)}");
        });
    }

    private static void Esperar(Func<bool> cond, float segundosDeJogo)
    {
        esperar = cond;
        esperarAte = Time.time + segundosDeJogo;
    }

    // Renderiza a câmera do jogador direto numa imagem (a Game View não redesenha com o Unity em segundo plano).
    private static void CapturaCamera(string nome)
    {
        var cam = Camera.main;
        if (cam == null) { log.AppendLine("  captura falhou: sem câmera"); return; }
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var antes = cam.targetTexture;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(Folder, nome), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine("  captura: " + nome);
        }
        catch (Exception e) { log.AppendLine("  captura falhou: " + e.Message); }
        finally { cam.targetTexture = antes; Object.DestroyImmediate(rt); }
    }

    private static void Captura(string nome)
    {
        try { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, nome)); log.AppendLine("  captura: " + nome); }
        catch (Exception e) { log.AppendLine("  captura falhou: " + e.Message); }
    }

    private static void Finish()
    {
        log.AppendLine($"== fim: {falhas} falha(s), {errors} erro(s) no console ==");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "prologotest.txt"), log.ToString());
        step = 99;
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }
}
