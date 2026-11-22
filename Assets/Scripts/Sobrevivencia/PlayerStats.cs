using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Necessidades do Cândido: vida, fome, sede, higiene e sanidade.
/// A stamina é a do FirstPersonController (este script só ajusta a
/// recuperação dela conforme a fome/sede e soma stamina ao beber).
///
/// Tudo usa Time.deltaTime: congela sozinho na pausa, no carregamento e na morte.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    public enum Tipo { Vida, Fome, Sede, Higiene, Sanidade }

    [Serializable]
    public class Necessidade
    {
        public string nome = "";
        public float maximo = 100f;
        public float atual = 100f;
        [Tooltip("Quanto perde por minuto de jogo.")]
        public float perdaPorMinuto;

        public float Fracao => maximo > 0f ? Mathf.Clamp01(atual / maximo) : 0f;
    }

    [Serializable]
    public class ItemConsumivel
    {
        public string itemId = "lata_feijao";
        public string nome = "LATA DE FEIJÃO";
        public bool bebida;
        public float fome, sede, stamina, higiene, vida, sanidade;
        public AudioClip som;
    }

    public static PlayerStats Instance { get; private set; }

    [Header("Necessidades")]
    public Necessidade vida = new Necessidade { nome = "VIDA", perdaPorMinuto = 0f };
    public Necessidade fome = new Necessidade { nome = "FOME", perdaPorMinuto = 7f };      // ~14 min para zerar
    public Necessidade sede = new Necessidade { nome = "SEDE", perdaPorMinuto = 10f };     // ~10 min
    public Necessidade higiene = new Necessidade { nome = "HIGIENE", perdaPorMinuto = 3f }; // ~33 min
    public Necessidade sanidade = new Necessidade { nome = "SANIDADE", perdaPorMinuto = 0f };

    [Header("Consequências (por minuto)")]
    [Tooltip("Dano na vida com a fome zerada.")]
    public float danoFomeZerada = 30f;
    [Tooltip("Dano na vida com a sede zerada.")]
    public float danoSedeZerada = 40f;
    [Tooltip("Sanidade perdida com fome ou sede zerada.")]
    public float sanidadeFomeSedeZerada = 6f;
    [Tooltip("Sanidade perdida com a higiene abaixo de 20%.")]
    public float sanidadeHigieneBaixa = 2f;
    [Tooltip("Vida recuperada quando fome e sede estão acima de 50%.")]
    public float regeneracaoVida = 2f;

    [Header("Stamina (do FirstPersonController)")]
    [Tooltip("Recuperação da stamina com fome ou sede abaixo de 30% (multiplicador).")]
    [Range(0f, 1f)] public float recuperacaoComFomeOuSedeBaixa = 0.5f;
    [Tooltip("Recuperação da stamina com fome ou sede zerada (multiplicador).")]
    [Range(0f, 1f)] public float recuperacaoComFomeOuSedeZerada = 0.25f;

    [Header("Comer e beber")]
    public List<ItemConsumivel> consumiveis = new List<ItemConsumivel>
    {
        new ItemConsumivel { itemId = "lata_feijao", nome = "LATA DE FEIJÃO", fome = 35f, sanidade = 2f },
        new ItemConsumivel { itemId = "biscoito", nome = "BISCOITO VELHO", fome = 15f },
        new ItemConsumivel { itemId = "carne_cozida", nome = "CARNE ASSADA", fome = 45f, vida = 5f },
        new ItemConsumivel { itemId = "agua", nome = "GARRAFA D'ÁGUA", bebida = true, sede = 40f, stamina = 25f },
        new ItemConsumivel { itemId = "soda", nome = "SODA", bebida = true, sede = 20f, stamina = 50f, sanidade = 3f },
    };
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somComer;
    [SerializeField] private AudioClip somBeber;

    /// <summary>Disparado uma vez quando a vida (ou a sanidade) zera. Texto = causa.</summary>
    public event Action<string> OnMorte;

    public bool Morto { get; private set; }
    public FirstPersonController Jogador { get; private set; }

    private float recuperacaoStaminaBase = -1f;
    private string ultimaCausaDano = "FERIMENTOS";

    private void Awake()
    {
        Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // O inventário é persistente (DontDestroyOnLoad); cria se nenhuma cena trouxe um.
        if (PlayerInventory.Instance == null)
            new GameObject("PlayerInventory").AddComponent<PlayerInventory>();

        // Veio de outra cena (ex.: o trem saindo do prólogo): continua com as mesmas necessidades.
        if (transporte != null)
        {
            vida.atual = transporte[0]; fome.atual = transporte[1]; sede.atual = transporte[2];
            higiene.atual = transporte[3]; sanidade.atual = transporte[4];
            transporte = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (!Morto) transporte = new[] { vida.atual, fome.atual, sede.atual, higiene.atual, sanidade.atual };
    }

    // Valores levados de uma cena para a próxima.
    private static float[] transporte;

    /// <summary>Esquece os valores levados entre cenas (jogo novo / voltar ao menu).</summary>
    public static void LimparTransporte() => transporte = null;

    private void Start()
    {
        EncontrarJogador();
    }

    private void EncontrarJogador()
    {
        if (Jogador != null) return;
        Jogador = FindFirstObjectByType<FirstPersonController>();
        if (Jogador != null && recuperacaoStaminaBase < 0f)
            recuperacaoStaminaBase = Jogador.recuperacaoStamina;
    }

    // ------------------------------------------------------------ Simulação

    private void Update()
    {
        if (Morto) return;
        EncontrarJogador();

        float minutos = Time.deltaTime / 60f;
        if (minutos <= 0f) return;

        Mudar(fome, -fome.perdaPorMinuto * minutos);
        Mudar(sede, -sede.perdaPorMinuto * minutos);
        Mudar(higiene, -higiene.perdaPorMinuto * minutos);
        Mudar(sanidade, -sanidade.perdaPorMinuto * minutos);

        // Consequências
        if (fome.atual <= 0f) Dano(danoFomeZerada * minutos, "DE FOME");
        if (sede.atual <= 0f) Dano(danoSedeZerada * minutos, "DE SEDE");

        if (fome.atual <= 0f || sede.atual <= 0f)
            Mudar(sanidade, -sanidadeFomeSedeZerada * minutos);

        if (higiene.Fracao < 0.2f)
            Mudar(sanidade, -sanidadeHigieneBaixa * minutos);

        if (fome.Fracao > 0.5f && sede.Fracao > 0.5f && vida.atual < vida.maximo)
            Mudar(vida, regeneracaoVida * minutos);

        if (sanidade.atual <= 0f) Morrer("ENLOUQUECEU");

        AjustarStamina();
    }

    private void AjustarStamina()
    {
        if (Jogador == null || recuperacaoStaminaBase < 0f) return;

        float mult = 1f;
        if (fome.atual <= 0f || sede.atual <= 0f) mult = recuperacaoComFomeOuSedeZerada;
        else if (fome.Fracao < 0.3f || sede.Fracao < 0.3f) mult = recuperacaoComFomeOuSedeBaixa;

        Jogador.recuperacaoStamina = recuperacaoStaminaBase * mult;
    }

    // ------------------------------------------------------------ API

    public Necessidade Get(Tipo tipo)
    {
        switch (tipo)
        {
            case Tipo.Vida: return vida;
            case Tipo.Fome: return fome;
            case Tipo.Sede: return sede;
            case Tipo.Higiene: return higiene;
            default: return sanidade;
        }
    }

    public float StaminaFracao => Jogador != null && Jogador.staminaMax > 0f ? Mathf.Clamp01(Jogador.staminaAtual / Jogador.staminaMax) : 1f;

    public void Dano(float quantidade, string causa = "FERIMENTOS")
    {
        if (Morto || quantidade <= 0f) return;
        ultimaCausaDano = causa;
        Mudar(vida, -quantidade);
        if (vida.atual <= 0f) Morrer(ultimaCausaDano);
    }

    public void Curar(float quantidade) => Mudar(vida, quantidade);
    public void Comer(float quantidade) => Mudar(fome, quantidade);
    public void Beber(float quantidade) => Mudar(sede, quantidade);
    public void Lavar(float quantidade) => Mudar(higiene, quantidade);
    public void Acalmar(float quantidade) => Mudar(sanidade, quantidade);

    public void SomarStamina(float quantidade)
    {
        EncontrarJogador();
        if (Jogador == null) return;
        Jogador.staminaAtual = Mathf.Clamp(Jogador.staminaAtual + quantidade, 0f, Jogador.staminaMax);
    }

    /// <summary>Define valores direto (ex.: prólogo começa com fome/sede baixas). Use valores de 0 a 1.</summary>
    public void DefinirFracoes(float vidaF, float fomeF, float sedeF, float higieneF, float sanidadeF)
    {
        vida.atual = vida.maximo * Mathf.Clamp01(vidaF);
        fome.atual = fome.maximo * Mathf.Clamp01(fomeF);
        sede.atual = sede.maximo * Mathf.Clamp01(sedeF);
        higiene.atual = higiene.maximo * Mathf.Clamp01(higieneF);
        sanidade.atual = sanidade.maximo * Mathf.Clamp01(sanidadeF);
    }

    public ItemConsumivel GetConsumivel(string itemId) => consumiveis.Find(c => c.itemId == itemId);

    /// <summary>Aplica os efeitos de um consumível (sem mexer no inventário).</summary>
    public void Aplicar(ItemConsumivel item)
    {
        if (item == null || Morto) return;
        Comer(item.fome);
        Beber(item.sede);
        Lavar(item.higiene);
        Curar(item.vida);
        Acalmar(item.sanidade);
        SomarStamina(item.stamina);

        var clip = item.som != null ? item.som : (item.bebida ? somBeber : somComer);
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    /// <summary>Consome do inventário o primeiro item de comida (ou bebida). Retorna o item usado.</summary>
    public ItemConsumivel ConsumirDoInventario(bool bebida)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || Morto) return null;

        foreach (var item in consumiveis)
        {
            if (item.bebida != bebida || inv.GetQuantity(item.itemId) <= 0) continue;
            if (!inv.RemoveItem(item.itemId, 1)) continue;
            Aplicar(item);
            return item;
        }

        return null;
    }

    public int ContarNoInventario(bool bebida)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return 0;
        int total = 0;
        foreach (var item in consumiveis)
            if (item.bebida == bebida) total += inv.GetQuantity(item.itemId);
        return total;
    }

    /// <summary>Usado ao carregar save ou ao recomeçar.</summary>
    public void Reviver()
    {
        Morto = false;
    }

    private void Morrer(string causa)
    {
        if (Morto) return;
        Morto = true;
        vida.atual = Mathf.Max(0f, vida.atual);
        OnMorte?.Invoke(causa);
    }

    private static void Mudar(Necessidade n, float delta)
    {
        n.atual = Mathf.Clamp(n.atual + delta, 0f, n.maximo);
    }
}
