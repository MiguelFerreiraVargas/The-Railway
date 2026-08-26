using UnityEngine;

public class CollectibleItem : MonoBehaviour, ICollectible
{
    [Header("Item")]
    [SerializeField] private string itemName = "Item";

    [SerializeField] private int amount = 1;

    [Header("Efeito")]
    [SerializeField] private GameObject collectEffectPrefab;

    public void Collect()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning(
                "InventoryManager não encontrado!"
            );

            return;
        }

        InventoryManager.Instance.AddItem(
            itemName,
            amount
        );

        if (collectEffectPrefab != null)
        {
            Instantiate(
                collectEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        Destroy(gameObject);
    }
}