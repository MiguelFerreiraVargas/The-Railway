using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Linha do quadro de partidas. Quando selecionada, a faixa acende em âmbar,
/// o texto fica escuro e o cursor ">" pisca, como um painel de LED.
/// </summary>
public class DepartureBoardButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text cursor;
    [SerializeField] private Image highlight;

    [SerializeField] private Color ledColor = new Color(1f, 0.68f, 0.18f, 1f);
    [SerializeField] private Color ledDim = new Color(1f, 0.68f, 0.18f, 0.55f);
    [SerializeField] private Color selectedText = new Color(0.06f, 0.04f, 0.01f, 1f);
    [SerializeField] private float speed = 14f;

    private bool selected;
    private float amount;

    private void OnEnable()
    {
        ApplyVisual(selected ? 1f : 0f, true);
    }

    private void OnDisable()
    {
        selected = false;
        amount = 0f;
        ApplyVisual(0f, true);
    }

    private void Update()
    {
        amount = Mathf.MoveTowards(amount, selected ? 1f : 0f, Time.unscaledDeltaTime * speed);
        bool blinkOn = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
        ApplyVisual(amount, blinkOn);
    }

    /// <summary>Aplica o visual (0 = normal, 1 = selecionado). Também usado pelo editor.</summary>
    public void ApplyVisual(float t, bool cursorOn)
    {
        if (highlight != null)
            highlight.color = new Color(ledColor.r, ledColor.g, ledColor.b, ledColor.a * t);

        if (label != null)
            label.color = Color.Lerp(ledDim, selectedText, t);

        if (cursor != null)
        {
            cursor.color = selectedText;
            cursor.alpha = t > 0.5f && cursorOn ? 1f : 0f;
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        Menu3DController.Instance?.PlayMove();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
