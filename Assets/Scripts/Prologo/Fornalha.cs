using System.Collections;
using UnityEngine;

/// <summary>
/// Fornalha da locomotiva: recebe carvão (do inventário) e depois é acesa com fósforos.
/// </summary>
public class Fornalha : Interagivel
{
    [SerializeField] private int carvaoNecessario = 3;
    [SerializeField] private string carvaoItemId = "carvao";
    [SerializeField] private string fosforoItemId = "fosforos";
    [SerializeField] private Light luzDoFogo;
    [SerializeField] private GameObject fumacaChamine;
    [SerializeField] private GameObject brasas;
    [SerializeField] private AudioSource somFogo;
    [SerializeField] private AudioClip somCarvao;

    public const string FlagCarvao = "prologo.carvao_ok";
    public const string FlagFogo = "prologo.fogo";
    private const string FlagContagem = "prologo.carvao_n";

    public static int CarvaoColocado { get; private set; }
    public int CarvaoNecessario => carvaoNecessario;

    private int Inventario(string id) => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(id) : 0;

    public override string Texto
    {
        get
        {
            if (GameFlags.Has(FlagFogo)) return "FORNALHA ACESA";
            if (CarvaoColocado < carvaoNecessario)
                return Inventario(carvaoItemId) > 0 ? $"COLOCAR CARVÃO ({CarvaoColocado}/{carvaoNecessario})" : $"FORNALHA VAZIA ({CarvaoColocado}/{carvaoNecessario})";
            return Inventario(fosforoItemId) > 0 ? "ACENDER A FORNALHA" : "FORNALHA PRONTA PARA ACENDER";
        }
    }

    public override bool Disponivel => base.Disponivel && !GameFlags.Has(FlagFogo);

    private IEnumerator Start()
    {
        CarvaoColocado = 0;
        AplicarVisual(false);
        yield return null;

        // Restaura do save: quantos carvões já foram colocados e se está acesa
        for (int i = 1; i <= carvaoNecessario; i++)
            if (GameFlags.Has(FlagContagem + i)) CarvaoColocado = i;
        if (GameFlags.Has(FlagCarvao)) CarvaoColocado = carvaoNecessario;
        if (GameFlags.Has(FlagFogo)) AplicarVisual(true);
    }

    public override void Interagir()
    {
        if (CarvaoColocado < carvaoNecessario)
        {
            if (Inventario(carvaoItemId) <= 0)
            {
                Narrador.Falar("Vazia. Preciso de carvão... deve ter algum no galpão.");
                return;
            }

            // coloca tudo o que tiver, até completar
            while (CarvaoColocado < carvaoNecessario && PlayerInventory.Instance.RemoveItem(carvaoItemId, 1))
            {
                CarvaoColocado++;
                GameFlags.Set(FlagContagem + CarvaoColocado);
            }

            if (somCarvao != null) AudioSource.PlayClipAtPoint(somCarvao, transform.position);

            if (CarvaoColocado >= carvaoNecessario)
            {
                GameFlags.Set(FlagCarvao);
                Narrador.Falar("Finalmente.");
            }
            else
            {
                int falta = carvaoNecessario - CarvaoColocado;
                Narrador.Falar($"Preciso só de mais {Extenso(falta)}...");
            }
            return;
        }

        if (Inventario(fosforoItemId) <= 0)
        {
            Narrador.Falar("Carvão eu tenho. Falta fogo... fósforo, isqueiro, qualquer coisa.");
            return;
        }

        PlayerInventory.Instance.RemoveItem(fosforoItemId, 1);
        GameFlags.Set(FlagFogo);
        AplicarVisual(true);
        Narrador.Falar("Pegou! Vamos, velhinha... esquenta.");
    }

    private static string Extenso(int n)
    {
        switch (n)
        {
            case 1: return "um";
            case 2: return "dois";
            case 3: return "três";
            case 4: return "quatro";
            case 5: return "cinco";
            default: return n.ToString();
        }
    }

    private void AplicarVisual(bool aceso)
    {
        if (luzDoFogo != null) luzDoFogo.enabled = aceso;
        if (fumacaChamine != null) fumacaChamine.SetActive(aceso);
        if (brasas != null) brasas.SetActive(aceso);
        if (somFogo != null) { if (aceso) somFogo.Play(); else somFogo.Stop(); }
    }
}
