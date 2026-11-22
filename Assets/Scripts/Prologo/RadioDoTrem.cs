using System.Collections;
using UnityEngine;

/// <summary>
/// Rádio velho no trem repetindo sempre a mesma transmissão.
/// O chiado é gerado por código; se houver uma voz gravada (vozDaMensagem),
/// ela toca junto. As legendas aparecem quando o jogador está perto.
/// </summary>
public class RadioDoTrem : Interagivel
{
    [Header("Mensagem")]
    [SerializeField] [TextArea(2, 4)] private string[] frases =
    {
        "Ei... tem alguém aí? Está me ouvindo?",
        "Temos abrigo, alimentos e roupas.",
        "Venha até as coordenadas: menos cento e trinta e dois... quatrocentos e trinta e dois... menos vinte e dois.",
        "Nós vamos te ajudar. Repito...",
    };
    [Tooltip("Voz gravada da mensagem (opcional). Sem ela, só o chiado + legenda.")]
    [SerializeField] private AudioClip vozDaMensagem;
    [SerializeField] private float intervaloEntreRepeticoes = 14f;
    [SerializeField] private float raioDasLegendas = 14f;

    [Header("Som")]
    [SerializeField] private AudioSource chiado;
    [SerializeField] private float volumeChiado = 0.35f;

    [SerializeField] private string flagOuviu = "prologo.radio";
    [SerializeField] private string flagPerto = "prologo.radio_perto";

    private Transform jogador;
    private bool transmitindo;

    public override string Texto => GameFlags.Has(flagOuviu) ? "OUVIR O RÁDIO DE NOVO" : "MEXER NO RÁDIO";

    private void Start()
    {
        if (chiado == null) chiado = gameObject.AddComponent<AudioSource>();
        chiado.clip = GerarChiado();
        chiado.loop = true;
        chiado.spatialBlend = 1f;
        chiado.minDistance = 2f;
        chiado.maxDistance = 40f;
        chiado.rolloffMode = AudioRolloffMode.Linear;
        chiado.volume = volumeChiado;
        chiado.Play();
        StartCoroutine(Repetir());
    }

    private IEnumerator Repetir()
    {
        yield return new WaitForSeconds(3f);
        while (true)
        {
            if (Perto()) yield return Transmitir(falaDoJogador: false);
            yield return new WaitForSeconds(intervaloEntreRepeticoes);
        }
    }

    private bool Perto()
    {
        if (jogador == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) jogador = p.transform;
        }
        return jogador != null && Vector3.Distance(jogador.position, transform.position) <= raioDasLegendas;
    }

    public override void Interagir()
    {
        // Se já está transmitindo, o Cândido presta atenção nessa mesma mensagem.
        jogadorPediu = true;
        if (!transmitindo) StartCoroutine(Transmitir(falaDoJogador: true));
    }

    private bool jogadorPediu;

    private IEnumerator Transmitir(bool falaDoJogador)
    {
        if (transmitindo) yield break;
        transmitindo = true;

        if (!GameFlags.Has(flagPerto)) GameFlags.Set(flagPerto);
        if (vozDaMensagem != null) AudioSource.PlayClipAtPoint(vozDaMensagem, transform.position);

        foreach (var f in frases)
            Narrador.Falar(f, null, 0f, "RÁDIO");

        while (Narrador.Falando) yield return null;

        if ((falaDoJogador || jogadorPediu) && !GameFlags.Has(flagOuviu))
        {
            GameFlags.Set(flagOuviu);
            Narrador.Falar("Menos cento e trinta e dois... quatrocentos e trinta e dois... menos vinte e dois.");
            Narrador.Falar("É pra lá que esse trilho vai. Se esse trem andar, eu chego lá.");
        }

        jogadorPediu = false;
        transmitindo = false;
    }

    /// <summary>Chiado de rádio: ruído filtrado com estalos e um zumbido de fundo.</summary>
    private static AudioClip GerarChiado()
    {
        const int rate = 22050;
        int n = rate * 4;
        var data = new float[n];
        var rnd = new System.Random(42);
        float lp = 0f, hp = 0f, prev = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.35f;          // passa-baixa
            hp = lp - prev; prev = lp;       // passa-alta simples (tira o grave)
            float s = hp * 0.9f;

            // variação lenta de intensidade (sinal "indo e vindo")
            s *= 0.6f + 0.4f * Mathf.Sin(t * 1.7f) * Mathf.Sin(t * 0.43f + 1f);

            // estalos
            if (rnd.NextDouble() < 0.0009) s += (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.8f;

            // zumbido de 60 Hz bem baixo
            s += Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.03f;

            data[i] = s;
        }

        // emenda o loop sem estalo
        int fade = rate / 20;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] *= k;
            data[n - 1 - i] *= k;
        }

        var clip = AudioClip.Create("Chiado do Radio", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
