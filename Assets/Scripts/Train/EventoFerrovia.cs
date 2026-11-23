using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Obstáculo nos trilhos (GDD: eventos ferroviários). O trem freia sozinho antes dele e só segue
/// depois que o Cândido desce e resolve: árvore caída (machado corta rápido; sem machado, arrastar
/// gasta fôlego), deslizamento de pedras (tirar uma a uma) ou animais parados na linha (espantar).
/// </summary>
public class EventoFerrovia : Interagivel, IAtingivel
{
    public enum Tipo { ArvoreCaida, Pedras, Animais }

    public Tipo tipo = Tipo.ArvoreCaida;
    [Tooltip("Posição no trilho (0 = começo, 1 = fim).")]
    [Range(0f, 1f)] public float t;
    [Tooltip("Marca do save: já resolvido.")]
    public string flag = "";
    public int vezesSemFerramenta = 8;
    public int golpesDeMachado = 3;
    public float folegoPorVez = 14f;
    [Tooltip("Partes que somem conforme o Cândido tira (pedras); a árvore inteira rola para fora.")]
    public Transform[] partes;
    public int animais = 3;

    public static readonly List<EventoFerrovia> Todos = new List<EventoFerrovia>();

    public bool Resolvido => resolvido || (!string.IsNullOrEmpty(flag) && GameFlags.Has(flag));
    public bool Vivo => !Resolvido && tipo == Tipo.ArvoreCaida;

    private bool resolvido, avisou, mexendo;
    private int feitos;
    private readonly List<Animal> bichos = new List<Animal>();

    private bool TemMachado => PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity("machado") > 0;

    public override string Texto
    {
        get
        {
            switch (tipo)
            {
                case Tipo.ArvoreCaida:
                    return TemMachado ? $"CORTAR A ÁRVORE ({feitos}/{golpesDeMachado})" : $"ARRASTAR A ÁRVORE ({feitos}/{vezesSemFerramenta})";
                case Tipo.Pedras:
                    return $"TIRAR AS PEDRAS ({feitos}/{Mathf.Max(1, partes != null ? partes.Length : vezesSemFerramenta)})";
                default:
                    return "ESPANTAR OS BICHOS";
            }
        }
    }

    public override bool Disponivel => base.Disponivel && !Resolvido && !mexendo;
    public override float Prioridade => 0.5f;

    private void OnEnable() => Todos.Add(this);
    private void OnDisable() => Todos.Remove(this);

    private void Start()
    {
        if (Resolvido) { Limpar(true); return; }
        if (tipo == Tipo.Animais) StartCoroutine(PorBichos());
    }

    private TrainInteraction trem;

    private void Update()
    {
        if (avisou || Resolvido) return;
        if (trem == null) trem = FindFirstObjectByType<TrainInteraction>();
        // o trem parou perto (pelo freio automático): avisa o que tem na frente
        if (trem != null && trem.VelocidadeAtual < 0.01f && trem.PosicaoNoTrilho < t && (trem.transform.position - transform.position).sqrMagnitude < 45f * 45f && trem.IsInsideTrain)
            TremParou();
    }

    /// <summary>A posição no trilho do primeiro obstáculo à frente (ou 2 se não tem nenhum).</summary>
    public static float ProximoBloqueio(float tAtual, out EventoFerrovia evento)
    {
        evento = null;
        float melhor = 2f;
        foreach (var e in Todos)
            if (e != null && !e.Resolvido && e.t > tAtual - 0.0005f && e.t < melhor) { melhor = e.t; evento = e; }
        return melhor;
    }

    /// <summary>O trem parou diante do obstáculo.</summary>
    public void TremParou()
    {
        if (avisou) return;
        avisou = true;
        switch (tipo)
        {
            case Tipo.ArvoreCaida: Narrador.Falar(TemMachado ? "Uma árvore caída nos trilhos. O machado resolve." : "Uma árvore caída nos trilhos... sem machado vai ser no braço."); break;
            case Tipo.Pedras: Narrador.Falar("Deslizamento. Pedra pra todo lado nos trilhos."); break;
            default: Narrador.Falar("Bichos parados na linha. Preciso espantar eles."); break;
        }
        Objetivos.Adicionar(flag, tipo == Tipo.ArvoreCaida ? "Tirar a árvore dos trilhos" : tipo == Tipo.Pedras ? "Limpar as pedras dos trilhos" : "Espantar os animais da linha");
    }

    // golpe de machado (ou de qualquer arma corpo a corpo) na árvore
    public void Atingir(float dano, Vector3 origem, string arma)
    {
        if (Resolvido || tipo != Tipo.ArvoreCaida) return;
        if (arma == "machado") Avancar(golpesDeMachado);
        else if (Time.frameCount % 2 == 0) Narrador.Falar("Assim não vai. Precisa de um machado... ou arrastar.");
    }

    public override void Interagir()
    {
        if (Resolvido) return;
        if (tipo == Tipo.Animais) { Animal.Barulho(transform.position, 30f); return; }
        if (tipo == Tipo.ArvoreCaida && TemMachado) { Avancar(golpesDeMachado); return; }

        var j = PlayerStats.Instance != null ? PlayerStats.Instance.Jogador : null;
        if (j != null && j.staminaAtual < folegoPorVez) { Narrador.Falar("Sem fôlego... um minuto."); return; }
        if (j != null) j.staminaAtual -= folegoPorVez;
        int total = tipo == Tipo.Pedras && partes != null && partes.Length > 0 ? partes.Length : vezesSemFerramenta;
        if (tipo == Tipo.Pedras && partes != null && feitos < partes.Length && partes[feitos] != null) StartCoroutine(Rolar(partes[feitos], 0.8f, true));
        Avancar(total);
    }

    private void Avancar(int total)
    {
        feitos++;
        ArmasDoJogador.MarcarCombate(0f); // esforço não conta para a sanidade, só segura o trem parado
        if (feitos >= total) { Resolver(); return; }
        StartCoroutine(Sacudir());
        if (feitos == total - 1) Narrador.Falar("Mais uma...");
    }

    private void Resolver()
    {
        resolvido = true;
        if (!string.IsNullOrEmpty(flag)) GameFlags.Set(flag);
        Narrador.Falar(tipo == Tipo.ArvoreCaida ? "Pronto. Caminho livre." : tipo == Tipo.Pedras ? "Limpo. Dá pra passar." : "Foram embora. Linha livre.");
        Limpar(false);
    }

    private void Limpar(bool jaEstava)
    {
        if (tipo == Tipo.Animais) { foreach (var b in bichos) if (b != null) Destroy(b.gameObject); gameObject.SetActive(!jaEstava); return; }
        if (jaEstava) { gameObject.SetActive(false); return; }
        StartCoroutine(Rolar(transform, 1.6f, false));
    }

    // a peça rola para fora dos trilhos e some
    private IEnumerator Rolar(Transform peca, float tempo, bool soPeca)
    {
        mexendo = !soPeca;
        Vector3 de = peca.position;
        Vector3 lado = transform.right * (Random.value < 0.5f ? 1f : -1f);
        Vector3 para = de + lado * 3.5f + Vector3.down * 0.6f;
        var r0 = peca.rotation;
        for (float k = 0f; k < 1f; k += Time.deltaTime / tempo)
        {
            peca.position = Vector3.Lerp(de, para, k);
            peca.rotation = r0 * Quaternion.Euler(0f, 0f, 70f * k * Mathf.Sign(Vector3.Dot(lado, transform.right)));
            yield return null;
        }
        peca.gameObject.SetActive(false);
        mexendo = false;
        if (!soPeca) gameObject.SetActive(false);
    }

    private IEnumerator Sacudir()
    {
        mexendo = true;
        var p0 = transform.position;
        for (float k = 0f; k < 0.3f; k += Time.deltaTime)
        {
            transform.position = p0 + Random.insideUnitSphere * 0.05f;
            yield return null;
        }
        transform.position = p0;
        mexendo = false;
    }

    // bichos parados na linha: resolvido quando não tem mais nenhum perto
    private IEnumerator PorBichos()
    {
        var sp = FindFirstObjectByType<SpawnerDeAnimais>();
        while (!Resolvido)
        {
            yield return new WaitForSeconds(0.5f);
            var jog = GameObject.FindWithTag("Player");
            if (jog == null || sp == null) continue;
            float distJog = Vector3.Distance(jog.transform.position, transform.position);
            bichos.RemoveAll(b => b == null);

            // os bichos só aparecem quando o Cândido (ou o trem) se aproxima; longe, eles somem sozinhos
            if (bichos.Count == 0)
            {
                if (distJog > 120f) continue;
                for (int i = 0; i < animais; i++)
                {
                    var pos = transform.position + transform.right * Random.Range(-2.5f, 2.5f) + transform.forward * Random.Range(-1.5f, 1.5f);
                    var ter = Terrain.activeTerrain;
                    if (ter != null) pos.y = ter.SampleHeight(pos) + ter.transform.position.y;
                    var go = sp.Criar("cervo", pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    var a = go != null ? go.GetComponent<Animal>() : null;
                    if (a != null) { a.distVer = 7f; bichos.Add(a); }
                }
                continue;
            }

            bool algum = false;
            foreach (var b in bichos)
                if (b.Vivo && (b.transform.position - transform.position).sqrMagnitude < 9f * 9f) { algum = true; break; }
            if (!algum) Resolver();
        }
    }
}
