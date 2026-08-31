using UnityEngine;

// Coloca esse script em qualquer objeto que o player deva poder coletar
// (junto com um Collider, na layer configurada como interactableLayer no PlayerArmActions).
public class CollectibleItem : MonoBehaviour, ICollectible
{
    [Header("Coleta")]
    [SerializeField] private string itemId = "item_teste";
    [SerializeField] private int amount = 1;
    [SerializeField] private GameObject collectEffectPrefab; // opcional: partícula/efeito ao coletar

    public void Collect()
    {
        PlayerInventory.Instance?.AddItem(itemId, amount);

        if (collectEffectPrefab != null)
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}