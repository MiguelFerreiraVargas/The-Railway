using System.Collections.Generic;

/// <summary>
/// Catálogo dos itens do jogo: nome para mostrar, peso (kg) e tipo. A mochila usa o peso;
/// a tela da mochila usa o nome e o tipo (comer, beber, equipar).
/// </summary>
public static class Itens
{
    public enum Tipo { Comida, Bebida, Arma, Municao, Material, Outro }

    public class Info
    {
        public string id, nome;
        public float peso;
        public Tipo tipo;
        public string descricao;
    }

    private static readonly Dictionary<string, Info> todos = new Dictionary<string, Info>();

    static Itens()
    {
        Add("lata_feijao", "Lata de feijão", 0.5f, Tipo.Comida, "Fome +35.");
        Add("biscoito", "Biscoito velho", 0.2f, Tipo.Comida, "Fome +15.");
        Add("carne_crua", "Carne crua", 0.8f, Tipo.Comida, "Mata a fome, mas faz mal crua.");
        Add("agua", "Garrafa d'água", 1.0f, Tipo.Bebida, "Sede +40, estamina +25.");
        Add("soda", "Soda", 0.4f, Tipo.Bebida, "Sede +20, estamina +50, acalma um pouco.");
        Add("fosforos", "Caixa de fósforos", 0.05f, Tipo.Material, "Para acender fogo.");
        Add("galho", "Galho seco", 0.5f, Tipo.Material, "Lenha.");
        Add("municao", "Munição", 0.02f, Tipo.Municao, "Balas para o rifle.");
        Add("faca", "Faca", 0.3f, Tipo.Arma, "Corpo a corpo. Mais dano que o soco.");
        Add("machado", "Machado", 1.5f, Tipo.Arma, "Corpo a corpo pesado. Corta árvores caídas.");
        Add("rifle", "Rifle de caça", 3.5f, Tipo.Arma, "Atira longe. Usa munição.");
        Add("carvao", "Pedra de carvão", 0f, Tipo.Material, "Vai na mão até o tênder.");
        Add("lanterna", "Lanterna", 0.4f, Tipo.Outro, "Na mão direita. [L] liga e desliga.");
        Add("pilha", "Pilha", 0.05f, Tipo.Outro, "Recarrega a lanterna.");
    }

    private static void Add(string id, string nome, float peso, Tipo tipo, string descricao)
        => todos[id] = new Info { id = id, nome = nome, peso = peso, tipo = tipo, descricao = descricao };

    public static Info Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return todos.TryGetValue(id, out var i) ? i : new Info { id = id, nome = id, peso = 0.5f, tipo = Tipo.Outro, descricao = "" };
    }

    public static float Peso(string id) => Get(id)?.peso ?? 0f;
    public static string Nome(string id) => Get(id)?.nome ?? id;
}
