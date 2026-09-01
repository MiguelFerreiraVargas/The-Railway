using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    public Text[] slotNameTexts;
    public Text[] slotTimeTexts;

    public GameSaveController gameSaveController;

    void Start()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            if (SaveManager.HasSave(i))
            {
                SaveData data = SaveManager.Load(i);

                slotNameTexts[i].text = data.saveName;

                int hours = Mathf.FloorToInt(data.playTimeSeconds / 3600);
                int minutes = Mathf.FloorToInt(
                    (data.playTimeSeconds % 3600) / 60
                );

                slotTimeTexts[i].text =
                    $"{hours:00}h {minutes:00}m played";
            }
            else
            {
                slotNameTexts[i].text = "Empty slot";
                slotTimeTexts[i].text = "";
            }
        }
    }

    public void SaveSlot(int slot)
    {
        gameSaveController.SaveGame(slot);
        RefreshSlots();
    }

    public void LoadSlot(int slot)
    {
        gameSaveController.LoadGame(slot);
    }

    public void DeleteSlot(int slot)
    {
        SaveManager.DeleteSave(slot);
        RefreshSlots();
    }
}