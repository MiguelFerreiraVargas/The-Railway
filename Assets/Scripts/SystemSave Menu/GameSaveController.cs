using UnityEngine;

public class GameSaveController : MonoBehaviour
{
    public Transform player;
    public PlayTimeTracker playTimeTracker;

    public int currentLevel = 1;

    [Header("Salvamento automatico ao fechar o jogo")]
    [Tooltip("Salva sozinho se o jogo for fechado (Alt+F4, X da janela, Sair).")]
    public bool autoSaveOnQuit = true;

    [Tooltip("No editor, parar o Play tambem conta como fechar. Deixe desligado para nao criar saves ao testar.")]
    public bool autoSaveInEditor = false;

    // Slot da viagem atual (carregado ou ultimo salvo). -1 = viagem nova, ainda sem slot.
    public static int CurrentSlot = -1;

    private const int SlotCount = 3;

    void Start()
    {
        // Verifica se viemos do menu carregando um save
        if (SaveGameLoader.selectedSlot >= 0)
        {
            LoadGame(SaveGameLoader.selectedSlot);

            // Impede que o mesmo save seja carregado novamente
            SaveGameLoader.selectedSlot = -1;
        }
        else
        {
            CurrentSlot = -1;
        }
    }

    void OnApplicationQuit()
    {
        if (!autoSaveOnQuit)
            return;

        if (Application.isEditor && !autoSaveInEditor)
            return;

        int slot = ChooseAutoSaveSlot();
        SaveGame(slot);
        Debug.Log("Salvamento automatico ao fechar: Slot " + (slot + 1));
    }

    // Slot atual; se a viagem nunca foi salva, o primeiro vazio;
    // se todos estiverem ocupados, o jogado ha mais tempo.
    public static int ChooseAutoSaveSlot()
    {
        if (CurrentSlot >= 0 && CurrentSlot < SlotCount)
            return CurrentSlot;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!SaveManager.HasSave(i))
                return i;
        }

        int oldest = 0;
        System.DateTime oldestDate = System.DateTime.MaxValue;

        for (int i = 0; i < SlotCount; i++)
        {
            SaveData data = SaveManager.Load(i);
            System.DateTime date;

            if (data != null && System.DateTime.TryParseExact(data.lastPlayedDate, "dd/MM/yyyy HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date)
                && date < oldestDate)
            {
                oldestDate = date;
                oldest = i;
            }
        }

        return oldest;
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
        CurrentSlot = slot;

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
        CurrentSlot = slot;

        // Atualiza a data para o momento em que o jogador entrou
        SaveManager.Save(slot, data);

        Debug.Log(
            "Save carregado: Slot " + (slot + 1)
        );
    }
}