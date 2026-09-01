using System.IO;
using UnityEngine;

public static class SaveManager
{
    private const int MAX_SLOTS = 3;

    private static string GetPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");
    }

    public static void Save(int slot, SaveData data)
    {
        if (slot < 0 || slot >= MAX_SLOTS)
        {
            Debug.LogError("Invalid slot!");
            return;
        }

        data.slotIndex = slot;
        data.lastSaveDate = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetPath(slot), json);
    }

    public static SaveData Load(int slot)
    {
        string path = GetPath(slot);

        if (!File.Exists(path))
            return null;

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static bool HasSave(int slot)
    {
        return File.Exists(GetPath(slot));
    }

    public static void DeleteSave(int slot)
    {
        string path = GetPath(slot);
        if (File.Exists(path))
            File.Delete(path);
    }
}