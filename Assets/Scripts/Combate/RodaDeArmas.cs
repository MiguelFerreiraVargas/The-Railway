using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Roda de armas: segurar Tab abre (o tempo fica lento), o mouse escolhe a fatia e soltar Tab
/// equipa. Um toque rápido no Tab continua abrindo a mochila. Mostra só o que o Cândido tem
/// (o resto fica apagado) e, no meio, a ficha da arma: dano, alcance, velocidade, fôlego e munição.
/// Criada pelo ArmasDoJogador.
/// </summary>
public class RodaDeArmas : MonoBehaviour
{
    public static RodaDeArmas Instance { get; private set; }
    public static bool Aberta { get; private set; }

    public Key tecla = Key.Tab;
    [Tooltip("Segurar mais que isto abre a roda; menos é um toque (mochila).")]
    public float tempoSegurar = 0.2f;
    public float tempoLento = 0.25f;

    private struct Fatia { public string id, nome, tipo; public Image fundo, borda, icone; public TMP_Text rotulo; }
    private static readonly (string id, string nome, string tipo)[] Itens =
    {
        ("", "MÃOS", "Desarmado"),
        ("faca", "FACA", "Corpo a corpo"),
        ("machado", "MACHADO", "Corpo a corpo · duas mãos"),
        ("rifle", "RIFLE", "Arma de fogo · duas mãos"),
        ("lanterna", "LANTERNA", "Luz"),
    };

    private const float Raio = 330f, RaioDentro = 165f, Folga = 2.5f;
    private static readonly Color CorFatia = new Color(0.07f, 0.06f, 0.05f, 0.88f);
    private static readonly Color CorEscolhida = new Color(0.3f, 0.17f, 0.07f, 0.95f);
    private static readonly Color CorAmbar = new Color(0.91f, 0.69f, 0.29f);
    private static readonly Color CorTexto = new Color(0.93f, 0.86f, 0.7f);

    private ArmasDoJogador armas;
    private TMP_FontAsset fonte;
    private CanvasGroup grupo;
    private RectTransform roda;
    private readonly List<Fatia> fatias = new List<Fatia>();
    private TMP_Text nome, tipo, dica, extra;
    private readonly List<(TMP_Text rotulo, Image fundo, Image cheio)> barras = new List<(TMP_Text, Image, Image)>();
    private Image ponteiro;
    private StarterAssetsInputs inputs;
    private float apertouEm = -1f, escalaAntes = 1f;
    private Vector2 mira;
    private int escolhida = -1;
    private bool iconesProntos;

    public static RodaDeArmas Criar(ArmasDoJogador armas, TMP_FontAsset fonte)
    {
        var go = new GameObject("Roda de Armas");
        var r = go.AddComponent<RodaDeArmas>();
        r.armas = armas; r.fonte = fonte;
        r.Montar();
        return r;
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Aberta) { Aberta = false; Time.timeScale = escalaAntes; }
    }

    private bool Bloqueado() =>
        PauseMenu.IsPaused || Leitura.Aberta || MapaDoMundo.Aberto || Desmaio.Ativo
        || TremInteragivel.ComJogador != null || TrainCameraView.Ativa
        || (PlayerStats.Instance != null && PlayerStats.Instance.Morto);

    private void Update()
    {
        var kb = Keyboard.current;
        float agora = Time.unscaledTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, Aberta ? 1f : 0f, Time.unscaledDeltaTime * 9f);
        roda.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, grupo.alpha);
        if (kb == null) return;

        if (Aberta && Bloqueado()) { Fechar(false); return; }
        if (kb[tecla].wasPressedThisFrame)
        {
            if (TelaMochila.Aberta) { TelaMochila.Instance?.Mostrar(false); apertouEm = -1f; }
            else if (!Bloqueado()) apertouEm = agora;
        }
        if (apertouEm >= 0f && !Aberta && kb[tecla].isPressed && agora - apertouEm >= tempoSegurar) Abrir();
        if (kb[tecla].wasReleasedThisFrame && apertouEm >= 0f)
        {
            if (Aberta) Fechar(true);
            else if (TelaMochila.Instance != null) TelaMochila.Instance.Mostrar(true); // toque: mochila
            apertouEm = -1f;
        }

        if (!Aberta) return;
        if (inputs != null) inputs.look = Vector2.zero;
        if (kb.escapeKey.wasPressedThisFrame) { Fechar(false); apertouEm = -1f; return; }

        // o mouse empurra um ponteiro dentro da roda; a direção escolhe a fatia
        var mouse = Mouse.current;
        if (mouse != null)
        {
            mira += mouse.delta.ReadValue() * 1.4f;
            if (mira.magnitude > 120f) mira = mira.normalized * 120f;
            float rolar = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(rolar) > 0.1f) mira = Direcao(Proxima(escolhida, rolar < 0f ? 1 : -1)) * 120f;
        }
        if (mira.magnitude > 40f) { int i = IndiceDaDirecao(mira); if (i != escolhida) escolhida = i; }
        ponteiro.rectTransform.anchoredPosition = mira * 1.15f;
        Atualizar();
    }

    // ------------------------------------------------------------ abrir, fechar, escolher

    public void Abrir()
    {
        if (Aberta) return;
        if (!iconesProntos) { iconesProntos = true; FazerIcones(); }
        Aberta = true;
        escalaAntes = Time.timeScale;
        Time.timeScale = tempoLento;
        if (inputs == null) inputs = FindFirstObjectByType<StarterAssetsInputs>();
        if (inputs != null) { inputs.cursorInputForLook = false; inputs.look = Vector2.zero; }
        // começa na que está na mão
        int atual = Indice(armas != null ? armas.Equipada : "");
        if (atual == 0 && LanternaNaMao.NaMaoAgora && (armas == null || armas.Equipada == "")) atual = Indice("lanterna");
        mira = Direcao(atual) * 80f;
        escolhida = -1;
        Escolher(atual);
        Atualizar();
    }

    /// <summary>Fecha; com <paramref name="equipar"/>, usa a fatia escolhida.</summary>
    public void Fechar(bool equipar)
    {
        if (!Aberta) return;
        Aberta = false;
        Time.timeScale = escalaAntes;
        if (inputs != null) inputs.cursorInputForLook = true;
        if (equipar && escolhida >= 0 && Tem(Itens[escolhida].id)) Usar(Itens[escolhida].id);
    }

    /// <summary>Escolhe e usa (para testes e atalhos).</summary>
    public void Usar(string id)
    {
        if (armas == null) return;
        if (id == "lanterna")
        {
            if (!LanternaNaMao.NaMaoAgora) LanternaNaMao.Guardar(false);
            return;
        }
        if (armas.Equipada != id) armas.Equipar(id);
    }

    public void Escolher(int i)
    {
        if (i < 0 || i >= fatias.Count) return;
        escolhida = i;
        mira = Direcao(i) * 80f; // o ponteiro vai junto (senão o mouse desfaz a escolha)
    }

    public string Escolhida => escolhida >= 0 ? Itens[escolhida].id : null;

    private static bool Tem(string id)
    {
        if (id == "") return true;
        var inv = PlayerInventory.Instance;
        return inv != null && inv.GetQuantity(id) > 0;
    }

    private static int Indice(string id) { for (int i = 0; i < Itens.Length; i++) if (Itens[i].id == id) return i; return 0; }
    private float Passo => 360f / Itens.Length;
    // 0 em cima, no sentido do relógio
    private Vector2 Direcao(int i) { float a = (90f - i * Passo) * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
    private int IndiceDaDirecao(Vector2 d)
    {
        float ang = Mathf.Repeat(90f - Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + Passo / 2f, 360f);
        return Mathf.Clamp(Mathf.FloorToInt(ang / Passo), 0, Itens.Length - 1);
    }
    private int Proxima(int i, int dir)
    {
        for (int k = 1; k <= Itens.Length; k++) { int j = (int)Mathf.Repeat(i + dir * k, Itens.Length); if (Tem(Itens[j].id)) return j; }
        return i;
    }

    // ------------------------------------------------------------ aparência a cada quadro

    private void Atualizar()
    {
        for (int i = 0; i < fatias.Count; i++)
        {
            var f = fatias[i];
            bool tem = Tem(f.id), sel = i == escolhida;
            f.fundo.color = sel ? (tem ? CorEscolhida : new Color(0.16f, 0.12f, 0.1f, 0.92f)) : CorFatia;
            f.borda.color = sel ? (tem ? CorAmbar : new Color(0.5f, 0.45f, 0.4f)) : new Color(0f, 0f, 0f, 0f);
            f.icone.color = tem ? (sel ? Color.white : new Color(0.85f, 0.8f, 0.7f, 0.85f)) : new Color(0.5f, 0.5f, 0.5f, 0.18f);
            f.rotulo.text = tem ? "" : "?";
            float s = sel ? 1.12f : 1f;
            f.icone.rectTransform.localScale = Vector3.one * s;
        }

        if (escolhida < 0) return;
        var it = Itens[escolhida];
        bool possui = Tem(it.id);
        nome.text = possui ? it.nome : "???";
        tipo.text = possui ? it.tipo : "Ainda não achei.";
        var inv = PlayerInventory.Instance;

        string[] rotulos; float[] valores;
        if (it.id == "lanterna")
        {
            var luz = LuzDaLanterna.Instance;
            float carga = luz != null ? luz.Carga : 0f;
            rotulos = new[] { "Bateria", "Alcance", "Abertura" };
            valores = new[] { carga, luz != null ? Mathf.Clamp01(luz.alcance / 20f) : 0f, luz != null ? Mathf.Clamp01(luz.angulo / 70f) : 0f };
            int pilhas = inv != null ? inv.GetQuantity("pilha") : 0;
            extra.text = possui ? $"{Mathf.RoundToInt(carga * 100f)}%   ·   pilhas: {pilhas}   ·   {(LanternaNaMao.NaMaoAgora ? "na mão" : "guardada")}" : "";
        }
        else
        {
            var d = ArmasDoJogador.Ficha(it.id);
            rotulos = new[] { "Dano", "Alcance", "Velocidade", "Fôlego" };
            valores = new[]
            {
                Mathf.Clamp01(d.dano / 75f),
                Mathf.Clamp01(Mathf.Log10(Mathf.Max(1f, d.alcance)) / Mathf.Log10(120f) * 0.85f + 0.15f),
                Mathf.Clamp01(0.6f / d.intervalo),
                d.tiro ? 1f : Mathf.Clamp01(1f - d.estamina / 22f),
            };
            if (it.id == "rifle" && armas != null)
                extra.text = possui ? $"MUNIÇÃO  {armas.NoPente}/{armas.capacidadePente}   <color=#B0A080>(+{(inv != null ? inv.GetQuantity("municao") : 0)} na mochila)</color>" : "";
            else extra.text = possui && ArmasDoJogador.DuasMaos(it.id) ? "<color=#B0A080>a lanterna vai para a mochila</color>" : "";
        }
        for (int i = 0; i < barras.Count; i++)
        {
            bool usa = i < rotulos.Length && possui;
            barras[i].rotulo.gameObject.SetActive(usa);
            barras[i].fundo.gameObject.SetActive(usa);
            if (!usa) continue;
            barras[i].rotulo.text = rotulos[i];
            barras[i].cheio.fillAmount = valores[i];
        }
        string naMao = armas != null && armas.Equipada == it.id && it.id != "" ? "  <color=#E8B04A>(na mão)</color>" : "";
        dica.text = $"SOLTE [TAB] PARA {(possui ? (it.id == "lanterna" ? "SEGURAR" : "EQUIPAR") : "VOLTAR")}{naMao}";
    }

    // ------------------------------------------------------------ montagem

    private void Montar()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
        var escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f); escala.matchWidthOrHeight = 1f;
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f; grupo.blocksRaycasts = false; grupo.interactable = false;

        // escurece a tela em volta
        var veu = Rect("Véu", transform, Vector2.zero, new Vector2(0.5f, 0.5f));
        veu.anchorMin = Vector2.zero; veu.anchorMax = Vector2.one; veu.sizeDelta = Vector2.zero;
        veu.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.015f, 0.01f, 0.55f);

        roda = Rect("Roda", transform, new Vector2(Raio * 2f, Raio * 2f), new Vector2(0.5f, 0.5f));

        var setorTex = Setor(512, RaioDentro / Raio, 1f, Passo - Folga, true);
        var bordaTex = Setor(512, 0.955f, 1f, Passo - Folga, false);
        for (int i = 0; i < Itens.Length; i++)
        {
            var f = new Fatia { id = Itens[i].id, nome = Itens[i].nome, tipo = Itens[i].tipo };
            var fr = Rect("Fatia " + Itens[i].nome, roda, new Vector2(Raio * 2f, Raio * 2f), new Vector2(0.5f, 0.5f));
            fr.localRotation = Quaternion.Euler(0f, 0f, -i * Passo);
            f.fundo = fr.gameObject.AddComponent<Image>(); f.fundo.sprite = setorTex; f.fundo.raycastTarget = false;
            var br = Rect("Borda", fr, new Vector2(Raio * 2f + 18f, Raio * 2f + 18f), new Vector2(0.5f, 0.5f));
            f.borda = br.gameObject.AddComponent<Image>(); f.borda.sprite = bordaTex; f.borda.raycastTarget = false;
            Vector2 pos = Direcao(i) * ((Raio + RaioDentro) * 0.5f);
            var ir = Rect("Ícone", roda, new Vector2(118f, 118f), new Vector2(0.5f, 0.5f));
            ir.anchoredPosition = pos;
            f.icone = ir.gameObject.AddComponent<Image>(); f.icone.preserveAspect = true; f.icone.raycastTarget = false;
            f.rotulo = Texto("?", roda, 64f, TextAlignmentOptions.Center);
            f.rotulo.rectTransform.anchoredPosition = pos; f.rotulo.color = new Color(0.6f, 0.55f, 0.5f, 0.5f);
            fatias.Add(f);
        }

        // miolo: ficha da arma
        var miolo = Rect("Miolo", roda, new Vector2(RaioDentro * 2f - 16f, RaioDentro * 2f - 16f), new Vector2(0.5f, 0.5f));
        var mi = miolo.gameObject.AddComponent<Image>(); mi.sprite = Setor(256, 0f, 1f, 360f, true); mi.color = new Color(0.04f, 0.035f, 0.03f, 0.92f);
        var aro = Rect("Aro", roda, new Vector2(RaioDentro * 2f - 6f, RaioDentro * 2f - 6f), new Vector2(0.5f, 0.5f));
        var ai = aro.gameObject.AddComponent<Image>(); ai.sprite = Setor(256, 0.965f, 1f, 360f, false); ai.color = new Color(CorAmbar.r, CorAmbar.g, CorAmbar.b, 0.6f);

        nome = Texto("Nome", miolo, 30f, TextAlignmentOptions.Center); nome.rectTransform.anchoredPosition = new Vector2(0f, 98f); nome.color = CorAmbar;
        tipo = Texto("Tipo", miolo, 17f, TextAlignmentOptions.Center); tipo.rectTransform.anchoredPosition = new Vector2(0f, 68f); tipo.color = new Color(0.72f, 0.66f, 0.55f);
        var linha = Rect("Linha", miolo, new Vector2(220f, 2f), new Vector2(0.5f, 0.5f)); linha.anchoredPosition = new Vector2(0f, 50f);
        linha.gameObject.AddComponent<Image>().color = new Color(CorAmbar.r, CorAmbar.g, CorAmbar.b, 0.45f);
        for (int i = 0; i < 4; i++)
        {
            float y = 24f - i * 28f;
            var rot = Texto("Rótulo", miolo, 17f, TextAlignmentOptions.Left);
            rot.rectTransform.sizeDelta = new Vector2(110f, 26f); rot.rectTransform.anchoredPosition = new Vector2(-62f, y);
            var fundo = Rect("Barra", miolo, new Vector2(118f, 9f), new Vector2(0.5f, 0.5f)); fundo.anchoredPosition = new Vector2(62f, y);
            var fi = fundo.gameObject.AddComponent<Image>(); fi.color = new Color(1f, 1f, 1f, 0.12f);
            var ch = Rect("Cheio", fundo, new Vector2(118f, 9f), new Vector2(0.5f, 0.5f));
            var ci = ch.gameObject.AddComponent<Image>(); ci.sprite = Branco(); ci.type = Image.Type.Filled; ci.fillMethod = Image.FillMethod.Horizontal; ci.color = CorTexto;
            barras.Add((rot, fi, ci));
        }
        extra = Texto("Extra", miolo, 15f, TextAlignmentOptions.Center); extra.rectTransform.sizeDelta = new Vector2(300f, 40f);
        extra.rectTransform.anchoredPosition = new Vector2(0f, -100f); extra.textWrappingMode = TextWrappingModes.Normal;

        dica = Texto("Dica", transform, 22f, TextAlignmentOptions.Center);
        dica.rectTransform.sizeDelta = new Vector2(900f, 40f);
        dica.rectTransform.anchoredPosition = new Vector2(0f, -Raio - 50f); dica.color = new Color(0.8f, 0.74f, 0.62f);
        var titulo = Texto("Título", transform, 26f, TextAlignmentOptions.Center);
        titulo.rectTransform.sizeDelta = new Vector2(600f, 40f); titulo.rectTransform.anchoredPosition = new Vector2(0f, Raio + 46f);
        titulo.text = "ARMAS"; titulo.color = CorAmbar; titulo.characterSpacing = 12f;

        var pr = Rect("Ponteiro", roda, new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
        ponteiro = pr.gameObject.AddComponent<Image>(); ponteiro.sprite = Setor(64, 0f, 1f, 360f, false); ponteiro.color = new Color(1f, 0.85f, 0.55f, 0.8f);
    }

    // fotos das armas: o modelo em silhueta clara (como um desenho), feito uma vez
    private void FazerIcones()
    {
        var modelos = new Dictionary<string, GameObject>
        {
            { "faca", armas != null ? armas.modeloFaca : null },
            { "machado", armas != null ? armas.modeloMachado : null },
            { "rifle", armas != null ? armas.modeloRifle : null },
        };
        var lant = FindFirstObjectByType<LanternaNaMao>(FindObjectsInactive.Include);
        for (int i = 0; i < fatias.Count; i++)
        {
            string id = fatias[i].id;
            Sprite s = null;
            if (id == "") s = Punho();
            else if (id == "lanterna") s = lant != null ? IconeDoModelo(lant.gameObject, -35f) : null;
            else if (modelos.TryGetValue(id, out var m) && m != null) s = IconeDoModelo(m, id == "rifle" ? -20f : 45f);
            fatias[i].icone.sprite = s;
        }
    }

    private static Sprite IconeDoModelo(GameObject modelo, float giro)
    {
        const int tam = 256, camada = 31;
        var copia = new GameObject("Ícone");
        copia.transform.position = new Vector3(0f, -900f, 0f);
        foreach (var mf in modelo.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) continue;
            var p = new GameObject("p");
            p.layer = camada;
            p.transform.SetParent(copia.transform, false);
            p.transform.localPosition = modelo.transform.InverseTransformPoint(mf.transform.position);
            p.transform.localRotation = Quaternion.Inverse(modelo.transform.rotation) * mf.transform.rotation;
            p.transform.localScale = mf.transform.lossyScale;
            p.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            p.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
        }
        var rs = copia.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Destroy(copia); return null; }
        // o eixo comprido na diagonal da foto
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        Vector3 s = b.size;
        Vector3 comprido = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
        Vector3 fino = s.x <= s.y && s.x <= s.z ? Vector3.right : s.y <= s.z ? Vector3.up : Vector3.forward;
        copia.transform.rotation = Quaternion.AngleAxis(giro, Vector3.forward) * Quaternion.Inverse(Quaternion.LookRotation(fino, Vector3.Cross(comprido, fino)));
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);

        var camGo = new GameObject("Câmera do Ícone");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.08f;
        cam.transform.position = b.center - Vector3.forward * (b.extents.z + 2f);
        cam.transform.rotation = Quaternion.identity;
        cam.nearClipPlane = 0.01f; cam.farClipPlane = b.size.z + 4f;
        cam.cullingMask = 1 << camada;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(1f, 0f, 1f, 0f);
        cam.enabled = false;
        var dados = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        dados.renderPostProcessing = false; dados.renderShadows = false;
        var rt = RenderTexture.GetTemporary(tam, tam, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var antes = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, tam, tam), 0, 0);
        RenderTexture.active = antes;
        cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        Destroy(camGo); Destroy(copia);

        // silhueta clara com um pouco do relevo do modelo
        var px = tex.GetPixels();
        float lumMax = 0.001f;
        for (int i = 0; i < px.Length; i++) if (!Fundo(px[i])) lumMax = Mathf.Max(lumMax, px[i].grayscale);
        for (int i = 0; i < px.Length; i++)
        {
            if (Fundo(px[i])) { px[i] = new Color(1f, 1f, 1f, 0f); continue; }
            float l = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(px[i].grayscale / lumMax));
            px[i] = new Color(l, l * 0.97f, l * 0.92f, 1f);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f));
    }

    private static bool Fundo(Color c) => Mathf.Abs(c.r - 1f) + c.g + Mathf.Abs(c.b - 1f) < 0.12f;

    // punho fechado desenhado (as mãos não têm modelo solto)
    private static Sprite Punho()
    {
        const int tam = 128;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        var px = new Color[tam * tam];
        // dedos (4 nós), palma, polegar, punho
        var formas = new List<(Vector2 c, Vector2 meio, float r)>
        {
            (new Vector2(44, 88), new Vector2(9, 11), 8), (new Vector2(63, 91), new Vector2(9, 12), 8),
            (new Vector2(82, 89), new Vector2(9, 11), 8), (new Vector2(99, 83), new Vector2(8, 10), 7),
            (new Vector2(70, 66), new Vector2(34, 18), 12), (new Vector2(40, 62), new Vector2(20, 9), 8),
            (new Vector2(72, 32), new Vector2(20, 22), 6),
        };
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
            {
                float a = 0f;
                foreach (var (c, meio, r) in formas)
                {
                    Vector2 q = new Vector2(Mathf.Abs(x - c.x), Mathf.Abs(y - c.y)) - meio + Vector2.one * r;
                    float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                    a = Mathf.Max(a, Mathf.Clamp01(0.5f - d));
                }
                // vincos entre os dedos
                foreach (float vx in new[] { 53.5f, 72.5f, 90.5f }) if (y > 78 && Mathf.Abs(x - vx) < 1.2f) a *= 0.25f;
                px[y * tam + x] = new Color(0.95f, 0.92f, 0.86f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f));
    }

    // setor de anel (fatia da roda), com a borda de fora meio raspada como pincel
    private static Sprite Setor(int tam, float r0, float r1, float abertura, bool pincel)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[tam * tam];
        float meio = tam / 2f, px1 = 1f / meio;
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
            {
                float dx = (x + 0.5f - meio) / meio, dy = (y + 0.5f - meio) / meio;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg); // 0 em cima
                float fora = r1 - (pincel ? 0.035f * Mathf.PerlinNoise(ang * 0.35f, 3.1f) : 0f);
                float a = Mathf.Clamp01((r - r0) / px1 + 0.5f) * Mathf.Clamp01((fora - r) / px1 + 0.5f);
                if (abertura < 359f) a *= Mathf.Clamp01((abertura / 2f - ang) * r * meio * Mathf.Deg2Rad + 0.5f);
                px[y * tam + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f));
    }

    private static Sprite branco;
    private static Sprite Branco()
    {
        if (branco != null) return branco;
        var t = new Texture2D(4, 4); var p = new Color[16]; for (int i = 0; i < 16; i++) p[i] = Color.white; t.SetPixels(p); t.Apply();
        return branco = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    private static RectTransform Rect(string nome, Transform pai, Vector2 tam, Vector2 ancora)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = ancora;
        rt.sizeDelta = tam;
        return rt;
    }

    private TMP_Text Texto(string nome, Transform pai, float tam, TextAlignmentOptions al)
    {
        var rt = Rect(nome, pai, new Vector2(300f, 40f), new Vector2(0.5f, 0.5f));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.fontSize = tam; t.alignment = al; t.color = CorTexto; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.richText = true;
        return t;
    }
}
