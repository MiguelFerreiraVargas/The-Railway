using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra do carvão da locomotiva, no lado direito da tela: enche de baixo para cima, uma marca por
/// pedra, mostra quantos metros ainda dá para andar. Anima ao gastar (brasa tremendo no topo),
/// brilha ao abastecer e pisca vermelha quando está acabando. Criada pelo CombustivelTrem.
/// </summary>
public class BarraCarvao : MonoBehaviour
{
    private CombustivelTrem tender;
    private TrainInteraction trem;
    private CanvasGroup grupo;
    private Image enche, brasa, moldura, brilho;
    private TMP_Text valor, metros;
    private RectTransform caixa;
    private float mostrado = -1f, flash, ultimo;

    private const float Altura = 300f, Largura = 34f;
    private static readonly Color Cheio = new Color(1f, 0.55f, 0.15f);
    private static readonly Color Pouco = new Color(0.85f, 0.15f, 0.08f);

    public static BarraCarvao Criar(CombustivelTrem tender, TMP_FontAsset fonte)
    {
        var go = new GameObject("Barra do Carvão");
        var b = go.AddComponent<BarraCarvao>();
        b.tender = tender;
        b.Montar(fonte);
        return b;
    }

    private void Update()
    {
        if (tender == null) { Destroy(gameObject); return; }
        if (trem == null) trem = tender.GetComponentInParent<TrainInteraction>();
        float dt = Time.unscaledDeltaTime;
        float alvo = tender.Carvao / Mathf.Max(1, tender.Capacidade);
        if (mostrado < 0f) { mostrado = alvo; ultimo = tender.Carvao; }
        if (tender.Carvao > ultimo + 0.5f) flash = 1f; // abasteceu
        ultimo = tender.Carvao;
        mostrado = Mathf.Lerp(mostrado, alvo, 1f - Mathf.Exp(-dt * 6f));
        flash = Mathf.MoveTowards(flash, 0f, dt * 1.5f);

        bool andando = trem != null && trem.VelocidadeAtual > 0.1f;
        bool dentro = trem != null && trem.IsInsideTrain;
        bool fim = alvo < 0.2f;
        float t = Time.unscaledTime;

        // enche de baixo para cima
        var r = enche.rectTransform;
        r.sizeDelta = new Vector2(Largura - 8f, (Altura - 8f) * Mathf.Clamp01(mostrado));
        Color cor = Color.Lerp(Pouco, Cheio, Mathf.Clamp01(mostrado / 0.35f));
        if (fim) cor = Color.Lerp(cor, new Color(1f, 0.3f, 0.2f), 0.5f + 0.5f * Mathf.Sin(t * 7f));
        enche.color = Color.Lerp(cor, Color.white, flash * 0.7f);

        // brasa no topo: treme quando o trem está queimando carvão
        brasa.rectTransform.anchoredPosition = new Vector2(0f, 4f + (Altura - 8f) * Mathf.Clamp01(mostrado));
        float tremor = andando ? 0.55f + 0.45f * Mathf.PerlinNoise(t * 9f, 0f) : 0.35f;
        brasa.color = new Color(1f, 0.85f, 0.4f, mostrado > 0.005f ? tremor : 0f);
        brasa.rectTransform.sizeDelta = new Vector2(Largura - 6f, andando ? 6f + 3f * Mathf.PerlinNoise(0f, t * 11f) : 4f);

        moldura.color = fim ? Color.Lerp(new Color(0.35f, 0.08f, 0.05f), new Color(0.9f, 0.2f, 0.1f), 0.5f + 0.5f * Mathf.Sin(t * 7f)) : new Color(0.22f, 0.16f, 0.1f);
        brilho.color = new Color(1f, 0.7f, 0.3f, flash * 0.6f);
        caixa.anchoredPosition = new Vector2(-34f + (fim && andando ? Mathf.Sin(t * 40f) * 1.5f : 0f), 40f);

        int n = Mathf.CeilToInt(tender.Carvao);
        valor.text = $"{n}/{tender.Capacidade}";
        valor.color = fim ? new Color(1f, 0.45f, 0.35f) : new Color(0.93f, 0.86f, 0.7f);
        metros.text = $"{Mathf.RoundToInt(tender.Carvao * tender.MetrosPorCarvao / 10f) * 10} m";

        // dentro do trem fica bem à vista; fora, mais discreta
        bool some = MapaDoMundo.Aberto || TelaMochila.Aberta || PauseMenu.IsPaused;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, some ? 0f : dentro || fim ? 1f : 0.6f, dt * 3f);
    }

    private void Montar(TMP_FontAsset fonte)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 52;
        var escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 1f;
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false; grupo.interactable = false; grupo.alpha = 0f;

        caixa = Rect("Barra", transform, new Vector2(Largura, Altura), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        moldura = caixa.gameObject.AddComponent<Image>();
        var fundo = Rect("Fundo", caixa, new Vector2(Largura - 6f, Altura - 6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        fundo.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.035f, 0.92f);

        enche = Rect("Carvão", caixa, new Vector2(Largura - 8f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).gameObject.AddComponent<Image>();
        enche.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        brasa = Rect("Brasa", caixa, new Vector2(Largura - 6f, 4f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        brilho = Rect("Brilho", caixa, new Vector2(Largura + 14f, Altura + 14f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        brilho.transform.SetAsFirstSibling();

        // uma marca por pedra
        int cap = Mathf.Max(1, tender.Capacidade);
        for (int i = 1; i < cap; i++)
        {
            var m = Rect("Marca", caixa, new Vector2(Largura - 8f, 2f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            m.anchoredPosition = new Vector2(0f, 4f + (Altura - 8f) * i / cap);
            m.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        }

        var titulo = Texto("Titulo", caixa, "CARVÃO", 20f, fonte);
        titulo.rectTransform.anchoredPosition = new Vector2(0f, Altura / 2f + 20f);
        valor = Texto("Valor", caixa, "", 24f, fonte);
        valor.rectTransform.anchoredPosition = new Vector2(0f, -Altura / 2f - 20f);
        metros = Texto("Metros", caixa, "", 17f, fonte);
        metros.rectTransform.anchoredPosition = new Vector2(0f, -Altura / 2f - 44f);
        metros.color = new Color(0.75f, 0.68f, 0.55f);
    }

    private static RectTransform Rect(string nome, Transform pai, Vector2 tam, Vector2 ancora, Vector2 pivo)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = ancora; rt.pivot = pivo;
        rt.sizeDelta = tam;
        return rt;
    }

    private static TMP_Text Texto(string nome, Transform pai, string texto, float tam, TMP_FontAsset fonte)
    {
        var rt = Rect(nome, pai, new Vector2(140f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.text = texto; t.fontSize = tam; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.93f, 0.86f, 0.7f); t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
