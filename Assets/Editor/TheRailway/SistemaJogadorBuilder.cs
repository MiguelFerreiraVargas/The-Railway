using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Cria o prefab "Sistema do Jogador": necessidades (PlayerStats), HUD das barras,
/// aviso de interação, legendas, objetivos, leitura de jornais e tela de morte.
/// </summary>
public static class SistemaJogadorBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Sistema do Jogador/Sistema do Jogador.prefab";
    private const string LedFontPath = "Assets/Prefabs/VCR_OSD_MONO_1.001 SDF.asset";
    private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly Color Led = new Color(1f, 0.68f, 0.18f, 1f);
    private static readonly Color LedDim = new Color(1f, 0.68f, 0.18f, 0.55f);
    private static readonly Color Ivory = new Color(0.93f, 0.9f, 0.83f, 1f);

    private static TMP_FontAsset led, body;

    [MenuItem("The Railway/Sistema do Jogador/Criar prefab")]
    private static void BuildFromMenu()
    {
        var sb = new StringBuilder();
        Build(sb);
        Debug.Log("[The Railway] Sistema do Jogador:\n" + sb);
    }

    public static void Build(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Sistema do Jogador"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Sistema do Jogador");

        led = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LedFontPath);
        body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        var go = BuildRoot();
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        log.AppendLine("prefab salvo: " + PrefabPath);
    }

    /// <summary>Coloca o prefab numa cena (sem duplicar) e desliga as barras antigas de vida/fome.</summary>
    public static void InstallInScene(string scenePath, StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { log.AppendLine("ERRO: prefab não encontrado"); return; }

        if (Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = prefab.name;
            log.AppendLine("Sistema do Jogador colocado em " + scene.name);
        }
        else log.AppendLine("a cena já tinha o Sistema do Jogador");

        // As barras antigas (mesma arte para vida e fome) dão lugar ao HUD novo.
        foreach (var b in Object.FindObjectsByType<BarraVida>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.activeSelf) { b.gameObject.SetActive(false); log.AppendLine("barra antiga desativada: " + b.name + " (vida)"); }
        foreach (var b in Object.FindObjectsByType<BarraFome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.activeSelf) { b.gameObject.SetActive(false); log.AppendLine("barra antiga desativada: " + b.name + " (fome)"); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva: " + scenePath);
    }

    // ------------------------------------------------------------ Montagem

    private static GameObject BuildRoot()
    {
        var root = new GameObject("Sistema do Jogador", typeof(AudioSource));
        var audio = root.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.ignoreListenerPause = true;

        var stats = root.AddComponent<PlayerStats>();
        var sso = new SerializedObject(stats);
        sso.FindProperty("audioSource").objectReferenceValue = audio;
        sso.FindProperty("somComer").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/Comendo.wav");
        sso.ApplyModifiedPropertiesWithoutUndo();

        // Canvas
        var canvasGo = new GameObject("Tela", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var c = (RectTransform)canvasGo.transform;

        BuildHud(root, c);
        BuildInteraction(root, c);
        BuildSubtitles(root, c, audio);
        BuildObjectives(root, c, audio);
        BuildReading(root, c, audio);
        BuildDeath(root, c, audio);

        // Interface na camada "UI" (padrão do Unity)
        foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;

        return root;
    }

    private static void BuildHud(GameObject root, RectTransform c)
    {
        var hudGo = Group(c, "HUD", stretch: true);
        var hud = (RectTransform)hudGo.transform;
        var hudCg = hudGo.GetComponent<CanvasGroup>();
        hudCg.interactable = false;
        hudCg.blocksRaycasts = false;

        // Ponto de mira
        var mira = Img(hud, "Mira", new Color(1f, 1f, 1f, 0.55f), Vector2.zero, new Vector2(6f, 6f), center: true);

        // Painel na arte do jogo (moldura com janelas)
        var barras = PainelDeArte(hud, out float topoPainel);

        var dicas = Txt(hud, "Dicas Consumo", "", body, 22, Ivory, new Vector2(28f, topoPainel + 22f), new Vector2(700f, 34f), TextAlignmentOptions.Left, anchor: new Vector2(0f, 0f));
        var aviso = Txt(hud, "Aviso Consumo", "", led, 30, Led, new Vector2(0f, 510f), new Vector2(900f, 50f), TextAlignmentOptions.Center, anchor: new Vector2(0.5f, 0f));
        aviso.alpha = 0f;

        var h = root.AddComponent<HudSobrevivencia>();
        var so = new SerializedObject(h);
        so.FindProperty("grupo").objectReferenceValue = hudCg;
        string[] campos = { "vida", "fome", "sede", "stamina", "higiene", "sanidade" };
        for (int i = 0; i < 6; i++) so.FindProperty(campos[i]).objectReferenceValue = barras[i];
        so.FindProperty("dicasConsumo").objectReferenceValue = dicas;
        so.FindProperty("avisoConsumo").objectReferenceValue = aviso;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------ Painel (arte)

    private const string PainelPath = "Assets/Arte/pixilart-drawing (1).png";
    private const string BarraArtePath = "Assets/Arte/pixilart-frames/pixil-frame-0.png";
    private const string CantilPasta = "Assets/Arte/pixilart-frames (1)";
    private const int CantilQuadros = 15;
    private const float PxPainel = 6f;                       // 1 pixel da moldura = 6 px na tela (1080p)
    private static readonly Vector2 CantoPainel = new Vector2(24f, 24f);
    private static readonly Color FundoJanela = new Color(0.12f, 0.09f, 0.08f, 1f);

    /// <summary>
    /// Monta o HUD em cima da moldura desenhada: o cantil (sede) na janela grande,
    /// um cantil roxo (sanidade) na pequena, vida no tubo vertical e fome, higiene
    /// e estamina nas fendas, com a barra segmentada da arte. Devolve as 6 barras na
    /// ordem vida, fome, sede, stamina, higiene, sanidade.
    /// </summary>
    private static BarraSegmentada[] PainelDeArte(RectTransform hud, out float topo)
    {
        var painelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PainelPath);
        var barraSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarraArtePath);
        var cantil = Enumerable.Range(0, CantilQuadros).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{CantilPasta}/pixil-frame-{i}.png")).ToArray();
        if (painelSprite == null || barraSprite == null || cantil.Any(c => c == null))
            throw new System.Exception("Arte do HUD não encontrada em Assets/Arte (moldura, barra ou quadros do cantil).");

        var conteudo = ArteInspetor.Conteudo(PainelPath);
        var buracos = ArteInspetor.Buracos(PainelPath);
        var janelaGrande = buracos.First(r => r.width >= 18 && r.height >= 15);
        var janelaPequena = buracos.First(r => r.width >= 10 && r.width <= 16 && r.height >= 10);
        var tubo = buracos.First(r => r.height > r.width * 3);
        var fendas = buracos.Where(r => r.width > r.height * 8).OrderByDescending(r => r.yMin).ToList(); // de cima para baixo
        var fendaFome = fendas[0];
        var fendaHigiene = fendas[1];
        var fendaEstamina = fendas[2];

        Vector2 Tela(float tx, float ty) => CantoPainel + new Vector2(tx - conteudo.x, ty - conteudo.y) * PxPainel;
        Rect Janela(RectInt r)
        {
            var min = Tela(r.xMin, r.yMin);
            var max = Tela(r.xMax, r.yMax);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        var raiz = new GameObject("Painel", typeof(RectTransform));
        var rp = (RectTransform)raiz.transform;
        rp.SetParent(hud, false);
        Stretch(rp);

        var cantilConteudo = ArteInspetor.Conteudo($"{CantilPasta}/pixil-frame-0.png");
        var barraConteudo = ArteInspetor.Conteudo(BarraArtePath);
        // por dentro do contorno preto da barra (1 pixel da arte = 4 px da imagem)
        var barraMiolo = new Rect(barraConteudo.x + 4, barraConteudo.y + 4, barraConteudo.width - 8, barraConteudo.height - 8);

        var vida = Fenda(rp, "VIDA", Janela(tubo), barraSprite, barraMiolo, new Color(0.9f, 0.25f, 0.2f), vertical: true);
        var fome = Fenda(rp, "FOME", Janela(fendaFome), barraSprite, barraMiolo, new Color(0.98f, 0.66f, 0.3f), vertical: false);
        var higiene = Fenda(rp, "HIGIENE", Janela(fendaHigiene), barraSprite, barraMiolo, new Color(0.5f, 0.88f, 0.76f), vertical: false);
        var estamina = Fenda(rp, "ESTAMINA", Janela(fendaEstamina), barraSprite, barraMiolo, new Color(0.95f, 0.92f, 0.55f), vertical: false);

        // cantis: 1 pixel da arte do cantil vira 9 px (grande) e 6 px (pequeno), centrados na janela
        var sede = Cantil(rp, "SEDE", Janela(janelaGrande).center, 9f, cantil, cantilConteudo, Color.white);
        var sanidade = Cantil(rp, "SANIDADE", Janela(janelaPequena).center, 6f, cantil, cantilConteudo, new Color(0.78f, 0.52f, 1f));

        // a moldura por cima de tudo
        var inteiro = new RectInt(conteudo.x, conteudo.y, conteudo.width, conteudo.height);
        Arte(rp, "Moldura", painelSprite, new Rect(conteudo.x, conteudo.y, conteudo.width, conteudo.height),
            Janela(inteiro).center, new Vector2(conteudo.width, conteudo.height) * PxPainel, rot90: false);

        // nomes das fendas, do lado de fora da moldura
        float xNome = Tela(conteudo.xMax + 2, 0).x;
        var nomes = new[] { ("FOME", fendaFome, fome), ("HIGIENE", fendaHigiene, higiene), ("ESTAMINA", fendaEstamina, estamina) };
        foreach (var (nome, fenda, barra) in nomes)
        {
            var t = Txt(rp, "Nome " + nome, nome, led, 28, new Color(0.85f, 0.82f, 0.75f, 0.9f),
                new Vector2(xNome, Janela(fenda).center.y), new Vector2(220f, 26f), TextAlignmentOptions.Left, anchor: Vector2.zero, pivot: new Vector2(0f, 0.5f));
            var so = new SerializedObject(barra);
            so.FindProperty("rotulo").objectReferenceValue = t;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        topo = Tela(0, conteudo.yMax).y;
        return new[] { vida, fome, sede, estamina, higiene, sanidade };
    }

    /// <summary>
    /// Coloca um sprite de canvas grande (arte do pixilart) de modo que só a parte
    /// "conteudo" (em pixels da textura) ocupe "tamanho" na tela, centrada em "centro".
    /// </summary>
    private static Image Arte(RectTransform parent, string nome, Sprite sprite, Rect conteudo, Vector2 centro, Vector2 tamanho, bool rot90)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var tex = sprite.rect.size;
        float comprimento = rot90 ? tamanho.y : tamanho.x, espessura = rot90 ? tamanho.x : tamanho.y;
        float sx = comprimento / conteudo.width, sy = espessura / conteudo.height;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(conteudo.center.x / tex.x, conteudo.center.y / tex.y);
        rt.sizeDelta = new Vector2(tex.x * sx, tex.y * sy);
        rt.anchoredPosition = centro;
        rt.localRotation = rot90 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Fenda da moldura: fundo escuro + barra segmentada que aparece por uma máscara.</summary>
    private static BarraSegmentada Fenda(RectTransform parent, string nome, Rect janela, Sprite barra, Rect barraMiolo, Color cor, bool vertical)
    {
        var go = new GameObject("Barra " + nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = janela.min;
        rt.sizeDelta = janela.size;

        Img(rt, "Fundo", FundoJanela, Vector2.zero, janela.size, center: false);

        var mascaraGo = new GameObject("Mascara", typeof(RectTransform), typeof(RectMask2D));
        var m = (RectTransform)mascaraGo.transform;
        m.SetParent(rt, false);
        m.anchorMin = m.anchorMax = Vector2.zero;
        m.pivot = Vector2.zero;
        m.anchoredPosition = Vector2.zero;
        m.sizeDelta = janela.size;

        var arte = Arte(m, "Gomos", barra, barraMiolo, janela.size / 2f, janela.size, rot90: vertical);
        arte.color = cor;

        var bar = go.AddComponent<BarraSegmentada>();
        var so = new SerializedObject(bar);
        so.FindProperty("segmentos").arraySize = 0;
        so.FindProperty("cor").colorValue = cor;
        so.FindProperty("mascara").objectReferenceValue = m;
        so.FindProperty("arteMascarada").objectReferenceValue = arte;
        so.FindProperty("tamanhoCheio").vector2Value = janela.size;
        so.FindProperty("vertical").boolValue = vertical;
        so.FindProperty("passos").intValue = 10;
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    /// <summary>Cantil da arte (quadros esvaziando) dentro de uma janela redonda.</summary>
    private static BarraSegmentada Cantil(RectTransform parent, string nome, Vector2 centro, float pxPorPixel, Sprite[] quadros, RectInt conteudo, Color cor)
    {
        var go = new GameObject("Cantil " + nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Stretch(rt);

        // a arte do cantil tem pixels de 10x10 na imagem
        var tamanho = new Vector2(conteudo.width, conteudo.height) / 10f * pxPorPixel;
        var img = Arte(rt, "Quadro", quadros[0], new Rect(conteudo.x, conteudo.y, conteudo.width, conteudo.height), centro, tamanho, rot90: false);
        img.color = cor;

        var bar = go.AddComponent<BarraSegmentada>();
        var so = new SerializedObject(bar);
        so.FindProperty("segmentos").arraySize = 0;
        so.FindProperty("cor").colorValue = cor;
        so.FindProperty("imagemQuadros").objectReferenceValue = img;
        var q = so.FindProperty("quadros");
        q.arraySize = quadros.Length;
        for (int i = 0; i < quadros.Length; i++) q.GetArrayElementAtIndex(i).objectReferenceValue = quadros[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    private static void BuildInteraction(GameObject root, RectTransform c)
    {
        var g = Group(c, "Aviso Interacao", stretch: false);
        var rt = (RectTransform)g.transform;
        rt.anchoredPosition = new Vector2(0f, -45f);
        rt.sizeDelta = new Vector2(900f, 50f);
        var cg = g.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        var bg = Img(rt, "Fundo", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, new Vector2(520f, 44f), center: true);
        var t = Txt(rt, "Texto", "[E] USAR", led, 28, Ivory, Vector2.zero, new Vector2(880f, 50f), TextAlignmentOptions.Center);

        var pi = root.AddComponent<PlayerInteractor>();
        var so = new SerializedObject(pi);
        so.FindProperty("aviso").objectReferenceValue = cg;
        so.FindProperty("avisoTexto").objectReferenceValue = t;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildSubtitles(GameObject root, RectTransform c, AudioSource audio)
    {
        var g = Group(c, "Legendas", stretch: false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 405f); // acima do painel das necessidades
        rt.sizeDelta = new Vector2(1060f, 90f);
        var cg = g.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        Img(rt, "Fundo", new Color(0f, 0f, 0f, 0.5f), Vector2.zero, new Vector2(1060f, 90f), center: true);
        var t = Txt(rt, "Texto", "", body, 30, Ivory, Vector2.zero, new Vector2(1000f, 84f), TextAlignmentOptions.Center);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.fontStyle = FontStyles.Italic;

        var n = root.AddComponent<Narrador>();
        var so = new SerializedObject(n);
        so.FindProperty("grupo").objectReferenceValue = cg;
        so.FindProperty("texto").objectReferenceValue = t;
        so.FindProperty("vozSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildObjectives(GameObject root, RectTransform c, AudioSource audio)
    {
        var g = Group(c, "Objetivos", stretch: false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -36f);
        rt.sizeDelta = new Vector2(640f, 400f);
        var cg = g.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        Txt(rt, "Titulo", "OBJETIVOS", led, 26, Led, new Vector2(0f, -16f), new Vector2(600f, 32f), TextAlignmentOptions.Left, anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 0.5f));
        var lista = Txt(rt, "Lista", "", body, 24, Ivory, new Vector2(0f, -40f), new Vector2(620f, 360f), TextAlignmentOptions.TopLeft, anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f));
        lista.textWrappingMode = TextWrappingModes.Normal;
        lista.lineSpacing = 6f;

        var o = root.AddComponent<Objetivos>();
        var so = new SerializedObject(o);
        so.FindProperty("texto").objectReferenceValue = lista;
        so.FindProperty("grupo").objectReferenceValue = cg;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildReading(GameObject root, RectTransform c, AudioSource audio)
    {
        var g = Group(c, "Leitura", stretch: true);
        var cg = g.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        var rt = (RectTransform)g.transform;

        var dim = Img(rt, "Escurecer", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.zero, center: true);
        Stretch(dim.rectTransform);

        var paperBorder = Img(rt, "Papel Borda", new Color(0.55f, 0.5f, 0.42f), Vector2.zero, new Vector2(926f, 1006f), center: true);
        paperBorder.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.4f);
        var paper = Img(rt, "Papel", new Color(0.86f, 0.82f, 0.71f), Vector2.zero, new Vector2(910f, 990f), center: true);
        paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.4f);
        var p = paper.rectTransform;

        var ink = new Color(0.13f, 0.11f, 0.09f);
        var jornal = Txt(p, "Jornal", "O DIÁRIO", body, 58, ink, new Vector2(0f, 420f), new Vector2(840f, 80f), TextAlignmentOptions.Center);
        jornal.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        jornal.characterSpacing = 6f;
        Img(p, "Linha 1", ink, new Vector2(0f, 378f), new Vector2(840f, 5f), center: true);
        var data = Txt(p, "Data", "", body, 22, new Color(0.25f, 0.22f, 0.18f), new Vector2(0f, 352f), new Vector2(840f, 34f), TextAlignmentOptions.Center);
        Img(p, "Linha 2", ink, new Vector2(0f, 326f), new Vector2(840f, 3f), center: true);
        var manchete = Txt(p, "Manchete", "", body, 48, ink, new Vector2(0f, 240f), new Vector2(820f, 150f), TextAlignmentOptions.Center);
        manchete.fontStyle = FontStyles.Bold;
        manchete.textWrappingMode = TextWrappingModes.Normal;
        var corpo = Txt(p, "Corpo", "", body, 27, new Color(0.18f, 0.16f, 0.13f), new Vector2(0f, -110f), new Vector2(800f, 540f), TextAlignmentOptions.TopJustified);
        corpo.textWrappingMode = TextWrappingModes.Normal;
        corpo.lineSpacing = 4f;
        Txt(rt, "Fechar", "[E]  FECHAR", led, 26, Led, new Vector2(0f, -520f), new Vector2(600f, 40f), TextAlignmentOptions.Center);

        var l = root.AddComponent<Leitura>();
        var so = new SerializedObject(l);
        so.FindProperty("grupo").objectReferenceValue = cg;
        so.FindProperty("jornal").objectReferenceValue = jornal;
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("manchete").objectReferenceValue = manchete;
        so.FindProperty("corpo").objectReferenceValue = corpo;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("somAbrir").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelAbrindo.mp3");
        so.FindProperty("somFechar").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelFechando.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildDeath(GameObject root, RectTransform c, AudioSource audio)
    {
        var red = Group(c, "Morte Vermelho", stretch: true);
        var redCg = red.GetComponent<CanvasGroup>();
        redCg.alpha = 0f; redCg.interactable = false; redCg.blocksRaycasts = false;
        var redImg = Img((RectTransform)red.transform, "Cor", new Color(0.18f, 0.01f, 0.02f, 0.88f), Vector2.zero, Vector2.zero, center: true);
        Stretch(redImg.rectTransform);

        var g = Group(c, "Morte", stretch: true);
        var cg = g.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        var rt = (RectTransform)g.transform;

        const float W = 1100f;
        var panel = Img(rt, "Painel", new Color(0.015f, 0.012f, 0.01f, 0.96f), Vector2.zero, new Vector2(W, 560f), center: true);
        var p = panel.rectTransform;
        var causa = Txt(p, "Causa", "VOCÊ MORREU", led, 64, new Color(1f, 0.35f, 0.25f), new Vector2(0f, 200f), new Vector2(W - 60f, 90f), TextAlignmentOptions.Center);
        Img(p, "Linha", Led, new Vector2(0f, 140f), new Vector2(W - 100f, 5f), center: true);

        var bLoad = Line(p, "Carregar", "CARREGAR ÚLTIMO SAVE", W, 60f);
        var bRestart = Line(p, "Recomecar", "RECOMEÇAR DO INÍCIO", W, -40f);
        var bMenu = Line(p, "Menu", "MENU PRINCIPAL", W, -140f);

        var d = root.AddComponent<TelaDeMorte>();
        UnityEventTools.AddPersistentListener(bLoad.onClick, new UnityAction(d.CarregarUltimoSave));
        UnityEventTools.AddPersistentListener(bRestart.onClick, new UnityAction(d.RecomecarDoInicio));
        UnityEventTools.AddPersistentListener(bMenu.onClick, new UnityAction(d.IrAoMenu));
        Chain(bLoad, bRestart, bMenu);

        var so = new SerializedObject(d);
        so.FindProperty("grupo").objectReferenceValue = cg;
        so.FindProperty("vermelho").objectReferenceValue = redCg;
        so.FindProperty("causa").objectReferenceValue = causa;
        so.FindProperty("botaoCarregar").objectReferenceValue = bLoad.gameObject;
        so.FindProperty("botaoRecomecar").objectReferenceValue = bRestart.gameObject;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------ Prévia

    public static void Capture(StringBuilder log)
    {
        // Cena temporária ABERTA POR CIMA (aditiva): não fecha nem altera a cena do usuário.
        var previousActive = EditorSceneManager.GetActiveScene();
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(temp);
        try
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var camGo = new GameObject("Camera Previa");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.36f, 0.34f);
            cam.cullingMask = 1 << 5; // só UI
            var canvas = go.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // HUD com valores variados + objetivos + legenda + aviso
            var hud = go.transform.Find("Tela/HUD").GetComponent<CanvasGroup>();
            hud.alpha = 1f;
            var valores = new System.Collections.Generic.Dictionary<string, float>
            {
                { "VIDA", 0.8f }, { "FOME", 0.42f }, { "SEDE", 0.55f }, { "ESTAMINA", 0.7f }, { "HIGIENE", 0.3f }, { "SANIDADE", 0.85f },
            };
            foreach (var b in go.GetComponentsInChildren<BarraSegmentada>(true))
                foreach (var kv in valores)
                    if (b.name.EndsWith(" " + kv.Key)) b.Definir(kv.Value);
            go.transform.Find("Tela/HUD/Dicas Consumo").GetComponent<TMP_Text>().text = "<color=#FFB040>[F]</color> COMER (2)   <color=#FFB040>[G]</color> BEBER (1)";
            Set(go, "Tela/Objetivos", 1f);
            go.transform.Find("Tela/Objetivos/Lista").GetComponent<TMP_Text>().text =
                "<color=#7A6A50>[X] <s>Encontrar algo para beber</s></color>\n[ ] Fazer o trem funcionar\n<size=80%><color=#C8A060>      carvão 1/3</color></size>\n[ ] Descobrir de onde vem o rádio";
            Set(go, "Tela/Legendas", 1f);
            go.transform.Find("Tela/Legendas/Texto").GetComponent<TMP_Text>().text = "Minha cabeça... quanto tempo eu fiquei apagado?";
            Set(go, "Tela/Aviso Interacao", 1f);
            go.transform.Find("Tela/Aviso Interacao/Texto").GetComponent<TMP_Text>().text = "<color=#FFB040>[E]</color>  PEGAR CARVÃO";
            Shot(cam, rt, "jogador_1_hud.png", log);

            Set(go, "Tela/HUD", 0f); Set(go, "Tela/Objetivos", 0f); Set(go, "Tela/Legendas", 0f); Set(go, "Tela/Aviso Interacao", 0f);
            Set(go, "Tela/Leitura", 1f);
            go.transform.Find("Tela/Leitura/Papel/Jornal").GetComponent<TMP_Text>().text = "Folha da Estação";
            go.transform.Find("Tela/Leitura/Papel/Data").GetComponent<TMP_Text>().text = "Terça-feira, 14 de abril de 2020  •  Edição 1.284";
            go.transform.Find("Tela/Leitura/Papel/Manchete").GetComponent<TMP_Text>().text = "Doença misteriosa deixa o dedo mínimo roxo";
            go.transform.Find("Tela/Leitura/Papel/Corpo").GetComponent<TMP_Text>().text = "Médicos do hospital regional registraram ontem o terceiro caso de uma doença ainda sem nome. Os pacientes chegam com o dedo mínimo arroxeado e tremores nas mãos...";
            Shot(cam, rt, "jogador_2_jornal.png", log);

            Set(go, "Tela/Leitura", 0f);
            Set(go, "Tela/Morte Vermelho", 1f);
            Set(go, "Tela/Morte", 1f);
            go.transform.Find("Tela/Morte/Painel/Causa").GetComponent<TMP_Text>().text = "VOCÊ MORREU DE SEDE";
            go.transform.Find("Tela/Morte/Painel/Carregar").GetComponent<DepartureBoardButton>().ApplyVisual(1f, true);
            Shot(cam, rt, "jogador_3_morte.png", log);

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
        }
        finally
        {
            if (previousActive.IsValid()) EditorSceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(temp, true);
        }
    }

    private static void Set(GameObject root, string path, float a)
    {
        var cg = root.transform.Find(path)?.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = a;
    }

    private static void Shot(Camera cam, RenderTexture rt, string file, StringBuilder log)
    {
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        Directory.CreateDirectory("Temp/TheRailway");
        File.WriteAllBytes(Path.Combine("Temp/TheRailway", file), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        log.AppendLine("prévia: " + file);
    }

    // ------------------------------------------------------------ Ajudantes

    private static GameObject Group(RectTransform parent, string name, bool stretch)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (stretch) Stretch(rt);
        return go;
    }

    private static Image Img(RectTransform parent, string name, Color color, Vector2 pos, Vector2 size, bool center)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (!center)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
        }
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI Txt(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align, Vector2? anchor = null, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (anchor.HasValue) rt.anchorMin = rt.anchorMax = anchor.Value;
        if (pivot.HasValue) rt.pivot = pivot.Value;
        else if (align == TextAlignmentOptions.Left) rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button Line(RectTransform parent, string name, string text, float width, float y)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DepartureBoardButton));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(width - 80f, 80f);
        var img = go.GetComponent<Image>();
        img.color = new Color(Led.r, Led.g, Led.b, 0f);
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;
        float left = -(width - 80f) / 2f;
        var label = Txt(rt, "Texto", text, led, 52, LedDim, new Vector2(left + 70f, 0f), new Vector2(width - 180f, 80f), TextAlignmentOptions.Left);
        var cursor = Txt(rt, "Cursor", ">", led, 52, Color.black, new Vector2(left + 32f, 0f), new Vector2(40f, 80f), TextAlignmentOptions.Center);
        var dbb = go.GetComponent<DepartureBoardButton>();
        var so = new SerializedObject(dbb);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("cursor").objectReferenceValue = cursor;
        so.FindProperty("highlight").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
        dbb.ApplyVisual(0f, false);
        return button;
    }

    private static void Chain(params Selectable[] s)
    {
        for (int i = 0; i < s.Length; i++)
            s[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = s[(i - 1 + s.Length) % s.Length], selectOnDown = s[(i + 1) % s.Length] };
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
