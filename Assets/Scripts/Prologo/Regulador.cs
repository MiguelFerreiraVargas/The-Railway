using UnityEngine;

/// <summary>
/// Alavanca do regulador na cabine: primeiro precisa encaixar a alavanca (item),
/// depois, com água, carvão e fogo, puxar faz o trem partir.
/// </summary>
public class Regulador : Interagivel
{
    [SerializeField] private string alavancaItemId = "alavanca";
    [SerializeField] private GameObject alavancaVisual; // aparece quando encaixada
    [SerializeField] private PrologoController controller;

    public const string FlagAlavanca = "prologo.alavanca_ok";

    private bool TemAlavanca => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity(alavancaItemId) > 0;

    public override string Texto
    {
        get
        {
            if (!GameFlags.Has(FlagAlavanca)) return TemAlavanca ? "ENCAIXAR A ALAVANCA" : "REGULADOR SEM ALAVANCA";
            return PrologoController.TremPronto ? "PUXAR A ALAVANCA" : "ALAVANCA DO REGULADOR";
        }
    }

    public override bool Disponivel => base.Disponivel && !GameFlags.Has(PrologoController.FlagPartiu);

    private void Update()
    {
        if (alavancaVisual != null) alavancaVisual.SetActive(GameFlags.Has(FlagAlavanca));
    }

    public override void Interagir()
    {
        if (!GameFlags.Has(FlagAlavanca))
        {
            if (!TemAlavanca)
            {
                Narrador.Falar("Arrancaram a alavanca do regulador. Sem ela esse trem não sai do lugar.");
                return;
            }
            PlayerInventory.Instance.RemoveItem(alavancaItemId, 1);
            GameFlags.Set(FlagAlavanca);
            Narrador.Falar("Encaixou certinho.");
            return;
        }

        if (!PrologoController.TremPronto)
        {
            Narrador.Falar(PrologoController.OQueFalta());
            return;
        }

        if (controller != null) controller.Partir();
    }
}
