using System.Collections;
using UnityEngine;

public class Campfire : MonoBehaviour, IInteractable
{
    [Header("Cozimento")]
    [SerializeField] private float cookingTime = 5f;

    private bool cooking;

    public void Interact()
    {
        if (cooking)
            return;

        if (InventoryManager.Instance == null)
            return;

        if (!InventoryManager.Instance.HasItem("Carne Crua"))
        {
            Debug.Log("Você não tem carne crua.");
            return;
        }

        StartCoroutine(CookMeat());
    }

    private IEnumerator CookMeat()
    {
        cooking = true;

        Debug.Log("Cozinhando carne...");

        InventoryManager.Instance.RemoveItem(
            "Carne Crua",
            1
        );

        yield return new WaitForSeconds(
            cookingTime
        );

        InventoryManager.Instance.AddItem(
            "Carne Cozida",
            1
        );

        Debug.Log("Carne cozida!");

        cooking = false;
    }
}