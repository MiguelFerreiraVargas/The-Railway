using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monte de carvão do galpão: dá uma pedra por vez (fica na mão do Cândido até ir para a fornalha).
/// O monte não some; deixa de aceitar o E quando a fornalha já tem carvão suficiente.
/// </summary>
public class MonteDeCarvao : Interagivel
{
    [SerializeField] private string itemId = "carvao";
    [SerializeField] [TextArea] private string falaPrimeira = "Carvão. Vou precisar de uns três desses.";
    [SerializeField] [TextArea] private string falaMaoCheia = "Uma pedra de cada vez... só tenho duas mãos.";
    [SerializeField] private AudioClip som;
    [Tooltip("Marca que encerra o monte (no prólogo: fornalha cheia). Vazio = nunca encerra por marca.")]
    [SerializeField] private string flagEncerra = Fornalha.FlagCarvao;
    [Tooltip("Pedras no monte (-1 = sem fim).")]
    [SerializeField] private int estoque = -1;
    [Tooltip("Marca única deste monte, para lembrar quantas pedras já saíram (save).")]
    [SerializeField] private string flagMonte = "";
    [Tooltip("Marca da primeira pedra pega (para a fala de primeira vez).")]
    [SerializeField] private string flagPegouAlgum = "prologo.carvao_pegou";
    [Tooltip("Avisa (fala do Cândido) quando o jogador passa perto e ainda tem carvão aqui.")]
    [SerializeField] private bool avisarPerto = false;
    [SerializeField] private float distanciaAviso = 35f;
    [SerializeField] [TextArea] private string falaPerto = "Ali! Um monte de carvão.";

    /// <summary>Todos os montes da cena (o mapa e o objetivo do trem usam).</summary>
    public static readonly List<MonteDeCarvao> Todos = new List<MonteDeCarvao>();

    /// <summary>Se ainda dá para pegar carvão aqui.</summary>
    public bool TemCarvao => (string.IsNullOrEmpty(flagEncerra) || !GameFlags.Has(flagEncerra)) && Restantes != 0;
    /// <summary>Pedras que ainda saem (-1 = sem fim).</summary>
    public int Pedras => Restantes;

    private bool avisou;
    private float proximaChecagem;

    private int NaMao => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(itemId) : 0;

    public override string Texto => NaMao >= CarvaoNaMao.Capacidade ? "MÃOS OCUPADAS" : acao;

    public override bool Disponivel => base.Disponivel && (string.IsNullOrEmpty(flagEncerra) || !GameFlags.Has(flagEncerra)) && Restantes != 0;

    // pedras que ainda saem do monte (o save lembra pelas marcas flagMonte.1, .2...)
    private int Restantes
    {
        get
        {
            if (estoque < 0) return -1;
            int tiradas = 0;
            if (!string.IsNullOrEmpty(flagMonte)) while (tiradas < estoque && GameFlags.Has($"{flagMonte}.{tiradas + 1}")) tiradas++;
            else tiradas = tiradasSemFlag;
            return Mathf.Max(0, estoque - tiradas);
        }
    }
    private int tiradasSemFlag;

    private void Reset()
    {
        acao = "PEGAR CARVÃO";
    }

    private void Awake()
    {
        GarantirColisor();
        CarvaoNaMao.Garantir(GetComponentInChildren<Renderer>());
    }

    private void OnEnable() => Todos.Add(this);
    private void OnDisable() => Todos.Remove(this);

    private void Update()
    {
        if (!avisarPerto || avisou || Time.time < proximaChecagem) return;
        proximaChecagem = Time.time + 0.5f;
        var cam = Camera.main;
        if (cam == null || !TemCarvao) return;
        if ((cam.transform.position - transform.position).sqrMagnitude > distanciaAviso * distanciaAviso) return;
        avisou = true;
        if (!string.IsNullOrEmpty(falaPerto)) Narrador.Falar(falaPerto);
    }

    /// <summary>O monte com carvão mais perto de um ponto (ou null).</summary>
    public static MonteDeCarvao MaisPerto(Vector3 ponto, out float distancia)
    {
        MonteDeCarvao melhor = null; distancia = float.MaxValue;
        foreach (var m in Todos)
        {
            if (m == null || !m.TemCarvao) continue;
            float d = Vector3.Distance(ponto, m.transform.position);
            if (d < distancia) { distancia = d; melhor = m; }
        }
        return melhor;
    }

    public override void Interagir()
    {
        if (NaMao >= CarvaoNaMao.Capacidade)
        {
            Narrador.Falar(falaMaoCheia);
            return;
        }

        var inv = PlayerInventory.Instance;
        if (inv == null || !inv.AddItem(itemId, 1))
        {
            Narrador.Falar("Não cabe mais nada nos bolsos.");
            return;
        }

        if (estoque >= 0)
        {
            if (!string.IsNullOrEmpty(flagMonte)) GameFlags.Set($"{flagMonte}.{estoque - Restantes + 1}");
            else tiradasSemFlag++;
            if (Restantes == 0) Narrador.Falar("Essa foi a última pedra daqui.");
        }

        if (som != null) AudioSource.PlayClipAtPoint(som, transform.position);
        if (!GameFlags.Has(flagPegouAlgum))
        {
            GameFlags.Set(flagPegouAlgum);
            if (!string.IsNullOrEmpty(falaPrimeira)) Narrador.Falar(falaPrimeira);
        }
    }

    // Sem colisor a mira não acha o monte: cria um gatilho do tamanho das pedras (alinhado ao mundo).
    private void GarantirColisor()
    {
        if (GetComponentInChildren<Collider>() != null) return;
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);

        var go = new GameObject("Colisor do Monte");
        go.transform.SetParent(transform, false);
        go.transform.position = b.center;
        go.transform.rotation = Quaternion.identity;
        var s = transform.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
        var bc = go.AddComponent<BoxCollider>();
        bc.size = b.size;
        bc.isTrigger = true;
    }
}
