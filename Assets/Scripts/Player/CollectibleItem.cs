using UnityEngine;

public class CollectibleItem : MonoBehaviour, ICollectible, IInteractable
{
    [Header("Item")]
    [SerializeField] private string itemId = "item_teste";
    [SerializeField] private int amount = 1;

    [Header("Comida (opcional)")]
    [SerializeField] private bool isComida = false;
    [SerializeField] private int restauraFome = 20;

    [Header("Efeitos (opcional)")]
    [SerializeField] private GameObject collectEffectPrefab;
    [SerializeField] private GameObject consumeEffectPrefab;

    [Header("Outline")]
    [SerializeField] private Outline outline;

    private void Awake()
    {
        // Tenta pegar automaticamente se você esquecer de arrastar
        if (outline == null)
            outline = GetComponent<Outline>();

        HideOutline();
    }

    public void Collect()
    {
        HideOutline();

        PlayerInventory.Instance?.AddItem(itemId, amount);

        if (collectEffectPrefab != null)
            Instantiate(
                collectEffectPrefab,
                transform.position,
                Quaternion.identity
            );

        Destroy(gameObject);
    }

    public void Interact()
    {
        if (isComida)
        {
            HideOutline();

            BarraFome.Instance?.Comer(restauraFome);

            if (consumeEffectPrefab != null)
                Instantiate(
                    consumeEffectPrefab,
                    transform.position,
                    Quaternion.identity
                );

            Destroy(gameObject);
        }
        else
        {
            Collect();
        }
    }

    public void ShowOutline()
    {
        if (outline != null)
            outline.enabled = true;
    }

    public void HideOutline()
    {
        if (outline != null)
            outline.enabled = false;
    }
}