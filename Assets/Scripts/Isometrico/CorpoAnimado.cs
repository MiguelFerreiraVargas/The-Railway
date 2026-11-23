using UnityEngine;

/// <summary>
/// Anima por código o esqueleto do corpo do Cândido (male_casual): parado respirando,
/// caminhada e corrida, conforme a velocidade com que o jogador se move.
/// Não precisa de Animator nem de clipes; funciona com o rig "pelvis/thigh/shin/upper_arm...".
/// </summary>
public class CorpoAnimado : MonoBehaviour
{
    [Tooltip("De onde vem a velocidade (o jogador). Vazio = o pai deste objeto.")]
    public Transform raiz;
    public float velocidadeAndar = 4f;
    public float velocidadeCorrer = 6f;
    [Tooltip("Metros por ciclo completo (dois passos) andando e correndo.")]
    public float passadaAndando = 1.5f, passadaCorrendo = 2.3f;

    [Header("Amplitudes (graus)")]
    public float coxaAndando = 26f, coxaCorrendo = 42f;
    public float joelhoAndando = 35f, joelhoCorrendo = 70f;
    public float bracoAndando = 22f, bracoCorrendo = 40f;
    public float cotoveloBase = 12f, cotoveloCorrendo = 70f;
    public float inclinarCorrendo = 9f;

    private Transform pelvis, spine, chest, head, coxaE, coxaD, canelaE, canelaD, peE, peD, bracoE, bracoD, antebracoE, antebracoD;
    private Transform[] ossos;
    private Quaternion[] bind;
    private Vector3 posModelo;
    private Vector3 ultimaPos;
    private float fase, mistura, corrida, vel;
    private CharacterController cc;
    private TrainInteraction[] trens = new TrainInteraction[0];

    private void Awake()
    {
        if (raiz == null) raiz = transform.parent != null ? transform.parent : transform;
        pelvis = Osso("pelvis"); spine = Osso("spine"); chest = Osso("chest"); head = Osso("head");
        coxaE = Osso("thigh_left"); coxaD = Osso("thigh_right");
        canelaE = Osso("shin.L"); canelaD = Osso("shin.R");
        peE = Osso("foot_left"); peD = Osso("foot_right");
        bracoE = Osso("upper_arm_left"); bracoD = Osso("upper_arm_right");
        antebracoE = Osso("forearm_left"); antebracoD = Osso("forearm_right");
        ossos = new[] { pelvis, spine, chest, head, coxaE, coxaD, canelaE, canelaD, peE, peD, bracoE, bracoD, antebracoE, antebracoD };
        bind = new Quaternion[ossos.Length];
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) bind[i] = ossos[i].localRotation;
        posModelo = transform.localPosition;
        ultimaPos = raiz.position;
        cc = raiz.GetComponent<CharacterController>();
        trens = FindObjectsByType<TrainInteraction>(FindObjectsSortMode.None);

        // O Animator importado do .fbx não tem clipes; desligado para não brigar com os ossos
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;
    }

    private Transform Osso(string nome)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t;
        Debug.LogWarning($"[CorpoAnimado] osso \"{nome}\" não encontrado em {name}");
        return null;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // velocidade de andar: a do CharacterController (sentado no trem, a posição muda mas ele não anda)
        Vector3 d = raiz.position - ultimaPos; d.y = 0f;
        ultimaPos = raiz.position;
        float v;
        bool sentado = false;
        foreach (var t in trens) if (t != null && t.IsInsideTrain) { sentado = true; break; }
        if (sentado) { v = 0f; d = Vector3.zero; }
        else if (cc != null && cc.enabled)
        {
            var vc = cc.velocity; vc.y = 0f;
            v = vc.magnitude;
            d = vc * dt;
        }
        else v = 0f;
        if (v > 20f) v = 0f; // teleporte
        vel = Mathf.Lerp(vel, v, 1f - Mathf.Exp(-12f * dt));

        float alvoMistura = Mathf.Clamp01(vel / (velocidadeAndar * 0.5f));
        float alvoCorrida = Mathf.Clamp01((vel - velocidadeAndar) / Mathf.Max(0.1f, velocidadeCorrer - velocidadeAndar));
        mistura = Mathf.MoveTowards(mistura, alvoMistura, dt * 5f);
        corrida = Mathf.MoveTowards(corrida, alvoCorrida, dt * 4f);

        float passada = Mathf.Lerp(passadaAndando, passadaCorrendo, corrida);
        fase += d.magnitude / passada * Mathf.PI * 2f;
        fase = Mathf.Repeat(fase, Mathf.PI * 2f);

        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) ossos[i].localRotation = bind[i];

        Vector3 dir = raiz.right;
        float s = Mathf.Sin(fase), c = Mathf.Cos(fase);
        float coxa = Mathf.Lerp(coxaAndando, coxaCorrendo, corrida) * mistura;
        float joelho = Mathf.Lerp(joelhoAndando, joelhoCorrendo, corrida) * mistura;
        float braco = Mathf.Lerp(bracoAndando, bracoCorrendo, corrida) * mistura;
        float cotovelo = cotoveloBase + cotoveloCorrendo * corrida * mistura;

        // Pernas: ângulo negativo em volta do "direita" leva a perna para a frente
        Girar(coxaE, dir, -coxa * s);
        Girar(coxaD, dir, coxa * s);
        Girar(canelaE, dir, joelho * Mathf.Max(0f, c) + 4f * mistura);
        Girar(canelaD, dir, joelho * Mathf.Max(0f, -c) + 4f * mistura);
        Girar(peE, dir, -8f * mistura * Mathf.Max(0f, -s));
        Girar(peD, dir, -8f * mistura * Mathf.Max(0f, s));

        // Braços balançam ao contrário da perna do mesmo lado
        Girar(bracoE, dir, braco * s);
        Girar(bracoD, dir, -braco * s);
        Girar(antebracoE, dir, -cotovelo - 8f * mistura * Mathf.Max(0f, -s));
        Girar(antebracoD, dir, -cotovelo - 8f * mistura * Mathf.Max(0f, s));

        // Tronco: inclina correndo, gira de leve com os passos, respira parado
        float respira = Mathf.Sin(Time.time * 1.6f) * (1f - mistura);
        Girar(spine, dir, -inclinarCorrendo * corrida * mistura);
        Girar(spine, Vector3.up, 5f * s * mistura);
        Girar(chest, dir, -1.2f * respira);
        Girar(head, Vector3.up, -4f * s * mistura);

        // Sobe e desce a cada passo
        float pulo = (Mathf.Abs(c) - 0.5f) * Mathf.Lerp(0.035f, 0.07f, corrida) * mistura;
        transform.localPosition = posModelo + new Vector3(0f, pulo + respira * 0.004f, 0f);
    }

    private static void Girar(Transform t, Vector3 eixo, float graus)
    {
        if (t == null || Mathf.Abs(graus) < 0.001f) return;
        t.rotation = Quaternion.AngleAxis(graus, eixo) * t.rotation;
    }
}
