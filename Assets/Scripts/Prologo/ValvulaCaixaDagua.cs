using System.Collections;
using UnityEngine;

/// <summary>
/// Válvula da caixa-d'água: emperrada até o jogador ter a chave inglesa.
/// Aberta, enche a caldeira da locomotiva e libera a torneira.
/// </summary>
public class ValvulaCaixaDagua : Interagivel
{
    [SerializeField] private string ferramentaItemId = "chave_inglesa";
    [SerializeField] private Transform volante;          // gira ao abrir
    [SerializeField] private GameObject jatoDeAgua;      // partículas da água caindo
    [SerializeField] private AudioSource somAgua;
    [SerializeField] private float duracaoDoJato = 6f;

    public const string FlagAberta = "prologo.valvula";
    public const string FlagCaldeira = "prologo.caldeira";

    public override string Texto => GameFlags.Has(FlagAberta) ? "VÁLVULA ABERTA" : TemFerramenta ? "ABRIR A VÁLVULA COM A CHAVE" : "ABRIR A VÁLVULA";
    public override bool Disponivel => base.Disponivel && !GameFlags.Has(FlagAberta);

    private bool TemFerramenta => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity(ferramentaItemId) > 0;

    private IEnumerator Start()
    {
        if (jatoDeAgua != null) jatoDeAgua.SetActive(false);
        yield return null;
        if (GameFlags.Has(FlagAberta) && volante != null) volante.localRotation *= Quaternion.Euler(0f, 0f, 540f);
    }

    public override void Interagir()
    {
        if (!TemFerramenta)
        {
            Narrador.Falar("Emperrada de ferrugem. Na mão não vai... preciso de alguma ferramenta.");
            GameFlags.Set("prologo.valvula_emperrada");
            return;
        }

        GameFlags.Set(FlagAberta);
        GameFlags.Set(FlagCaldeira);
        StartCoroutine(Abrir());
    }

    private IEnumerator Abrir()
    {
        Narrador.Falar("Vai... vai... isso!");
        if (volante != null)
        {
            var ini = volante.localRotation;
            float t = 0f;
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                volante.localRotation = ini * Quaternion.Euler(0f, 0f, Mathf.SmoothStep(0f, 540f, t / 1.6f));
                yield return null;
            }
        }

        if (jatoDeAgua != null) jatoDeAgua.SetActive(true);
        if (somAgua != null) somAgua.Play();
        Narrador.Falar("Água! A caldeira da locomotiva tá enchendo... e a torneira voltou a pingar.");
        yield return new WaitForSeconds(duracaoDoJato);
        if (jatoDeAgua != null)
        {
            var ps = jatoDeAgua.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop();
        }
        if (somAgua != null) somAgua.Stop();
    }
}
