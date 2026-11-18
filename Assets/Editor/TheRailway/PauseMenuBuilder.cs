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
/// Cria o prefab "Menu de Pausa" (painel de LED) e instala na cena aberta.
/// Para a cena Teste save também liga o save no GameManager.
/// </summary>
public static class PauseMenuBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Pausa Esc/Menu de Pausa.prefab";
    public const string TestScenePath = "Assets/Scenes/Teste save.unity";
    private const string LedFontPath = "Assets/Prefabs/VCR_OSD_MONO_1.001 SDF.asset";
    private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string RootName = "Menu de Pausa";

    private static readonly Color Led = new Color(1f, 0.68f, 0.18f, 1f);
    private static readonly Color LedDim = new Color(1f, 0.68f, 0.18f, 0.5f);

    private const float W = 1000f, H = 720f;

    [MenuItem("The Railway/Pausa/Criar e instalar na cena aberta")]
    private static void BuildFromMenu()
    {
        var sb = new StringBuilder();
        InstallInOpenScene(sb);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[The Railway] Pausa:\n" + sb);
    }

    /// <summary>Abre a cena Teste save, instala a pausa, salva e gera prévias.</summary>
    public static void BuildForTestScene(StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        InstallInOpenScene(log);
        SetupTestSceneSave(log);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva: " + TestScenePath);
        Capture(log);
    }

    public static void InstallInOpenScene(StringBuilder log)
    {
        foreach (var old in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);

        var go = BuildUi();
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction);
        log.AppendLine("prefab salvo: " + PrefabPath + (prefab != null ? "" : " (FALHOU)"));
        log.AppendLine("pausa instalada na cena " + EditorSceneManager.GetActiveScene().name);
    }

    // ------------------------------------------------------------ Cena de teste

    private static void SetupTestSceneSave(StringBuilder log)
    {
        // Painel antigo (Resume/Quit) sem lógica: só desativa.
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Canvas" && root.transform.Find("Panel/Quit") != null && root.activeSelf)
            {
                root.SetActive(false);
                log.AppendLine("painel antigo Resume/Quit desativado (não apagado)");
            }
        }

        var manager = GameObject.Find("GameManager");
        if (manager == null)
        {
            manager = new GameObject("GameManager");
            log.AppendLine("GameManager criado");
        }

        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(manager);
        if (removed > 0) log.AppendLine($"{removed} script(s) faltando removido(s) do GameManager");

        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        // (no editor, GetComponent devolve um "nulo falso": não dá para usar ??)
        var tracker = manager.GetComponent<PlayTimeTracker>();
        if (tracker == null) tracker = manager.AddComponent<PlayTimeTracker>();
        var saver = manager.GetComponent<GameSaveController>();
        if (saver == null) saver = manager.AddComponent<GameSaveController>();
        saver.player = player != null ? player.transform : null;
        saver.playTimeTracker = tracker;
        EditorUtility.SetDirty(saver);
        log.AppendLine("GameManager com GameSaveController + PlayTimeTracker" + (player != null ? $" (jogador: {player.name})" : " (AVISO: jogador não encontrado)"));
    }

    // ------------------------------------------------------------ Interface

    private static GameObject BuildUi()
    {
        var ledFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LedFontPath);
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        var go = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;
        var rootGroup = go.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f; // escondido fora do jogo para não atrapalhar a edição
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        // Fundo escurecido
        var dim = Image(root, "Escurecer", new Color(0.02f, 0.01f, 0.04f, 0.68f));
        Stretch(dim.rectTransform);

        // Painel: moldura metálica + fundo do LED
        var frame = Image(root, "Moldura", new Color(0.09f, 0.09f, 0.1f, 1f));
        frame.rectTransform.sizeDelta = new Vector2(W + 24f, H + 24f);
        var panel = Image(root, "Painel", new Color(0.015f, 0.012f, 0.01f, 1f));
        panel.rectTransform.sizeDelta = new Vector2(W, H);

        var ledsGo = new GameObject("LEDs", typeof(RectTransform), typeof(CanvasGroup));
        var leds = (RectTransform)ledsGo.transform;
        leds.SetParent(panel.rectTransform, false);
        Stretch(leds);

        // Lista principal
        var main = Group(leds, "Lista");
        Text(main, "Titulo", "PAUSADO", ledFont, 78, Led, new Vector2(-W / 2 + 60, H / 2 - 70), new Vector2(600, 100), TextAlignmentOptions.Left);
        var clock = Text(main, "Relogio", "00:00", ledFont, 78, Led, new Vector2(W / 2 - 60, H / 2 - 70), new Vector2(400, 100), TextAlignmentOptions.Right);
        var clk = clock.gameObject.AddComponent<BoardClock>();
        SetRef(clk, "label", clock);
        Bar(main, new Vector2(0, H / 2 - 130), new Vector2(W - 100, 5));

        var bContinue = Line(main, "Continuar", "01  CONTINUAR", ledFont, H / 2 - 230);
        var bSave = Line(main, "Salvar", "02  SALVAR", ledFont, H / 2 - 330);
        var bOptions = Line(main, "Opcoes", "03  OPÇÕES", ledFont, H / 2 - 430);
        var bQuit = Line(main, "Sair", "04  SAIR", ledFont, H / 2 - 530);

        // Lista de saves
        var save = Group(leds, "Salvar");
        Text(save, "Titulo", "SALVAR VIAGEM", ledFont, 70, Led, new Vector2(-W / 2 + 60, H / 2 - 70), new Vector2(W - 120, 100), TextAlignmentOptions.Left);
        Bar(save, new Vector2(0, H / 2 - 130), new Vector2(W - 100, 5));
        Text(save, "Colunas", "       TEMPO  DATA", ledFont, 46, LedDim, new Vector2(-W / 2 + 60, H / 2 - 175), new Vector2(W - 120, 60), TextAlignmentOptions.Left);

        var slots = new Button[3];
        var lines = new SaveBoardLine[3];
        for (int i = 0; i < 3; i++)
        {
            slots[i] = Line(save, $"Slot {i + 1}", $"0{i + 1}  -- VAZIO --", ledFont, H / 2 - 255 - i * 95);
            lines[i] = slots[i].gameObject.AddComponent<SaveBoardLine>();
            var lso = new SerializedObject(lines[i]);
            lso.FindProperty("slot").intValue = i;
            lso.FindProperty("label").objectReferenceValue = slots[i].GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Texto");
            lso.ApplyModifiedPropertiesWithoutUndo();
        }
        var bBack = Line(save, "Voltar", "<   VOLTAR", ledFont, H / 2 - 545);

        // Opções
        var options = Group(leds, "Opcoes");
        Text(options, "Titulo", "OPÇÕES", ledFont, 78, Led, new Vector2(-W / 2 + 60, H / 2 - 70), new Vector2(W - 120, 100), TextAlignmentOptions.Left);
        Bar(options, new Vector2(0, H / 2 - 130), new Vector2(W - 100, 5));
        var oVolume = OptionRow(options, "Volume", OptionLine.Kind.Volume, "VOLUME", ledFont, H / 2 - 230);
        var oSens = OptionRow(options, "Sensibilidade", OptionLine.Kind.Sensitivity, "SENSIBILIDADE", ledFont, H / 2 - 330);
        var oFull = OptionRow(options, "Tela Cheia", OptionLine.Kind.Fullscreen, "TELA CHEIA", ledFont, H / 2 - 430);
        var bOptionsBack = Line(options, "Voltar", "<   VOLTAR", ledFont, H / 2 - 545);

        // Sair para...
        var quit = Group(leds, "Sair");
        Text(quit, "Titulo", "SAIR PARA", ledFont, 78, Led, new Vector2(-W / 2 + 60, H / 2 - 70), new Vector2(W - 120, 100), TextAlignmentOptions.Left);
        Bar(quit, new Vector2(0, H / 2 - 130), new Vector2(W - 100, 5));
        var bDesktop = Line(quit, "Area de Trabalho", "01  ÁREA DE TRABALHO", ledFont, H / 2 - 230);
        var bToMenu = Line(quit, "Voltar ao Menu", "02  VOLTAR AO MENU", ledFont, H / 2 - 330);
        var bQuitBack = Line(quit, "Voltar", "<   VOLTAR", ledFont, H / 2 - 545);

        // Mensagem (salvo / erro)
        var msg = Text(leds, "Mensagem", "", ledFont, 44, Led, new Vector2(0, -H / 2 + 50), new Vector2(W - 60, 70), TextAlignmentOptions.Center);
        msg.alpha = 0f;

        // Dicas abaixo do painel
        var hint = Text(root, "Dicas", "ESC  VOLTAR      ENTER  SELECIONAR      SETAS  AJUSTAR", bodyFont, 22, new Color(0.9f, 0.86f, 0.78f, 0.8f), new Vector2(0, -H / 2 - 50), new Vector2(900, 50), TextAlignmentOptions.Center);
        hint.characterSpacing = 8f;

        // Som e painel de LED
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.ignoreListenerPause = true;


        // Controlador
        var pause = go.AddComponent<PauseMenu>();
        UnityEventTools.AddPersistentListener(bContinue.onClick, new UnityAction(pause.Resume));
        UnityEventTools.AddPersistentListener(bSave.onClick, new UnityAction(pause.OpenSave));
        UnityEventTools.AddPersistentListener(bOptions.onClick, new UnityAction(pause.OpenOptions));
        UnityEventTools.AddPersistentListener(bQuit.onClick, new UnityAction(pause.OpenQuit));
        UnityEventTools.AddPersistentListener(bOptionsBack.onClick, new UnityAction(pause.BackToMain));
        UnityEventTools.AddPersistentListener(bDesktop.onClick, new UnityAction(pause.Quit));
        UnityEventTools.AddPersistentListener(bToMenu.onClick, new UnityAction(pause.GoToMainMenu));
        UnityEventTools.AddPersistentListener(bQuitBack.onClick, new UnityAction(pause.BackToMain));
        for (int i = 0; i < 3; i++)
            UnityEventTools.AddIntPersistentListener(slots[i].onClick, pause.SaveToSlot, i);
        UnityEventTools.AddPersistentListener(bBack.onClick, new UnityAction(pause.BackToMain));

        Chain(bContinue, bSave, bOptions, bQuit);
        Chain(slots[0], slots[1], slots[2], bBack);
        Chain(oVolume, oSens, oFull, bOptionsBack);
        Chain(bDesktop, bToMenu, bQuitBack);

        var so = new SerializedObject(pause);
        so.FindProperty("root").objectReferenceValue = rootGroup;
        so.FindProperty("mainList").objectReferenceValue = main.GetComponent<CanvasGroup>();
        so.FindProperty("saveList").objectReferenceValue = save.GetComponent<CanvasGroup>();
        so.FindProperty("optionsList").objectReferenceValue = options.GetComponent<CanvasGroup>();
        so.FindProperty("quitList").objectReferenceValue = quit.GetComponent<CanvasGroup>();
        so.FindProperty("firstOptionsButton").objectReferenceValue = oVolume.gameObject;
        so.FindProperty("firstQuitButton").objectReferenceValue = bDesktop.gameObject;
        so.FindProperty("firstMainButton").objectReferenceValue = bContinue.gameObject;
        so.FindProperty("firstSaveButton").objectReferenceValue = slots[0].gameObject;
        var arr = so.FindProperty("saveLines");
        arr.arraySize = 3;
        for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
        so.FindProperty("message").objectReferenceValue = msg;
        so.FindProperty("uiAudio").objectReferenceValue = audio;
        so.FindProperty("confirmClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelAbrindo.mp3");
        so.FindProperty("backClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelFechando.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        // Só a lista principal começa visível
        foreach (var hidden in new[] { save, options, quit })
        {
            var g = hidden.GetComponent<CanvasGroup>();
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
        }

        return go;
    }

    // ------------------------------------------------------------ Prévias

    public static void Capture(StringBuilder log)
    {
        var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (pause == null || cam == null) { log.AppendLine("prévia: sem pausa ou câmera"); return; }

        var canvas = pause.GetComponent<Canvas>();
        var root = pause.GetComponent<CanvasGroup>();
        var main = pause.transform.Find("Painel/LEDs/Lista")?.GetComponent<CanvasGroup>();
        var save = pause.transform.Find("Painel/LEDs/Salvar")?.GetComponent<CanvasGroup>();
        var first = main != null ? main.GetComponentInChildren<DepartureBoardButton>(true) : null;
        var firstSave = save != null ? save.GetComponentInChildren<DepartureBoardButton>(true) : null;

        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevMode = canvas.renderMode;

        try
        {
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.4f);

            root.alpha = 0f;
            Shot(cam, rt, "pausa_0_jogo.png", log);

            root.alpha = 1f;
            first?.ApplyVisual(1f, true);
            Shot(cam, rt, "pausa_1_menu.png", log);
            first?.ApplyVisual(0f, false);

            main.alpha = 0f; save.alpha = 1f;
            firstSave?.ApplyVisual(1f, true);
            Shot(cam, rt, "pausa_2_salvar.png", log);
            firstSave?.ApplyVisual(0f, false);
            save.alpha = 0f;

            var options = pause.transform.Find("Painel/LEDs/Opcoes")?.GetComponent<CanvasGroup>();
            var quit = pause.transform.Find("Painel/LEDs/Sair")?.GetComponent<CanvasGroup>();
            if (options != null)
            {
                options.alpha = 1f;
                var o = options.GetComponentInChildren<DepartureBoardButton>(true);
                o?.ApplyVisual(1f, true);
                Shot(cam, rt, "pausa_3_opcoes.png", log);
                o?.ApplyVisual(0f, false);
                options.alpha = 0f;
            }
            if (quit != null)
            {
                quit.alpha = 1f;
                var q = quit.GetComponentInChildren<DepartureBoardButton>(true);
                q?.ApplyVisual(1f, true);
                Shot(cam, rt, "pausa_4_sair.png", log);
                q?.ApplyVisual(0f, false);
                quit.alpha = 0f;
            }
        }
        finally
        {
            cam.targetTexture = prevTarget;
            canvas.renderMode = prevMode;
            canvas.worldCamera = null;
            root.alpha = 0f;
            if (main != null) main.alpha = 1f;
            if (save != null) save.alpha = 0f;
            Object.DestroyImmediate(rt);
        }
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

    private static RectTransform Group(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

    private static Button Line(RectTransform parent, string name, string text, TMP_FontAsset font, float y)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DepartureBoardButton));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(W - 80f, 86f);

        var img = go.GetComponent<Image>();
        img.color = new Color(Led.r, Led.g, Led.b, 0f);
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;

        float left = -(W - 80f) / 2f;
        var label = Text(rt, "Texto", text, font, 60, LedDim, new Vector2(left + 70f, 0f), new Vector2(W - 180f, 86f), TextAlignmentOptions.Left);
        var cursor = Text(rt, "Cursor", ">", font, 60, Color.black, new Vector2(left + 32f, 0f), new Vector2(40f, 86f), TextAlignmentOptions.Center);

        var dbb = go.GetComponent<DepartureBoardButton>();
        var so = new SerializedObject(dbb);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("cursor").objectReferenceValue = cursor;
        so.FindProperty("highlight").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
        dbb.ApplyVisual(0f, false);
        return button;
    }

    private static Selectable OptionRow(RectTransform parent, string name, OptionLine.Kind kind, string title, TMP_FontAsset font, float y)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Selectable), typeof(DepartureBoardButton), typeof(OptionLine));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(W - 80f, 86f);

        var img = go.GetComponent<Image>();
        img.color = new Color(Led.r, Led.g, Led.b, 0f);
        var sel = go.GetComponent<Selectable>();
        sel.transition = Selectable.Transition.None;
        sel.targetGraphic = img;

        float left = -(W - 80f) / 2f;
        var label = Text(rt, "Texto", title, font, 56, LedDim, new Vector2(left + 70f, 0f), new Vector2(W - 180f, 86f), TextAlignmentOptions.Left);
        var cursor = Text(rt, "Cursor", ">", font, 56, Color.black, new Vector2(left + 32f, 0f), new Vector2(40f, 86f), TextAlignmentOptions.Center);

        var dbb = go.GetComponent<DepartureBoardButton>();
        var dso = new SerializedObject(dbb);
        dso.FindProperty("label").objectReferenceValue = label;
        dso.FindProperty("cursor").objectReferenceValue = cursor;
        dso.FindProperty("highlight").objectReferenceValue = img;
        dso.ApplyModifiedPropertiesWithoutUndo();
        dbb.ApplyVisual(0f, false);

        var opt = go.GetComponent<OptionLine>();
        var oso = new SerializedObject(opt);
        oso.FindProperty("kind").enumValueIndex = (int)kind;
        oso.FindProperty("title").stringValue = title;
        oso.FindProperty("label").objectReferenceValue = label;
        oso.ApplyModifiedPropertiesWithoutUndo();
        opt.Refresh();

        return sel;
    }

    private static void Chain(params Selectable[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length],
                selectOnDown = buttons[(i + 1) % buttons.Length],
            };
        }
    }

    private static Image Image(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = name == "Escurecer"; // bloqueia cliques no jogo atrás
        return img;
    }

    private static void Bar(RectTransform parent, Vector2 pos, Vector2 size)
    {
        var img = Image(parent, "Linha", Led);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        img.raycastTarget = false;
    }

    private static TextMeshProUGUI Text(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        if (align == TextAlignmentOptions.Left) rt.pivot = new Vector2(0f, 0.5f);
        else if (align == TextAlignmentOptions.Right) rt.pivot = new Vector2(1f, 0.5f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
