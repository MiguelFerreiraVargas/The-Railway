using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Junta no SaveData (e devolve ao carregar) o que vai além da posição:
/// cena, necessidades, stamina, progresso (GameFlags) e inventário.
/// </summary>
public static class SaveSnapshot
{
    public const int Versao = 2;

    public static void Capturar(SaveData d)
    {
        if (d == null) return;
        d.versao = Versao;
        d.cena = SceneManager.GetActiveScene().name;

        var s = PlayerStats.Instance;
        if (s != null)
        {
            d.vida = s.vida.atual;
            d.fome = s.fome.atual;
            d.sede = s.sede.atual;
            d.higiene = s.higiene.atual;
            d.sanidade = s.sanidade.atual;
        }

        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) d.stamina = fpc.staminaAtual;

        d.flags = GameFlags.ToList();

        d.inventario = new List<InventorySlot>();
        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            foreach (var slot in inv.Slots)
                if (slot != null && !string.IsNullOrEmpty(slot.itemId) && slot.quantity > 0)
                    d.inventario.Add(new InventorySlot { itemId = slot.itemId, quantity = slot.quantity });
        }
    }

    public static void Aplicar(SaveData d)
    {
        if (d == null) return;

        // Saves antigos (versão 1) não têm esses dados: mantém os valores da cena.
        if (d.versao >= Versao)
        {
            var s = PlayerStats.Instance;
            if (s != null)
            {
                if (d.vida >= 0f) s.vida.atual = Mathf.Max(1f, d.vida);
                if (d.fome >= 0f) s.fome.atual = d.fome;
                if (d.sede >= 0f) s.sede.atual = d.sede;
                if (d.higiene >= 0f) s.higiene.atual = d.higiene;
                if (d.sanidade >= 0f) s.sanidade.atual = Mathf.Max(1f, d.sanidade);
                s.Reviver();
            }

            var fpc = Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc != null && d.stamina >= 0f) fpc.staminaAtual = d.stamina;

            GameFlags.Load(d.flags);

            var inv = PlayerInventory.Instance;
            if (inv != null && d.inventario != null)
            {
                inv.LimparInventario();
                foreach (var slot in d.inventario)
                    inv.AddItem(slot.itemId, slot.quantity);
            }
        }
    }

    /// <summary>Lê só a cena de um save (para o menu saber qual cena abrir).</summary>
    public static string CenaDoSave(int slot, string padrao)
    {
        var d = SaveManager.Load(slot);
        return d != null && !string.IsNullOrEmpty(d.cena) ? d.cena : padrao;
    }
}
