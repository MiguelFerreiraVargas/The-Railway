using System.Collections;
using UnityEngine;

/// <summary>Jornal/documento no chão: abre a tela de leitura.</summary>
public class Jornal : Interagivel
{
    [SerializeField] private string nomeJornal = "Folha da Estação";
    [SerializeField] private string data = "";
    [SerializeField] private string manchete = "";
    [SerializeField] [TextArea(4, 12)] private string corpo = "";
    [SerializeField] private string flag = "";
    [SerializeField] [TextArea] private string falaDepois = "";

    public override string Texto => GameFlags.Has(flag) ? "LER DE NOVO" : acao;

    public override void Interagir()
    {
        bool primeiraVez = !GameFlags.Has(flag);
        Leitura.Abrir(nomeJornal, data, manchete, corpo, () =>
        {
            GameFlags.Set(flag);
            if (primeiraVez && !string.IsNullOrEmpty(falaDepois)) Narrador.Falar(falaDepois);
        });
    }
}
