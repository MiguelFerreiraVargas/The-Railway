using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela da mochila (Tab ou I): o que o Cândido carrega, o peso e o limite (bolsos ou mochila).
/// Clicar usa (comer, beber), equipa (armas) ou larga o item. O jogo não pausa.
/// </summary>
public class TelaMochila : MonoBehaviour
{
    public TMP_FontAsset fonte;
    public Key tecla = Key.Tab;
    public Key teclaAlternativa = Key.I;

    public static bool Aberta { get; private set; }
    public static TelaMochila Instance { get; private set; }

    private static readonly Color Papel = new Color(0.93f, 0.86f, 0.7f);
    private static readonly Color Laranja = new Color(1f, 0.69f, 0.25f);

    private CanvasGroup grupo;
    private RectTransform lista;
    private TMP_Text titulo, peso, vazio;
    private Image barra;
    private readonly List<GameObject> linhas = new List<GameObject>();
    private bool sujo = true;
    private StarterAssetsInputs inputs;

    private void Awake()
    {
        Instance = this;
        Aberta = false;
        Montar();
    }

    private void OnEnable()
    {
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.OnInventoryChanged += Sujar;
    }

    private void OnDisable()
    {
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.OnInventoryChanged -= Sujar;
        if (Aberta) Mostrar(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; Aberta = false; }
    }

    private void Sujar() => sujo = true;

    private void Update()
    {
        bool bloqueado = PauseMenu.IsPaused || Leitura.Aberta || MapaDoMundo.Aberto || (PlayerStats.Instance != null && PlayerStats.Instance.Morto);
        var kb = Keyboard.current;
        if (kb != null && !bloqueado && (kb[tecla].wasPressedThisFrame || kb[teclaAlternativa].wasPressedThisFrame)) Mostrar(!Aberta);
        else if (Aberta && (bloqueado || (kb != null && kb.escapeKey.wasPressedThisFrame))) Mostrar(false);

        grupo.alpha = Mathf.MoveTowards(grupo.alpha, Aberta ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (Aberta)
        {
            if (inputs != null) inputs.look = Vector2.zero;
            if (sujo) Refazer();
        }
    }

    public void Mostrar(bool abrir)
    {
        if (abrir == Aberta) return;
        Aberta = abrir;
        grupo.blocksRaycasts = abrir; grupo.interactable = abrir;
        if (inputs == null) inputs = FindFirstObjectByType<StarterAssetsInputs>();
        bool iso = ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        if (abrir)
        {
            sujo = true;
            if (inputs != null) { inputs.cursorInputForLook = false; inputs.look = Vector2.zero; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            if (inputs != null) inputs.cursorInputForLook = true;
            if (!iso) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }

    // ------------------------------------------------------------ conteúdo

    private void Refazer()
    {
        sujo = false;
        foreach (var l in linhas) Destroy(l);
        linhas.Clear();
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        titulo.text = inv.TemMochila ? "MOCHILA" : "BOLSOS  <size=60%><color=#B0A080>(sem mochila: cabe pouco)</color></size>";
        float p = inv.PesoAtual, cap = inv.Capacidade;
        peso.text = $"{p:0.0} / {cap:0} kg";
        barra.fillAmount = cap > 0f ? Mathf.Clamp01(p / cap) : 0f;
        barra.color = p / Mathf.Max(0.01f, cap) > 0.85f ? new Color(0.85f, 0.3f, 0.2f) : Laranja;

        var armas = ArmasDoJogador.Instance;
        float y = 0f;
        int n = 0;
        foreach (var slot in inv.Slots)
        {
            if (string.IsNullOrEmpty(slot.itemId) || slot.quantity <= 0) continue;
            var info = Itens.Get(slot.itemId);
            bool equipada = armas != null && armas.Equipada == slot.itemId;
            CriarLinha(info, slot.quantity, equipada, y);
            y -= 58f; n++;
        }
        lista.sizeDelta = new Vector2(lista.sizeDelta.x, Mathf.Max(58f, -y));
        vazio.gameObject.SetActive(n == 0);
    }

    private void CriarLinha(Itens.Info info, int qtd, bool equipada, float y)
    {
        var linha = Rect("Linha " + info.id, lista, new Vector2(760f, 52f));
        linha.anchorMin = linha.anchorMax = new Vector2(0.5f, 1f); linha.pivot = new Vector2(0.5f, 1f);
        linha.anchoredPosition = new Vector2(0f, y);
        linha.gameObject.AddComponent<Image>().color = equipada ? new Color(0.35f, 0.25f, 0.1f, 0.9f) : new Color(0.12f, 0.1f, 0.08f, 0.85f);
        linhas.Add(linha.gameObject);

        string q = info.tipo == Itens.Tipo.Arma ? "" : $"  <color=#B0A080>x{qtd}</color>";
        var nome = Texto("Nome", linha, $"{info.nome}{q}{(equipada ? "  <color=#E8B04A>[na mão]</color>" : "")}", 24f, TextAlignmentOptions.Left, Papel);
        nome.rectTransform.anchoredPosition = new Vector2(-150f, 8f); nome.rectTransform.sizeDelta = new Vector2(420f, 30f);
        var desc = Texto("Desc", linha, $"{info.descricao}  <color=#807060>{info.peso * qtd:0.0} kg</color>", 16f, TextAlignmentOptions.Left, new Color(0.75f, 0.68f, 0.55f));
        desc.rectTransform.anchoredPosition = new Vector2(-150f, -14f); desc.rectTransform.sizeDelta = new Vector2(420f, 22f);

        string acao = info.tipo == Itens.Tipo.Comida ? "COMER" : info.tipo == Itens.Tipo.Bebida ? "BEBER" : info.tipo == Itens.Tipo.Arma ? (equipada ? "GUARDAR" : "EQUIPAR") : info.id == "pilha" ? "PÔR NA LANTERNA" : info.id == "lanterna" ? (LanternaNaMao.Guardada ? "SEGURAR" : "GUARDAR") : null;
        if (acao != null) Botao(linha, acao, new Vector2(185f, 0f), () => Usar(info.id));
        if (info.id == "lanterna" && LuzDaLanterna.Instance != null)
            desc.text = $"Pilha: {Mathf.RoundToInt(LuzDaLanterna.Instance.Carga * 100f)}%   [L] liga e desliga  <color=#807060>{info.peso:0.0} kg</color>";
        if (info.id != "carvao") Botao(linha, "LARGAR", new Vector2(310f, 0f), () => Largar(info.id));
    }

    private void Usar(string id)
    {
        var info = Itens.Get(id);
        var inv = PlayerInventory.Instance;
        var stats = PlayerStats.Instance;
        if (id == "lanterna")
        {
            LanternaNaMao.Guardar(!LanternaNaMao.Guardada);
        }
        else if (id == "pilha")
        {
            var luz = LuzDaLanterna.Instance;
            if (!LanternaNaMao.TemLanterna) Narrador.Falar("Pilha... mas cadê a lanterna?");
            else if (luz != null && luz.PorPilha()) Narrador.Falar("Pilha nova.");
        }
        else if (info.tipo == Itens.Tipo.Arma)
        {
            var armas = ArmasDoJogador.Instance;
            if (armas != null) armas.Equipar(armas.Equipada == id ? "" : id);
        }
        else if (stats != null && inv != null && inv.GetQuantity(id) > 0)
        {
            var c = stats.GetConsumivel(id);
            if (c == null && id == "carne_crua")
                c = new PlayerStats.ItemConsumivel { itemId = id, nome = "CARNE CRUA", fome = 30f, vida = -8f, sanidade = -3f };
            if (c != null && inv.RemoveItem(id, 1))
            {
                stats.Aplicar(c);
                if (id == "carne_crua") Narrador.Falar("Crua... o estômago revirou.");
            }
        }
        sujo = true;
    }

    private void Largar(string id)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        var armas = ArmasDoJogador.Instance;
        if (armas != null && armas.Equipada == id) armas.Equipar("");
        int qtd = Itens.Get(id).tipo == Itens.Tipo.Municao ? Mathf.Min(10, inv.GetQuantity(id)) : 1;
        inv.RemoveItem(id, qtd);
        sujo = true;
    }

    // ------------------------------------------------------------ montagem

    private void Montar()
    {
        var raiz = new GameObject("Mochila (tela)");
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        var escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 1f;
        raiz.AddComponent<GraphicRaycaster>();
        grupo = raiz.AddComponent<CanvasGroup>();
        grupo.alpha = 0f; grupo.blocksRaycasts = false; grupo.interactable = false;

        var fundo = Rect("Fundo", raiz.transform, Vector2.zero);
        fundo.anchorMin = Vector2.zero; fundo.anchorMax = Vector2.one;
        fundo.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var painel = Rect("Painel", raiz.transform, new Vector2(820f, 760f));
        painel.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.05f, 0.95f);

        titulo = Texto("Titulo", painel, "MOCHILA", 36f, TextAlignmentOptions.Left, Papel);
        titulo.rectTransform.anchoredPosition = new Vector2(-40f, 345f); titulo.rectTransform.sizeDelta = new Vector2(700f, 44f);
        peso = Texto("Peso", painel, "", 22f, TextAlignmentOptions.Right, Papel);
        peso.rectTransform.anchoredPosition = new Vector2(250f, 300f); peso.rectTransform.sizeDelta = new Vector2(240f, 30f);
        var fundoBarra = Rect("Barra", painel, new Vector2(520f, 12f));
        fundoBarra.anchoredPosition = new Vector2(-110f, 300f);
        fundoBarra.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.17f, 0.13f);
        var enche = Rect("Enche", fundoBarra, new Vector2(520f, 12f));
        barra = enche.gameObject.AddComponent<Image>();
        barra.type = Image.Type.Filled; barra.fillMethod = Image.FillMethod.Horizontal;
        barra.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        lista = Rect("Lista", painel, new Vector2(760f, 560f));
        lista.anchorMin = lista.anchorMax = new Vector2(0.5f, 1f); lista.pivot = new Vector2(0.5f, 1f);
        lista.anchoredPosition = new Vector2(0f, -100f);
        vazio = Texto("Vazio", painel, "Nada. Os bolsos estão vazios.", 22f, TextAlignmentOptions.Center, new Color(0.6f, 0.55f, 0.45f));
        vazio.rectTransform.anchoredPosition = new Vector2(0f, 150f);

        var dica = Texto("Dica", painel, $"<color=#E8B04A>[{tecla}]</color> fechar    <color=#E8B04A>[1-4]</color> trocar de arma    <color=#E8B04A>[R]</color> recarregar", 18f, TextAlignmentOptions.Center, new Color(0.75f, 0.68f, 0.55f));
        dica.rectTransform.anchoredPosition = new Vector2(0f, -350f); dica.rectTransform.sizeDelta = new Vector2(780f, 26f);
    }

    private void Botao(RectTransform pai, string texto, Vector2 pos, UnityEngine.Events.UnityAction acao)
    {
        var b = Rect("Botao " + texto, pai, new Vector2(110f, 36f));
        b.anchoredPosition = pos;
        var img = b.gameObject.AddComponent<Image>();
        img.color = new Color(0.3f, 0.22f, 0.12f);
        var bt = b.gameObject.AddComponent<Button>();
        var cores = bt.colors; cores.highlightedColor = new Color(1f, 0.8f, 0.5f); cores.pressedColor = new Color(0.8f, 0.6f, 0.3f); bt.colors = cores;
        bt.onClick.AddListener(acao);
        var t = Texto("T", b, texto, 18f, TextAlignmentOptions.Center, Papel);
        t.rectTransform.sizeDelta = new Vector2(110f, 36f);
    }

    private static RectTransform Rect(string nome, Transform pai, Vector2 tam)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tam;
        return rt;
    }

    private TMP_Text Texto(string nome, Transform pai, string texto, float tam, TextAlignmentOptions alinhar, Color cor)
    {
        var rt = Rect(nome, pai, new Vector2(400f, 40f));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.text = texto; t.fontSize = tam; t.alignment = alinhar; t.color = cor;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
