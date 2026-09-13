using UnityEngine;

public class CraftingSystem : MonoBehaviour
{
    public Inventory inventory;
    public GameObject torchPrefab;      // prefab da tocha já com Light
    public Transform handSlot;          // onde a tocha vai aparecer (ex: mão do player)

    public int gravetosNecessarios = 3;
    private GameObject tochaAtual;

    public void TentarCraftarTocha()
    {
        if (inventory.HasItem("Graveto", gravetosNecessarios))
        {
            inventory.RemoveItem("Graveto", gravetosNecessarios);
            CriarTocha();
            Debug.Log("Tocha criada!");
        }
        else
        {
            Debug.Log("Gravetos insuficientes para criar a tocha.");
        }
    }

    private void CriarTocha()
    {
        if (tochaAtual != null) Destroy(tochaAtual);
        tochaAtual = Instantiate(torchPrefab, handSlot.position, handSlot.rotation, handSlot);
    }
}