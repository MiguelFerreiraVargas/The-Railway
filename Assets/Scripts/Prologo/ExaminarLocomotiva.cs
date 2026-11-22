using UnityEngine;

/// <summary>
/// Olhar a locomotiva de perto: o Cândido percebe o que falta para ela andar
/// (e isso vira a lista de objetivos).
/// </summary>
public class ExaminarLocomotiva : Interagivel
{
    public const string Flag = "prologo.examinou";

    public override string Texto => GameFlags.Has(Flag) ? "O QUE AINDA FALTA?" : "EXAMINAR A LOCOMOTIVA";

    public override void Interagir()
    {
        if (!GameFlags.Has(Flag))
        {
            Narrador.Falar("Uma locomotiva a vapor... velha, mas inteira.");
            Narrador.Falar("A caldeira tá seca. A fornalha, vazia. E levaram a alavanca do regulador.");
            Narrador.Falar("Água, carvão, fogo e a alavanca. Se eu achar isso, ela anda.");
            GameFlags.Set(Flag);
            return;
        }

        Narrador.Falar(PrologoController.OQueFalta());
    }
}
