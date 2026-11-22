using System;

[Serializable]
public class SaveData
{
    public string saveName;

    public float playTimeSeconds;

    public string lastPlayedDate;

    public int slotIndex;

    public float posX;
    public float posY;
    public float posZ;

    public int level;

    // ---- versão 2: cena, necessidades, progresso e inventário (ver SaveSnapshot)
    public int versao = 1;
    public string cena = "";
    public float vida = -1f, fome = -1f, sede = -1f, higiene = -1f, sanidade = -1f, stamina = -1f;
    public System.Collections.Generic.List<string> flags = new System.Collections.Generic.List<string>();
    public System.Collections.Generic.List<InventorySlot> inventario = new System.Collections.Generic.List<InventorySlot>();
}