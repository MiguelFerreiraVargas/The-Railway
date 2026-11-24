using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Móvel que dá para vasculhar uma vez (prateleira, estoque, geladeira, armário): E sorteia
/// alguns itens de uma tabela e põe no inventário. O save lembra que já foi vasculhado.
/// </summary>
public class Recipiente : Interagivel
{
    public enum Tipo { PrateleiraLoja, Estoque, Geladeira, Armario }

    public Tipo tipo = Tipo.Armario;
    [Tooltip("Marca do save deste móvel (já vasculhado).")]
    public string flag = "";
    [Tooltip("Semente do sorteio (cada móvel dá sempre a mesma coisa).")]
    public int semente;
    public int minimo = 0, maximo = 2;
    public AudioClip som;

    private bool vazio;

    private struct Item { public string id; public int qtd, peso; }

    private static readonly Dictionary<Tipo, Item[]> Tabelas = new Dictionary<Tipo, Item[]>
    {
        { Tipo.PrateleiraLoja, new[] { I("biscoito", 1, 40), I("soda", 1, 25), I("lata_feijao", 1, 20), I("fosforos", 1, 8), I("agua", 1, 7) } },
        { Tipo.Estoque,        new[] { I("lata_feijao", 2, 30), I("agua", 2, 25), I("biscoito", 2, 20), I("fosforos", 1, 12), I("municao", 6, 8), I("soda", 2, 5), I("rifle", 1, 2), I("faca", 1, 3), I("pilha", 2, 10) } },
        { Tipo.Geladeira,      new[] { I("agua", 1, 45), I("soda", 1, 40), I("lata_feijao", 1, 15) } },
        { Tipo.Armario,        new[] { I("lata_feijao", 1, 35), I("fosforos", 1, 20), I("biscoito", 1, 20), I("agua", 1, 15), I("municao", 4, 10), I("faca", 1, 3), I("machado", 1, 2), I("pilha", 1, 8), I("lanterna", 1, 3) } },
    };

    private static readonly Dictionary<string, (string um, string varios)> Nomes = new Dictionary<string, (string, string)>
    {
        { "lata_feijao", ("uma lata de comida", "latas de comida") },
        { "agua", ("uma garrafa d'água", "garrafas d'água") },
        { "soda", ("uma soda", "sodas") },
        { "biscoito", ("um pacote de biscoito", "pacotes de biscoito") },
        { "fosforos", ("uma caixa de fósforos", "caixas de fósforos") },
        { "municao", ("munição", "balas") },
        { "faca", ("uma faca", "facas") },
        { "machado", ("um machado", "machados") },
        { "rifle", ("um rifle de caça", "rifles") },
        { "lanterna", ("uma lanterna", "lanternas") },
        { "pilha", ("uma pilha", "pilhas") },
    };

    private static Item I(string id, int qtd, int peso) => new Item { id = id, qtd = qtd, peso = peso };

    public override string Texto
    {
        get
        {
            switch (tipo)
            {
                case Tipo.Geladeira: return "ABRIR E VASCULHAR";
                case Tipo.Estoque: return "VASCULHAR O ESTOQUE";
                case Tipo.PrateleiraLoja: return "VASCULHAR A PRATELEIRA";
                default: return "VASCULHAR";
            }
        }
    }

    public override bool Disponivel => base.Disponivel && !vazio;

    private void Start()
    {
        vazio = !string.IsNullOrEmpty(flag) && GameFlags.Has(flag);
    }

    public override void Interagir()
    {
        if (vazio) return;
        vazio = true;
        if (!string.IsNullOrEmpty(flag)) GameFlags.Set(flag);
        if (som != null) AudioSource.PlayClipAtPoint(som, transform.position, 0.7f);

        var rnd = new System.Random(semente);
        int quantos = rnd.Next(minimo, maximo + 1);
        var achados = new Dictionary<string, int>();
        var tabela = Tabelas[tipo];
        int total = 0; foreach (var it in tabela) total += it.peso;
        for (int i = 0; i < quantos; i++)
        {
            int sorte = rnd.Next(total);
            foreach (var it in tabela)
            {
                // arma que já tem não vem de novo (as armas são raras e únicas)
                if (sorte < it.peso && (Itens.Get(it.id).tipo == Itens.Tipo.Arma || it.id == "lanterna") && (achados.ContainsKey(it.id) || (PlayerInventory.Instance != null && PlayerInventory.Instance.GetQuantity(it.id) > 0))) break;
                if (sorte < it.peso)
                {
                    achados.TryGetValue(it.id, out int ja);
                    achados[it.id] = ja + it.qtd;
                    break;
                }
                sorte -= it.peso;
            }
        }

        var inv = PlayerInventory.Instance;
        var pegos = new List<string>();
        bool cheio = false;
        foreach (var kv in achados)
        {
            if (inv != null && inv.AddItem(kv.Key, kv.Value)) pegos.Add(Descrever(kv.Key, kv.Value));
            else cheio = true;
        }

        if (pegos.Count == 0) Narrador.Falar(cheio ? "Tinha coisa aqui, mas não cabe mais nada nos bolsos." : Vazio());
        else Narrador.Falar("Achei " + Juntar(pegos) + ".");
    }

    private string Vazio()
    {
        switch (tipo)
        {
            case Tipo.Geladeira: return "Vazia. Só o cheiro ficou.";
            case Tipo.PrateleiraLoja: return "Já levaram tudo daqui.";
            case Tipo.Estoque: return "Caixas vazias.";
            default: return "Nada aqui.";
        }
    }

    private static string Descrever(string id, int qtd)
    {
        if (!Nomes.TryGetValue(id, out var n)) return qtd > 1 ? $"{qtd}x {id}" : id;
        if (id == "municao") return qtd > 1 ? $"{qtd} balas" : "uma bala";
        return qtd > 1 ? $"{qtd} {n.varios}" : n.um;
    }

    private static string Juntar(List<string> partes)
    {
        if (partes.Count == 1) return partes[0];
        var sb = new StringBuilder();
        for (int i = 0; i < partes.Count; i++)
        {
            if (i > 0) sb.Append(i == partes.Count - 1 ? " e " : ", ");
            sb.Append(partes[i]);
        }
        return sb.ToString();
    }
}
