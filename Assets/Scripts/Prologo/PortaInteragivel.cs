using System.Collections;
using UnityEngine;

/// <summary>Porta que abre girando. Pode estar trancada e precisar de um item (chave).</summary>
public class PortaInteragivel : Interagivel
{
    [SerializeField] private Transform folha;
    [SerializeField] private float angulo = -100f;
    [SerializeField] private float duracao = 0.9f;
    [SerializeField] private string chaveItemId = "";
    [SerializeField] private string flagAberta = "";
    [SerializeField] [TextArea] private string falaTrancada = "Trancada.";
    [SerializeField] private string flagTentouTrancada = ""; // pista: tentou abrir sem a chave
    [SerializeField] private AudioClip somAbrir;

    private bool aberta, animando;

    public override string Texto => !string.IsNullOrEmpty(chaveItemId) && !TemChave ? "PORTA TRANCADA" : "ABRIR A PORTA";
    public override bool Disponivel => base.Disponivel && !aberta && !animando;

    private bool TemChave => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity(chaveItemId) > 0;

    private IEnumerator Start()
    {
        yield return null;
        if (!string.IsNullOrEmpty(flagAberta) && GameFlags.Has(flagAberta)) AbrirNaHora();
    }

    public override void Interagir()
    {
        if (!string.IsNullOrEmpty(chaveItemId) && !TemChave)
        {
            Narrador.Falar(falaTrancada);
            if (!string.IsNullOrEmpty(flagTentouTrancada)) GameFlags.Set(flagTentouTrancada);
            return;
        }

        if (!string.IsNullOrEmpty(flagAberta)) GameFlags.Set(flagAberta);
        if (somAbrir != null) AudioSource.PlayClipAtPoint(somAbrir, transform.position);
        StartCoroutine(Abrir());
    }

    private IEnumerator Abrir()
    {
        animando = true;
        var alvo = folha != null ? folha : transform;
        var ini = alvo.localRotation;
        var fim = ini * Quaternion.Euler(0f, angulo, 0f);
        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            alvo.localRotation = Quaternion.Slerp(ini, fim, Mathf.SmoothStep(0f, 1f, t / duracao));
            yield return null;
        }
        alvo.localRotation = fim;
        aberta = true;
        animando = false;
    }

    private void AbrirNaHora()
    {
        var alvo = folha != null ? folha : transform;
        alvo.localRotation *= Quaternion.Euler(0f, angulo, 0f);
        aberta = true;
    }
}
