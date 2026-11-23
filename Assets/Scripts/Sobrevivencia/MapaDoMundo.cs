using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Mapa da região (tecla M): a imagem de cima do terreno com o trilho e as construções
/// (feita no editor pelo MapaBuilder) e, por cima, os montes de carvão (laranja = ainda tem,
/// cinza = vazio, com quantas pedras restam), o trem e o Cândido. O monte com carvão mais
/// perto pisca. O jogo não pausa com o mapa aberto.
/// </summary>
public class MapaDoMundo : MonoBehaviour
{
    [Tooltip("Imagem de cima do terreno (Assets/Textures/Mapa/MapaDoMundo.png).")]
    public Texture2D imagem;
    [Tooltip("Canto do terreno no mundo (x, z) e tamanho coberto pela imagem (m).")]
    public Vector2 mundoMin;
    public Vector2 mundoTamanho = new Vector2(1000f, 1000f);
    public TMP_FontAsset fonte;
    public Key tecla = Key.M;
    [Tooltip("Lado do mapa na tela (em pixels de uma tela 1920x1080).")]
    public float tamanho = 860f;

    /// <summary>Se o mapa está aberto agora.</summary>
    public static bool Aberto { get; private set; }
    public static MapaDoMundo Instance { get; private set; }

    private static readonly Color CorCarvao = new Color(1f, 0.55f, 0.12f);
    private static readonly Color CorVazio = new Color(0.45f, 0.42f, 0.38f);
    private static readonly Color CorTinta = new Color(0.18f, 0.12f, 0.07f);
    private static readonly Color CorTrem = new Color(0.62f, 0.12f, 0.08f);
    private static readonly Color CorJogador = new Color(0.1f, 0.45f, 0.95f);

    private CanvasGroup grupo;
    private RectTransform areaMapa;
    private RectTransform marcaJogador, marcaTrem, marcaBase;
    private TMP_Text rodape;
    private readonly List<(MonteDeCarvao monte, RectTransform rt, Image cor, TMP_Text texto)> marcas = new List<(MonteDeCarvao, RectTransform, Image, TMP_Text)>();
    private Sprite circulo, seta;
    private Transform trem;
    private float alvo;

    private void Awake()
    {
        Instance = this;
        Aberto = false;
        circulo = Circulo();
        seta = Seta();
        Montar();
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; Aberto = false; }
    }

    private void Update()
    {
        bool bloqueado = PauseMenu.IsPaused || Leitura.Aberta;
        if (!bloqueado && Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame) alvo = alvo > 0.5f ? 0f : 1f;
        if (bloqueado) alvo = 0f;
        Aberto = alvo > 0.5f;

        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alvo, Time.unscaledDeltaTime * 6f);
        if (grupo.alpha > 0.001f) Atualizar();
    }

    /// <summary>Abre ou fecha (para testes e outros scripts).</summary>
    public void Mostrar(bool abrir)
    {
        alvo = abrir ? 1f : 0f;
        Aberto = abrir;
    }

    // ------------------------------------------------------------ marcas

    private void Atualizar()
    {
        var cam = Camera.main;
        Vector3 jogador = cam != null ? cam.transform.position : Vector3.zero;
        var perto = MonteDeCarvao.MaisPerto(jogador, out float dist);

        // montes novos (ou que sumiram) refazem a lista
        if (marcas.Count != MonteDeCarvao.Todos.Count) RefazerMontes();
        foreach (var (monte, rt, cor, texto) in marcas)
        {
            if (monte == null) continue;
            rt.anchoredPosition = NoMapa(monte.transform.position);
            bool tem = monte.TemCarvao;
            cor.color = tem ? CorCarvao : CorVazio;
            int pedras = monte.Pedras;
            texto.text = pedras < 0 ? "" : pedras.ToString();
            float escala = monte == perto ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
            rt.localScale = Vector3.one * escala;
        }

        if (trem == null)
        {
            var t = FindFirstObjectByType<TrainInteraction>();
            if (t != null) trem = t.transform;
        }
        if (trem != null)
        {
            marcaTrem.anchoredPosition = NoMapa(trem.position);
            marcaTrem.localEulerAngles = new Vector3(0f, 0f, -Rumo(trem.forward));
        }
        var baseFinal = ChegadaBase.Instance;
        marcaBase.gameObject.SetActive(baseFinal != null);
        if (baseFinal != null) marcaBase.anchoredPosition = NoMapa(baseFinal.transform.position);
        if (cam != null)
        {
            marcaJogador.anchoredPosition = NoMapa(jogador);
            marcaJogador.localEulerAngles = new Vector3(0f, 0f, -Rumo(cam.transform.forward));
        }

        int comCarvao = 0;
        foreach (var m in MonteDeCarvao.Todos) if (m != null && m.TemCarvao) comCarvao++;
        rodape.text = perto != null
            ? $"{comCarvao} montes com carvão   ·   o mais perto está a {Mathf.RoundToInt(dist / 10f) * 10} m"
            : "não sobrou carvão nos montes conhecidos";
    }

    private void RefazerMontes()
    {
        foreach (var m in marcas) if (m.rt != null) Destroy(m.rt.gameObject);
        marcas.Clear();
        foreach (var monte in MonteDeCarvao.Todos)
        {
            if (monte == null) continue;
            // contorno escuro + bolinha colorida + quantas pedras restam
            var rt = NovoRect("Monte", areaMapa, new Vector2(26f, 26f));
            var borda = rt.gameObject.AddComponent<Image>();
            borda.sprite = circulo; borda.color = CorTinta;
            var dentro = NovoRect("Cor", rt, new Vector2(18f, 18f));
            var cor = dentro.gameObject.AddComponent<Image>();
            cor.sprite = circulo;
            var texto = NovoTexto("Pedras", rt, "", 20f, TextAlignmentOptions.Center, CorTinta);
            texto.rectTransform.sizeDelta = new Vector2(60f, 24f);
            texto.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            texto.fontStyle = FontStyles.Bold;
            marcas.Add((monte, rt, cor, texto));
        }
        // trem e jogador ficam por cima dos montes
        marcaTrem.SetAsLastSibling();
        marcaJogador.SetAsLastSibling();
    }

    private Vector2 NoMapa(Vector3 mundo)
    {
        float u = (mundo.x - mundoMin.x) / Mathf.Max(1f, mundoTamanho.x);
        float v = (mundo.z - mundoMin.y) / Mathf.Max(1f, mundoTamanho.y);
        u = Mathf.Clamp01(u); v = Mathf.Clamp01(v);
        return new Vector2((u - 0.5f) * tamanho, (v - 0.5f) * tamanho);
    }

    // ângulo de uma direção no chão, em graus, a partir do norte (+Z) no sentido horário
    private static float Rumo(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

    // ------------------------------------------------------------ montagem da tela

    private void Montar()
    {
        var raiz = new GameObject("Mapa (tela)");
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 1f;
        grupo = raiz.AddComponent<CanvasGroup>();
        grupo.alpha = 0f; grupo.blocksRaycasts = false; grupo.interactable = false;

        var fundo = NovoRect("Fundo", raiz.transform, Vector2.zero);
        fundo.anchorMin = Vector2.zero; fundo.anchorMax = Vector2.one;
        fundo.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

        var moldura = NovoRect("Moldura", raiz.transform, new Vector2(tamanho + 28f, tamanho + 28f));
        moldura.anchoredPosition = new Vector2(0f, 10f);
        moldura.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.17f, 0.1f);

        areaMapa = NovoRect("Mapa", moldura, new Vector2(tamanho, tamanho));
        var img = areaMapa.gameObject.AddComponent<RawImage>();
        img.texture = imagem;
        img.color = imagem != null ? Color.white : new Color(0.8f, 0.72f, 0.56f);

        var titulo = NovoTexto("Titulo", moldura, "MAPA DA REGIÃO", 34f, TextAlignmentOptions.Center, new Color(0.93f, 0.86f, 0.7f));
        titulo.rectTransform.anchoredPosition = new Vector2(0f, tamanho * 0.5f + 34f);
        titulo.rectTransform.sizeDelta = new Vector2(tamanho, 40f);

        rodape = NovoTexto("Rodape", moldura, "", 22f, TextAlignmentOptions.Center, new Color(0.93f, 0.86f, 0.7f));
        rodape.rectTransform.anchoredPosition = new Vector2(0f, -tamanho * 0.5f - 30f);
        rodape.rectTransform.sizeDelta = new Vector2(tamanho + 200f, 30f);

        // legenda ao lado do mapa
        var legenda = NovoRect("Legenda", raiz.transform, new Vector2(300f, 260f));
        legenda.anchoredPosition = new Vector2(tamanho * 0.5f + 200f, 10f);
        float y = 100f;
        void Linha(string nome, Sprite sprite, Color cor, Vector2 tam, float giro = 0f)
        {
            var icone = NovoRect(nome, legenda, tam);
            icone.anchoredPosition = new Vector2(-120f, y);
            icone.localEulerAngles = new Vector3(0f, 0f, giro);
            var im = icone.gameObject.AddComponent<Image>();
            im.sprite = sprite; im.color = cor;
            var t = NovoTexto(nome, legenda, nome, 22f, TextAlignmentOptions.Left, new Color(0.93f, 0.86f, 0.7f));
            t.rectTransform.anchoredPosition = new Vector2(30f, y);
            t.rectTransform.sizeDelta = new Vector2(240f, 30f);
            y -= 42f;
        }
        Linha("carvão (pedras)", circulo, CorCarvao, new Vector2(20f, 20f));
        Linha("monte vazio", circulo, CorVazio, new Vector2(20f, 20f));
        Linha("você", seta, CorJogador, new Vector2(24f, 28f));
        Linha("o trem", null, CorTrem, new Vector2(26f, 11f));
        Linha("casas e vagões", null, CorTinta, new Vector2(16f, 16f));
        var fechar = NovoTexto("Fechar", legenda, $"[{tecla}] fechar", 20f, TextAlignmentOptions.Left, new Color(0.75f, 0.68f, 0.55f));
        fechar.rectTransform.anchoredPosition = new Vector2(10f, y - 20f);
        fechar.rectTransform.sizeDelta = new Vector2(260f, 30f);

        marcaTrem = NovoRect("Trem", areaMapa, new Vector2(12f, 30f));
        var it = marcaTrem.gameObject.AddComponent<Image>();
        it.color = CorTrem;
        var contornoTrem = marcaTrem.gameObject.AddComponent<UnityEngine.UI.Outline>();
        contornoTrem.effectColor = CorTinta; contornoTrem.effectDistance = new Vector2(2f, 2f);

        // a base do rádio, no fim da linha
        marcaBase = NovoRect("Base", areaMapa, new Vector2(30f, 30f));
        var ib = marcaBase.gameObject.AddComponent<Image>();
        ib.color = new Color(0.75f, 0.1f, 0.08f);
        marcaBase.localEulerAngles = new Vector3(0f, 0f, 45f);
        var tb = NovoTexto("Nome", marcaBase, "BASE", 22f, TextAlignmentOptions.Center, new Color(0.6f, 0.05f, 0.03f));
        tb.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
        tb.rectTransform.anchoredPosition = new Vector2(-20f, 20f);
        tb.fontStyle = FontStyles.Bold;

        marcaJogador = NovoRect("Jogador", areaMapa, new Vector2(24f, 30f));
        var ij = marcaJogador.gameObject.AddComponent<Image>();
        ij.sprite = seta; ij.color = CorJogador;
        var contornoJog = marcaJogador.gameObject.AddComponent<UnityEngine.UI.Outline>();
        contornoJog.effectColor = Color.white; contornoJog.effectDistance = new Vector2(1.5f, 1.5f);
    }

    private static RectTransform NovoRect(string nome, Transform pai, Vector2 tam)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tam;
        return rt;
    }

    private TMP_Text NovoTexto(string nome, Transform pai, string texto, float tam, TextAlignmentOptions alinhar, Color cor)
    {
        var rt = NovoRect(nome, pai, new Vector2(300f, 40f));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.text = texto; t.fontSize = tam; t.alignment = alinhar; t.color = cor;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // ------------------------------------------------------------ desenhos simples

    private static Sprite Circulo()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                byte a = (byte)(Mathf.Clamp01(n / 2f - d) * 255);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    // seta apontando para cima (a frente do jogador)
    private static Sprite Seta()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        Vector2 a = new Vector2(n / 2f, n - 2f), b = new Vector2(4f, 2f), c = new Vector2(n - 4f, 2f), m = new Vector2(n / 2f, 18f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool dentro = (NoTriangulo(p, a, b, m) || NoTriangulo(p, a, m, c));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(dentro ? 255 : 0));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    private static bool NoTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        float s2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
        float s3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
        return (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
    }
}
