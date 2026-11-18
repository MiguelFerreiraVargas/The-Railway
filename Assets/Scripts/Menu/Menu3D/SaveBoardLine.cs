using TMPro;
using UnityEngine;

/// <summary>
/// Linha de save no quadro "CARREGAR": mostra nome, tempo jogado e data do slot.
/// </summary>
public class SaveBoardLine : MonoBehaviour
{
    [SerializeField] private int slot;
    [SerializeField] private TMP_Text label;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (label == null)
            return;

        string number = (slot + 1).ToString("00");

        if (!SaveManager.HasSave(slot))
        {
            label.text = $"{number}  -- VAZIO --";
            return;
        }

        SaveData data = SaveManager.Load(slot);

        if (data == null)
        {
            label.text = $"{number}  -- ERRO --";
            return;
        }

        int hours = Mathf.FloorToInt(data.playTimeSeconds / 3600f);
        int minutes = Mathf.FloorToInt(data.playTimeSeconds % 3600f / 60f);
        string name = string.IsNullOrEmpty(data.saveName) ? "VIAGEM" : data.saveName.ToUpperInvariant();

        if (name.Length > 10)
            name = name.Substring(0, 10);

        string date = string.IsNullOrEmpty(data.lastPlayedDate) ? "" : data.lastPlayedDate.Substring(0, Mathf.Min(5, data.lastPlayedDate.Length));

        label.text = $"{number}  {name,-10} {hours:00}H{minutes:00}  {date}";
    }
}
