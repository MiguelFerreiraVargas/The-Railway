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

    private const string FlagPegouAlgum = "prologo.carvao_pegou";

    private int NaMao => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(itemId) : 0;

    public override string Texto => NaMao > 0 ? "MÃOS OCUPADAS" : acao;

    public override bool Disponivel => base.Disponivel && !GameFlags.Has(Fornalha.FlagCarvao);

    private void Reset()
    {
        acao = "PEGAR CARVÃO";
    }

    private void Awake()
    {
        GarantirColisor();
        CarvaoNaMao.Garantir(GetComponentInChildren<Renderer>());
    }

    public override void Interagir()
    {
        if (NaMao > 0)
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

        if (som != null) AudioSource.PlayClipAtPoint(som, transform.position);
        if (!GameFlags.Has(FlagPegouAlgum))
        {
            GameFlags.Set(FlagPegouAlgum);
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
