using System.Collections;
using UnityEngine;

/// <summary>Beber água ou lavar o rosto numa torneira/balde (só depois que a água estiver ligada).</summary>
public class Torneira : Interagivel
{
    public enum Uso { Beber, Lavar }

    [SerializeField] private Uso uso = Uso.Beber;
    [SerializeField] private string flagNecessaria = "prologo.valvula";
    [SerializeField] private string flagAoUsar = "";
    [SerializeField] private float quantidade = 40f;
    [SerializeField] [TextArea] private string falaSemAgua = "Seco.";
    [SerializeField] [TextArea] private string falaPrimeiraVez = "";
    [SerializeField] private AudioClip som;

    public override string Texto
    {
        get
        {
            if (!string.IsNullOrEmpty(flagNecessaria) && !GameFlags.Has(flagNecessaria)) return uso == Uso.Beber ? "TORNEIRA SECA" : "BALDE VAZIO";
            return uso == Uso.Beber ? "BEBER ÁGUA" : "LAVAR O ROSTO";
        }
    }

    public override void Interagir()
    {
        if (!string.IsNullOrEmpty(flagNecessaria) && !GameFlags.Has(flagNecessaria))
        {
            Narrador.Falar(falaSemAgua);
            return;
        }

        var s = PlayerStats.Instance;
        if (s != null)
        {
            if (uso == Uso.Beber) { s.Beber(quantidade); s.SomarStamina(20f); }
            else { s.Lavar(quantidade); s.Acalmar(4f); }
        }

        if (som != null) AudioSource.PlayClipAtPoint(som, transform.position);

        bool primeira = !string.IsNullOrEmpty(flagAoUsar) && !GameFlags.Has(flagAoUsar);
        if (!string.IsNullOrEmpty(flagAoUsar)) GameFlags.Set(flagAoUsar);
        if (primeira && !string.IsNullOrEmpty(falaPrimeiraVez)) Narrador.Falar(falaPrimeiraVez);
    }
}
