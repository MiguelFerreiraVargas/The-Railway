using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estilo da UI do menu: título com brilho violeta, botões como placas de estação
/// iluminadas por lampião e painel de saves escuro e legível.
/// Mexe nos objetos do Canvas da cena (vira override do prefab) com Undo.
/// </summary>
public static class MenuUiStyle
{
    private const string TitleMaterialPath = "Assets/Prefabs/Menu Canva/Titulo Violeta.mat";
    private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string UndoName = "Atmosfera noturna do menu";

    private static readonly Color Ivory = new Color(0.93f, 0.9f, 0.83f, 1f);
    private static readonly Color Amber = new Color(1f, 0.72f, 0.36f, 1f);
    private static readonly Color Muted = new Color(0.72f, 0.7f, 0.66f, 1f);
    private static readonly Color Violet = new Color(0.48f, 0.22f, 0.78f, 0.6f);

    public static MenuTuning T = new MenuTuning();

    public static void Apply(List<string> report)
    {
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.transform.Find("ButtonController") != null);

        if (canvas == null)
        {
            report.Add("Canvas do menu não encontrado (UI não alterada)");
            return;
        }

        var root = canvas.transform;
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        StyleTitle(root, report);
        StylePrompt(root, bodyFont, report);
        StyleButtons(root, bodyFont, report);
        StyleSavePanel(root, bodyFont, report);
    }

    // ----------------------------------------------------------------- Título

    private static void StyleTitle(Transform root, List<string> report)
    {
        var title = root.Find("Ttile")?.GetComponent<TMP_Text>();

        if (title == null)
            return;

        Undo.RecordObject(title, UndoName);
        Undo.RecordObject(title.rectTransform, UndoName);

        // O subtítulo vai no mesmo texto para acompanhar as animações do título.
        title.text = $"THE RAILWAY\n<font=\"LiberationSans SDF\"><size=22%><cspace={T.subtitleSpacing}>A ÚLTIMA VIAGEM</cspace></size></font>";
        title.fontSize = T.titleFontSize;
        title.characterSpacing = T.titleCharacterSpacing;
        title.lineSpacing = -10f;
        title.color = Ivory;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        title.rectTransform.sizeDelta = new Vector2(1500f, 260f);

        var material = GetTitleMaterial(title.fontSharedMaterial);

        if (material != null)
            title.fontSharedMaterial = material;

        report.Add("Título maior com subtítulo \"A ÚLTIMA VIAGEM\" e brilho violeta");
    }

    private static Material GetTitleMaterial(Material source)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);

        if (material == null)
        {
            if (source == null)
                return null;

            material = new Material(source) { name = "Titulo Violeta" };
            AssetDatabase.CreateAsset(material, TitleMaterialPath);
        }

        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", Violet);
        material.SetFloat("_UnderlaySoftness", 0.75f);
        material.SetFloat("_UnderlayDilate", 0.35f);
        material.SetFloat("_UnderlayOffsetX", 0f);
        material.SetFloat("_UnderlayOffsetY", 0f);
        EditorUtility.SetDirty(material);

        return material;
    }

    // ------------------------------------------------------- "Aperte um botão"

    private static void StylePrompt(Transform root, TMP_FontAsset bodyFont, List<string> report)
    {
        var prompt = root.Find("Press")?.GetComponent<TMP_Text>();

        if (prompt == null)
            return;

        Undo.RecordObject(prompt, UndoName);
        Undo.RecordObject(prompt.rectTransform, UndoName);

        if (bodyFont != null)
            prompt.font = bodyFont;

        prompt.text = "APERTE QUALQUER BOTÃO";
        prompt.fontSize = 24f;
        prompt.characterSpacing = 14f;
        prompt.color = Muted;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.rectTransform.sizeDelta = new Vector2(900f, 50f);

        report.Add("Texto \"APERTE QUALQUER BOTÃO\" limpo e espaçado");
    }

    // ---------------------------------------------------------------- Botões

    private static void StyleButtons(Transform root, TMP_FontAsset bodyFont, List<string> report)
    {
        var controller = root.Find("ButtonController");

        if (controller == null)
            return;

        var entries = new[]
        {
            ("Start", "JOGAR"),
            ("Carregar", "CARREGAR"),
            ("exit", "SAIR"),
        };

        int styled = 0;

        for (int i = 0; i < entries.Length; i++)
        {
            var button = controller.Find(entries[i].Item1);

            if (button == null)
                continue;

            var rect = (RectTransform)button;
            Undo.RecordObject(rect, UndoName);
            rect.sizeDelta = new Vector2(440f, 64f);
            rect.anchoredPosition = new Vector2(0f, T.buttonY[Mathf.Min(i, T.buttonY.Length - 1)]);

            var image = button.GetComponent<Image>();

            if (image != null)
            {
                Undo.RecordObject(image, UndoName);
                image.color = new Color(0f, 0f, 0f, 0f); // fundo invisível, mas ainda clicável
                image.raycastTarget = true;
            }

            var uiButton = button.GetComponent<Button>();

            if (uiButton != null)
            {
                Undo.RecordObject(uiButton, UndoName);
                uiButton.transition = Selectable.Transition.None;
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                Undo.RecordObject(label, UndoName);
                Undo.RecordObject(label.rectTransform, UndoName);

                if (bodyFont != null)
                    label.font = bodyFont;

                label.text = entries[i].Item2;
                label.fontSize = T.buttonFontSize;
                label.fontStyle = FontStyles.Normal;
                label.characterSpacing = 12f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.78f, 0.76f, 0.72f, 0.85f);
                label.textWrappingMode = TextWrappingModes.NoWrap;

                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                labelRect.anchoredPosition = Vector2.zero;
            }

            var marker = GetOrCreateMarker(button);

            var anim = button.GetComponent<MenuButtonAnimation>();

            if (anim == null)
                anim = Undo.AddComponent<MenuButtonAnimation>(button.gameObject);

            var so = new SerializedObject(anim);
            so.FindProperty("selectedScale").floatValue = 1.05f;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("labelSlide").floatValue = 0f;
            so.FindProperty("selectedCharacterSpacing").floatValue = 8f;
            so.FindProperty("selectionMarker").objectReferenceValue = marker;
            so.ApplyModifiedProperties();

            styled++;
        }

        report.Add($"{styled} botões como placas de estação: texto espaçado que acende em âmbar com linha de luz embaixo");
    }

    private static CanvasGroup GetOrCreateMarker(Transform button)
    {
        var existing = button.Find("Marcador");

        if (existing != null)
            return existing.GetComponent<CanvasGroup>();

        var go = new GameObject("Marcador", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, UndoName);

        var rect = (RectTransform)go.transform;
        rect.SetParent(button, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 6f);
        rect.sizeDelta = new Vector2(160f, 3f);

        var image = go.GetComponent<Image>();
        image.color = Amber;
        image.raycastTarget = false;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        return group;
    }

    // ---------------------------------------------------------- Painel de save

    private static void StyleSavePanel(Transform root, TMP_FontAsset bodyFont, List<string> report)
    {
        var save = root.Find("Save");

        if (save == null)
            return;

        var panel = save.GetComponent<Image>();

        if (panel != null)
        {
            Undo.RecordObject(panel, UndoName);
            panel.color = new Color(0.02f, 0.025f, 0.035f, T.savePanelAlpha);
        }

        // Painel cobrindo a tela inteira.
        if (save is RectTransform saveRect)
        {
            Undo.RecordObject(saveRect, UndoName);
            Stretch(saveRect);
        }

        var title = save.Find("Title")?.GetComponent<TMP_Text>();

        if (title != null)
        {
            Undo.RecordObject(title, UndoName);
            title.text = "CARREGAR";
            title.color = Ivory;
            title.characterSpacing = 6f;

            var material = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);

            // Só usa o material violeta se for da mesma fonte (mesmo atlas) do título.
            if (material != null && title.font != null && title.font.material != null &&
                material.GetTexture("_MainTex") == title.font.material.GetTexture("_MainTex"))
            {
                title.fontSharedMaterial = material;
            }
        }

        var close = save.Find("X")?.GetComponent<Image>();

        if (close != null)
        {
            Undo.RecordObject(close, UndoName);
            close.color = new Color(Ivory.r, Ivory.g, Ivory.b, 0.8f);
        }

        var slots = save.Find("SaveSlots");

        if (slots == null)
            return;

        if (slots is RectTransform slotsRect)
        {
            Undo.RecordObject(slotsRect, UndoName);
            Stretch(slotsRect);
        }

        float[] slotX = { -500f, 0f, 500f };
        int index = 0;

        foreach (Transform slot in slots)
        {
            if (!slot.name.StartsWith("Slot"))
                continue;

            var rect = slot as RectTransform;

            if (rect != null)
            {
                Undo.RecordObject(rect, UndoName);
                Center(rect);
                rect.sizeDelta = new Vector2(440f, 260f);
                rect.anchoredPosition = new Vector2(slotX[Mathf.Min(index, 2)], -20f);
                rect.localScale = Vector3.one;
            }

            var image = slot.GetComponent<Image>();

            if (image != null)
            {
                Undo.RecordObject(image, UndoName);
                image.color = new Color(0.1f, 0.11f, 0.14f, 0.95f);
            }

            var button = slot.GetComponent<Button>();

            if (button != null)
            {
                Undo.RecordObject(button, UndoName);
                var colors = button.colors;
                colors.colorMultiplier = 2f;
                colors.normalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                colors.highlightedColor = new Color(0.95f, 0.78f, 0.55f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(0.4f, 0.35f, 0.3f, 1f);
                colors.fadeDuration = 0.12f;
                button.colors = colors;
            }

            foreach (var text in slot.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(text, UndoName);
                Undo.RecordObject(text.rectTransform, UndoName);

                if (bodyFont != null)
                    text.font = bodyFont;

                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                Center(text.rectTransform);
                text.rectTransform.sizeDelta = new Vector2(400f, 50f);
                text.rectTransform.localScale = Vector3.one;

                string n = text.name;

                if (n.StartsWith("SlotNumber"))
                {
                    text.text = $"SLOT {index + 1}";
                    text.fontSize = 22f;
                    text.characterSpacing = 14f;
                    text.color = Amber;
                    text.rectTransform.anchoredPosition = new Vector2(0f, 85f);
                }
                else if (n.Contains("SaveName"))
                {
                    text.fontSize = 36f;
                    text.characterSpacing = 2f;
                    text.color = Ivory;
                    text.rectTransform.anchoredPosition = new Vector2(0f, 25f);
                }
                else if (n.StartsWith("PlayTime"))
                {
                    text.fontSize = 24f;
                    text.characterSpacing = 4f;
                    text.color = Muted;
                    text.rectTransform.anchoredPosition = new Vector2(0f, -40f);
                }
                else if (n.StartsWith("LastPlayed"))
                {
                    text.fontSize = 20f;
                    text.characterSpacing = 4f;
                    text.color = new Color(Muted.r, Muted.g, Muted.b, 0.7f);
                    text.rectTransform.anchoredPosition = new Vector2(0f, -85f);
                }
            }

            index++;
        }

        report.Add($"Painel de saves escuro com {index} cartões legíveis que acendem em âmbar ao selecionar");
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
