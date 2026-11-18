using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liga e desliga um painel de LED com piscadas e linhas acendendo uma a uma.
/// O som é gerado por código a partir do MESMO padrão de piscadas, então
/// estalos, zumbido e "tics" ficam sincronizados com o que aparece na tela.
/// </summary>
public class LedBoardPower : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Grupo com os LEDs (textos e faixas). O fundo do painel fica de fora.")]
    [SerializeField] private CanvasGroup leds;
    [SerializeField] private Light panelLight;
    [SerializeField] private AudioSource audioSource;

    [Header("Tempo")]
    [Tooltip("Intervalo entre uma linha acender e a próxima.")]
    [SerializeField] private float rowInterval = 0.06f;

    [Header("Som")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
    [Range(0f, 0.2f)] [SerializeField] private float humVolume = 0.035f;
    [SerializeField] private float humFrequency = 100f;

    // Padrão das piscadas ao ligar: (tempo, brilho)
    private static readonly Vector2[] OnPattern =
    {
        new Vector2(0.00f, 1f), new Vector2(0.04f, 0f), new Vector2(0.11f, 0.7f), new Vector2(0.15f, 0f),
        new Vector2(0.27f, 1f), new Vector2(0.31f, 0.25f), new Vector2(0.37f, 1f), new Vector2(0.41f, 0f),
        new Vector2(0.50f, 1f), new Vector2(0.56f, 0f),
    };

    // Padrão ao desligar: lampejo e apaga
    private static readonly Vector2[] OffPattern =
    {
        new Vector2(0.00f, 1f), new Vector2(0.05f, 0.3f), new Vector2(0.09f, 1f), new Vector2(0.15f, 0f),
    };

    private const float RowsStart = 0.62f; // quando as linhas começam a acender, uma a uma
    private const int SampleRate = 44100;

    private readonly List<List<CanvasRenderer>> rows = new List<List<CanvasRenderer>>();
    private AudioClip onClip, offClip, humClip;
    private AudioSource humSource;
    private float lightBase;
    private Coroutine routine;

    public bool IsOn { get; private set; }

    private void Awake()
    {
        if (panelLight != null)
            lightBase = panelLight.intensity;

        CollectRows();

        onClip = BuildOnClip();
        offClip = BuildOffClip();
        humClip = BuildHumLoop();

        if (audioSource != null && humVolume > 0f)
        {
            humSource = gameObject.AddComponent<AudioSource>();
            humSource.clip = humClip;
            humSource.loop = true;
            humSource.playOnAwake = false;
            humSource.volume = 0f;
            humSource.spatialBlend = audioSource.spatialBlend;
            humSource.minDistance = audioSource.minDistance;
            humSource.maxDistance = audioSource.maxDistance;
        }

        SetInstant(false);
    }

    // ------------------------------------------------------------- Comandos

    public void PowerOn(float delay = 0f)
    {
        if (IsOn) return;
        IsOn = true;
        Restart(OnRoutine(delay));
    }

    public void PowerOff()
    {
        if (!IsOn) return;
        IsOn = false;
        Restart(OffRoutine());
    }

    public void SetInstant(bool on)
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        IsOn = on;
        SetLevel(on ? 1f : 0f);
        SetRows(on ? rows.Count : 0);
        SetHum(on ? humVolume : 0f);
    }

    private void Restart(IEnumerator next)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(next);
    }

    // ------------------------------------------------------------- Animações

    private IEnumerator OnRoutine(float delay)
    {
        SetLevel(0f);
        SetRows(rows.Count);

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Play(onClip);
        float t = 0f;
        float end = RowsStart + rows.Count * rowInterval;

        while (t < end)
        {
            t += Time.unscaledDeltaTime;

            if (t < RowsStart)
            {
                SetRows(rows.Count);
                SetLevel(Sample(OnPattern, t));
            }
            else
            {
                // Linhas acendendo de cima para baixo
                SetLevel(1f);
                SetRows(Mathf.FloorToInt((t - RowsStart) / rowInterval) + 1);
            }

            yield return null;
        }

        SetLevel(1f);
        SetRows(rows.Count);
        SetHum(humVolume);
        routine = null;
    }

    private IEnumerator OffRoutine()
    {
        Play(offClip);
        SetHum(0f);
        float t = 0f;
        float end = OffPattern[OffPattern.Length - 1].x + 0.01f;

        while (t < end)
        {
            t += Time.unscaledDeltaTime;
            SetLevel(Sample(OffPattern, t));
            yield return null;
        }

        SetLevel(0f);
        routine = null;
    }

    private static float Sample(Vector2[] pattern, float t)
    {
        float level = 0f;
        foreach (var p in pattern)
        {
            if (p.x <= t) level = p.y;
            else break;
        }
        return level;
    }

    // ------------------------------------------------------------- Visual

    private void CollectRows()
    {
        rows.Clear();
        if (leds == null) return;

        // Agrupa os gráficos por altura (cada linha do painel vira um grupo).
        var graphics = leds.GetComponentsInChildren<Graphic>(true);
        var byRow = new SortedDictionary<int, List<CanvasRenderer>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));

        foreach (var g in graphics)
        {
            float y = leds.transform.InverseTransformPoint(g.rectTransform.position).y;
            int key = Mathf.RoundToInt(y / 30f);
            if (!byRow.TryGetValue(key, out var list)) byRow[key] = list = new List<CanvasRenderer>();
            list.Add(g.canvasRenderer);
        }

        rows.AddRange(byRow.Values);
    }

    private void SetLevel(float level)
    {
        if (leds != null) leds.alpha = level;
        if (panelLight != null) panelLight.intensity = lightBase * level;
    }

    private void SetRows(int visible)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            float a = i < visible ? 1f : 0f;
            foreach (var cr in rows[i])
                if (cr != null) cr.SetAlpha(a);
        }
    }

    private void SetHum(float v)
    {
        if (humSource == null) return;
        humSource.volume = v;
        if (v > 0f && !humSource.isPlaying) humSource.Play();
        if (v <= 0f && humSource.isPlaying) humSource.Stop();
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    // ------------------------------------------------------------- Áudio gerado

    private AudioClip BuildOnClip()
    {
        float duration = RowsStart + rows.Count * rowInterval + 0.35f;
        var clicks = Changes(OnPattern);
        var ticks = Enumerable.Range(0, rows.Count).Select(i => RowsStart + i * rowInterval).ToArray();
        return Synth("Painel Ligando", duration, t => t < RowsStart ? Sample(OnPattern, t) : 1f, clicks, ticks, false, RowsStart + rows.Count * rowInterval);
    }

    private AudioClip BuildOffClip()
    {
        float duration = 0.55f;
        var clicks = Changes(OffPattern);
        return Synth("Painel Desligando", duration, t => Sample(OffPattern, t), clicks, new float[0], true, OffPattern[OffPattern.Length - 1].x);
    }

    private static float[] Changes(Vector2[] pattern)
    {
        var list = new List<float>();
        float last = 0f;
        foreach (var p in pattern)
        {
            if (!Mathf.Approximately(p.y, last)) list.Add(p.x);
            last = p.y;
        }
        return list.ToArray();
    }

    private AudioClip Synth(string name, float duration, System.Func<float, float> level, float[] clicks, float[] ticks, bool falling, float humEnd)
    {
        int n = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[n];
        var rnd = new System.Random(name.GetHashCode());
        float env = 0f, phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;

            // Zumbido elétrico seguindo o brilho (suavizado para não estalar)
            float target = level(t);
            if (!falling && t > humEnd) target = 0.35f * Mathf.Exp(-(t - humEnd) / 0.15f);
            if (falling && t > humEnd) target = 0.8f * Mathf.Exp(-(t - humEnd) / 0.12f);
            env += (target - env) * (1f - Mathf.Exp(-1f / (0.004f * SampleRate)));

            float f = humFrequency;
            if (falling) f = Mathf.Lerp(humFrequency, humFrequency * 0.35f, Mathf.Clamp01(t / duration));
            phase += 2f * Mathf.PI * f / SampleRate;
            float hum = 0.5f * Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase) + 0.2f * Mathf.Sin(3f * phase);
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float s = env * (hum * 0.22f + noise * 0.025f);

            // Estalo de relé a cada mudança de brilho
            foreach (float c in clicks)
            {
                float d = t - c;
                if (d < 0f || d > 0.08f) continue;
                s += noise * Mathf.Exp(-d / 0.0035f) * 0.5f;
                s += Mathf.Sin(2f * Mathf.PI * 70f * d) * Mathf.Exp(-d / 0.025f) * 0.35f;
            }

            // "Tic" de cada linha acendendo
            foreach (float k in ticks)
            {
                float d = t - k;
                if (d < 0f || d > 0.05f) continue;
                s += Mathf.Sin(2f * Mathf.PI * 1900f * d) * Mathf.Exp(-d / 0.008f) * 0.22f;
            }

            data[i] = s;
        }

        Normalize(data, 0.7f);
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip BuildHumLoop()
    {
        // 1 segundo com número inteiro de ciclos: emenda sem estalo no loop.
        int n = SampleRate;
        var data = new float[n];
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float p = 2f * Mathf.PI * humFrequency * i / SampleRate;
            data[i] = 0.5f * Mathf.Sin(p) + 0.3f * Mathf.Sin(2f * p) + 0.15f * Mathf.Sin(3f * p) + (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.04f;
        }
        Normalize(data, 0.6f);
        var clip = AudioClip.Create("Zumbido do Painel", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        if (max < 1e-5f) return;
        float k = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= k;
    }
}
