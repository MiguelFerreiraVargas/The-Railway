using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotUI
    {
        public Image icon;              // ícone do item nesse slot
        public Text quantityText;       // troca por TMP_Text se usar TextMeshPro
        public GameObject selectedFrame; // borda/destaque, ativa só quando esse slot tá selecionado
    }

    [Header("Slots (arrasta na mesma ordem do inventário, slot 0 = tecla 1)")]
    [SerializeField] private SlotUI[] slotsUI;

    [Header("Nome do item selecionado (o texto tipo 'Right Pocket')")]
    [SerializeField] private Text nomeItemSelecionado;

    [Header("Barra de Capacidade")]
    [SerializeField] private Image barraCapacidade; // Image com Fill Amount (Filled)
    [SerializeField] private int capacidadeMaxima = 20; // soma de quantidades que "enche" a barra

    private void OnEnable()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged += AtualizarUI;
            PlayerInventory.Instance.OnSelectedSlotChanged += AtualizarSelecao;
        }

        AtualizarUI();
        AtualizarSelecao(PlayerInventory.Instance != null ? PlayerInventory.Instance.SelectedSlotIndex : 0);
    }

    private void OnDisable()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged -= AtualizarUI;
            PlayerInventory.Instance.OnSelectedSlotChanged -= AtualizarSelecao;
        }
    }

    private void Update()
    {
        // teclas 1-9 selecionam o slot correspondente
        if (Keyboard.current == null || PlayerInventory.Instance == null)
            return;

        for (int i = 0; i < slotsUI.Length && i < 9; i++)
        {
            KeyControl tecla = Keyboard.current[(Key)((int)Key.Digit1 + i)];

            if (tecla != null && tecla.wasPressedThisFrame)
            {
                PlayerInventory.Instance.SelectSlot(i);
            }
        }
    }

    private void AtualizarUI()
    {
        if (PlayerInventory.Instance == null)
            return;

        var slots = PlayerInventory.Instance.Slots;
        int totalItens = 0;

        for (int i = 0; i < slotsUI.Length; i++)
        {
            SlotUI ui = slotsUI[i];
            bool temSlotDeInventario = i < slots.Count;
            InventorySlot slotData = temSlotDeInventario ? slots[i] : null;
            bool vazio = slotData == null || string.IsNullOrEmpty(slotData.itemId);

            if (!vazio)
            {
                ItemData item = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(slotData.itemId) : null;

                if (ui.icon != null)
                {
                    ui.icon.enabled = true;
                    ui.icon.sprite = item != null ? item.icon : null;
                }

                if (ui.quantityText != null)
                    ui.quantityText.text = slotData.quantity > 1 ? slotData.quantity.ToString() : "";

                totalItens += slotData.quantity;
            }
            else
            {
                if (ui.icon != null)
                    ui.icon.enabled = false;

                if (ui.quantityText != null)
                    ui.quantityText.text = "";
            }
        }

        if (barraCapacidade != null)
            barraCapacidade.fillAmount = capacidadeMaxima > 0 ? Mathf.Clamp01((float)totalItens / capacidadeMaxima) : 0f;
    }

    private void AtualizarSelecao(int index)
    {
        for (int i = 0; i < slotsUI.Length; i++)
        {
            if (slotsUI[i].selectedFrame != null)
                slotsUI[i].selectedFrame.SetActive(i == index);
        }

        if (nomeItemSelecionado == null || PlayerInventory.Instance == null)
            return;

        var slots = PlayerInventory.Instance.Slots;

        if (index < slots.Count && !string.IsNullOrEmpty(slots[index].itemId))
        {
            ItemData item = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(slots[index].itemId) : null;
            nomeItemSelecionado.text = item != null ? item.displayName : slots[index].itemId;
        }
        else
        {
            nomeItemSelecionado.text = "";
        }
    }
}