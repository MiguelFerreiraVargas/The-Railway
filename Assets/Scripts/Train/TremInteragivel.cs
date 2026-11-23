using UnityEngine;

/// <summary>
/// Liga o trem ao sistema de interação (E): "SUBIR NO TREM" / "DESCER DO TREM".
/// Funciona na isométrica (o mais próximo do Cândido) e em primeira pessoa (mira),
/// no lugar do antigo TrainClick, que só olhava pelo centro da tela.
/// </summary>
public class TremInteragivel : Interagivel
{
    [SerializeField] private TrainInteraction trem;

    /// <summary>O trem em que o jogador está sentado (null se não estiver em nenhum).</summary>
    public static TremInteragivel ComJogador
    {
        get
        {
            foreach (var t in FindObjectsByType<TremInteragivel>(FindObjectsSortMode.None))
                if (t.trem != null && t.trem.IsInsideTrain) return t;
            return null;
        }
    }

    public override string Texto => trem != null && trem.IsInsideTrain ? "DESCER DO TREM" : "SUBIR NO TREM";

    public override bool Disponivel => base.Disponivel && trem != null;

    // o trem é grande: qualquer coisa menor perto dele tem preferência; sentado, descer ganha de tudo
    public override float Prioridade => trem != null && trem.IsInsideTrain ? 10f : -1.5f;

    private void Awake()
    {
        if (trem == null) trem = GetComponent<TrainInteraction>();
    }

    public override void Interagir()
    {
        if (trem != null) trem.ToggleTrain();
    }
}
