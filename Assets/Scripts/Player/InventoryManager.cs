using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [System.Serializable]
    public class InventoryItem
    {
        public string itemName;
        public int amount;

        public InventoryItem(string name, int quantity)
        {
            itemName = name;
            amount = quantity;
        }
    }

    [Header("UI")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("Inventário")]
    [SerializeField]
    private List<InventoryItem> items =
        new List<InventoryItem>();

    private bool isOpen;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // TAB abre
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (!isOpen)
                OpenInventory();
        }

        // ESC fecha
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isOpen)
                CloseInventory();
        }
    }

    public void AddItem(string itemName, int amount = 1)
    {
        InventoryItem existing =
            items.Find(x => x.itemName == itemName);

        if (existing != null)
        {
            existing.amount += amount;
        }
        else
        {
            items.Add(
                new InventoryItem(
                    itemName,
                    amount
                )
            );
        }

        Debug.Log(
            $"Adicionado: {itemName} x{amount}"
        );
    }

    public bool HasItem(string itemName)
    {
        InventoryItem item =
            items.Find(x => x.itemName == itemName);

        return item != null && item.amount > 0;
    }

    public bool RemoveItem(
        string itemName,
        int amount = 1)
    {
        InventoryItem item =
            items.Find(x => x.itemName == itemName);

        if (item == null)
            return false;

        if (item.amount < amount)
            return false;

        item.amount -= amount;

        if (item.amount <= 0)
            items.Remove(item);

        return true;
    }

    private void OpenInventory()
    {
        isOpen = true;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        Debug.Log("Inventário aberto");
    }

    private void CloseInventory()
    {
        isOpen = false;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;

        Debug.Log("Inventário fechado");
    }
}