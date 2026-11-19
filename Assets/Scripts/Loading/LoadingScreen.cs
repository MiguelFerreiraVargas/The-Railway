using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela de carregamento entre o menu e o jogo.
/// Uso: LoadingScreen.Load("CenaPrincipal");
///
/// Some só quando: a cena carregou, todos os ILoadingProgress da cena estão
/// prontos (ex.: vegetação gerada) e o jogo rodou alguns frames estável.
/// Enquanto isso o mundo fica congelado (tempo parado, jogador travado).
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private const string ResourcePath = "Tela de Carregamento";

    public static bool IsLoading { get; private set; }

    [Header("Interface")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Image[] segments;
    [SerializeField] private Color segmentOn = new Color(1f, 0.68f, 0.18f, 1f);
    [SerializeField] private Color segmentOff = new Color(1f, 0.68f, 0.18f, 0.12f);

    [Header("Tempo")]
    [Tooltip("Tempo mínimo na tela, para não piscar.")]
    [SerializeField] private float minShowTime = 2.5f;
    [Tooltip("Tempo máximo esperando o mapa; depois disso a tela some de qualquer jeito.")]
    [SerializeField] private float maxWaitTime = 40f;
    [Tooltip("Quantos frames seguidos sem travada para considerar o jogo estável.")]
    [SerializeField] private int stableFrames = 30;
    [Tooltip("Um frame mais lento que isso (segundos) conta como travada.")]
    [SerializeField] private float stableFrameTime = 0.05f;
    [SerializeField] private float fadeOutTime = 0.6f;

    [Header("Dicas")]
    [TextArea]
    [SerializeField] private string[] tips =
    {
        "O vírus Violeta começa pelo mindinho roxo.",
        "Sem carvão, a locomotiva para. Nunca deixe acabar.",
        "As coordenadas do rádio: -132 432 -22.",
        "Cândido não lembra de nada. Os documentos podem ajudar.",
        "Carne crua estraga com o tempo. Use a fogueira.",
        "Água e soda recuperam a stamina.",
        "Às vezes o trilho está bloqueado. Desça e explore.",
    };

    private float shownProgress;
    private string baseStatus = "";

    // ------------------------------------------------------------- Entrada

    public static void Load(string sceneName)
    {
        if (IsLoading)
            return;

        var prefab = Resources.Load<LoadingScreen>(ResourcePath);

        if (prefab == null)
        {
            Debug.LogWarning("[The Railway] Tela de carregamento não encontrada em Resources; carregando direto.");
            SceneManager.LoadScene(sceneName);
            return;
        }

        var screen = Instantiate(prefab);
        screen.name = "Tela de Carregamento";
        DontDestroyOnLoad(screen.gameObject);
        screen.StartCoroutine(screen.Run(sceneName));
    }

    // ------------------------------------------------------------- Fluxo

    private IEnumerator Run(string sceneName)
    {
        IsLoading = true;
        float started = Time.realtimeSinceStartup;
        float Elapsed() => Time.realtimeSinceStartup - started;

        if (group != null) group.alpha = 1f;
        if (tipText != null && tips != null && tips.Length > 0)
            tipText.text = tips[Random.Range(0, tips.Length)];

        // 1) Carrega a cena em segundo plano (0% a 40%)
        SetStatus("CARREGANDO A CENA");
        var op = SceneManager.LoadSceneAsync(sceneName);

        while (op != null && !op.isDone)
        {
            SetProgress(Mathf.Clamp01(op.progress / 0.9f) * 0.4f);
            yield return null;
        }

        // Congela o mundo enquanto o mapa termina de montar
        yield return null; // deixa os Start() da cena rodarem (ex.: carregar o save)
        float timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        var frozen = FreezePlayer();

        // 2) Espera os sistemas da cena ficarem prontos (40% a 90%)
        var systems = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(b => b.isActiveAndEnabled).OfType<ILoadingProgress>().ToList();

        foreach (var s in systems) s.SetLoadingBoost(true);
        yield return null;

        while (Elapsed() < maxWaitTime)
        {
            var pending = systems.Where(s => s != null && !s.IsLoaded).ToList();
            if (pending.Count == 0) break;

            float p = systems.Count == 0 ? 1f : systems.Average(s => s.IsLoaded ? 1f : Mathf.Clamp01(s.LoadingProgress));
            SetProgress(0.4f + 0.5f * p);
            SetStatus(pending[0].LoadingLabel);
            yield return null;
        }

        foreach (var s in systems) if (s != null) s.SetLoadingBoost(false);

        // 3) Espera o jogo rodar estável, sem travadas (90% a 100%)
        SetStatus("AJUSTANDO OS TRILHOS");
        int steady = 0;
        while (steady < stableFrames && Elapsed() < maxWaitTime)
        {
            steady = Time.unscaledDeltaTime <= stableFrameTime ? steady + 1 : 0;
            SetProgress(0.9f + 0.1f * steady / Mathf.Max(1, stableFrames));
            yield return null;
        }

        while (Elapsed() < minShowTime)
        {
            SetProgress(1f);
            yield return null;
        }

        SetProgress(1f);
        SetStatus("PRONTO");
        yield return new WaitForSecondsRealtime(0.25f);

        // Libera o mundo e some
        Time.timeScale = timeScaleBefore;
        UnfreezePlayer(frozen);
        IsLoading = false;

        float t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            if (group != null) group.alpha = 1f - t / fadeOutTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Segurança: nunca deixa o jogo congelado se algo der errado
        if (IsLoading)
        {
            IsLoading = false;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        // Pontinhos animados no texto da etapa
        if (statusText != null && !string.IsNullOrEmpty(baseStatus) && baseStatus != "PRONTO")
        {
            int dots = Mathf.FloorToInt(Time.unscaledTime * 3f) % 4;
            statusText.text = baseStatus + new string('.', dots);
        }
    }

    // ------------------------------------------------------------- Visual

    private void SetStatus(string status)
    {
        baseStatus = string.IsNullOrEmpty(status) ? "CARREGANDO" : status;
        if (statusText != null) statusText.text = baseStatus;
    }

    private void SetProgress(float target)
    {
        // A barra nunca anda para trás
        shownProgress = Mathf.Max(shownProgress, Mathf.Clamp01(target));

        if (percentText != null)
            percentText.text = $"{Mathf.RoundToInt(shownProgress * 100f)}%";

        if (segments == null) return;
        int lit = Mathf.RoundToInt(shownProgress * segments.Length);
        for (int i = 0; i < segments.Length; i++)
            if (segments[i] != null) segments[i].color = i < lit ? segmentOn : segmentOff;
    }

    // ------------------------------------------------------------- Jogador

    private struct Frozen
    {
        public FirstPersonController[] controllers;
        public bool[] wasEnabled;
    }

    private static Frozen FreezePlayer()
    {
        var controllers = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        var was = new bool[controllers.Length];
        for (int i = 0; i < controllers.Length; i++)
        {
            was[i] = controllers[i].enabled;
            controllers[i].enabled = false; // a câmera do jogador gira mesmo com o tempo parado
        }
        return new Frozen { controllers = controllers, wasEnabled = was };
    }

    private static void UnfreezePlayer(Frozen frozen)
    {
        if (frozen.controllers == null) return;
        for (int i = 0; i < frozen.controllers.Length; i++)
            if (frozen.controllers[i] != null) frozen.controllers[i].enabled = frozen.wasEnabled[i];

        foreach (var input in FindObjectsByType<StarterAssetsInputs>(FindObjectsSortMode.None))
        {
            input.look = Vector2.zero;
            input.move = Vector2.zero;
        }
    }
}
