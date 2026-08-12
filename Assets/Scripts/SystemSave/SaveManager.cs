using UnityEngine;
using System.IO;
using System.Collections;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    [SerializeField] private Transform player;
    [SerializeField] private float autoSaveInterval = 30f;

    private string savePath;

    private void Awake()
    {
        Instance = this;
        savePath = Application.persistentDataPath + "/save.json";
    }   

    private void Start()
    {
        LoadGame();
        StartCoroutine(AutoSave());
    }

    private IEnumerator AutoSave()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoSaveInterval);
            SaveGame();
        }
    }

    public void SaveGame()
    {
        SaveData data = new SaveData();

        data.posX = player.position.x;
        data.posY = player.position.y;
        data.posZ = player.position.z;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);

        Debug.Log("Saved!");
    }

    public void LoadGame()
    {
        if (!File.Exists(savePath))
            return;

        string json = File.ReadAllText(savePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        player.position = new Vector3(data.posX, data.posY, data.posZ);

        Debug.Log("Loaded!");
    }
}

[System.Serializable]
public class SaveData
{
    public float posX;
    public float posY;
    public float posZ;
}