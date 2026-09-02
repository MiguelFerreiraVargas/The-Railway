using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveGameLoader : MonoBehaviour
{
    public static int selectedSlot = -1;

    [Header("Nome da cena onde o jogador está")]
    public string gameSceneName = "Game";

    public void LoadSlot(int slot)
    {
        // Verifica se existe save
        if (!SaveManager.HasSave(slot))
        {
            Debug.Log("Esse slot está vazio!");
            return;
        }

        // Guarda qual slot foi escolhido
        selectedSlot = slot;

        Debug.Log("Carregando Slot " + (slot + 1));

        // Carrega a cena do jogo
        SceneManager.LoadScene(gameSceneName);
    }
}