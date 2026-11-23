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

    /// <summary>A luz criada (para testes).</summary>
    public Light Luz { get; private set; }

    private LanternaNaMao naMao;
    private MeshFilter malha;
    private AudioSource som;

    private void Awake()
    {
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
        if (Keyboard.current == null || PauseMenu.IsPaused || Leitura.Aberta) return;
        if (Keyboard.current[tecla].wasPressedThisFrame) Alternar();
    }

    public void Alternar()
    {
        ligada = !ligada;
        if (Luz != null) Luz.enabled = ligada;
        if (somClique != null) som.PlayOneShot(somClique);
    }

    private void LateUpdate()
    {
        if (Luz == null) return;
        var modo = ModoCamera.Instance;
        bool iso = modo != null && modo.Isometrico;
        Luz.intensity = iso ? intensidadeIso : intensidade;
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
