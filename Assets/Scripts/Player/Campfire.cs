using System.Collections;
using UnityEngine;

// Coloca esse script na fogueira (com um Collider na interactableLayer).
// Cada interação pega 1 carne crua do inventário e, depois de "cookTime"
// segundos, devolve 1 carne cozida.
public class Campfire : MonoBehaviour, IInteractable
{
    [Header("Receita")]
    [SerializeField] private string rawItemId = "carne_crua";
    [SerializeField] private string cookedItemId = "carne_cozida";
    [SerializeField] private float cookTime = 5f;

    [Header("Feedback (opcional)")]
    [SerializeField] private GameObject cookingEffect; // ex: mais fumaça enquanto cozinha

    [Header("Outline")]
    [SerializeField] private Outline outline;

    private bool isCooking;

    private void Awake()
    {
        if (outline == null)
            outline = GetComponent<Outline>();

        HideOutline();
    }

    // Devolve false (sem tocar animação) se já tá cozinhando ou não tem carne crua
    public bool Interact()
    {
        if (isCooking)
            return false;

        if (PlayerInventory.Instance == null)
            return false;

        if (PlayerInventory.Instance.GetQuantity(rawItemId) <= 0)
        {
            Debug.Log("Sem carne crua pra cozinhar.");
            return false;
        }

        StartCoroutine(CookOne());
        return true;
    }

    private IEnumerator CookOne()
    {
        isCooking = true;

        PlayerInventory.Instance.RemoveItem(rawItemId, 1);

        if (cookingEffect != null)
            cookingEffect.SetActive(true);

        yield return new WaitForSeconds(cookTime);

        PlayerInventory.Instance.AddItem(cookedItemId, 1);

        if (cookingEffect != null)
            cookingEffect.SetActive(false);

        isCooking = false;
    }

    public void ShowOutline()
    {
        if (outline != null)
            outline.enabled = true;
    }

    public void HideOutline()
    {
        if (outline != null)
            outline.enabled = false;
    }
}