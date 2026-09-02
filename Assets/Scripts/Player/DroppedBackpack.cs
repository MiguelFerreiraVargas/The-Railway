using System.Collections.Generic;
using UnityEngine;

public class DroppedBackpack : MonoBehaviour, ICollectible
{
    private List<InventorySlot> itensGuardados;

    public void Configurar(List<InventorySlot> itens)
    {
        itensGuardados = itens;
    }

    public void Collect()
    {
        if (itensGuardados != null && PlayerInventory.Instance != null)
        {
            foreach (var slot in itensGuardados)
            {
                if (string.IsNullOrEmpty(slot.itemId) || slot.quantity <= 0)
                    continue; // ignora slots vazios

                PlayerInventory.Instance.AddItem(slot.itemId, slot.quantity);
            }
        }

        Destroy(gameObject);
    }
}