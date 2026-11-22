using UnityEngine;

/// <summary>
/// Base de tudo que o jogador usa com "E" olhando para o objeto.
/// O objeto precisa de um Collider (pode ser trigger).
/// </summary>
public abstract class Interagivel : MonoBehaviour
{
    [Tooltip("Texto do aviso, ex.: \"PEGAR CARVÃO\".")]
    [SerializeField] protected string acao = "USAR";

    /// <summary>Texto mostrado no aviso (pode mudar conforme o estado).</summary>
    public virtual string Texto => acao;

    /// <summary>Se aparece o aviso e aceita o E agora.</summary>
    public virtual bool Disponivel => isActiveAndEnabled;

    public abstract void Interagir();
}
