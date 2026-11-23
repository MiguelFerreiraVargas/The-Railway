using System.Collections;
using UnityEngine;

/// <summary>
/// Porta de um cômodo trancado. Não abre sozinha; E força a fechadura (algumas vezes) até ceder.
/// Enquanto está trancada, o cômodo fica coberto (não dá para ver o que tem dentro na isométrica).
/// Destrancada uma vez, o save lembra (GameFlags).
/// </summary>
public class PortaTrancada : Interagivel
{
    public PortaAutomatica porta;
    [Tooltip("Cobertura escura em cima do cômodo (some ao destrancar).")]
    public GameObject cobertura;
    [Tooltip("Marca do save: a porta já foi aberta.")]
    public string flag = "";
    public int forcarVezes = 3;
    public AudioClip somTranco;

    private int tentativas;
    private bool sacudindo;

    public bool Trancada => porta != null && porta.trancada;

    public override string Texto => tentativas == 0 ? "TRANCADA — FORÇAR A PORTA" : $"FORÇAR A PORTA ({tentativas}/{forcarVezes})";
    public override bool Disponivel => base.Disponivel && Trancada && !sacudindo;
    public override float Prioridade => 1f;

    private void Reset() => acao = "FORÇAR A PORTA";

    private void Start()
    {
        if (porta == null) porta = GetComponent<PortaAutomatica>();
        bool aberta = !string.IsNullOrEmpty(flag) && GameFlags.Has(flag);
        if (porta != null) porta.trancada = !aberta;
        if (cobertura != null) cobertura.SetActive(!aberta);
    }

    public override void Interagir()
    {
        if (!Trancada) return;
        tentativas++;
        if (somTranco != null) AudioSource.PlayClipAtPoint(somTranco, transform.position, 0.8f);
        if (tentativas < forcarVezes)
        {
            Narrador.Falar(tentativas == 1 ? "Trancada... dá pra forçar." : "Quase... mais uma.");
            StartCoroutine(Sacudir());
            return;
        }
        Narrador.Falar("Cedeu.");
        if (!string.IsNullOrEmpty(flag)) GameFlags.Set(flag);
        if (cobertura != null) cobertura.SetActive(false);
        porta.Destrancar();
    }

    private IEnumerator Sacudir()
    {
        sacudindo = true;
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            porta.Sacudir(Mathf.Sin(t * 60f) * 2.5f * (1f - t / 0.35f));
            yield return null;
        }
        porta.Sacudir(0f);
        sacudindo = false;
    }
}
