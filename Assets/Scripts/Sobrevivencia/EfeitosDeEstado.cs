using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// O corpo do Cândido na tela: borda vermelha ao levar golpe, borda escura pulsando com fome ou sede
/// no fim, roxo com a sanidade baixa e tela preta no desmaio.
/// </summary>
public class EfeitosDeEstado : MonoBehaviour
{
    public static EfeitosDeEstado Instance { get; private set; }

    private Image borda, preto;
    private float golpe;
    private float escuro;      // 0..1 alvo da tela preta (desmaio, final)
    private float escuroAtual;
    private float velEscuro = 1f;
    private float vidaAntes = -1f;

    private void Awake()
    {
        Instance = this;
        var raiz = new GameObject("Efeitos de Estado");
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        raiz.AddComponent<CanvasScaler>();

        borda = Cheia("Borda", raiz.transform);
        borda.sprite = Vinheta();
        borda.color = Color.clear;
        preto = Cheia("Preto", raiz.transform);
        preto.color = new Color(0f, 0f, 0f, 0f);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Flash vermelho de golpe.</summary>
    public static void Golpe() { if (Instance != null) Instance.golpe = 1f; }

    /// <summary>Escurece a tela (1) ou volta (0), na velocidade dada (por segundo).</summary>
    public static void Escurecer(float alvo, float velocidade)
    {
        if (Instance == null) return;
        Instance.escuro = alvo;
        Instance.velEscuro = velocidade;
    }

    private void Update()
    {
        var s = PlayerStats.Instance;
        float dt = Time.unscaledDeltaTime;
        golpe = Mathf.MoveTowards(golpe, 0f, dt * 1.6f);

        Color cor = Color.clear;
        if (s != null)
        {
            if (vidaAntes >= 0f && s.vida.atual < vidaAntes - 0.5f) golpe = Mathf.Max(golpe, 0.6f);
            vidaAntes = s.vida.atual;

            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            // fome/sede no fim: escurece a borda, pulsando (vai desmaiar)
            float critico = Mathf.Max(1f - s.fome.Fracao / 0.15f, 1f - s.sede.Fracao / 0.15f);
            critico = Mathf.Clamp01(critico);
            // sanidade baixa: roxo
            float louco = Mathf.Clamp01(1f - s.sanidade.Fracao / 0.35f);

            cor = Color.Lerp(Color.clear, new Color(0f, 0f, 0f, 0.85f), critico * (0.55f + 0.45f * pulso));
            if (louco > 0f)
            {
                var roxo = new Color(0.35f, 0.05f, 0.45f, 0.75f * louco * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 1.3f)));
                cor = Color.Lerp(cor, roxo, roxo.a);
                cor.a = Mathf.Max(cor.a, roxo.a);
            }
            if (s.vida.Fracao < 0.3f) { cor = Color.Lerp(cor, new Color(0.5f, 0f, 0f, 0.6f), (0.3f - s.vida.Fracao) / 0.3f * pulso); }
        }
        if (golpe > 0f) cor = Color.Lerp(cor, new Color(0.75f, 0.02f, 0.02f, 0.9f), golpe);
        borda.color = cor;

        escuroAtual = Mathf.MoveTowards(escuroAtual, escuro, dt * velEscuro);
        preto.color = new Color(0f, 0f, 0f, escuroAtual);
    }

    private static Image Cheia(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    // transparente no meio, opaca nas bordas
    private static Sprite Vinheta()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0.55f, 1.25f, d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}
