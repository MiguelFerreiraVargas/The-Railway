using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A base do rádio (coordenadas -132 432 -22), no fim da ferrovia. Chegar com o trem avisa;
/// no portão, E entra e o jogo termina: epílogo, créditos e volta para o menu.
/// </summary>
public class ChegadaBase : Interagivel
{
    public static ChegadaBase Instance { get; private set; }
    public const string FlagObjetivo = "final.base";

    public string cenaMenu = "Menu";
    public TMP_FontAsset fonte;
    [TextArea] public string[] epilogo =
    {
        "O portão rangeu. Do outro lado, fogueiras acesas... e vozes.",
        "Gente. Gente de verdade.",
        "Uma mulher me olhou de cima a baixo, viu o mindinho sem nada de roxo, e abriu caminho.",
        "Me deram água. Me perguntaram o nome. Pela primeira vez em muito tempo eu soube responder: Cândido.",
        "Não sei o que aconteceu com o mundo. Mas, aqui, eu não estou mais sozinho.",
        "A ferrovia termina aqui. A minha história, não.",
    };

    private bool acabando;

    public override string Texto => "ENTRAR NA BASE";
    public override bool Disponivel => base.Disponivel && !acabando;

    private void Awake()
    {
        Instance = this;
        acao = "ENTRAR NA BASE";
    }

    private void OnEnable() => TrainInteraction.ChegouAoFim += Chegou;
    private void OnDisable() => TrainInteraction.ChegouAoFim -= Chegou;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        Objetivos.Adicionar(FlagObjetivo, "Chegar à base do rádio (-132 432 -22)");
        Objetivos.Detalhe(FlagObjetivo, "no fim da ferrovia   [M] mapa");
    }

    private void Chegou()
    {
        Narrador.Falar("As coordenadas... é aqui. Tem luz lá dentro. Tem gente!");
        Objetivos.Detalhe(FlagObjetivo, "desça do trem e entre pelo portão");
    }

    public override void Interagir()
    {
        if (acabando) return;
        acabando = true;
        GameFlags.Set(FlagObjetivo);
        StartCoroutine(Final());
    }

    private IEnumerator Final()
    {
        // o Cândido para; a tela vai escurecendo
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var inputs = FindFirstObjectByType<StarterAssetsInputs>();
        if (inputs != null) { inputs.move = Vector2.zero; inputs.look = Vector2.zero; }
        if (fpc != null) fpc.enabled = false;
        var stats = PlayerStats.Instance;
        if (stats != null) stats.enabled = false; // ninguém morre de fome nos créditos

        var tela = MontarTela(out var grupo, out var texto, out var titulo);
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 2.5f) { grupo.alpha = a; yield return null; }
        grupo.alpha = 1f;

        foreach (var linha in epilogo)
        {
            texto.text = linha;
            for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 1.2f) { texto.alpha = a; yield return null; }
            texto.alpha = 1f;
            yield return new WaitForSecondsRealtime(Mathf.Clamp(linha.Length * 0.06f, 3f, 6.5f));
            for (float a = 1f; a > 0f; a -= Time.unscaledDeltaTime / 0.9f) { texto.alpha = a; yield return null; }
            texto.alpha = 0f;
        }

        titulo.text = "THE RAILWAY\n<size=40%>FIM</size>";
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 2f) { titulo.alpha = a; yield return null; }
        texto.text = "<size=70%>Obrigado por jogar.</size>";
        texto.rectTransform.anchoredPosition = new Vector2(0f, -170f);
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 1.5f) { texto.alpha = a; yield return null; }
        yield return new WaitForSecondsRealtime(6f);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(cenaMenu)) SceneManager.LoadScene(cenaMenu);
        Destroy(tela);
    }

    private GameObject MontarTela(out CanvasGroup grupo, out TMP_Text texto, out TMP_Text titulo)
    {
        var raiz = new GameObject("Final do Jogo");
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f); escala.matchWidthOrHeight = 1f;
        grupo = raiz.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        var fundo = new GameObject("Preto", typeof(RectTransform));
        var rf = (RectTransform)fundo.transform; rf.SetParent(raiz.transform, false);
        rf.anchorMin = Vector2.zero; rf.anchorMax = Vector2.one; rf.offsetMin = rf.offsetMax = Vector2.zero;
        fundo.AddComponent<Image>().color = Color.black;
        texto = NovoTexto(raiz.transform, 38f, new Vector2(0f, 0f));
        titulo = NovoTexto(raiz.transform, 110f, new Vector2(0f, 60f));
        titulo.text = "";
        return raiz;
    }

    private TMP_Text NovoTexto(Transform pai, float tam, Vector2 pos)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(1500f, 300f);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.fontSize = tam; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.93f, 0.86f, 0.7f);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.alpha = 0f;
        return t;
    }
}
