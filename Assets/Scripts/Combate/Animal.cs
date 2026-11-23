using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Bicho do mato. Passivo (cervo) foge do Cândido e de barulho; agressivo (lobo, urso) persegue
/// e ataca; territorial avança só se chegarem perto demais. Anda no terreno, desvia do que é
/// sólido, toca as animações do próprio modelo e, morto, vira carcaça com carne.
/// </summary>
public class Animal : MonoBehaviour, IAtingivel
{
    public enum Comportamento { Passivo, Agressivo, Territorial }
    private enum Estado { Vagando, Parado, Fugindo, Perseguindo, Atacando, Morto }

    public string especie = "cervo";
    [Tooltip("Como aparece na causa da morte: \"ATACADO POR UM URSO\".")]
    public string causaMorte = "ATACADO POR UM ANIMAL";
    public Comportamento comportamento = Comportamento.Passivo;
    public float vida = 60f;
    public float velAndar = 1.4f, velCorrer = 7f;
    public float distVer = 18f;
    public float distAtaque = 1.9f;
    public float dano = 10f;
    public float intervaloAtaque = 1.2f;
    public int carne = 2;
    [Tooltip("Velocidade (m/s) em que as animações de andar e correr foram feitas.")]
    public float velAnimAndar = 1.4f, velAnimCorrer = 6f;

    [Header("Animações (do próprio modelo)")]
    public AnimationClip clipParado;
    public AnimationClip clipAndar;
    public AnimationClip clipCorrer;
    public AnimationClip clipAtacar;
    public AnimationClip clipFerido;
    public AnimationClip clipMorrer;

    public static readonly List<Animal> Todos = new List<Animal>();

    public bool Vivo => estado != Estado.Morto;
    public bool Hostil => comportamento != Comportamento.Passivo && (estado == Estado.Perseguindo || estado == Estado.Atacando);
    public Vector3 Centro => col != null ? col.bounds.center : transform.position + Vector3.up;

    private Estado estado = Estado.Vagando;
    private Transform jogador;
    private Vector3 destino;
    private float ate, proximoAtaque, vidaInicial;
    private Collider col;
    private Animator animator;
    private PlayableGraph grafo;
    private AnimationClipPlayable tocando;
    private AnimationClip clipAtual;
    private bool repetir;
    private float travadoDesde = -1f;
    private float giroDesvio;

    private void Awake()
    {
        col = GetComponent<Collider>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.runtimeAnimatorController = null; // quem toca é o grafo
        vidaInicial = vida;
    }

    private void OnEnable() => Todos.Add(this);
    private void OnDisable() { Todos.Remove(this); }
    private void OnDestroy() { if (grafo.IsValid()) grafo.Destroy(); }

    private void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) jogador = p.transform;
        NovoPasseio();
    }

    /// <summary>Tiro ou barulho alto perto: cervo foge, lobo/urso vem ver.</summary>
    public static void Barulho(Vector3 onde, float raio)
    {
        foreach (var a in Todos.ToArray())
        {
            if (a == null || !a.Vivo || (a.transform.position - onde).sqrMagnitude > raio * raio) continue;
            if (a.comportamento == Comportamento.Passivo) a.Fugir(onde, 9f);
            else if (a.comportamento == Comportamento.Agressivo) a.Perseguir();
        }
    }

    // ------------------------------------------------------------ IAtingivel

    public void Atingir(float danoRecebido, Vector3 origem, string arma)
    {
        if (!Vivo) return;
        vida -= danoRecebido;
        if (vida <= 0f) { Morrer(); return; }
        if (clipFerido != null) Tocar(clipFerido, false, 1f);
        ate = Time.time + 0.35f;
        if (comportamento == Comportamento.Passivo) Fugir(origem, 10f);
        else Perseguir();
    }

    // ------------------------------------------------------------ cérebro

    private void Update()
    {
        if (estado == Estado.Morto) { AnimarLoop(); return; }
        if (jogador == null) { var p = GameObject.FindWithTag("Player"); if (p != null) jogador = p.transform; else return; }

        Vector3 aoJogador = jogador.position - transform.position; aoJogador.y = 0f;
        float dist = aoJogador.magnitude;
        bool jogadorProtegido = Estrutura.AlgumaComJogador || TremInteragivel.ComJogador != null;
        bool jogadorMorto = PlayerStats.Instance != null && PlayerStats.Instance.Morto;

        // longe demais: some (o spawner põe outros)
        if (dist > 160f) { Destroy(gameObject); return; }

        switch (comportamento)
        {
            case Comportamento.Passivo:
                if (dist < distVer && estado != Estado.Fugindo) Fugir(jogador.position, 8f);
                break;
            case Comportamento.Agressivo:
                if (!jogadorProtegido && !jogadorMorto && dist < distVer && estado != Estado.Perseguindo && estado != Estado.Atacando) Perseguir();
                break;
            case Comportamento.Territorial:
                if (!jogadorProtegido && !jogadorMorto && dist < distVer * 0.55f && estado != Estado.Perseguindo && estado != Estado.Atacando) Perseguir();
                break;
        }

        switch (estado)
        {
            case Estado.Vagando:
                if (Andar(destino, velAndar) < 0.8f || Time.time > ate) { estado = Estado.Parado; ate = Time.time + Random.Range(3f, 8f); }
                break;
            case Estado.Parado:
                Tocar(clipParado, true, 1f);
                if (Time.time > ate) NovoPasseio();
                break;
            case Estado.Fugindo:
                Andar(destino, velCorrer);
                if (Time.time > ate) NovoPasseio();
                break;
            case Estado.Perseguindo:
                if (jogadorProtegido || jogadorMorto || dist > distVer * 2.2f) { NovoPasseio(); break; }
                ArmasDoJogador.MarcarCombate();
                if (dist <= distAtaque) { estado = Estado.Atacando; break; }
                Andar(jogador.position, velCorrer);
                break;
            case Estado.Atacando:
                if (jogadorProtegido || jogadorMorto) { NovoPasseio(); break; }
                ArmasDoJogador.MarcarCombate();
                Virar(aoJogador);
                if (dist > distAtaque * 1.35f) { estado = Estado.Perseguindo; break; }
                if (Time.time >= proximoAtaque)
                {
                    proximoAtaque = Time.time + intervaloAtaque;
                    Tocar(clipAtacar != null ? clipAtacar : clipParado, false, 1f);
                    ate = Time.time + 0.5f;
                    var stats = PlayerStats.Instance;
                    if (stats != null && !stats.Morto)
                    {
                        stats.Dano(dano, causaMorte);
                        ArmasDoJogador.MarcarCombate(dano);
                        EfeitosDeEstado.Golpe();
                    }
                }
                else if (Time.time > ate) Tocar(clipParado, true, 1f);
                break;
        }
        AnimarLoop();
    }

    private void NovoPasseio()
    {
        estado = Estado.Vagando;
        var r = Random.insideUnitCircle * 12f;
        destino = transform.position + new Vector3(r.x, 0f, r.y);
        ate = Time.time + 12f;
    }

    private void Fugir(Vector3 de, float duracao)
    {
        estado = Estado.Fugindo;
        Vector3 longe = transform.position - de; longe.y = 0f;
        if (longe.sqrMagnitude < 0.01f) longe = transform.forward;
        destino = transform.position + longe.normalized * 40f;
        ate = Time.time + duracao;
    }

    private void Perseguir()
    {
        if (estado == Estado.Morto) return;
        if (estado != Estado.Perseguindo && estado != Estado.Atacando && comportamento != Comportamento.Passivo && especie != "cervo")
            Narrador.Falar(especie == "urso" ? "Um urso... devagar. Devagar." : especie == "lobo" ? "Lobos!" : "Ele tá vindo!");
        estado = Estado.Perseguindo;
    }

    // anda até o ponto (no chão), desviando do que é sólido; devolve a distância que falta
    private float Andar(Vector3 alvo, float vel)
    {
        Vector3 d = alvo - transform.position; d.y = 0f;
        float dist = d.magnitude;
        if (Time.time < ate - 100f) return dist;
        if (dist < 0.3f) { Tocar(clipParado, true, 1f); return dist; }
        Vector3 dir = d / dist;
        if (giroDesvio != 0f) dir = Quaternion.Euler(0f, giroDesvio, 0f) * dir;

        // algo sólido na frente (parede, tronco, casa): tenta contornar
        Vector3 olho = transform.position + Vector3.up * 0.8f;
        float raioCorpo = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) : 0.4f;
        bool bloqueado = Physics.SphereCast(olho, Mathf.Max(0.2f, raioCorpo * 0.6f), dir, out var h, 1.4f, ~0, QueryTriggerInteraction.Ignore)
                         && !(h.collider is TerrainCollider) && !h.collider.transform.IsChildOf(transform) && (jogador == null || !h.collider.transform.IsChildOf(jogador));
        if (bloqueado)
        {
            if (travadoDesde < 0f) { travadoDesde = Time.time; giroDesvio = Random.value < 0.5f ? 70f : -70f; }
            else if (Time.time - travadoDesde > 1.2f) { giroDesvio = -giroDesvio; travadoDesde = Time.time; }
            dir = Quaternion.Euler(0f, giroDesvio, 0f) * (d / dist);
        }
        else if (travadoDesde >= 0f && Time.time - travadoDesde > 0.8f) { travadoDesde = -1f; giroDesvio = 0f; }

        Virar(dir);
        float passo = vel * Time.deltaTime;
        Vector3 novo = transform.position + transform.forward * passo;
        var ter = Terrain.activeTerrain;
        if (ter != null) novo.y = ter.SampleHeight(novo) + ter.transform.position.y;
        transform.position = novo;

        if (Time.time > ate - 100f || estado != Estado.Atacando)
        {
            bool correndo = vel > velAndar * 1.5f;
            var clip = correndo && clipCorrer != null ? clipCorrer : clipAndar;
            float animVel = correndo ? vel / Mathf.Max(0.1f, velAnimCorrer) : vel / Mathf.Max(0.1f, velAnimAndar);
            if (Time.time >= ate || estado != Estado.Atacando) Tocar(clip, true, Mathf.Clamp(animVel, 0.5f, 2f));
        }
        return dist;
    }

    private void Virar(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 360f * Time.deltaTime);
    }

    private void Morrer()
    {
        estado = Estado.Morto;
        vida = 0f;
        if (clipMorrer != null) Tocar(clipMorrer, false, 1f);
        else StartCoroutine(Tombar());
        if (comportamento == Comportamento.Passivo && PlayerStats.Instance != null) PlayerStats.Instance.Acalmar(-2f);
        // vira carcaça: dá carne
        if (carne > 0)
        {
            var c = gameObject.AddComponent<Carcaca>();
            c.carne = carne;
            c.especie = especie;
        }
        if (col != null) col.isTrigger = true;
        Narrador.Falar(especie == "cervo" ? "Desculpa, bicho. Eu preciso comer." : especie == "urso" ? "Acabou... acabou." : "Pronto. Pronto...");
    }

    private System.Collections.IEnumerator Tombar()
    {
        var de = transform.rotation;
        var para = de * Quaternion.Euler(0f, 0f, 85f);
        for (float t = 0f; t < 1f; t += Time.deltaTime * 2.2f)
        {
            transform.rotation = Quaternion.Slerp(de, para, t);
            yield return null;
        }
        transform.rotation = para;
        if (grafo.IsValid()) grafo.Stop();
    }

    // ------------------------------------------------------------ animação (Playables: toca qualquer clipe do modelo)

    private void Tocar(AnimationClip clip, bool emLoop, float velocidade)
    {
        if (clip == null || animator == null) return;
        if (clip == clipAtual && tocando.IsValid()) { tocando.SetSpeed(velocidade); repetir = emLoop; return; }
        if (!grafo.IsValid())
        {
            grafo = PlayableGraph.Create("Animal " + name);
            grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        }
        var saida = AnimationPlayableOutput.Create(grafo, "saida", animator);
        if (tocando.IsValid()) tocando.Destroy();
        tocando = AnimationClipPlayable.Create(grafo, clip);
        tocando.SetSpeed(velocidade);
        saida.SetSourcePlayable(tocando);
        grafo.Play();
        clipAtual = clip;
        repetir = emLoop;
    }

    private void AnimarLoop()
    {
        if (!tocando.IsValid() || clipAtual == null) return;
        double t = tocando.GetTime();
        if (t >= clipAtual.length)
        {
            if (repetir) tocando.SetTime(t % clipAtual.length);
            else if (estado != Estado.Morto) { /* fica no último quadro até a próxima animação */ }
        }
    }
}

/// <summary>Animal morto: dá para pegar a carne (uma vez).</summary>
public class Carcaca : Interagivel
{
    public int carne = 2;
    public string especie = "cervo";
    private bool pegou;

    public override string Texto => $"PEGAR CARNE ({carne})";
    public override bool Disponivel => base.Disponivel && !pegou;

    private void Awake() => acao = "PEGAR CARNE";

    public override void Interagir()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        if (!inv.AddItem("carne_crua", carne)) { Narrador.Falar("Não cabe mais nada. Preciso de espaço."); return; }
        pegou = true;
        Narrador.Falar(carne > 1 ? $"{carne} pedaços de carne. Melhor assar antes." : "Um pedaço de carne.");
        Destroy(gameObject, 20f);
    }
}
