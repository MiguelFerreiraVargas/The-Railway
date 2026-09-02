using UnityEngine;
using UnityEngine.InputSystem;


public class InventoryUI : MonoBehaviour, IClosablePanel
{
    [Header("UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private UnityEngine.UI.Text contentText; 

    [Header("Input")]
    [SerializeField] private InputActionReference toggleInventoryAction; 

    private bool isOpen;

    private void Awake()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.action.Enable();
            toggleInventoryAction.action.performed += OnToggle;
        }

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnInventoryChanged += RefreshUI;
    }

    private void OnDisable()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.action.performed -= OnToggle;
            toggleInventoryAction.action.Disable();
        }

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnInventoryChanged -= RefreshUI;
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        if (isOpen)
            ClosePanel();
        else
            Open();
    }

    private void Open()
    {
        if (inventoryPanel == null)
            return;

        inventoryPanel.SetActive(true);
        isOpen = true;

        UIManager.Instance?.OpenPanel(this);
        RefreshUI();
    }

    public void ClosePanel()
    {
        if (!isOpen)
            return;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        isOpen = false;
        UIManager.Instance?.ClosePanelInternal(this);
    }

    private void RefreshUI()
    {
        if (contentText == null || PlayerInventory.Instance == null)
            return;

        contentText.text = "";

        foreach (var slot in PlayerInventory.Instance.Slots)
        {
            if (string.IsNullOrEmpty(slot.itemId))
                continue; // slot vazio, não mostra

            contentText.text += $"{slot.itemId} x{slot.quantity}\n";
        }
    }
}