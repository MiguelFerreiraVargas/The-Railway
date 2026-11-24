using UnityEngine;

/// <summary>
/// Carvão da locomotiva (GDD: "a locomotiva depende de combustível para continuar a viagem").
/// Fica num ponto do tênder: com uma pedra de carvão na mão, E abastece. O trem gasta carvão
/// conforme anda; sem carvão ele não sai do lugar e, se acabar no caminho, freia sozinho.
/// </summary>
public class CombustivelTrem : Interagivel
{
    [SerializeField] private string itemCarvao = "carvao";
    [SerializeField] private int capacidade = 10;
    [Tooltip("Carvão no começo da cena (o que sobrou do prólogo).")]
    [SerializeField] private float carvaoInicial = 3f;
    [Tooltip("Metros de trilho por pedra de carvão.")]
    [SerializeField] private float metrosPorCarvao = 170f;
    [SerializeField] private AudioClip somCarvao;

    public const string FlagObjetivo = "viagem.carvao_trem";

    public float Carvao { get; private set; }
    public bool TemCarvao => Carvao > 0.001f;
    public int Capacidade => capacidade;
    public float MetrosPorCarvao => metrosPorCarvao;

    private float avisouPouco = -1f;

    private int NaMao => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(itemCarvao) : 0;

    public override string Texto
    {
        get
        {
            int n = Mathf.CeilToInt(Carvao);
            if (Carvao >= capacidade - 0.01f) return $"TÊNDER CHEIO ({n}/{capacidade})";
            return NaMao > 0 ? $"COLOCAR CARVÃO NO TREM ({n}/{capacidade})" : $"CARVÃO DO TREM ({n}/{capacidade})";
        }
    }

    private void Awake()
    {
        Carvao = Mathf.Clamp(carvaoInicial, 0f, capacidade);
    }

    private void Start()
    {
        // barra na direita da tela
        var obj = FindFirstObjectByType<Objetivos>();
        var texto = obj != null ? obj.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        BarraCarvao.Criar(this, texto != null ? texto.font : null);
        Objetivos.Adicionar(FlagObjetivo, "Manter carvão na locomotiva");
        AtualizarObjetivo();
    }

    public override void Interagir()
    {
        if (Carvao >= capacidade - 0.01f)
        {
            Narrador.Falar("Cheio. Não cabe mais nada aí.");
            return;
        }
        if (NaMao <= 0 || PlayerInventory.Instance == null || !PlayerInventory.Instance.RemoveItem(itemCarvao, 1))
        {
            Narrador.Falar(TemCarvao ? "Ainda tem um pouco... mas vou precisar de mais carvão." : "Vazio. Sem carvão ela não sai do lugar.");
            return;
        }

        Carvao = Mathf.Min(capacidade, Carvao + 1f);
        if (somCarvao != null) AudioSource.PlayClipAtPoint(somCarvao, transform.position);
        int n = Mathf.CeilToInt(Carvao);
        if (n >= capacidade) Narrador.Falar("Cheio. Agora dá pra ir longe.");
        else if (n == 1) Narrador.Falar("Uma pedra... já dá pra andar um pouco.");
        AtualizarObjetivo();
    }

    /// <summary>Gasta carvão pelos metros andados. Retorna false se acabou.</summary>
    public bool Gastar(float metros)
    {
        if (!TemCarvao) return false;
        Carvao = Mathf.Max(0f, Carvao - metros / Mathf.Max(1f, metrosPorCarvao));
        if (Carvao < 1f && Carvao > 0f && Time.time - avisouPouco > 60f)
        {
            avisouPouco = Time.time;
            Narrador.Falar("O carvão tá acabando... preciso parar e procurar mais.");
        }
        AtualizarObjetivo();
        return TemCarvao;
    }

    private void AtualizarObjetivo()
    {
        string detalhe = $"no tênder: {Mathf.CeilToInt(Carvao)}/{capacidade}" + (NaMao > 0 ? "   (uma pedra na mão)" : "");
        // onde achar mais: o monte com carvão mais perto e o mapa
        var cam = Camera.main;
        if (cam != null && MonteDeCarvao.MaisPerto(cam.transform.position, out float dist) != null)
            detalhe += $"\nmonte de carvão mais perto: {Mathf.RoundToInt(dist / 10f) * 10} m   [M] mapa";
        Objetivos.Detalhe(FlagObjetivo, detalhe);
    }

    private void Update()
    {
        // a pedra na mão muda o texto do objetivo
        if (Time.frameCount % 20 == 0) AtualizarObjetivo();
    }
}
