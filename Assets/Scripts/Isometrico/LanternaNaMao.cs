using UnityEngine;

/// <summary>
/// A lanterna fica segurada na mão direita dos braços de primeira pessoa: o braço direito fica
/// parado numa pose de punho fechado (tirada de uma animação dos braços) e a lanterna atravessa
/// o punho, apontando para a frente. Ela continua filha da câmera e só copia a posição/rotação
/// da mão a cada quadro, assim não herda escala do rig.
/// </summary>
public class LanternaNaMao : MonoBehaviour
{
    [Tooltip("Raiz dos braços (ArmPrincipal).")]
    public Transform bracos;
    [Tooltip("Lado da mão que segura: \".R\" (direita) ou \".L\".")]
    public string lado = ".R";
    [Tooltip("Animação de onde sai a pose do braço que segura (punho fechado).")]
    public string clipePose = "guard_idle";
    [Range(0f, 1f)] public float momentoPose = 0.1f;
    [Tooltip("Ajuste fino da posição no punho (no espaço da câmera, m).")]
    public Vector3 ajuste = Vector3.zero;
    [Tooltip("Quanto a lanterna sai para a frente do punho (fração do comprimento: 0 = punho no meio dela).")]
    [Range(0f, 0.5f)] public float avanco = 0.22f;
    [Tooltip("Comprimento da lanterna na mão (m). 0 = mantém a escala.")]
    public float comprimento = 0.2f;
    [Tooltip("Quanto o pulso dobra para a lanterna apontar para a frente (0 = como na animação).")]
    [Range(0f, 1f)] public float dobrarPulso = 0.65f;
    [Tooltip("A ponta com a lente fica no lado positivo do eixo comprido da malha.")]
    public bool lenteNoPositivo = true;

    /// <summary>Osso da mão que a lanterna está seguindo (null até montar).</summary>
    public Transform Mao { get; private set; }

    /// <summary>Se alguma lanterna está ocupando a mão direita agora.</summary>
    public static bool MaoDireitaOcupada => ativas > 0 && TemLanterna;

    /// <summary>A lanterna é achada no mundo (casas): só fica na mão depois de pegar uma.</summary>
    public static bool TemLanterna => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity("lanterna") > 0;

    private Renderer[] malhas;
    private static int ativas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar() => ativas = 0;

    private Vector3 posNaMao;       // posição da lanterna no espaço da mão
    private Quaternion rotNaMao;    // rotação da lanterna relativa à mão
    private PoseDoBraco pose;

    private void Start() => Montar();

    private void OnEnable() { if (Mao != null) ativas++; }
    private void OnDisable() { if (Mao != null) ativas--; }

    /// <summary>Grava a pose do braço e encaixa a lanterna no punho. Funciona fora do Play (prévia).</summary>
    public void Montar()
    {
        if (bracos == null || Mao != null) return;
        pose = PoseDoBraco.Gravar(bracos, lado, clipePose, momentoPose);
        if (pose == null) return;
        var cam = transform.parent;
        Vector3 frente = cam != null ? cam.forward : Vector3.forward;

        pose.Aplicar();
        pose.Punho(frente, out Vector3 punho, out Vector3 eixo);
        // dobra o pulso para o buraco do punho apontar mais para a frente
        if (dobrarPulso > 0f)
        {
            pose.Mao.rotation = Quaternion.FromToRotation(eixo, Vector3.Slerp(eixo, frente, dobrarPulso)) * pose.Mao.rotation;
            pose.Regravar(pose.Mao);
            pose.Punho(frente, out punho, out eixo);
        }

        // o eixo comprido da malha atravessa o punho, com a lente para a frente
        var mf = GetComponentInChildren<MeshFilter>();
        float comp = 0.2f;
        Vector3 centroMalha = transform.position;
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            var s = b.size;
            Vector3 eixoLocal = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
            if (comprimento > 0f)
            {
                float atual = mf.transform.TransformVector(Vector3.Scale(eixoLocal, s)).magnitude;
                if (atual > 1e-4f) transform.localScale *= comprimento / atual;
            }
            if (!lenteNoPositivo) eixoLocal = -eixoLocal;
            Vector3 eixoMundo = mf.transform.TransformDirection(eixoLocal);
            transform.rotation = Quaternion.FromToRotation(eixoMundo, eixo) * transform.rotation;
            comp = mf.transform.TransformVector(Vector3.Scale(eixoLocal, s)).magnitude;
            centroMalha = mf.transform.TransformPoint(b.center);
        }
        Vector3 ajusteMundo = cam != null ? cam.TransformVector(ajuste) : ajuste;
        transform.position += punho + eixo * (comp * avanco) + ajusteMundo - centroMalha;

        // guarda a pose em relação à mão (sem escala: só rotação e um ponto)
        posNaMao = pose.Mao.InverseTransformPoint(transform.position);
        rotNaMao = Quaternion.Inverse(pose.Mao.rotation) * transform.rotation;
        Mao = pose.Mao;
        if (isActiveAndEnabled) ativas++;
    }

    private void LateUpdate()
    {
        if (Mao == null) return;
        // sem lanterna no inventário: some, e o braço direito fica livre (o Animator manda)
        if (malhas == null) malhas = GetComponentsInChildren<Renderer>(true);
        bool tem = TemLanterna;
        bool iso = ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        foreach (var r in malhas) if (r != null && r.enabled != (tem && !iso)) r.enabled = tem && !iso;
        if (!tem) return;
        // o Animator já rodou neste quadro: o braço que segura volta para a pose do punho fechado
        pose.Aplicar();
        transform.SetPositionAndRotation(Mao.TransformPoint(posNaMao), Mao.rotation * rotNaMao);
    }
}
