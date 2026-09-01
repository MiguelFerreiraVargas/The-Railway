using System;

[Serializable]
public class SaveData 
{
    public string saveName;
    public float playTimeSeconds;
    public string lastSaveDate;
    public int slotIndex;

    public float posX, posY, posZ;
    public int level;
}