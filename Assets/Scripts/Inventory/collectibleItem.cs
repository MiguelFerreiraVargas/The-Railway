using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    public string itemName = "Graveto";
    public int amount = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Inventory inventory = other.GetComponent<Inventory>();
            if (inventory != null)
            {
                inventory.AddItem(itemName, amount);
                Destroy(gameObject);
            }
        }
    }
}