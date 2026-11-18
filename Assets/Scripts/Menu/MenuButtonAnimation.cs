using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonAnimation : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("Scale")]
    [SerializeField] private float selectedScale = 1.12f;
    [SerializeField] private float animationSpeed = 10f;

    [Header("Texto")]
    [Tooltip("Texto do botão. Se vazio, procura um TMP_Text nos filhos.")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private bool tintLabel = true;
    [SerializeField] private Color normalColor = new Color(0.78f, 0.76f, 0.72f, 0.85f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.74f, 0.38f, 1f);

    [Tooltip("Quanto o texto desliza para a direita quando selecionado (pixels).")]
    [SerializeField] private float labelSlide = 18f;

    [Tooltip("Espaçamento extra entre letras quando selecionado.")]
    [SerializeField] private float selectedCharacterSpacing = 6f;

    [Header("Marcador (opcional)")]
    [Tooltip("Objeto mostrado ao lado do botão selecionado (ex.: uma seta ou lanterna).")]
    [SerializeField] private CanvasGroup selectionMarker;

    [Header("Som (opcional)")]
    [SerializeField] private AudioSource uiAudio;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchVariation = 0.06f;

    private Vector3 normalScale;
    private Vector3 targetScale;
    private RectTransform labelRect;
    private Vector2 labelStartPosition;
    private float baseCharacterSpacing;
    private float selection;
    private bool isSelected;

    private void Awake()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            labelRect = label.rectTransform;
            labelStartPosition = labelRect.anchoredPosition;
            baseCharacterSpacing = label.characterSpacing;

            if (tintLabel)
                label.color = normalColor;
        }

        if (selectionMarker != null)
            selectionMarker.alpha = 0f;
    }

    private void OnDisable()
    {
        // Evita que o botão volte "preso" no estado selecionado.
        isSelected = false;
        selection = 0f;
        targetScale = normalScale;
        transform.localScale = normalScale;
        ApplySelectionVisuals();
    }

    private void Update()
    {
        float step = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, step);
        selection = Mathf.Lerp(selection, isSelected ? 1f : 0f, step);

        ApplySelectionVisuals();
    }

    private void ApplySelectionVisuals()
    {
        if (label != null)
        {
            if (tintLabel)
                label.color = Color.Lerp(normalColor, selectedColor, selection);

            label.characterSpacing = baseCharacterSpacing + selectedCharacterSpacing * selection;

            if (labelRect != null)
                labelRect.anchoredPosition = labelStartPosition + new Vector2(labelSlide * selection, 0f);
        }

        if (selectionMarker != null)
            selectionMarker.alpha = selection;
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        targetScale = normalScale * selectedScale;
        PlayClip(hoverClip);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        targetScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClip(clickClip);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClip(clickClip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (uiAudio == null || clip == null)
            return;

        uiAudio.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        uiAudio.PlayOneShot(clip);
    }
}
