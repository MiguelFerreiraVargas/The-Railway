using System.Collections;
using UnityEngine;

/// <summary>Usar o banheiro: recupera higiene (com um tempo de espera).</summary>
public class Banheiro : Interagivel
{
    [SerializeField] private float higiene = 25f;
    [SerializeField] private float espera = 90f;
    [SerializeField] [TextArea] private string fala = "Pelo menos isso ainda funciona... mais ou menos.";

    private float liberadoEm;

    public override string Texto => Time.time < liberadoEm ? "AGORA NÃO" : "USAR O BANHEIRO";

    public override void Interagir()
    {
        if (Time.time < liberadoEm)
        {
            Narrador.Falar("Acabei de sair daí.");
            return;
        }

        liberadoEm = Time.time + espera;
        PlayerStats.Instance?.Lavar(higiene);
        PlayerStats.Instance?.Acalmar(3f);
        GameFlags.Set("prologo.banheiro");
        if (!string.IsNullOrEmpty(fala)) Narrador.Falar(fala);
    }
}
