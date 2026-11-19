using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Cria o prefab da tela de carregamento em Assets/Resources (o LoadingScreen
/// carrega de lá sozinho) e gera uma prévia.
/// </summary>
public static class LoadingScreenBuilder
{
    public const string PrefabPath = "Assets/Resources/Tela de Carregamento.prefab";
    private const string LedFontPath = "Assets/Prefabs/VCR_OSD_MONO_1.001 SDF.asset";
    private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const int SegmentCount = 40;

    private static readonly Color Led = new Color(1f, 0.68f, 0.18f, 1f);
    private static readonly Color LedDim = new Color(1f, 0.68f, 0.18f, 0.55f);

    [MenuItem("The Railway/Tela de Carregamento/Criar prefab")]
    private static void BuildFromMenu()
    {
        var sb = new StringBuilder();
        Build(sb);
        Debug.Log("[The Railway] Tela de carregamento:\n" + sb);
    }

    public static void Build(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var go = BuildUi();
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        log.AppendLine("prefab salvo: " + PrefabPath);
    }

    private static GameObject BuildUi()
    {
        var ledFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LedFontPath);
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        var go = new GameObject("Tela de Carregamento", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // na frente de tudo
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true; // nada atrás recebe clique

        var bg = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        var bgRt = (RectTransform)bg.transform;
        bgRt.SetParent(root, false);
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.01f, 0.008f, 0.012f, 1f);

        const float left = -780f;
        Text(root, "Titulo", "CARREGANDO VIAGEM", ledFont, 76, Led, new Vector2(left, 110), new Vector2(1400, 100), TextAlignmentOptions.Left);
        var status = Text(root, "Etapa", "CARREGANDO A CENA", ledFont, 40, LedDim, new Vector2(left, 30), new Vector2(1200, 60), TextAlignmentOptions.Left);
        var percent = Text(root, "Porcentagem", "0%", ledFont, 40, Led, new Vector2(-left, 30), new Vector2(300, 60), TextAlignmentOptions.Right);

        // Barra em segmentos de LED
        var bar = new GameObject("Barra", typeof(RectTransform));
        var barRt = (RectTransform)bar.transform;
        barRt.SetParent(root, false);
        const float step = 39f, size = 33f;
        var segments = new Image[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            var seg = new GameObject($"Segmento {i + 1}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)seg.transform;
            rt.SetParent(barRt, false);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left + i * step, -50f);
            rt.sizeDelta = new Vector2(size, 40f);
            segments[i] = seg.GetComponent<Image>();
            segments[i].color = new Color(Led.r, Led.g, Led.b, 0.12f);
            segments[i].raycastTarget = false;
        }

        var tip = Text(root, "Dica", "", bodyFont, 28, new Color(0.92f, 0.88f, 0.8f, 0.7f), new Vector2(0, -360), new Vector2(1600, 60), TextAlignmentOptions.Center);
        tip.fontStyle = FontStyles.Italic;

        var screen = go.AddComponent<LoadingScreen>();
        var so = new SerializedObject(screen);
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("percentText").objectReferenceValue = percent;
        so.FindProperty("tipText").objectReferenceValue = tip;
        var arr = so.FindProperty("segments");
        arr.arraySize = segments.Length;
        for (int i = 0; i < segments.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    // ------------------------------------------------------------ Prévia

    /// <summary>Renderiza a tela numa cena vazia temporária e volta para a cena que estava aberta.</summary>
    public static void Capture(StringBuilder log)
    {
        string previous = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var go = Object.Instantiate(prefab);
            var cam = Camera.main;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            // Simula 57% e uma etapa/dica
            var segs = go.transform.Find("Barra").GetComponentsInChildren<Image>();
            for (int i = 0; i < segs.Length; i++)
                segs[i].color = i < Mathf.RoundToInt(0.57f * segs.Length) ? Led : new Color(Led.r, Led.g, Led.b, 0.12f);
            go.transform.Find("Etapa").GetComponent<TMP_Text>().text = "GERANDO A FLORESTA..";
            go.transform.Find("Porcentagem").GetComponent<TMP_Text>().text = "57%";
            go.transform.Find("Dica").GetComponent<TMP_Text>().text = "As coordenadas do rádio: -132 432 -22.";

            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Directory.CreateDirectory("Temp/TheRailway");
            File.WriteAllBytes("Temp/TheRailway/carregamento.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            log.AppendLine("prévia: carregamento.png");
        }
        finally
        {
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
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
        tmp.raycastTarget = false;
        return tmp;
    }
}
