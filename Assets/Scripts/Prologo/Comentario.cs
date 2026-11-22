using System.Collections;
using UnityEngine;

/// <summary>Só fala algo ao olhar/usar (pistas, comentários do Cândido).</summary>
public class Comentario : Interagivel
{
    [SerializeField] [TextArea] private string fala = "";
    [SerializeField] private string flag = "";

    public override void Interagir()
    {
        Narrador.Falar(fala);
        if (!string.IsNullOrEmpty(flag)) GameFlags.Set(flag);
    }
}
