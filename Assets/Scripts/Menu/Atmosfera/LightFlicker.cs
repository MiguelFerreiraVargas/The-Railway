using System.Collections;
using UnityEngine;

/// <summary>
/// Faz uma luz oscilar como uma lâmpada velha de poste (sódio) ou lampião.
/// Também pode "acender" com falhas depois de um atraso, para a abertura do menu.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Intensidade")]
    [Tooltip("Intensidade base da luz. Se 0, usa a intensidade atual da Light.")]
    [SerializeField] private float baseIntensity = 0f;

    [Tooltip("Quanto a luz varia em torno da base (0.1 = 10%).")]
    [Range(0f, 1f)]
    [SerializeField] private float flickerAmount = 0.12f;

    [Tooltip("Velocidade da oscilação suave.")]
    [SerializeField] private float flickerSpeed = 3f;

    [Header("Falhas (lâmpada morrendo)")]
    [Tooltip("Chance por segundo de a lâmpada falhar rapidamente.")]
    [Range(0f, 2f)]
    [SerializeField] private float failChancePerSecond = 0.05f;

    [Tooltip("Intensidade relativa durante a falha (0 = apaga).")]
    [Range(0f, 1f)]
    [SerializeField] private float failIntensity = 0.08f;

    [SerializeField] private Vector2 failDuration = new Vector2(0.04f, 0.18f);

    [Header("Acendimento na abertura")]
    [SerializeField] private bool igniteOnStart = true;

    [Tooltip("Tempo antes de a luz começar a acender.")]
    [SerializeField] private float igniteDelay = 0.5f;

    [Tooltip("Duração do acendimento com piscadas.")]
    [SerializeField] private float igniteDuration = 0.9f;

    [Header("Emissão (opcional)")]
    [Tooltip("Renderer da lâmpada cuja emissão acompanha a luz.")]
    [SerializeField] private Renderer emissiveRenderer;

    [SerializeField] private int emissiveMaterialIndex = 0;

    [ColorUsage(false, true)]
    [SerializeField] private Color emissiveColor = new Color(4f, 2.2f, 0.9f);

    [Header("Som (opcional)")]
    [Tooltip("Zumbido elétrico; o volume acompanha a luz.")]
    [SerializeField] private AudioSource buzzSource;

    [SerializeField] private float buzzMaxVolume = 0.15f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Light targetLight;
    private MaterialPropertyBlock propertyBlock;
    private float noiseSeed;
    private float ignitionFactor = 1f;
    private float failTimer;

    public float BaseIntensity
    {
        get => baseIntensity;
        set => baseIntensity = Mathf.Max(0f, value);
    }

    public float IgniteDelay
    {
        get => igniteDelay;
        set => igniteDelay = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        targetLight = GetComponent<Light>();

        if (baseIntensity <= 0f)
            baseIntensity = targetLight.intensity;

        noiseSeed = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        if (igniteOnStart && Application.isPlaying)
        {
            ignitionFactor = 0f;
            StartCoroutine(IgniteRoutine());
        }
        else
        {
            ignitionFactor = 1f;
        }
    }

    private IEnumerator IgniteRoutine()
    {
        ApplyIntensity(0f);

        yield return new WaitForSecondsRealtime(igniteDelay);

        float elapsed = 0f;

        while (elapsed < igniteDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / igniteDuration);

            // Piscadas aleatórias que ficam mais estáveis no fim.
            bool blink = Random.value > Mathf.Lerp(0.35f, 0.97f, progress);
            ignitionFactor = blink ? Random.Range(0.6f, 1.1f) * progress : progress * 0.15f;

            yield return null;
        }

        ignitionFactor = 1f;
    }

    private void Update()
    {
        float time = Time.unscaledTime * flickerSpeed;

        // Soma de duas ondas de ruído: uma lenta e uma rápida.
        float slow = Mathf.PerlinNoise(noiseSeed, time * 0.35f);
        float fast = Mathf.PerlinNoise(noiseSeed + 37.1f, time * 2.3f);
        float noise = (slow * 0.7f + fast * 0.3f) * 2f - 1f;

        float factor = 1f + noise * flickerAmount;

        if (failTimer > 0f)
        {
            failTimer -= Time.unscaledDeltaTime;
            factor *= failIntensity;
        }
        else if (Random.value < failChancePerSecond * Time.unscaledDeltaTime)
        {
            failTimer = Random.Range(failDuration.x, failDuration.y);
        }

        ApplyIntensity(baseIntensity * factor * ignitionFactor);
    }

    private void ApplyIntensity(float intensity)
    {
        if (targetLight == null)
            return;

        targetLight.intensity = intensity;

        float relative = baseIntensity > 0f ? intensity / baseIntensity : 0f;

        if (emissiveRenderer != null)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            emissiveRenderer.GetPropertyBlock(propertyBlock, emissiveMaterialIndex);
            propertyBlock.SetColor(EmissionColorId, emissiveColor * relative);
            emissiveRenderer.SetPropertyBlock(propertyBlock, emissiveMaterialIndex);
        }

        if (buzzSource != null)
            buzzSource.volume = buzzMaxVolume * Mathf.Clamp01(relative);
    }
}
