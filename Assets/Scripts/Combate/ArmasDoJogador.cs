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
    private bool guardouLanterna;
    public bool Recarregando => Time.time < fimRecarga;

    private struct Dados { public float dano, alcance, estamina, intervalo; public bool tiro; public string nome; }
    private static readonly Dictionary<string, Dados> Armas = new Dictionary<string, Dados>
    {
        { "",        new Dados { nome = "MÃOS",   dano = 8f,  alcance = 1.8f, estamina = 14f, intervalo = 0.6f } },
        { "faca",    new Dados { nome = "FACA",   dano = 24f, alcance = 1.9f, estamina = 9f,  intervalo = 0.5f } },
        { "machado", new Dados { nome = "MACHADO", dano = 42f, alcance = 2.2f, estamina = 18f, intervalo = 0.95f } },
        { "rifle",   new Dados { nome = "RIFLE",  dano = 75f, alcance = 120f, estamina = 0f,  intervalo = 1.1f, tiro = true } },
    };

    /// <summary>Números de uma arma (para a roda de armas).</summary>
    public static (float dano, float alcance, float intervalo, float estamina, bool tiro) Ficha(string id)
    {
        var d = Armas.TryGetValue(id ?? "", out var x) ? x : Armas[""];
        return (d.dano, d.alcance, d.intervalo, d.estamina, d.tiro);
    }

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
    private GameObject naMao;          // modelo da arma em primeira pessoa
    private string modeloDe = null;
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
        RodaDeArmas.Criar(this, fonte);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private bool Bloqueado()
    {
        return PauseMenu.IsPaused || Leitura.Aberta || MapaDoMundo.Aberto || TelaMochila.Aberta || Desmaio.Ativo || RodaDeArmas.Aberta
            || TremInteragivel.ComJogador != null || TrainCameraView.Ativa
            || (PlayerStats.Instance != null && PlayerStats.Instance.Morto);
    }

    // a arma e o soco são da mão esquerda: ocupada se ela estiver com carvão
    private bool MaosOcupadas => CarvaoNaMao.NaEsquerda;

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
        if (id != "" && CarvaoNaMao.NaEsquerda) { Narrador.Falar("A mão esquerda tá com o carvão. [Q] larga."); return; }
        if (DuasMaos(id) && CarvaoNaMao.Quantidade > 0) { Narrador.Falar("Preciso das duas mãos. [Q] larga o carvão."); return; }
        if (id != "" && !Armas.ContainsKey(id)) return;
        Equipada = id;
        // machado e rifle vão com as duas mãos: a lanterna vai para a mochila e volta depois
        if (DuasMaos(id) && LanternaNaMao.NaMaoAgora) { LanternaNaMao.Guardar(true); guardouLanterna = true; }
        else if (!DuasMaos(id) && guardouLanterna) { guardouLanterna = false; if (LanternaNaMao.Guardada) LanternaNaMao.Guardar(false); }
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
            return;
        }
        if (LanternaNaMao.NaMaoAgora)
        {
            // a lanterna também vai para o chão (copia só a malha: os scripts dela não vão junto)
            var src = FindFirstObjectByType<LanternaNaMao>();
            var mf = src != null ? src.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || !inv.RemoveItem("lanterna", 1)) return;
            var lt = new GameObject("Lanterna largada");
            lt.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            lt.transform.localScale = mf.transform.lossyScale;
            lt.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            lt.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
            NoChao(lt, "lanterna", "PEGAR LANTERNA");
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
    //
    // Os braços do rig não têm animação de arma: a arma é posta e animada por código (pose de
    // descanso, golpe, recuo, recarga, sacar) e as mãos agarram o cabo por IK (MaoNoCabo).
    // Machado e rifle: as duas mãos (a lanterna vai para a mochila). Faca: só a esquerda.

    /// <summary>Machado e rifle pedem as duas mãos.</summary>
    public static bool DuasMaos(string id) => id == "machado" || id == "rifle";
    public static bool DuasMaosOcupadas => Instance != null && DuasMaos(Instance.Equipada);

    private struct PoseArma
    {
        public Vector3 pos;    // no espaço da câmera
        public Quaternion rot; // leva (eixo, cima) da arma para a câmera
        public PoseArma(Vector3 pos, Vector3 eixo, Vector3 cima) { this.pos = pos; rot = Quaternion.LookRotation(eixo.normalized, cima.normalized); }
        // giro inteiro interpolado (sem misturar eixo e gume à parte, que dava voltas estranhas)
        public static PoseArma Lerp(PoseArma a, PoseArma b, float t) =>
            new PoseArma { pos = Vector3.LerpUnclamped(a.pos, b.pos, t), rot = Quaternion.Slerp(a.rot, b.rot, t) };
    }

    private GeometriaDaArma geo;
    private MaoNoCabo maoE, maoD;
    private Vector3 pivo, pegaE, pegaD;   // no espaço da arma
    private float raioE = 0.017f, raioD = 0.017f; // grossura do cabo onde cada mão pega (m)
    private float inicioSacar, inicioGolpe = -99f, passada;
    private CharacterController corpoCC;

    private static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float Sai(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    private static float Entra(float t) { t = Mathf.Clamp01(t); return t * t; }

    private void LateUpdate()
    {
        bool iso = ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        bool mostrar = !iso && Equipada != "" && !MaosOcupadas && !TrainCameraView.Ativa && TremInteragivel.ComJogador == null;
        if (modeloDe != Equipada) TrocarModelo();
        if (naMao != null) naMao.SetActive(mostrar);
        if (mostrar && naMao != null && cam != null && geo != null) Animar();

        if (clarao != null && clarao.enabled && Time.time > fimClarao) clarao.enabled = false;
        if (rastro != null && rastro.enabled && Time.time > fimRastro) rastro.enabled = false;
    }

    private PoseArma PoseDaArma()
    {
        float t = Time.time - inicioGolpe;
        if (Equipada == "machado")
        {
            // descanso: cabo na diagonal, cabeça em cima à direita, gume de lado (a lâmina aparece)
            // descanso (a pose escolhida): quase em pé no meio, mão esquerda embaixo, direita no meio do cabo
            var descanso = new PoseArma(new Vector3(0.065f, -0.1f, 0.48f), new Vector3(0.3f, 0.9f, 0.22f), new Vector3(-0.85f, 0.05f, 0.5f));
            // armado: machado erguido alto, na frente e à direita (as mãos não chegam perto do rosto)
            var armado = new PoseArma(new Vector3(0.18f, 0.02f, 0.47f), new Vector3(0.25f, 0.85f, -0.45f), new Vector3(-0.2f, 0.45f, 0.85f));
            // golpe na diagonal: do ombro direito para baixo à esquerda, a lâmina à mostra
            var golpe = new PoseArma(new Vector3(-0.06f, -0.07f, 0.45f), new Vector3(-0.75f, -0.15f, 0.65f), new Vector3(-0.55f, -0.8f, 0.2f));
            if (t < 0.16f) return PoseArma.Lerp(descanso, armado, Sai(t / 0.16f));
            if (t < 0.3f) return PoseArma.Lerp(armado, golpe, Entra((t - 0.16f) / 0.14f));
            if (t < 0.42f) return golpe;
            if (t < 0.85f) return PoseArma.Lerp(golpe, descanso, Suave((t - 0.42f) / 0.43f));
            return descanso;
        }
        if (Equipada == "faca")
        {
            // lâmina para cima e para dentro (aparece inteira); a estocada vai para a frente sem esconder a lâmina
            var descanso = new PoseArma(new Vector3(-0.12f, -0.13f, 0.42f), new Vector3(0.25f, 0.55f, 0.8f), new Vector3(-0.3f, -1f, 0.2f));
            // a estocada vai para a frente com a lâmina ainda inclinada (não some apontando para longe)
            var estocada = new PoseArma(new Vector3(-0.05f, -0.08f, 0.55f), new Vector3(0.45f, 0.55f, 0.7f), new Vector3(-0.3f, -1f, 0.1f));
            if (t < 0.09f) return PoseArma.Lerp(descanso, estocada, Sai(t / 0.09f));
            if (t < 0.15f) return estocada;
            if (t < 0.4f) return PoseArma.Lerp(estocada, descanso, Suave((t - 0.15f) / 0.25f));
            return descanso;
        }
        // rifle: na altura do peito, coronha no ombro direito
        // um pouco à direita e girado: aparece o lado do rifle e as duas mãos
        var pronto = new PoseArma(new Vector3(0.11f, -0.075f, 0.5f), new Vector3(-0.08f, 0.04f, 1f), new Vector3(-0.25f, 1f, 0f));
        recuo = Mathf.MoveTowards(recuo, 0f, Time.deltaTime * 4f);
        if (recuo > 0f)
        {
            float r = recuo * recuo;
            pronto.pos += new Vector3(0f, 0.02f, -0.07f) * r;
            pronto.rot = Quaternion.AngleAxis(-9f * r, Vector3.right) * pronto.rot;
        }
        if (Recarregando)
        {
            // abaixa e deita o rifle para o lado enquanto a mão esquerda mexe no ferrolho
            float tr = tempoRecarga - (fimRecarga - Time.time);
            float peso = Suave(tr / 0.3f) * (1f - Suave((tr - (tempoRecarga - 0.35f)) / 0.35f));
            var deitado = new PoseArma(new Vector3(0.06f, -0.08f, 0.43f), new Vector3(-0.25f, 0.25f, 1f), new Vector3(-0.9f, 0.4f, 0f));
            return PoseArma.Lerp(pronto, deitado, peso);
        }
        return pronto;
    }

    private void Animar()
    {
        var c = cam.transform;
        var pose = PoseDaArma();

        // sacar: sobe de baixo
        float s = Sai((Time.time - inicioSacar) / 0.4f);
        pose.pos += new Vector3(0f, -0.3f, -0.05f) * (1f - s);
        pose.rot = Quaternion.Slerp(Quaternion.AngleAxis(40f, Vector3.right) * pose.rot, pose.rot, s);

        // respira e balança ao andar
        if (corpoCC == null) corpoCC = GetComponent<CharacterController>();
        float vel = corpoCC != null ? new Vector2(corpoCC.velocity.x, corpoCC.velocity.z).magnitude : 0f;
        passada += Time.deltaTime * (2f + vel * 1.6f);
        float anda = Mathf.Clamp01(vel / 4f);
        pose.pos += new Vector3(Mathf.Sin(passada) * 0.012f * anda, -Mathf.Abs(Mathf.Cos(passada)) * 0.014f * anda + Mathf.Sin(Time.time * 1.7f) * 0.003f, 0f);

        var t = naMao.transform;
        t.rotation = c.rotation * pose.rot * Quaternion.Inverse(Quaternion.LookRotation(geo.Eixo, geo.Cima));
        t.position += c.TransformPoint(pose.pos) - t.TransformPoint(pivo);

        // mãos no cabo
        Vector3 eixoW = t.TransformDirection(geo.Eixo).normalized, cimaW = t.TransformDirection(geo.Cima).normalized;
        Vector3 baixo = -c.up, fora = c.right;
        if (Equipada == "rifle")
        {
            float tr = Recarregando ? tempoRecarga - (fimRecarga - Time.time) : -1f;
            Vector3 pe = pegaE;
            if (tr >= 0f)
            {
                // a esquerda vai até o ferrolho, puxa e empurra duas vezes, e volta
                float ida = Suave((tr - 0.25f) / 0.3f) * (1f - Suave((tr - (tempoRecarga - 0.5f)) / 0.3f));
                var ferrolho = geo.Ponto(0.5f, 0.95f) + geo.Eixo * (-0.06f * Mathf.Max(0f, Mathf.Sin((tr - 0.55f) * Mathf.PI * 2f / 0.6f)) * ida) / Mathf.Max(0.01f, t.lossyScale.x);
                pe = Vector3.Lerp(pegaE, ferrolho, ida);
            }
            // direita no pescoço da coronha (desce para trás); esquerda por baixo da telha, palma para cima
            maoD?.Segurar(t.TransformPoint(pegaD), -eixoW * 0.8f - cimaW * 0.6f, fora * 0.7f + cimaW * 0.4f - eixoW * 0.3f, Ombro(maoD) + baixo * 0.4f + fora * 0.25f, raioD);
            maoE?.Segurar(t.TransformPoint(pe), -eixoW, -cimaW - fora * 0.6f, Ombro(maoE) + baixo * 0.4f - fora * 0.3f, raioE);
        }
        else if (Equipada == "machado")
        {
            maoE?.Segurar(t.TransformPoint(pegaE), -eixoW, Ombro(maoE) - t.TransformPoint(pegaE), Ombro(maoE) + baixo * 0.45f - fora * 0.25f, raioE);
            maoD?.Segurar(t.TransformPoint(pegaD), -eixoW, Ombro(maoD) - t.TransformPoint(pegaD), Ombro(maoD) + baixo * 0.45f + fora * 0.25f, raioD);
        }
        else // faca
        {
            maoE?.Segurar(t.TransformPoint(pegaE), -eixoW, Ombro(maoE) - t.TransformPoint(pegaE) + baixo * 0.1f, Ombro(maoE) + baixo * 0.45f - fora * 0.25f, raioE);
        }
    }

    private static Vector3 Ombro(MaoNoCabo m) => m.Ombro.position;

    private void Golpear()
    {
        fimGolpe = Time.time + 0.45f;
        inicioGolpe = Time.time;
        if (Equipada != "") return; // as armas são animadas por código (PoseDaArma)
        if (animBracos == null)
        {
            var mao = FindFirstObjectByType<MaoPrimeiraPessoa>();
            if (mao != null) animBracos = mao.GetComponentInChildren<Animator>();
        }
        if (animBracos != null && animBracos.isActiveAndEnabled)
        {
            animBracos.SetBool("UseRightHand", !LanternaNaMao.MaoDireitaOcupada && !CarvaoNaMao.NaDireita);
            animBracos.SetTrigger("Jab");
        }
    }

    private void TrocarModelo()
    {
        modeloDe = Equipada;
        if (naMao != null) Destroy(naMao);
        naMao = null; geo = null;
        inicioSacar = Time.time; inicioGolpe = -99f;
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
        CameraDosBracos.PorNaCamada(naMao); // desenhada por cima do mundo: não entra nas paredes

        // tamanho e eixo (a arma ainda está sem giro: o eixo do mundo é o do objeto)
        float comprimento = Equipada == "faca" ? 0.3f : Equipada == "machado" ? 0.62f : 1f;
        bool pontaLarga = Equipada != "rifle"; // machado: a cabeça (larga) para a frente; rifle: o cano fino
        Orientar(naMao.transform, comprimento, pontaLarga, out _, out Vector3 eixo, out _);
        geo = GeometriaDaArma.Medir(naMao.transform, naMao.transform.InverseTransformDirection(eixo), Equipada != "rifle");

        // onde as mãos pegam (0 = fim do cabo, 1 = ponta)
        if (Equipada == "machado") { pegaE = geo.Ponto(0.06f); pegaD = geo.Ponto(0.36f); pivo = geo.Ponto(0.22f); }
        else if (Equipada == "faca") { pegaE = geo.Ponto(0.2f); pegaD = pegaE; pivo = pegaE; }
        else { pegaD = geo.Ponto(0.3f, 0.3f); pegaE = geo.Ponto(0.55f, 0.3f); pivo = geo.Ponto(0.45f); }

        // grossura do cabo em cada pegada; a arma vai presa na câmera (anda junto com os braços, sem atraso)
        float esc = naMao.transform.lossyScale.x;
        float uE = Equipada == "machado" ? 0.06f : Equipada == "faca" ? 0.2f : 0.55f, uD = Equipada == "machado" ? 0.36f : Equipada == "faca" ? 0.2f : 0.3f;
        raioE = Mathf.Clamp(geo.Raio(uE) * esc, 0.01f, 0.026f);
        raioD = Mathf.Clamp(geo.Raio(uD) * esc, 0.01f, 0.026f);
        naMao.transform.SetParent(cam.transform, true);

        if (maoE == null || maoD == null)
        {
            var bracos = FindFirstObjectByType<MaoPrimeiraPessoa>();
            if (bracos != null) { maoE = MaoNoCabo.Criar(bracos.transform, ".L"); maoD = MaoNoCabo.Criar(bracos.transform, ".R"); }
        }
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
        var lamina = System.Array.Find(mfs, m => m.name.Contains("Lamina"));
        if (lamina != null)
        {
            positivoLargo = Vector3.Dot(t.InverseTransformPoint(lamina.GetComponent<Renderer>().bounds.center) - lb.center, eixoLocal) > 0f;
            pontaLarga = true;
        }
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
        hud.text = $"<size=70%><color=#B0A080>[Tab] segurar: armas · toque: mochila   [Q] largar   [H] lanterna</color></size>\n<color=#E8B04A>{d.nome}</color>{extra}";
    }
}
