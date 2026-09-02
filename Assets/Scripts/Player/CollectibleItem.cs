using UnityEngine;

public class CollectibleItem : MonoBehaviour, ICollectible, IInteractable
{
    [Header("Item")]
    [SerializeField] private string itemId = "item_teste";
    [SerializeField] private int amount = 1;

    [Header("Comida (opcional)")]
    [SerializeField] private bool isComida = false;
    [SerializeField] private int restauraFome = 20; // usado só se isComida = true

    [Header("Efeitos (opcional)")]
    [SerializeField] private GameObject collectEffectPrefab; // ao guardar (Q)
    [SerializeField] private GameObject consumeEffectPrefab; // ao comer (E)

    [Header("Outline")]
    [SerializeField] private Outline outline;

    private void Awake()
    {
        if (outline == null)
            outline = GetComponent<Outline>();

        HideOutline();
    }

    // Q — guarda na mochila
    public void Collect()
    {
        PlayerInventory.Instance?.AddItem(itemId, amount);

        if (collectEffectPrefab != null)
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    public bool Interact()
    {
        if (isComida)
        {
            BarraFome.Instance?.Comer(restauraFome);

            if (consumeEffectPrefab != null)
                Instantiate(consumeEffectPrefab, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
        else
        {
            Collect();
        }

        return true;
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