using TMPro;
using UnityEngine;

public class SaveSlotUI : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public TMP_Text nameText;
        public TMP_Text timeText;
        public TMP_Text dateText;
    }

    public Slot[] slots = new Slot[3];

    void Start()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (SaveManager.HasSave(i))
            {
                SaveData data = SaveManager.Load(i);

                slots[i].nameText.text =
                    data.saveName;

                int hours = Mathf.FloorToInt(
                    data.playTimeSeconds / 3600
                );

                int minutes = Mathf.FloorToInt(
                    (data.playTimeSeconds % 3600) / 60
                );

                slots[i].timeText.text =
                    $"{hours:00}h {minutes:00}m";

                slots[i].dateText.text =
                    data.lastPlayedDate;
            }
            else
            {
                slots[i].nameText.text =
                    "SLOT VAZIO";

                slots[i].timeText.text = "";

                slots[i].dateText.text = "";
            }
        }
    }
}