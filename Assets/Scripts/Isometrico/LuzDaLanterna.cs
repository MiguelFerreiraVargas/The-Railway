using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Luz da lanterna (L liga e desliga). Em primeira pessoa sai da ponta da lanterna na mão;
/// na isométrica sai do peito do Cândido, para onde ele está virado.
/// </summary>
[DefaultExecutionOrder(100)] // depois do LanternaNaMao pôr a lanterna na mão
public class LuzDaLanterna : MonoBehaviour
{
    public Key tecla = Key.L;
    public bool ligada = true;
    [Header("Luz")]
    public Color cor = new Color(1f, 0.92f, 0.78f);
    [Tooltip("Forte perto, some em poucos metros: é uma lanterna, não um holofote (a noite continua noite).")]
    public float intensidade = 12f;
    public float alcance = 16f;
    [Tooltip("Na isométrica a câmera vê o facho de cima: mais fraco e curto.")]
    public float intensidadeIso = 6f;
    public float alcanceIso = 9f;
    public float angulo = 50f;
    public float anguloInterno = 26f;
    public LightShadows sombras = LightShadows.Soft;
    [Tooltip("Na isométrica: quanto a luz aponta para baixo (graus).")]
    public float inclinacaoIso = 18f;
    public AudioClip somClique;

    [Header("Bateria")]
    [Tooltip("Minutos de luz ligada com uma pilha cheia.")]
    public float duracaoMinutos = 6f;
    [Tooltip("Abaixo disto (0..1) a luz começa a piscar.")]
    public float cargaFraca = 0.15f;

    /// <summary>A luz criada (para testes).</summary>
    public Light Luz { get; private set; }
    public static LuzDaLanterna Instance { get; private set; }

    /// <summary>Carga da pilha (0 a 1). Uma pilha nova enche; acabando, troca sozinho se tiver pilha na mochila.</summary>
    public float Carga { get; set; } = 1f;
    public bool Acesa => Luz != null && Luz.enabled;
    private bool avisouFraca, avisouVazia;
    private float piscar;

    private LanternaNaMao naMao;
    private MeshFilter malha;
    private AudioSource som;

    private void Awake()
    {
        Instance = this;
        naMao = GetComponent<LanternaNaMao>();
        malha = GetComponentInChildren<MeshFilter>();

        var go = new GameObject("Luz da Lanterna");
        go.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        Luz = go.AddComponent<Light>();
        Luz.type = LightType.Spot;
        Luz.color = cor;
        Luz.intensity = intensidade;
        Luz.range = alcance;
        Luz.spotAngle = angulo;
        Luz.innerSpotAngle = anguloInterno;
        Luz.shadows = sombras;
        Luz.shadowBias = 0.05f;
        Luz.enabled = ligada;

        som = gameObject.AddComponent<AudioSource>();
        som.playOnAwake = false; som.spatialBlend = 0f; som.volume = 0.5f;
    }

    private void Update()
    {
        bool tem = LanternaNaMao.NaMaoAgora;
        if (tem && ligada && Carga > 0f)
        {
            Carga = Mathf.Max(0f, Carga - Time.deltaTime / Mathf.Max(1f, duracaoMinutos * 60f));
            if (Carga <= cargaFraca && !avisouFraca) { avisouFraca = true; Narrador.Falar("A luz tá fraquejando... a pilha tá no fim."); }
            if (Carga <= 0f) TrocarPilha();
        }
        if (Keyboard.current == null || PauseMenu.IsPaused || Leitura.Aberta) return;
        if (Keyboard.current[tecla].wasPressedThisFrame && tem) Alternar();
    }

    // pilha acabou: põe uma da mochila, se tiver
    private void TrocarPilha()
    {
        var inv = PlayerInventory.Instance;
        if (inv != null && inv.GetQuantity("pilha") > 0 && inv.RemoveItem("pilha", 1))
        {
            Carga = 1f; avisouFraca = false; avisouVazia = false;
            Narrador.Falar("Pilha nova. Luz de volta.");
            return;
        }
        if (!avisouVazia) { avisouVazia = true; Narrador.Falar("A pilha morreu. Preciso achar outra."); }
    }

    /// <summary>Pôr uma pilha agora (pela mochila).</summary>
    public bool PorPilha()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || inv.GetQuantity("pilha") <= 0 || !inv.RemoveItem("pilha", 1)) return false;
        Carga = 1f; avisouFraca = false; avisouVazia = false;
        return true;
    }

    public void Alternar()
    {
        ligada = !ligada;
        if (somClique != null) som.PlayOneShot(somClique);
    }

    private void LateUpdate()
    {
        if (Luz == null) return;
        var modo = ModoCamera.Instance;
        bool iso = modo != null && modo.Isometrico;
        // só acende com a lanterna na mão e pilha com carga; fraca, pisca
        Luz.enabled = ligada && LanternaNaMao.NaMaoAgora && Carga > 0f;
        float forca = 1f;
        if (Carga < cargaFraca)
        {
            piscar -= Time.deltaTime;
            if (piscar <= 0f) piscar = Random.Range(0.04f, 0.3f);
            forca = Mathf.Lerp(0.15f, 1f, Carga / cargaFraca) * (piscar < 0.08f ? Random.Range(0.1f, 0.6f) : 1f);
        }
        Luz.intensity = (iso ? intensidadeIso : intensidade) * forca;
        Luz.range = iso ? alcanceIso : alcance;

        if (iso || (naMao != null && naMao.Mao == null))
        {
            // isométrica: do peito do Cândido para a frente dele
            var corpo = modo != null ? modo.transform : transform;
            Vector3 frente = Vector3.ProjectOnPlane(corpo.forward, Vector3.up).normalized;
            Luz.transform.SetPositionAndRotation(corpo.position + Vector3.up * 1.2f + frente * 0.35f,
                Quaternion.AngleAxis(inclinacaoIso, Vector3.Cross(Vector3.up, frente)) * Quaternion.LookRotation(frente, Vector3.up));
            return;
        }

        // primeira pessoa: da lente, na direção em que a lanterna aponta
        if (malha != null && malha.sharedMesh != null)
        {
            var b = malha.sharedMesh.bounds;
            var s = b.size;
            Vector3 eixo = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
            if (naMao != null && !naMao.lenteNoPositivo) eixo = -eixo;
            Vector3 ponta = malha.transform.TransformPoint(b.center + Vector3.Scale(eixo, b.extents));
            Vector3 dir = malha.transform.TransformDirection(eixo).normalized;
            Luz.transform.SetPositionAndRotation(ponta + dir * 0.02f, Quaternion.LookRotation(dir, Vector3.up));
        }
        else
        {
            Luz.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }
}
