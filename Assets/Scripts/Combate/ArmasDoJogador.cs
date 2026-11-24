using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Qualquer coisa que leva golpe ou tiro (animais, a árvore caída nos trilhos).</summary>
public interface IAtingivel
{
    void Atingir(float dano, Vector3 origem, string arma);
    bool Vivo { get; }
}

/// <summary>
/// Armas do Cândido: mãos (soco), faca, machado e rifle de caça. 1-4 trocam, botão esquerdo ataca,
/// R recarrega o rifle. Corpo a corpo gasta estamina; o rifle gasta munição e faz barulho.
/// Em primeira pessoa a arma fica na mão esquerda (a direita segura a lanterna); na isométrica
/// o golpe/tiro vai para onde o mouse aponta. Depois de um combate, a sanidade cai.
/// </summary>
[DefaultExecutionOrder(95)]
public class ArmasDoJogador : MonoBehaviour
{
    public static ArmasDoJogador Instance { get; private set; }

    [Header("Modelos (cópias do kit de sobrevivência)")]
    public GameObject modeloFaca;
    public GameObject modeloMachado;
    public GameObject modeloRifle;
    public TMP_FontAsset fonte;

    [Header("Rifle")]
    public int capacidadePente = 5;
    public float tempoRecarga = 2.2f;

    [Header("Combate e sanidade")]
    [Tooltip("Segundos sem ameaça para o combate acabar.")]
    public float fimDoCombate = 10f;
    public float sanidadePorCombate = 6f;
    public float sanidadePorDano = 0.25f;

    /// <summary>Id da arma na mão ("" = mãos).</summary>
    public string Equipada { get; private set; } = "";
    public int NoPente { get; private set; }
    public bool Recarregando => Time.time < fimRecarga;

    private struct Dados { public float dano, alcance, estamina, intervalo; public bool tiro; public string nome; }
    private static readonly Dictionary<string, Dados> Armas = new Dictionary<string, Dados>
    {
        { "",        new Dados { nome = "MÃOS",   dano = 8f,  alcance = 1.8f, estamina = 14f, intervalo = 0.6f } },
        { "faca",    new Dados { nome = "FACA",   dano = 24f, alcance = 1.9f, estamina = 9f,  intervalo = 0.5f } },
        { "machado", new Dados { nome = "MACHADO", dano = 42f, alcance = 2.2f, estamina = 18f, intervalo = 0.95f } },
        { "rifle",   new Dados { nome = "RIFLE",  dano = 75f, alcance = 120f, estamina = 0f,  intervalo = 1.1f, tiro = true } },
    };

    // combate (para a sanidade)
    private static float ultimoCombate = -999f;
    private static float danoNoCombate;
    private static bool emCombate;

    /// <summary>Chamado por animais que atacam/perseguem e quando o Cândido acerta algo.</summary>
    public static void MarcarCombate(float danoRecebido = 0f)
    {
        ultimoCombate = Time.time;
        danoNoCombate += danoRecebido;
        emCombate = true;
    }

    public static bool EmCombate => emCombate;

    private float proximoAtaque, fimRecarga = -1f, fimGolpe = -1f, avisouFolego = -99f;
    private Camera cam;
    private FirstPersonController fpc;
    private Animator animBracos;
    private PoseDoBraco poseEsquerda;
    private GameObject naMao;          // modelo da arma em primeira pessoa
    private string modeloDe = null;
    private Vector3 posNaMao; private Quaternion rotNaMao;
    private float recuo;
    private Light clarao;
    private LineRenderer rastro;
    private float fimClarao, fimRastro;
    private AudioSource som;
    private AudioClip somTiro, somGolpe, somAcerto, somClique;
    private TMP_Text hud;

    private void Awake()
    {
        Instance = this;
        fpc = GetComponent<FirstPersonController>();
        som = gameObject.AddComponent<AudioSource>();
        som.playOnAwake = false; som.spatialBlend = 0f;
        somTiro = SomRuido("tiro", 0.45f, 9f, 0.9f, 0.55f);
        somGolpe = SomRuido("golpe", 0.18f, 18f, 0.35f, 0.1f);
        somAcerto = SomRuido("acerto", 0.12f, 30f, 0.6f, 0.9f);
        somClique = SomRuido("clique", 0.03f, 120f, 0.3f, 0f);
        CriarEfeitos();
        MontarHud();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private bool Bloqueado()
    {
        return PauseMenu.IsPaused || Leitura.Aberta || MapaDoMundo.Aberto || TelaMochila.Aberta || Desmaio.Ativo
            || TremInteragivel.ComJogador != null || TrainCameraView.Ativa
            || (PlayerStats.Instance != null && PlayerStats.Instance.Morto);
    }

    private bool MaosOcupadas => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity("carvao") > 0;

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        var inv = PlayerInventory.Instance;

        // largou/perdeu a arma
        if (Equipada != "" && (inv == null || inv.GetQuantity(Equipada) <= 0)) Equipar("");

        AtualizarCombate();
        AtualizarHud();

        if (Bloqueado()) return;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) Equipar("");
            if (kb.digit2Key.wasPressedThisFrame) TentarEquipar("faca");
            if (kb.digit3Key.wasPressedThisFrame) TentarEquipar("machado");
            if (kb.digit4Key.wasPressedThisFrame) TentarEquipar("rifle");
            if (kb.rKey.wasPressedThisFrame) Recarregar();
            if (kb.qKey.wasPressedThisFrame) Largar();
        }
        if (Recarregando) return;
        if (Recarregou()) { }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= proximoAtaque) Atacar();
    }

    private void TentarEquipar(string id)
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity(id) > 0) Equipar(Equipada == id ? "" : id);
        else Narrador.Falar($"Não tenho {(id == "rifle" ? "um rifle" : id == "machado" ? "um machado" : "uma faca")}.");
    }

    public void Equipar(string id)
    {
        id = id ?? "";
        if (id != "" && !Armas.ContainsKey(id)) return;
        Equipada = id;
        fimRecarga = -1f;
        if (som != null) som.PlayOneShot(somClique, 0.5f);
    }

    // ------------------------------------------------------------ largar (Q)

    /// <summary>Larga no chão o que está na mão: primeiro a pedra de carvão, depois a arma.</summary>
    public void Largar()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        if (inv.GetQuantity("carvao") > 0)
        {
            var monte = FindFirstObjectByType<MonteDeCarvao>();
            var modelo = monte != null ? monte.GetComponentInChildren<Renderer>() : null;
            if (!inv.RemoveItem("carvao", 1)) return;
            var pedra = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedra.name = "Carvão largado";
            Destroy(pedra.GetComponent<Collider>());
            if (modelo != null)
            {
                var mf = modelo.GetComponent<MeshFilter>();
                if (mf != null) pedra.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                pedra.GetComponent<Renderer>().sharedMaterial = modelo.sharedMaterial;
            }
            pedra.transform.localScale = Vector3.one * 0.14f;
            NoChao(pedra, "carvao", "PEGAR CARVÃO");
            return;
        }
        if (Equipada != "")
        {
            string id = Equipada;
            GameObject fonte = id == "faca" ? modeloFaca : id == "machado" ? modeloMachado : modeloRifle;
            Equipar("");
            if (!inv.RemoveItem(id, 1)) return;
            if (fonte == null) return;
            var arma = Instantiate(fonte);
            arma.name = "Arma largada: " + id;
            arma.SetActive(true);
            foreach (var c in arma.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var mb in arma.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            NoChao(arma, id, "PEGAR " + Itens.Nome(id).ToUpperInvariant());
        }
    }

    // põe o objeto no chão na frente do Cândido e deixa pegar de novo
    private void NoChao(GameObject go, string id, string acao)
    {
        Vector3 frente = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 p = transform.position + frente * 0.9f + Vector3.up * 1.2f;
        if (Physics.Raycast(p, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore)) p = h.point;
        else p = transform.position + frente * 0.9f;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * go.transform.rotation;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y + 0.01f, p.z - b.center.z);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = go.transform.InverseTransformPoint(b.center);
            var tam = go.transform.InverseTransformVector(b.size);
            float minimo = 0.35f / Mathf.Max(0.01f, go.transform.lossyScale.x);
            bc.size = new Vector3(Mathf.Max(Mathf.Abs(tam.x), minimo), Mathf.Max(Mathf.Abs(tam.y), minimo), Mathf.Max(Mathf.Abs(tam.z), minimo));
        }
        else go.transform.position = p;
        var item = go.AddComponent<ItemColetavel>();
        item.Configurar(id, 1, acao);
        if (som != null) som.PlayOneShot(somGolpe, 0.4f);
    }

    // ------------------------------------------------------------ ataque

    public void Atacar()
    {
        var d = Armas[Equipada];
        proximoAtaque = Time.time + d.intervalo;
        if (MaosOcupadas) { Narrador.Falar("Mãos ocupadas com o carvão."); return; }

        bool iso = ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        Vector3 origem, dir;
        if (!Mirar(iso, out origem, out dir)) return;

        if (d.tiro)
        {
            if (NoPente <= 0)
            {
                som.PlayOneShot(somClique);
                if (MunicaoNaMochila() > 0) Recarregar();
                else Narrador.Falar("Sem munição.");
                return;
            }
            NoPente--;
            som.PlayOneShot(somTiro, 0.9f);
            recuo = 1f;
            Animal.Barulho(transform.position, 70f);
            Vector3 fim = origem + dir * d.alcance;
            if (Physics.Raycast(origem, dir, out var hit, d.alcance, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            {
                fim = hit.point;
                var alvo = hit.collider.GetComponentInParent<IAtingivel>();
                if (alvo != null && alvo.Vivo) { alvo.Atingir(d.dano, origem, Equipada); som.PlayOneShot(somAcerto, 0.7f); MarcarCombate(); }
            }
            Efeitos(PontaDaArma(iso, origem, dir), fim);
            return;
        }

        // corpo a corpo: gasta fôlego
        var j = fpc != null ? fpc : FindFirstObjectByType<FirstPersonController>();
        if (j != null && j.staminaAtual < d.estamina)
        {
            if (Time.time - avisouFolego > 3f) { avisouFolego = Time.time; Narrador.Falar("Sem fôlego..."); }
            return;
        }
        if (j != null) j.staminaAtual -= d.estamina;
        som.PlayOneShot(somGolpe, 0.8f);
        Golpear();

        IAtingivel melhor = null; float melhorDist = float.MaxValue;
        var cols = Physics.OverlapSphere(origem + dir * (d.alcance * 0.55f), d.alcance * 0.6f, ~0, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            if (c.transform.IsChildOf(transform)) continue;
            var alvo = c.GetComponentInParent<IAtingivel>();
            if (alvo == null || !alvo.Vivo) continue;
            Vector3 p = c.ClosestPoint(origem);
            float dist = Vector3.Distance(origem, p);
            if (dist > d.alcance + 0.3f) continue;
            if (Vector3.Dot((p - origem).normalized, dir) < 0.3f && dist > 0.6f) continue;
            if (dist < melhorDist) { melhorDist = dist; melhor = alvo; }
        }
        if (melhor != null)
        {
            melhor.Atingir(d.dano, origem, Equipada);
            som.PlayOneShot(somAcerto, 0.8f);
            if (!(melhor is EventoFerrovia)) MarcarCombate();
        }
    }

    /// <summary>Origem e direção do ataque. Na isométrica vira o Cândido para o mouse.</summary>
    private bool Mirar(bool iso, out Vector3 origem, out Vector3 dir)
    {
        origem = transform.position + Vector3.up * 1.3f; dir = transform.forward;
        if (cam == null) return false;
        if (!iso)
        {
            origem = cam.transform.position;
            dir = cam.transform.forward;
            return true;
        }
        var mouse = Mouse.current;
        if (mouse == null) return true;
        var raio = cam.ScreenPointToRay(mouse.position.ReadValue());
        Vector3 ponto;
        if (Physics.Raycast(raio, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform)) ponto = hit.point;
        else
        {
            var plano = new Plane(Vector3.up, transform.position);
            if (!plano.Raycast(raio, out float t)) return true;
            ponto = raio.GetPoint(t);
        }
        Vector3 h = ponto - transform.position; h.y = 0f;
        if (h.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(h.normalized, Vector3.up);
        // mira no corpo do bicho, não no chão
        var alvoBicho = hit.collider != null ? hit.collider.GetComponentInParent<Animal>() : null;
        Vector3 mira = alvoBicho != null ? alvoBicho.Centro : new Vector3(ponto.x, ponto.y + 0.8f, ponto.z);
        dir = (mira - origem).normalized;
        return true;
    }

    private void Recarregar()
    {
        if (Equipada != "rifle" || Recarregando || NoPente >= capacidadePente) return;
        if (MunicaoNaMochila() <= 0) { Narrador.Falar("Acabou a munição."); return; }
        fimRecarga = Time.time + tempoRecarga;
        som.PlayOneShot(somClique, 0.8f);
    }

    private bool Recarregou()
    {
        if (fimRecarga < 0f || Time.time < fimRecarga) return false;
        fimRecarga = -1f;
        var inv = PlayerInventory.Instance;
        int falta = capacidadePente - NoPente;
        int pega = Mathf.Min(falta, MunicaoNaMochila());
        if (pega > 0 && inv.RemoveItem("municao", pega)) NoPente += pega;
        som.PlayOneShot(somClique, 1f);
        return true;
    }

    private int MunicaoNaMochila() => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity("municao") : 0;

    // ------------------------------------------------------------ combate e sanidade

    private void AtualizarCombate()
    {
        if (!emCombate || Time.time - ultimoCombate < fimDoCombate) return;
        emCombate = false;
        var stats = PlayerStats.Instance;
        if (stats == null || stats.Morto) { danoNoCombate = 0f; return; }
        float perda = sanidadePorCombate + Mathf.Min(14f, danoNoCombate * sanidadePorDano);
        danoNoCombate = 0f;
        stats.Acalmar(-perda);
        string[] falas = { "Minhas mãos não param de tremer...", "Respira... já passou. Já passou.", "Isso não sai da cabeça.", "Eu não sou assim... ou sou?" };
        Narrador.Falar(falas[Random.Range(0, falas.Length)]);
    }

    // ------------------------------------------------------------ visual em primeira pessoa

    private void LateUpdate()
    {
        bool iso = ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        bool mostrar = !iso && Equipada != "" && !MaosOcupadas && !TrainCameraView.Ativa && TremInteragivel.ComJogador == null;
        if (modeloDe != Equipada || lanternaDoModelo != LanternaNaMao.TemLanterna) TrocarModelo();
        if (naMao != null) naMao.SetActive(mostrar);

        if (mostrar && naMao != null && cam != null)
        {
            if (Equipada == "rifle")
            {
                // rifle na altura do quadril, apontando para a frente; recua no tiro
                recuo = Mathf.MoveTowards(recuo, 0f, Time.deltaTime * 6f);
                float r = Recarregando ? 0.12f : 0f;
                naMao.transform.SetPositionAndRotation(cam.transform.TransformPoint(posNaMao + new Vector3(0f, -r, -0.08f * recuo)),
                    cam.transform.rotation * Quaternion.Euler(-8f * recuo + 25f * r, 0f, 0f) * rotNaMao);
            }
            else if (poseEsquerda != null)
            {
                // braço esquerdo segurando; no golpe o Animator mostra o soco
                float peso = Time.time < fimGolpe ? 0f : 1f;
                poseEsquerda.Aplicar(peso);
                naMao.transform.SetPositionAndRotation(poseEsquerda.Mao.TransformPoint(posNaMao), poseEsquerda.Mao.rotation * rotNaMao);
            }
        }

        if (clarao != null && clarao.enabled && Time.time > fimClarao) clarao.enabled = false;
        if (rastro != null && rastro.enabled && Time.time > fimRastro) rastro.enabled = false;
    }

    private void Golpear()
    {
        fimGolpe = Time.time + 0.45f;
        if (animBracos == null)
        {
            var mao = FindFirstObjectByType<MaoPrimeiraPessoa>();
            if (mao != null) animBracos = mao.GetComponentInChildren<Animator>();
        }
        if (animBracos != null && animBracos.isActiveAndEnabled)
        {
            animBracos.SetBool("UseRightHand", false); // a direita está com a lanterna
            animBracos.SetTrigger("Jab");
        }
    }

    private bool lanternaDoModelo;

    private void TrocarModelo()
    {
        modeloDe = Equipada;
        lanternaDoModelo = LanternaNaMao.TemLanterna;
        if (naMao != null) Destroy(naMao);
        naMao = null;
        GameObject fonte = Equipada == "faca" ? modeloFaca : Equipada == "machado" ? modeloMachado : Equipada == "rifle" ? modeloRifle : null;
        if (fonte == null || cam == null) return;

        naMao = new GameObject("Arma na Mão (" + Equipada + ")");
        foreach (var mf in fonte.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var p = new GameObject(mf.name);
            p.transform.SetParent(naMao.transform, false);
            p.transform.localPosition = fonte.transform.InverseTransformPoint(mf.transform.position);
            p.transform.localRotation = Quaternion.Inverse(fonte.transform.rotation) * mf.transform.rotation;
            p.transform.localScale = mf.transform.lossyScale;
            p.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = p.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // tamanho e direção: o eixo comprido vai para a frente
        float comprimento = Equipada == "faca" ? 0.22f : Equipada == "machado" ? 0.5f : 0.95f;
        bool pontaLarga = Equipada != "rifle"; // machado: a cabeça (larga) para a frente; faca: a lâmina; rifle: o cano fino
        Orientar(naMao.transform, comprimento, pontaLarga, out Vector3 centro, out Vector3 eixo, out float comp);

        if (Equipada == "rifle")
        {
            naMao.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            Orientar(naMao.transform, comprimento, pontaLarga, out centro, out eixo, out comp);
            naMao.transform.rotation = Quaternion.FromToRotation(eixo, cam.transform.forward) * naMao.transform.rotation;
            Orientar(naMao.transform, comprimento, pontaLarga, out centro, out eixo, out comp);
            // com a lanterna na direita, o rifle fica do lado esquerdo
            Vector3 alvo = cam.transform.TransformPoint(LanternaNaMao.TemLanterna ? new Vector3(-0.14f, -0.24f, 0.44f) : new Vector3(0.14f, -0.22f, 0.42f));
            naMao.transform.position += alvo - centro;
            posNaMao = cam.transform.InverseTransformPoint(naMao.transform.position);
            rotNaMao = Quaternion.Inverse(cam.transform.rotation) * naMao.transform.rotation;
            return;
        }

        // faca e machado: no punho esquerdo (pose de punho fechado)
        if (poseEsquerda == null)
        {
            var mao = FindFirstObjectByType<MaoPrimeiraPessoa>();
            if (mao != null) poseEsquerda = PoseDoBraco.Gravar(mao.transform, ".L", "guard_idle", 0.1f);
        }
        if (poseEsquerda == null) return;
        poseEsquerda.Aplicar();
        poseEsquerda.Punho(cam.transform.forward, out Vector3 punho, out Vector3 eixoPunho);
        // a lâmina aponta para cima e para a frente, saindo do punho
        // a lâmina sai do punho para a frente (não tampa a visão); o machado fica mais em pé
        Vector3 para = Equipada == "machado"
            ? (cam.transform.up * 0.65f + cam.transform.forward * 0.55f + cam.transform.right * 0.15f + eixoPunho * 0.15f).normalized
            : (cam.transform.forward * 0.75f + cam.transform.up * 0.5f + eixoPunho * 0.2f).normalized;
        naMao.transform.rotation = Quaternion.FromToRotation(eixo, para) * naMao.transform.rotation;
        Orientar(naMao.transform, comprimento, pontaLarga, out centro, out eixo, out comp);
        float cabo = Equipada == "machado" ? 0.32f : 0.22f; // o punho segura perto do fim do cabo
        naMao.transform.position += punho + eixo * (comp * cabo) - centro;
        posNaMao = poseEsquerda.Mao.InverseTransformPoint(naMao.transform.position);
        rotNaMao = Quaternion.Inverse(poseEsquerda.Mao.rotation) * naMao.transform.rotation;
    }

    // ajusta a escala para o comprimento e devolve centro, eixo comprido (para a ponta escolhida) e comprimento
    private static void Orientar(Transform t, float comprimento, bool pontaLarga, out Vector3 centro, out Vector3 eixo, out float comp)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        // eixo comprido no espaço local da arma (as peças estão em pé de igual para igual)
        var mfs = t.GetComponentsInChildren<MeshFilter>();
        Bounds lb = new Bounds(t.InverseTransformPoint(b.center), Vector3.zero);
        foreach (var mf in mfs)
        {
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
                lb.Encapsulate(t.InverseTransformPoint(mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))));
        }
        var s = lb.size;
        Vector3 eixoLocal = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
        float tamLocal = Vector3.Dot(s, eixoLocal);
        if (tamLocal > 1e-4f && comprimento > 0f)
        {
            float atual = t.TransformVector(eixoLocal * tamLocal).magnitude;
            t.localScale *= comprimento / Mathf.Max(1e-4f, atual);
        }
        // qual ponta é a larga: compara a grossura dos vértices perto de cada ponta
        float maisLargo = 0f, menosLargo = 0f;
        foreach (var mf in mfs)
        {
            var m = mf.sharedMesh;
            Vector3[] vs;
            try { vs = m.vertices; } catch { continue; }
            foreach (var v in vs)
            {
                Vector3 lp = t.InverseTransformPoint(mf.transform.TransformPoint(v));
                float u = (Vector3.Dot(lp - lb.min, eixoLocal)) / Mathf.Max(1e-4f, tamLocal);
                Vector3 resto = lp - lb.center; resto -= Vector3.Project(resto, eixoLocal);
                if (u > 0.8f) maisLargo = Mathf.Max(maisLargo, resto.magnitude);
                if (u < 0.2f) menosLargo = Mathf.Max(menosLargo, resto.magnitude);
            }
        }
        bool positivoLargo = maisLargo >= menosLargo;
        if (positivoLargo != pontaLarga) eixoLocal = -eixoLocal;
        rs = t.GetComponentsInChildren<Renderer>();
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        centro = b.center;
        eixo = t.TransformDirection(eixoLocal).normalized;
        comp = comprimento;
    }

    private Vector3 PontaDaArma(bool iso, Vector3 origem, Vector3 dir)
    {
        if (!iso && naMao != null && naMao.activeSelf)
        {
            var rs = naMao.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.center + dir * (b.extents.magnitude * 0.9f);
        }
        return iso ? transform.position + Vector3.up * 1.3f + dir * 0.6f : origem + dir * 0.6f;
    }

    private void CriarEfeitos()
    {
        var go = new GameObject("Clarão do Tiro");
        go.transform.SetParent(transform, false);
        clarao = go.AddComponent<Light>();
        clarao.type = LightType.Point; clarao.range = 9f; clarao.intensity = 30f;
        clarao.color = new Color(1f, 0.78f, 0.45f); clarao.enabled = false;
        var lr = new GameObject("Rastro do Tiro");
        lr.transform.SetParent(transform, false);
        rastro = lr.AddComponent<LineRenderer>();
        rastro.positionCount = 2; rastro.widthMultiplier = 0.025f; rastro.useWorldSpace = true;
        rastro.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        rastro.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
        rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rastro.enabled = false;
    }

    private void Efeitos(Vector3 ponta, Vector3 fim)
    {
        clarao.transform.position = ponta; clarao.enabled = true; fimClarao = Time.time + 0.06f;
        rastro.SetPosition(0, ponta); rastro.SetPosition(1, fim); rastro.enabled = true; fimRastro = Time.time + 0.05f;
    }

    // sons feitos na hora (o projeto não tem arquivos de tiro/golpe): ruído com queda
    private static AudioClip SomRuido(string nome, float duracao, float queda, float volume, float grave)
    {
        int taxa = 22050, n = Mathf.CeilToInt(duracao * taxa);
        var dados = new float[n];
        var rnd = new System.Random(nome.GetHashCode());
        float filtro = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)taxa;
            float ruido = (float)(rnd.NextDouble() * 2 - 1);
            filtro = Mathf.Lerp(ruido, filtro, grave); // passa-baixa simples: mais grave
            dados[i] = filtro * volume * Mathf.Exp(-t * queda) * (grave > 0.5f ? 2.2f : 1f);
        }
        var clip = AudioClip.Create(nome, n, 1, taxa, false);
        clip.SetData(dados, 0);
        return clip;
    }

    // ------------------------------------------------------------ HUD da arma

    private void MontarHud()
    {
        var raiz = new GameObject("Arma (HUD)");
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 55;
        var escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f); escala.matchWidthOrHeight = 1f;
        var go = new GameObject("Texto", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(raiz.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 40f); rt.sizeDelta = new Vector2(520f, 70f);
        hud = go.AddComponent<TextMeshProUGUI>();
        if (fonte != null) hud.font = fonte;
        hud.fontSize = 26f; hud.alignment = TextAlignmentOptions.BottomRight;
        hud.color = new Color(0.93f, 0.86f, 0.7f); hud.raycastTarget = false;
    }

    private void AtualizarHud()
    {
        if (hud == null) return;
        bool some = TremInteragivel.ComJogador != null || TrainCameraView.Ativa || MapaDoMundo.Aberto;
        hud.alpha = some ? 0f : 1f;
        var d = Armas[Equipada];
        string extra = Equipada == "rifle" ? (Recarregando ? "  <color=#E8B04A>recarregando...</color>" : $"  {NoPente}/{capacidadePente}  <color=#B0A080>(+{MunicaoNaMochila()})</color>") : "";
        hud.text = $"<size=70%><color=#B0A080>[1-4] arma   [Q] largar   [Tab] mochila</color></size>\n<color=#E8B04A>{d.nome}</color>{extra}";
    }
}
