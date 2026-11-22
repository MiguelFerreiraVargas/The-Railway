using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Abertura do prólogo: o Cândido acorda com a visão turva, piscando, com dor de
/// cabeça, e anda tonto por um tempo. O jogador controla a câmera o tempo todo;
/// a tontura só soma um balanço leve em forma de "∞" por cima do que o jogador faz.
/// Começa com fome, sede e higiene baixas. Não roda se o jogo veio de um save.
/// </summary>
// Depois do CinemachineBrain (ordem 100), que posiciona a câmera a cada quadro.
[DefaultExecutionOrder(200)]
public class AcordarSequencia : MonoBehaviour
{
    [SerializeField] private CanvasGroup olhos;          // tela preta das pálpebras
    [SerializeField] private Volume volumeTontura;       // turvo, aberração, distorção
    [SerializeField] private float duracaoTontura = 20f;
    [SerializeField] private float velocidadeTonto = 1.7f;

    [Header("Balanço em \"∞\" (graus e segundos por volta)")]
    [SerializeField] private float amplitude = 1.6f;
    [SerializeField] private float periodo = 6f;

    [Header("Como ele acorda (0 a 1)")]
    [SerializeField] private float vida = 0.75f;
    [SerializeField] private float fome = 0.35f;
    [SerializeField] private float sede = 0.2f;
    [SerializeField] private float higiene = 0.12f;
    [SerializeField] private float sanidade = 0.7f;

    public const string Flag = "prologo.acordou";

    public static float Tontura { get; private set; }

    private bool pular;
    private FirstPersonController fpc;
    private float inicioBalanco;

    // o que foi escrito na câmera no último quadro (para nunca acumular)
    private Transform cam;
    private Quaternion ultimoEscrito, ultimoOffset = Quaternion.identity;
    private bool escreveu;

    private void Awake()
    {
        pular = SaveGameLoader.selectedSlot >= 0 || GameFlags.Has(Flag);
        Tontura = pular ? 0f : 1f;
        if (olhos != null) olhos.alpha = pular ? 0f : 1f;
        if (volumeTontura != null) volumeTontura.weight = pular ? 0f : 1f;
    }

    private IEnumerator Start()
    {
        if (pular) yield break;

        yield return null; // deixa o PlayerStats e o jogador iniciarem
        while (LoadingScreen.IsLoading) yield return null;

        PlayerStats.Instance?.DefinirFracoes(vida, fome, sede, higiene, sanidade);

        fpc = FindFirstObjectByType<FirstPersonController>();
        float move = 4f, sprint = 6f;
        if (fpc != null)
        {
            move = fpc.MoveSpeed;
            sprint = fpc.SprintSpeed;
            fpc.MoveSpeed = velocidadeTonto;
            fpc.SprintSpeed = velocidadeTonto;
        }
        inicioBalanco = Time.time;

        // Pálpebras pesadas
        Narrador.Falar("...");
        yield return Olhos(1f, 0.55f, 1.4f);
        yield return Olhos(0.55f, 1f, 0.5f);
        Narrador.Falar("Onde... onde eu tô?");
        yield return Olhos(1f, 0.25f, 1.6f);
        yield return Olhos(0.25f, 0.85f, 0.4f);
        yield return Olhos(0.85f, 0f, 2.2f);

        Narrador.Falar("Nossa... que dor de cabeça.");
        GameFlags.Set(Flag);
        Narrador.Falar("Que sede... minha boca tá seca. Preciso achar água.");

        // A tontura vai passando
        float t = 0f;
        while (t < duracaoTontura)
        {
            t += Time.deltaTime;
            Tontura = 1f - Mathf.Clamp01(t / duracaoTontura);
            if (volumeTontura != null) volumeTontura.weight = Tontura;
            if (fpc != null)
            {
                fpc.MoveSpeed = Mathf.Lerp(move, velocidadeTonto, Tontura);
                fpc.SprintSpeed = Mathf.Lerp(sprint, velocidadeTonto, Tontura);
            }
            yield return null;
        }

        Tontura = 0f;
        if (volumeTontura != null) volumeTontura.weight = 0f;
        if (fpc != null) { fpc.MoveSpeed = move; fpc.SprintSpeed = sprint; }
        Narrador.Falar("Melhor. Bem melhor.");
    }

    private IEnumerator Olhos(float de, float para, float tempo)
    {
        float t = 0f;
        while (t < tempo)
        {
            t += Time.deltaTime;
            if (olhos != null) olhos.alpha = Mathf.Lerp(de, para, Mathf.SmoothStep(0f, 1f, t / tempo));
            yield return null;
        }
        if (olhos != null) olhos.alpha = para;
    }

    // Balanço em "∞" (lemniscata: vai e volta na horizontal, duas ondas na vertical),
    // somado à câmera depois que o Cinemachine já a posicionou neste quadro.
    private void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        var atual = cam.localRotation;
        // Se ninguém reescreveu a câmera desde o último quadro, tira o balanço anterior antes.
        var semBalanco = escreveu && Quaternion.Angle(atual, ultimoEscrito) < 0.0001f ? atual * Quaternion.Inverse(ultimoOffset) : atual;

        if (Tontura <= 0f || pular)
        {
            if (escreveu) { cam.localRotation = semBalanco; escreveu = false; ultimoOffset = Quaternion.identity; }
            return;
        }

        float fase = (Time.time - inicioBalanco) * Mathf.PI * 2f / Mathf.Max(0.5f, periodo);
        float forca = amplitude * Mathf.SmoothStep(0f, 1f, Tontura);
        var offset = Quaternion.Euler(Mathf.Sin(fase * 2f) * forca * 0.5f, Mathf.Sin(fase) * forca, 0f);

        ultimoEscrito = semBalanco * offset;
        ultimoOffset = offset;
        cam.localRotation = ultimoEscrito;
        escreveu = true;
    }

    private void OnDisable()
    {
        if (escreveu && cam != null && Quaternion.Angle(cam.localRotation, ultimoEscrito) < 0.0001f)
            cam.localRotation = ultimoEscrito * Quaternion.Inverse(ultimoOffset);
        escreveu = false;
        Tontura = 0f;
    }
}
