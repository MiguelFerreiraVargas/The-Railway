using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSaveController : MonoBehaviour
{
    public Transform player;
    public PlayTimeTracker playTimeTracker;

    public int currentLevel = 1;

    public void SaveGame(int slot)
    {
        if (player == null)
        {
            Debug.LogError("Player não foi definido!");
            return;
        }

        if (playTimeTracker == null)
        {
            Debug.LogError("PlayTimeTracker não foi definido!");
            return;
        }

        SaveData data = new SaveData();

        data.saveName = "Save " + (slot + 1);
        data.playTimeSeconds = playTimeTracker.currentPlayTime;

        data.posX = player.position.x;
        data.posY = player.position.y;
        data.posZ = player.position.z;

        data.level = currentLevel;

        SaveManager.Save(slot, data);

        Debug.Log("Jogo salvo no slot " + slot);
    }

    public void LoadGame(int slot)
    {
        if (!SaveManager.HasSave(slot))
        {
            Debug.Log("Esse slot está vazio!");
            return;
        }

        SaveData data = SaveManager.Load(slot);

        if (player != null)
        {
            player.position = new Vector3(
                data.posX,
                data.posY,
                data.posZ
            );
        }

        if (playTimeTracker != null)
        {
            playTimeTracker.currentPlayTime = data.playTimeSeconds;
        }

        currentLevel = data.level;

        Debug.Log("Save carregado do slot " + slot);
    }
}