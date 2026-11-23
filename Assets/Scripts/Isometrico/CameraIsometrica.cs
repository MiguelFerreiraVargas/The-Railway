using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Câmera isométrica no estilo Project Zomboid: ângulo fixo de cima, segue o jogador
/// com suavidade e aproxima/afasta com a rodinha do mouse. Fica num CinemachineCamera
/// sem Body/Aim; este script posiciona o objeto e o Cinemachine só copia.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CameraIsometrica : MonoBehaviour
{
    public Transform alvo;
    [Tooltip("Altura do ponto que a câmera olha, acima dos pés do jogador.")]
    public float alturaFoco = 1.1f;
    [Range(20f, 80f)] public float inclinacao = 48f;
    public float giro = 45f;
    public float distancia = 20f;
    public float distanciaMin = 7f, distanciaMax = 34f;
    [Tooltip("Distância ao entrar numa casa (mais perto que lá fora).")]
    public float distanciaInterior = 12f;
    [Tooltip("Inclinação dentro de casa (mais de cima: as paredes tampam menos).")]
    [Range(20f, 85f)] public float inclinacaoInterior = 64f;

    /// <summary>Dentro de uma estrutura: usa a inclinação de interior.</summary>
    public bool Interior { get; set; }
    private float inclinacaoAtual = -1f;
    public float campoDeVisao = 30f;
    [Tooltip("Tempo de suavização do seguir (s).")]
    public float suavidade = 0.12f;
    public float velocidadeZoom = 0.012f;

    private CinemachineCamera cam;
    private Vector3 foco, velFoco;
    private float distAtual;
    private bool posicionada;

    private void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        cam.Lens.FieldOfView = campoDeVisao;
        cam.Lens.NearClipPlane = 0.3f;
        cam.Lens.FarClipPlane = 500f;
        distAtual = distancia;
    }

    private void OnEnable() => posicionada = false;

    public Quaternion Rotacao => Quaternion.Euler(inclinacaoAtual >= 0f ? inclinacaoAtual : inclinacao, giro, 0f);

    private void LateUpdate()
    {
        if (alvo == null) return;

        bool parado = Time.timeScale == 0f || PauseMenu.IsPaused || Leitura.Aberta;
        if (!parado && Mouse.current != null)
        {
            float roda = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(roda) > 0.01f)
                distancia = Mathf.Clamp(distancia - roda * velocidadeZoom * distancia, distanciaMin, distanciaMax);
        }

        float alvoInclinacao = Interior ? inclinacaoInterior : inclinacao;
        inclinacaoAtual = inclinacaoAtual < 0f || !posicionada ? alvoInclinacao
            : Mathf.MoveTowards(inclinacaoAtual, alvoInclinacao, 60f * Time.unscaledDeltaTime);

        Vector3 alvoFoco = alvo.position + Vector3.up * alturaFoco;
        if (!posicionada)
        {
            foco = alvoFoco;
            distAtual = distancia;
            velFoco = Vector3.zero;
            posicionada = true;
        }
        else
        {
            foco = Vector3.SmoothDamp(foco, alvoFoco, ref velFoco, suavidade, Mathf.Infinity, Time.unscaledDeltaTime);
            distAtual = Mathf.Lerp(distAtual, distancia, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        var rot = Rotacao;
        transform.SetPositionAndRotation(foco - rot * Vector3.forward * distAtual, rot);
        if (!Mathf.Approximately(cam.Lens.FieldOfView, campoDeVisao)) cam.Lens.FieldOfView = campoDeVisao;
    }

    /// <summary>Cola a câmera no jogador sem suavizar (ex.: depois de teleportar).</summary>
    public void Recentrar() => posicionada = false;
}
