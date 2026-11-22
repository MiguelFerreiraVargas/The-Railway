using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Conduz o prólogo na ferrovia abandonada: abre os objetivos conforme a
/// história avança e faz o trem partir para a CenaPrincipal no final.
/// </summary>
public class PrologoController : MonoBehaviour
{
    public const string FlagBebeu = "prologo.bebeu";
    public const string FlagPartiu = "prologo.partiu";
    public const string FlagJornais = "prologo.jornais";
    public const int TotalJornais = 5;

    [SerializeField] private string proximaCena = "CenaPrincipal";
    [SerializeField] private Fornalha fornalha;
    [SerializeField] private AudioSource somPartida;     // trilho / vapor
    [SerializeField] private AudioClip somFreio;
    [SerializeField] private Transform cameraParaTremer;

    public static bool TremPronto =>
        GameFlags.Has(ValvulaCaixaDagua.FlagCaldeira) &&
        GameFlags.Has(Fornalha.FlagFogo) &&
        GameFlags.Has(Regulador.FlagAlavanca);

    public static string OQueFalta()
    {
        var falta = new List<string>();
        if (!GameFlags.Has(ValvulaCaixaDagua.FlagCaldeira)) falta.Add("a caldeira continua seca");
        if (!GameFlags.Has(Fornalha.FlagCarvao)) falta.Add("falta carvão na fornalha");
        else if (!GameFlags.Has(Fornalha.FlagFogo)) falta.Add("a fornalha está apagada");
        if (!GameFlags.Has(Regulador.FlagAlavanca)) falta.Add("o regulador está sem alavanca");

        if (falta.Count == 0) return "Tudo pronto. É só puxar a alavanca.";
        string s = string.Join(", ", falta);
        return char.ToUpper(s[0]) + s.Substring(1) + ".";
    }

    private bool partindo;

    private IEnumerator Start()
    {
        yield return null; // espera o save (se houver) aplicar as marcas
        GameSaveControllerSeguro();
        while (true)
        {
            Sincronizar();
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    // O prólogo começa num "jogo novo" quando não veio de um save.
    private static void GameSaveControllerSeguro()
    {
        if (!GameFlags.Has("prologo.inicio")) GameFlags.Set("prologo.inicio");
    }

    private void Sincronizar()
    {
        Objetivos.Adicionar(FlagBebeu, "Encontrar água para beber");
        if (PlayerStats.Instance != null && PlayerStats.Instance.sede.Fracao > 0.45f) GameFlags.Set(FlagBebeu);

        if (GameFlags.Has("prologo.radio_perto") || GameFlags.Has("prologo.radio"))
            Objetivos.Adicionar("prologo.radio", "Descobrir de onde vem a voz no rádio");

        if (GameFlags.Has("prologo.radio"))
            Objetivos.Adicionar(ExaminarLocomotiva.Flag, "Examinar a locomotiva");

        if (GameFlags.Has(ExaminarLocomotiva.Flag))
        {
            Objetivos.Adicionar(ValvulaCaixaDagua.FlagCaldeira, "Encher a caldeira na caixa-d'água");
            Objetivos.Adicionar(Fornalha.FlagCarvao, "Colocar carvão na fornalha");
            Objetivos.Adicionar(Regulador.FlagAlavanca, "Achar a alavanca do regulador");
            Objetivos.Adicionar(Fornalha.FlagFogo, "Acender a fornalha");

            // Detalhes / pistas
            Objetivos.Detalhe(ValvulaCaixaDagua.FlagCaldeira,
                GameFlags.Has("prologo.valvula_emperrada") && !Tem("chave_inglesa") ? "a válvula emperrou: preciso de uma ferramenta" : "");

            int naMao = Quantidade("carvao");
            int colocado = fornalha != null ? Fornalha.CarvaoColocado : 0;
            int total = fornalha != null ? fornalha.CarvaoNecessario : 3;
            Objetivos.Detalhe(Fornalha.FlagCarvao, $"na fornalha: {colocado}/{total}" + (naMao > 0 ? "   (uma pedra na mão)" : ""));

            Objetivos.Detalhe(Regulador.FlagAlavanca,
                Tem("alavanca") ? "está comigo: encaixar no regulador" :
                GameFlags.Has("prologo.escritorio_trancado") && !Tem("chave_escritorio") ? "o escritório está trancado: procurar a chave" : "");

            Objetivos.Detalhe(Fornalha.FlagFogo, GameFlags.Has(Fornalha.FlagCarvao) && !Tem("fosforos") ? "preciso de fósforos" : "");
        }

        // Jornais (opcional)
        int lidos = 0;
        for (int i = 1; i <= TotalJornais; i++) if (GameFlags.Has("prologo.jornal" + i)) lidos++;
        if (lidos > 0)
        {
            Objetivos.Adicionar(FlagJornais, "Ler os jornais espalhados (opcional)");
            Objetivos.Detalhe(FlagJornais, $"{lidos}/{TotalJornais}");
            if (lidos >= TotalJornais) GameFlags.Set(FlagJornais);
        }

        if (TremPronto)
            Objetivos.Adicionar(FlagPartiu, "Puxar a alavanca do regulador e partir");
    }

    private static bool Tem(string id) => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity(id) > 0;
    private static int Quantidade(string id) => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(id) : 0;

    // ------------------------------------------------------------ Partida

    public void Partir()
    {
        if (partindo || !TremPronto) return;
        partindo = true;
        StartCoroutine(PartirRotina());
    }

    private IEnumerator PartirRotina()
    {
        GameFlags.Set(FlagPartiu);
        Narrador.Calar();
        Narrador.Falar("Segura firme, Cândido...");

        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)) f.enabled = false;
        if (somPartida != null) somPartida.Play();

        // Tremor crescente da partida
        var cam = cameraParaTremer != null ? cameraParaTremer : (Camera.main != null ? Camera.main.transform : null);
        Vector3 basePos = cam != null ? cam.localPosition : Vector3.zero;
        float t = 0f;
        while (t < 4f)
        {
            t += Time.deltaTime;
            float forca = Mathf.Lerp(0.005f, 0.05f, t / 4f);
            if (cam != null) cam.localPosition = basePos + Random.insideUnitSphere * forca;
            yield return null;
        }
        if (cam != null) cam.localPosition = basePos;

        Narrador.Falar("Menos cento e trinta e dois... quatrocentos e trinta e dois... menos vinte e dois. Lá vou eu.");
        yield return new WaitForSeconds(2.5f);

        GameFlags.Set("prologo.concluido");
        LoadingScreen.Load(proximaCena);
    }
}
