using UnityEngine;

public class GameSaveController : MonoBehaviour
{
    public Transform player;
    public PlayTimeTracker playTimeTracker;

    public int currentLevel = 1;

    void Start()
    {
        // Verifica se viemos do menu carregando um save
        if (SaveGameLoader.selectedSlot >= 0)
        {
            LoadGame(SaveGameLoader.selectedSlot);

            // Impede que o mesmo save seja carregado novamente
            SaveGameLoader.selectedSlot = -1;
        }
    }

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

        data.saveName = "Aventura";

        data.playTimeSeconds =
            playTimeTracker.currentPlayTime;

        data.posX = player.position.x;
        data.posY = player.position.y;
        data.posZ = player.position.z;

        data.level = currentLevel;

        SaveManager.Save(slot, data);

        Debug.Log("Jogo salvo no Slot " + (slot + 1));
    }

    public void LoadGame(int slot)
    {
        if (!SaveManager.HasSave(slot))
        {
            Debug.LogWarning("Save não encontrado!");
            return;
        }

        SaveData data = SaveManager.Load(slot);

        // Coloca o Player na posição salva
        if (player != null)
        {
            player.position = new Vector3(
                data.posX,
                data.posY,
                data.posZ
            );
        }

        // Recupera o tempo jogado
        if (playTimeTracker != null)
        {
            playTimeTracker.currentPlayTime =
                data.playTimeSeconds;
        }

        // Recupera o level
        currentLevel = data.level;

        // Atualiza a data para o momento em que o jogador entrou
        SaveManager.Save(slot, data);

        Debug.Log(
            "Save carregado: Slot " + (slot + 1)
        );
    }
}