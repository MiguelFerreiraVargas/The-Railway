using UnityEngine;

/// <summary>
/// Mostra as pedras de carvão nas mãos enquanto houver carvão no inventário. Com os braços de
/// primeira pessoa (CenaPrincipal) cabe uma pedra em cada mão livre: a primeira vai para a direita
/// (se a lanterna não estiver nela) e a segunda para a esquerda (se não tiver arma). Sem braços
/// (prólogo) é uma pedra só, presa à câmera no canto inferior esquerdo.
/// </summary>
public class CarvaoNaMao : MonoBehaviour
{
    [SerializeField] private string itemId = "carvao";
    [SerializeField] private Vector3 posicao = new Vector3(-0.17f, -0.12f, 0.42f);
    [SerializeField] private Vector3 rotacao = new Vector3(28f, -42f, -22f);
    [SerializeField] private Vector3 tamanho = new Vector3(0.085f, 0.065f, 0.075f);
    [Header("Nas mãos dos braços")]
    [Tooltip("Animação de onde sai a pose do braço segurando a pedra.")]
    [SerializeField] private string clipeMao = "guard_idle";
    [SerializeField] [Range(0f, 1f)] private float momentoMao = 0.1f;
    [Tooltip("Onde a pedra fica em relação ao centro do punho (no espaço da câmera, m).")]
    [SerializeField] private Vector3 ajusteNaMao = new Vector3(0f, 0.018f, 0f);
    [SerializeField] private float escalaNaMao = 0.75f;

    private static CarvaoNaMao instancia;
    private Transform pedraE, pedraD;
    private Transform cam;
    private float mostrarE, mostrarD;
    private Vector3 ultimaPos;
    private float passo;
    private PoseDoBraco poseE, poseD;
    private Renderer malhaBracos;
    private bool procurouBracos;

    /// <summary>Cria (uma vez por cena) as pedras das mãos, copiando a malha e o material do monte.</summary>
    public static void Garantir(Renderer modelo)
    {
        if (instancia != null) return;
        var go = new GameObject("Carvao na Mao");
        instancia = go.AddComponent<CarvaoNaMao>();
        instancia.Montar(modelo);
    }

    public static bool Visivel => instancia != null && ((instancia.pedraE != null && instancia.pedraE.gameObject.activeSelf) || (instancia.pedraD != null && instancia.pedraD.gameObject.activeSelf));

    // ------------------------------------------------------------ mãos

    public static int Quantidade => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity("carvao") : 0;
    private static bool TemBracos => instancia != null && instancia.poseE != null;

    /// <summary>A direita pode segurar carvão (tem braços e a lanterna não está nela).</summary>
    public static bool DireitaLivre => TemBracos && !LanternaNaMao.MaoDireitaOcupada && !ArmasDoJogador.DuasMaosOcupadas;
    /// <summary>A esquerda pode segurar carvão (sem arma de mão equipada).</summary>
    public static bool EsquerdaLivre => ArmasDoJogador.Instance == null || ArmasDoJogador.Instance.Equipada == "";

    /// <summary>Quantas pedras cabem nas mãos agora (no prólogo, sem braços, é uma).</summary>
    public static int Capacidade => !TemBracos ? 1 : (DireitaLivre ? 1 : 0) + (EsquerdaLivre ? 1 : 0);

    /// <summary>Tem pedra na mão direita (a primeira vai para ela quando está livre).</summary>
    public static bool NaDireita => Quantidade >= 1 && DireitaLivre;
    /// <summary>Tem pedra na mão esquerda.</summary>
    public static bool NaEsquerda => Quantidade >= 2 || (Quantidade == 1 && !DireitaLivre);

    private Transform NovaPedra(Renderer modelo, string nome)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = nome;
        Destroy(p.GetComponent<Collider>());
        var mf = modelo != null ? modelo.GetComponent<MeshFilter>() : null;
        if (mf != null && mf.sharedMesh != null) p.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var r = p.GetComponent<MeshRenderer>();
        if (modelo != null) r.sharedMaterial = modelo.sharedMaterial;
        // Na mão fica um pouco mais clara e brilhante que no monte, senão vira só um borrão preto
        var m = new Material(r.sharedMaterial);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.13f, 0.13f, 0.14f));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        p.transform.SetParent(transform, false);
        p.transform.localScale = tamanho;
        p.SetActive(false);
        CameraDosBracos.PorNaCamada(p);
        return p.transform;
    }

    private void Montar(Renderer modelo)
    {
        pedraE = NovaPedra(modelo, "Pedra (esquerda)");
        pedraD = NovaPedra(modelo, "Pedra (direita)");
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void LateUpdate()
    {
        if (pedraE == null) return;
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
            transform.SetParent(cam, false);
            ultimaPos = cam.position;
        }

        if (!procurouBracos)
        {
            procurouBracos = true;
            var bracos = FindFirstObjectByType<MaoPrimeiraPessoa>();
            if (bracos != null)
            {
                poseE = PoseDoBraco.Gravar(bracos.transform, ".L", clipeMao, momentoMao);
                poseD = PoseDoBraco.Gravar(bracos.transform, ".R", clipeMao, momentoMao);
                malhaBracos = bracos.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
        }

        bool isometrica = ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        bool podeVer = !Leitura.Aberta && !isometrica;
        bool comBracos = poseE != null && malhaBracos != null && malhaBracos.enabled && malhaBracos.gameObject.activeInHierarchy;
        bool querE = podeVer && (comBracos ? NaEsquerda : Quantidade > 0);
        bool querD = podeVer && comBracos && poseD != null && NaDireita;
        mostrarE = Mathf.MoveTowards(mostrarE, querE ? 1f : 0f, Time.deltaTime * (querE ? 4f : 6f));
        mostrarD = Mathf.MoveTowards(mostrarD, querD ? 1f : 0f, Time.deltaTime * (querD ? 4f : 6f));
        pedraE.gameObject.SetActive(mostrarE > 0.001f);
        pedraD.gameObject.SetActive(mostrarD > 0.001f);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        if (mostrarE <= 0.001f && mostrarD <= 0.001f) { ultimaPos = cam.position; return; }

        if (comBracos)
        {
            // cada mão fecha e a pedra vai junto com o punho
            NaMao(pedraE, poseE, mostrarE);
            NaMao(pedraD, poseD, mostrarD);
            return;
        }

        // prólogo (sem braços): presa à câmera, com balanço de caminhada
        Vector3 delta = cam.position - ultimaPos; delta.y = 0f;
        ultimaPos = cam.position;
        float vel = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        passo += Time.deltaTime * Mathf.Lerp(1.2f, 9f, Mathf.Clamp01(vel / 4f));
        float amp = Mathf.Lerp(0.004f, 0.016f, Mathf.Clamp01(vel / 4f));
        Vector3 balanco = new Vector3(Mathf.Sin(passo) * amp, -Mathf.Abs(Mathf.Cos(passo)) * amp, 0f);
        float e = Mathf.SmoothStep(0f, 1f, mostrarE);
        pedraE.localScale = tamanho;
        pedraE.localPosition = posicao + balanco + new Vector3(0f, (1f - e) * -0.35f, 0f);
        pedraE.localRotation = Quaternion.Euler(rotacao + new Vector3((1f - e) * 40f, 0f, 0f));
    }

    private void NaMao(Transform pedra, PoseDoBraco pose, float mostrar)
    {
        if (pose == null || mostrar <= 0.001f) return;
        float e = Mathf.SmoothStep(0f, 1f, mostrar);
        pose.Aplicar(e);
        pose.Punho(cam.forward, out Vector3 punho, out _);
        pedra.position = punho + cam.TransformVector(ajusteNaMao);
        pedra.localRotation = Quaternion.Euler(rotacao);
        pedra.localScale = tamanho * escalaNaMao;
    }
}
