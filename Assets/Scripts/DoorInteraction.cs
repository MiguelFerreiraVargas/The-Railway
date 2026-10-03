
using UnityEngine;
using System.Collections;

public class DoorInteraction : MonoBehaviour
{
    [Header("Porta")]
    public Transform porta;

    [Header("Abertura")]
    public float anguloAberta = 90f;
    public float duracao = 1f;

    [Header("Eixo de rotação")]
    public Vector3 eixoRotacao = Vector3.up;

    [Header("Deslocamento da dobradiça")]
    public Vector3 deslocamento = new Vector3(0.5f, 0f, 0f);

    [Header("Interação")]
    public float distancia = 3f;
    public KeyCode tecla = KeyCode.E;

    private Vector3 posicaoFechada;
    private Quaternion rotacaoFechada;

    private bool aberta;
    private bool animando;

    void Start()
    {
        if (porta == null)
            porta = transform;

        posicaoFechada = porta.localPosition;
        rotacaoFechada = porta.localRotation;
    }

    void Update()
    {
        if (animando || !Input.GetKeyDown(tecla))
            return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(
            cam.transform.position,
            cam.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, distancia))
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                StartCoroutine(AnimarPorta());
            }
        }
    }

    IEnumerator AnimarPorta()
    {
        animando = true;

        Vector3 posicaoInicial = porta.localPosition;
        Quaternion rotacaoInicial = porta.localRotation;

        Vector3 posicaoFinal = aberta
            ? posicaoFechada
            : posicaoFechada + deslocamento;

        Quaternion rotacaoFinal = aberta
            ? rotacaoFechada
            : rotacaoFechada *
              Quaternion.AngleAxis(
                  anguloAberta,
                  eixoRotacao.normalized
              );

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;

            float t = Mathf.Clamp01(tempo / duracao);
            float suave = Mathf.SmoothStep(0f, 1f, t);

            porta.localPosition = Vector3.Lerp(
                posicaoInicial,
                posicaoFinal,
                suave
            );

            porta.localRotation = Quaternion.Slerp(
                rotacaoInicial,
                rotacaoFinal,
                suave
            );

            yield return null;
        }

        porta.localPosition = posicaoFinal;
        porta.localRotation = rotacaoFinal;

        aberta = !aberta;
        animando = false;
    }
}
