using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Legendas do Cândido falando sozinho (e de rádios, etc.), em fila.
/// Uso: Narrador.Falar("Minha cabeça...");  ou com voz: Narrador.Falar(texto, clip).
/// Sem clip de voz, o texto aparece letra a letra com um murmúrio sintetizado (VozMurmurio).
/// </summary>
public class Narrador : MonoBehaviour
{
    private struct Fala
    {
        public string texto;
        public string quem;
        public AudioClip voz;
        public float duracao;
    }

    public static Narrador Instance { get; private set; }

    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private AudioSource vozSource;
    [Tooltip("Segundos por caractere quando a fala não tem áudio (mínimo 2s).")]
    [SerializeField] private float segundosPorLetra = 0.045f;

    [Header("Murmúrio")]
    [SerializeField] private bool murmurar = true;
    [Tooltip("Letras por segundo ao escrever a legenda.")]
    [SerializeField] private float letrasPorSegundo = 38f;
    [Range(0f, 1f)] [SerializeField] private float volumeMurmurio = 0.32f;

    private AudioSource murmurioSource;

    private readonly Queue<Fala> fila = new Queue<Fala>();
    private Coroutine rotina;

    private void Awake()
    {
        Instance = this;
        if (grupo != null) grupo.alpha = 0f;

        murmurioSource = gameObject.AddComponent<AudioSource>();
        murmurioSource.playOnAwake = false;
        murmurioSource.spatialBlend = 0f;
        murmurioSource.priority = 64;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Falar(string frase, AudioClip voz = null, float duracao = 0f, string quem = "")
    {
        if (Instance == null || string.IsNullOrEmpty(frase)) return;
        Instance.fila.Enqueue(new Fala { texto = frase, voz = voz, duracao = duracao, quem = quem });
        if (Instance.rotina == null) Instance.rotina = Instance.StartCoroutine(Instance.Tocar());
    }

    /// <summary>Esvazia a fila e para a fala atual.</summary>
    public static void Calar()
    {
        if (Instance == null) return;
        Instance.fila.Clear();
        if (Instance.rotina != null) Instance.StopCoroutine(Instance.rotina);
        Instance.rotina = null;
        if (Instance.grupo != null) Instance.grupo.alpha = 0f;
        if (Instance.vozSource != null) Instance.vozSource.Stop();
        if (Instance.murmurioSource != null) Instance.murmurioSource.Stop();
    }

    public static bool Falando => Instance != null && Instance.rotina != null;

    private IEnumerator Tocar()
    {
        while (fila.Count > 0)
        {
            var f = fila.Dequeue();
            bool temQuem = !string.IsNullOrEmpty(f.quem);
            if (texto != null)
            {
                texto.text = temQuem ? $"<color=#FFB040>{f.quem}:</color> {f.texto}" : f.texto;
                texto.maxVisibleCharacters = 99999;
            }

            float dur = f.duracao > 0f ? f.duracao : Mathf.Max(2f, f.texto.Length * segundosPorLetra);
            bool escrever = f.voz == null && murmurar && texto != null;
            if (f.voz != null && vozSource != null)
            {
                vozSource.PlayOneShot(f.voz);
                dur = Mathf.Max(dur, f.voz.length);
            }

            if (!escrever)
            {
                yield return Fade(1f, 0.2f);
                yield return new WaitForSeconds(dur);
            }
            else
            {
                // Escreve letra a letra, murmurando uma sílaba a cada grupo de vogais
                texto.ForceMeshUpdate();
                string visivel = texto.GetParsedText();
                int inicio = temQuem ? Mathf.Min(visivel.Length, f.quem.Length + 2) : 0;
                texto.maxVisibleCharacters = inicio;
                var tipo = temQuem ? VozMurmurio.Tipo.Radio : VozMurmurio.Tipo.Candido;

                yield return Fade(1f, 0.15f);
                float gasto = 0f, ultimaSilaba = -1f;
                for (int i = inicio; i < visivel.Length; i++)
                {
                    char c = visivel[i];
                    bool vogal = EhVogal(c);
                    bool comecaSilaba = vogal && (i == 0 || !EhVogal(visivel[i - 1]));
                    if (comecaSilaba && gasto - ultimaSilaba > 0.085f)
                    {
                        Murmurar(tipo);
                        ultimaSilaba = gasto;
                    }

                    texto.maxVisibleCharacters = i + 1;
                    float espera = 1f / Mathf.Max(5f, letrasPorSegundo) * Pausa(c, visivel, i);
                    gasto += espera;
                    yield return new WaitForSeconds(espera);
                }
                texto.maxVisibleCharacters = 99999;
                yield return new WaitForSeconds(Mathf.Max(1.0f, dur - gasto));
            }

            yield return Fade(0f, 0.25f);
            yield return new WaitForSeconds(0.1f);
        }

        rotina = null;
    }

    private void Murmurar(VozMurmurio.Tipo tipo)
    {
        if (murmurioSource == null) return;
        murmurioSource.pitch = Random.Range(0.93f, 1.08f);
        murmurioSource.PlayOneShot(VozMurmurio.Silaba(tipo), volumeMurmurio * Random.Range(0.75f, 1f));
    }

    private static bool EhVogal(char c)
    {
        return "aeiouáéíóúâêôãõàAEIOUÁÉÍÓÚÂÊÔÃÕÀ".IndexOf(c) >= 0;
    }

    // Multiplicador do tempo de cada letra: respira nas vírgulas, para nos pontos e reticências.
    private static float Pausa(char c, string s, int i)
    {
        bool fimDeBloco = i + 1 >= s.Length || s[i + 1] == ' ';
        switch (c)
        {
            case ',': case ';': return 5f;
            case '.': return fimDeBloco ? 8f : 3.5f; // "..." espalha a pausa entre os pontos
            case '!': case '?': return 8f;
            case '…': return 10f;
            case ' ': return 1.4f;
            default: return 1f;
        }
    }

    private IEnumerator Fade(float alvo, float tempo)
    {
        if (grupo == null) yield break;
        float ini = grupo.alpha, t = 0f;
        while (t < tempo)
        {
            t += Time.deltaTime; // tempo de jogo: a legenda congela junto com a pausa
            grupo.alpha = Mathf.Lerp(ini, alvo, t / tempo);
            yield return null;
        }
        grupo.alpha = alvo;
    }
}
