using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public string itemId;
    public int quantity;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Slots")]
    [SerializeField] private int slotCount = 9;

    private InventorySlot[] slots;

    public event Action OnInventoryChanged;
    public event Action<int> OnSelectedSlotChanged;

    public int SelectedSlotIndex { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        slots = new InventorySlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            slots[i] = new InventorySlot { itemId = "", quantity = 0 };
    }

    public bool AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
            return false;

        foreach (var slot in slots)
        {
            if (slot.itemId == itemId)
            {
                slot.quantity += amount;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        foreach (var slot in slots)
        {
            if (string.IsNullOrEmpty(slot.itemId))
            {
                slot.itemId = itemId;
                slot.quantity = amount;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        Debug.Log("Inventário cheio, não coube: " + itemId);
        return false;
    }

    public bool RemoveItem(string itemId, int amount = 1)
    {
        foreach (var slot in slots)
        {
            if (slot.itemId == itemId && slot.quantity >= amount)
            {
                slot.quantity -= amount;

                if (slot.quantity <= 0)
                {
                    slot.itemId = "";
                    slot.quantity = 0;
                }

                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    public int GetQuantity(string itemId)
    {
        int total = 0;

        foreach (var slot in slots)
        {
            if (slot.itemId == itemId)
                total += slot.quantity;
        }

        return total;
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return;

        SelectedSlotIndex = index;
        OnSelectedSlotChanged?.Invoke(index);
    }

    public void LimparInventario()
    {
        foreach (var slot in slots)
        {
            slot.itemId = "";
            slot.quantity = 0;
        }

        OnInventoryChanged?.Invoke();
    }

    public IReadOnlyList<InventorySlot> Slots => slots;
}