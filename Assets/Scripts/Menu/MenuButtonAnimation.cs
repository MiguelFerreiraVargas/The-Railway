using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonAnimation : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [Header("Scale")]
    [SerializeField] private float selectedScale = 1.12f;
    [SerializeField] private float animationSpeed = 10f;

    private Vector3 normalScale;
    private Vector3 targetScale;

    private void Awake()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.unscaledDeltaTime * animationSpeed
        );
    }

    public void OnSelect(BaseEventData eventData)
    {
        targetScale = normalScale * selectedScale;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        targetScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
    }
}