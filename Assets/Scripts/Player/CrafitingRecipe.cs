using System;
using System.Collections.Generic;
using UnityEngine;

// Um ingrediente da receita (ex: 3 gravetos)
[Serializable]
public class CraftingIngredient
{
    public string itemId;
    public int amount = 1;
}

// Uma receita de crafting (ex: 3 gravetos + 2 pedras -> 1 machado)
[Serializable]
public class CraftingRecipe
{
    [Header("Resultado")]
    public string resultItemId;
    public int resultAmount = 1;

    [Header("Ingredientes necessários")]
    public List<CraftingIngredient> ingredients = new List<CraftingIngredient>();

    // Nome só pra facilitar organização no Inspector
    public string nomeExibicao = "Nova Receita";
}