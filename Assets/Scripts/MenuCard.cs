using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class MenuCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverSpeed = 10f;

    [Header("Expand")]
    [SerializeField] private float expandDuration = 0.6f;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private bool isExpanded;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
    }

    private void Update()
    {
        if (isExpanded) return;

        Vector3 targetScale = isHovering ? originalScale * hoverScale : originalScale;

        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            Time.unscaledDeltaTime * hoverSpeed
        );
    }

    private bool isHovering;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isExpanded) return;
        isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isExpanded) return;
        isHovering = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isExpanded) return;

        StartCoroutine(ExpandToFullscreen());
    }

    private IEnumerator ExpandToFullscreen()
    {
        isExpanded = true;

        Vector2 startSize = rectTransform.sizeDelta;
        Vector3 startScale = rectTransform.localScale;
        Vector2 startPosition = rectTransform.anchoredPosition;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Vector2 targetSize = Vector2.zero;
        Vector3 targetScale = Vector3.one;
        Vector2 targetPosition = Vector2.zero;

        float time = 0f;

        while (time < expandDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / expandDuration);

            rectTransform.sizeDelta = Vector2.Lerp(startSize, targetSize, t);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);

            yield return null;
        }

        rectTransform.sizeDelta = targetSize;
        rectTransform.localScale = targetScale;
        rectTransform.anchoredPosition = targetPosition;
    }
}