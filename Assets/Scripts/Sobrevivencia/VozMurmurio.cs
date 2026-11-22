using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera, por síntese, sílabas de "murmúrio" (voz abafada, sem palavras) para acompanhar as legendas.
/// Cada sílaba é um trem de pulsos da glote passando por formantes de vogal e por um filtro
/// que abre e fecha como uma boca. Nada de arquivo de áudio: tudo é criado na primeira vez que é usado.
/// </summary>
public static class VozMurmurio
{
    public enum Tipo { Candido, Radio }

    private const int Variacoes = 14;
    private static readonly Dictionary<Tipo, AudioClip[]> cache = new Dictionary<Tipo, AudioClip[]>();

    // (F1, F2, F3) de vogais faladas por um homem adulto
    private static readonly Vector3[] Vogais =
    {
        new Vector3(730f, 1090f, 2440f), // a
        new Vector3(530f, 1840f, 2480f), // e
        new Vector3(400f, 1900f, 2550f), // i
        new Vector3(570f, 840f, 2410f),  // o
        new Vector3(320f, 870f, 2240f),  // u
        new Vector3(480f, 1300f, 2400f), // ə (neutra, a mais comum num murmúrio)
    };

    public static AudioClip Silaba(Tipo tipo)
    {
        if (!cache.TryGetValue(tipo, out var clips) || clips[0] == null)
        {
            clips = new AudioClip[Variacoes];
            var rng = new System.Random(tipo == Tipo.Candido ? 1971 : 4242);
            for (int i = 0; i < Variacoes; i++) clips[i] = Criar(tipo, i, rng);
            cache[tipo] = clips;
        }
        return clips[Random.Range(0, clips.Length)];
    }

    private static AudioClip Criar(Tipo tipo, int indice, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        int sr = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        bool radio = tipo == Tipo.Radio;

        float dur = R(0.10f, 0.19f);
        int n = Mathf.CeilToInt(dur * sr);
        var dados = new float[n];

        float f0 = radio ? R(122f, 138f) : R(98f, 112f); // Cândido: voz grave e cansada
        float queda = R(0.03f, 0.12f);                    // a entonação cai no fim da sílaba
        var vogal = Vogais[rng.Next(Vogais.Length)];
        vogal *= R(0.95f, 1.05f);

        // Ressonadores (formantes) em paralelo
        var f1 = Ressonador.Novo(vogal.x, 90f, sr);
        var f2 = Ressonador.Novo(vogal.y, 120f, sr);
        var f3 = Ressonador.Novo(vogal.z, 170f, sr);
        var nasal = Ressonador.Novo(270f, 110f, sr); // o "mm" de boca fechada

        float fase = 0f, gAntes = 0f, lp = 0f, lp2 = 0f, hp = 0f, hpPrev = 0f;
        float jitter = 0f;
        uint ruido = (uint)(indice * 7919 + 17);

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;

            // Frequência com leve tremor e queda
            if (i % 200 == 0) jitter = R(-0.012f, 0.012f);
            float f = f0 * (1.03f - queda * t) * (1f + jitter);
            fase += f / sr;
            if (fase >= 1f) fase -= 1f;

            // Pulso da glote (Rosenberg simplificado): sobe suave, fecha rápido
            float g = fase < 0.55f
                ? 0.5f * (1f - Mathf.Cos(Mathf.PI * fase / 0.55f))
                : fase < 0.8f ? Mathf.Cos(0.5f * Mathf.PI * (fase - 0.55f) / 0.25f) : 0f;

            // Sopro
            ruido ^= ruido << 13; ruido ^= ruido >> 17; ruido ^= ruido << 5;
            float sopro = ((ruido & 0xFFFF) / 32768f - 1f) * 0.08f;
            // Excitação = derivada do fluxo da glote (o "estalo" de cada fechamento)
            float fonte = (g - gAntes) * (sr / f) * 0.25f + sopro;
            gAntes = g;

            // Boca: começa fechada ("m"), abre na vogal e fecha de novo
            float abertura = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.08f) / 0.84f));
            float vogalSom = f1.Passo(fonte) + 0.5f * f2.Passo(fonte) + 0.2f * f3.Passo(fonte);
            float nasalSom = nasal.Passo(fonte);
            float s = Mathf.Lerp(nasalSom, vogalSom, 0.25f + 0.75f * abertura);

            // Abafado: passa-baixa que abre junto com a boca (murmúrio, não fala clara)
            float corte = radio ? 2800f : Mathf.Lerp(900f, 2200f, abertura);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * corte / sr);
            lp += a * (s - lp);
            lp2 += a * (lp - lp2);
            s = lp2;

            if (radio)
            {
                // Rádio: corta os graves, satura e chia
                hp = 0.97f * (hp + s - hpPrev);
                hpPrev = s;
                s = (float)System.Math.Tanh(hp * 3.2f) * 0.6f;
                s += ((ruido >> 16) / 32768f - 1f) * 0.035f;
            }

            // Envelope: ataque e soltura suaves
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f)) *
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.3f));
            dados[i] = s * env;
        }

        // Normaliza
        float pico = 0.0001f;
        for (int i = 0; i < n; i++) pico = Mathf.Max(pico, Mathf.Abs(dados[i]));
        float ganho = 0.85f / pico;
        for (int i = 0; i < n; i++) dados[i] *= ganho;

        var clip = AudioClip.Create($"Murmurio {tipo} {indice}", n, 1, sr, false);
        clip.SetData(dados, 0);
        return clip;
    }

    /// <summary>Passa-banda (biquad RBJ, ganho 1 no centro): realça um formante sem deixar passar o grave.</summary>
    private struct Ressonador
    {
        private float b0, b2, a1, a2, x1, x2, y1, y2;

        public static Ressonador Novo(float freq, float largura, int sr)
        {
            float w0 = 2f * Mathf.PI * freq / sr;
            float q = Mathf.Max(0.5f, freq / largura);
            float alfa = Mathf.Sin(w0) / (2f * q);
            float a0 = 1f + alfa;
            return new Ressonador { b0 = alfa / a0, b2 = -alfa / a0, a1 = -2f * Mathf.Cos(w0) / a0, a2 = (1f - alfa) / a0 };
        }

        public float Passo(float x)
        {
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            return y;
        }
    }
}
