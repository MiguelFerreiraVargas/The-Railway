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

    // Mochila: sem ela, so cabe o que vai nos bolsos (peso em kg, ver Itens)
    public const string FlagMochila = "jogador.mochila";
    [Header("Peso")]
    [SerializeField] private float capacidadeBolsos = 4f;
    [SerializeField] private float capacidadeMochila = 18f;
    public bool TemMochila => GameFlags.Has(FlagMochila);
    public float Capacidade => TemMochila ? capacidadeMochila : capacidadeBolsos;
    public float PesoAtual
    {
        get
        {
            float p = 0f;
            if (slots != null) foreach (var s in slots) if (!string.IsNullOrEmpty(s.itemId)) p += Itens.Peso(s.itemId) * s.quantity;
            return p;
        }
    }
    /// <summary>Avisado quando algo nao coube (id do item).</summary>
    public static event Action<string> NaoCoube;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        slotCount = Mathf.Max(slotCount, 16); // um slot por tipo de item
        slots = new InventorySlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            slots[i] = new InventorySlot { itemId = "", quantity = 0 };
    }

    public bool AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
            return false;

        // a mochila nao vai para dentro de si mesma: ela aumenta o que cabe
        if (itemId == "mochila")
        {
            GameFlags.Set(FlagMochila);
            OnInventoryChanged?.Invoke();
            return true;
        }

        if (PesoAtual + Itens.Peso(itemId) * amount > Capacidade + 0.0001f)
        {
            NaoCoube?.Invoke(itemId);
            return false;
        }

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