using System.Collections;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Fome ou sede zerada derrubam o Cândido: ele desmaia (tela preta, perde vida, acorda fraco com
/// um restinho de energia). Se zerar de novo antes de se recuperar, não acorda mais.
/// </summary>
public class Desmaio : MonoBehaviour
{
    public static bool Ativo { get; private set; }

    [Tooltip("Desmaiar de novo dentro deste tempo (s) mata.")]
    public float janelaFatal = 240f;
    public float danoDoDesmaio = 18f;
    [Tooltip("Quanto de fome/sede ele acorda (o resto do corpo dando o último fôlego).")]
    public float folegoAoAcordar = 8f;

    private float ultimoFome = -9999f, ultimoSede = -9999f;

    private void OnDisable() => Ativo = false;

    private void Update()
    {
        var s = PlayerStats.Instance;
        if (s == null || s.Morto || Ativo) return;
        if (s.fome.atual <= 0f) Desmaiar(true);
        else if (s.sede.atual <= 0f) Desmaiar(false);
    }

    private void Desmaiar(bool fome)
    {
        var s = PlayerStats.Instance;
        float ultimo = fome ? ultimoFome : ultimoSede;
        if (Time.time - ultimo < janelaFatal)
        {
            // segunda vez: o corpo não aguenta
            s.Dano(9999f, fome ? "DESMAIOU DE FOME E NÃO ACORDOU" : "DESIDRATADO");
            return;
        }
        if (fome) ultimoFome = Time.time; else ultimoSede = Time.time;
        StartCoroutine(Apagar(fome));
    }

    private IEnumerator Apagar(bool fome)
    {
        Ativo = true;
        var s = PlayerStats.Instance;
        var fpc = GetComponent<FirstPersonController>();
        var inputs = GetComponent<StarterAssetsInputs>();
        Narrador.Falar(fome ? "Tudo... girando... preciso de... comida..." : "A boca... seca... não...");
        yield return new WaitForSeconds(1.2f);
        EfeitosDeEstado.Escurecer(1f, 1.2f);
        if (fpc != null) fpc.enabled = false;
        if (inputs != null) inputs.move = Vector2.zero;
        yield return new WaitForSeconds(4.5f);

        if (s != null && !s.Morto)
        {
            s.Dano(danoDoDesmaio, fome ? "DE FOME" : "DE SEDE");
            if (fome) s.Comer(folegoAoAcordar); else s.Beber(folegoAoAcordar);
        }
        if (fpc != null) fpc.enabled = true;
        EfeitosDeEstado.Escurecer(0f, 0.5f);
        yield return new WaitForSeconds(1.5f);
        Ativo = false;
        if (s != null && !s.Morto)
            Narrador.Falar(fome ? "Eu apaguei... se acontecer de novo, eu não acordo. Preciso comer. Agora." : "Apaguei... água. Preciso de água antes que aconteça de novo.");
    }
}
