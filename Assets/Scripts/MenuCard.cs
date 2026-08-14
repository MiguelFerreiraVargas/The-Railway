using UnityEngine;
using UnityEngine.EventSystems;

public class MenuCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverSpeed = 12f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openTrigger = "Open";

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private bool isHovering;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void Update()
    {
        Vector3 targetScale = isHovering ? originalScale * hoverScale : originalScale;

        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            Time.unscaledDeltaTime * hoverSpeed
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (animator != null)
            animator.SetTrigger(openTrigger);
    }
}