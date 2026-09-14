using System.Collections.Generic;
using UnityEngine;

public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    [Header("Receitas disponíveis")]
    [SerializeField] private List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public IReadOnlyList<CraftingRecipe> Recipes => recipes;

    // Verifica se o player tem os itens necessários pra uma receita
    public bool CanCraft(CraftingRecipe recipe)
    {
        if (recipe == null || PlayerInventory.Instance == null)
            return false;

        foreach (var ingredient in recipe.ingredients)
        {
            int quantidadeAtual =
                PlayerInventory.Instance.GetQuantity(ingredient.itemId);

            if (quantidadeAtual < ingredient.amount)
                return false;
        }

        return true;
    }

    // Tenta craftar a receita: remove os ingredientes e adiciona o resultado
    public bool TryCraft(CraftingRecipe recipe)
    {
        if (!CanCraft(recipe))
        {
            Debug.Log("Faltam itens para craftar: " + recipe.nomeExibicao);
            return false;
        }

        foreach (var ingredient in recipe.ingredients)
        {
            PlayerInventory.Instance.RemoveItem(
                ingredient.itemId,
                ingredient.amount
            );
        }

        PlayerInventory.Instance.AddItem(
            recipe.resultItemId,
            recipe.resultAmount
        );

        Debug.Log("Craftou: " + recipe.nomeExibicao);
        return true;
    }

    // Atalho pra craftar direto pelo nome do item resultante (ex: "machado")
    public bool TryCraftByResultId(string resultItemId)
    {
        CraftingRecipe recipe = recipes.Find(r => r.resultItemId == resultItemId);

        if (recipe == null)
        {
            Debug.Log("Nenhuma receita encontrada para: " + resultItemId);
            return false;
        }

        return TryCraft(recipe);
    }
}