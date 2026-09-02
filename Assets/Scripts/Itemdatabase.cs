using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemData
{
    public string itemId;
    public string displayName;
    public Sprite icon;
}

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [SerializeField] private ItemData[] items;

    private Dictionary<string, ItemData> lookup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        lookup = new Dictionary<string, ItemData>();

        foreach (var item in items)
        {
            if (item != null && !string.IsNullOrEmpty(item.itemId))
                lookup[item.itemId] = item;
        }
    }

    public ItemData GetItem(string itemId)
    {
        if (lookup != null && lookup.TryGetValue(itemId, out ItemData item))
            return item;

        return null;
    }
}