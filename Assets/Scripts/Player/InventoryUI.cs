using UnityEngine;
using UnityEngine.InputSystem;

// Popula os slots visuais como você preferir (grid de ícones, lista de texto etc) —
// deixei o RefreshUI() com um exemplo simples em texto, troca pela sua UI de verdade.
public class InventoryUI : MonoBehaviour, IClosablePanel
{
    [Header("UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private UnityEngine.UI.Text contentText; // troca por TMP_Text se usar TextMeshPro

    [Header("Input")]
    [SerializeField] private InputActionReference toggleInventoryAction; // binda em <Keyboard>/tab

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

    // chamado pelo UIManager (ESC) ou por você mesmo (toggle, botão de fechar)
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
            contentText.text += $"{slot.itemId} x{slot.quantity}\n";
        }
    }
}