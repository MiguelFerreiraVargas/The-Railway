using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public string itemId;
    public int quantity;
}

// Coloca UMA VEZ num GameObject persistente (mesmo objeto do UIManager, por exemplo).
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
            return;

        InventorySlot slot = slots.Find(s => s.itemId == itemId);

        if (slot != null)
            slot.quantity += amount;
        else
            slots.Add(new InventorySlot { itemId = itemId, quantity = amount });

        OnInventoryChanged?.Invoke();
    }

    public bool RemoveItem(string itemId, int amount = 1)
    {
        InventorySlot slot = slots.Find(s => s.itemId == itemId);

        if (slot == null || slot.quantity < amount)
            return false;

        slot.quantity -= amount;

        if (slot.quantity <= 0)
            slots.Remove(slot);

        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetQuantity(string itemId)
    {
        InventorySlot slot = slots.Find(s => s.itemId == itemId);
        return slot != null ? slot.quantity : 0;
    }

    public IReadOnlyList<InventorySlot> Slots => slots;
}