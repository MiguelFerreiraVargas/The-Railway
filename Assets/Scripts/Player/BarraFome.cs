using UnityEngine;
using UnityEngine.UI;

public class BarraFome : MonoBehaviour
{
    public static BarraFome Instance { get; private set; }

    [Header("Imagem da Barra")]
    public Image imagemFome;

    [Header("Sprites da Fome - 10 estágios")]
    public Sprite fome100;
    public Sprite fome90;
    public Sprite fome80;
    public Sprite fome70;
    public Sprite fome60;
    public Sprite fome50;
    public Sprite fome40;
    public Sprite fome30;
    public Sprite fome20;
    public Sprite fome10;

    [Header("Configuração da Fome")]
    public int fomeMaxima = 100;
    public int fomeAtual = 100;

    [Header("Perda de Fome ao Longo do Tempo")]
    public float intervaloPerdaFome = 1f; // a cada quantos segundos perde fome
    public int quantidadePerdaFome = 1;   // quanto perde por vez

    [Header("Consequência - Fome Zerada")]
    public BarraVida barraVida;           // arrasta a barra de vida do player aqui
    public float intervaloDanoPorFome = 1f; // a cada quantos segundos toma dano com fome zerada
    public int danoPorFome = 5;

    [Header("Itens que restauram fome (opcional)")]
    public ItemComestivel[] itensComestiveis;

    private float perdaTimer;
    private float danoTimer;

    void Start()
    {
        Instance = this;
        AtualizarBarra();
    }

    void Update()
    {
        perdaTimer += Time.deltaTime;

        if (perdaTimer >= intervaloPerdaFome)
        {
            perdaTimer = 0f;
            PerderFome(quantidadePerdaFome);
        }

        if (fomeAtual <= 0)
        {
            danoTimer += Time.deltaTime;

            if (danoTimer >= intervaloDanoPorFome)
            {
                danoTimer = 0f;

                if (barraVida != null)
                    barraVida.TomarDano(danoPorFome);
            }
        }
        else
        {
            danoTimer = 0f;
        }
    }

    public void PerderFome(int quantidade)
    {
        fomeAtual -= quantidade;

        if (fomeAtual < 0)
            fomeAtual = 0;

        AtualizarBarra();
    }

    public void Comer(int quantidade)
    {
        fomeAtual += quantidade;

        if (fomeAtual > fomeMaxima)
            fomeAtual = fomeMaxima;

        AtualizarBarra();
    }

    // Come um item direto do PlayerInventory (ex: "carne_cozida"), se ele estiver
    // configurado em itensComestiveis. Retorna false se não tinha o item.
    public bool ComerItem(string itemId)
    {
        if (PlayerInventory.Instance == null || itensComestiveis == null)
            return false;

        ItemComestivel item = System.Array.Find(itensComestiveis, i => i.itemId == itemId);

        if (item == null)
            return false;

        if (!PlayerInventory.Instance.RemoveItem(itemId, 1))
            return false;

        Comer(item.restauraFome);
        return true;
    }

    void AtualizarBarra()
    {
        float porcentagem = (float)fomeAtual / fomeMaxima;

        if (porcentagem >= 0.9f)
        {
            imagemFome.sprite = fome100;
        }
        else if (porcentagem >= 0.8f)
        {
            imagemFome.sprite = fome90;
        }
        else if (porcentagem >= 0.7f)
        {
            imagemFome.sprite = fome80;
        }
        else if (porcentagem >= 0.6f)
        {
            imagemFome.sprite = fome70;
        }
        else if (porcentagem >= 0.5f)
        {
            imagemFome.sprite = fome60;
        }
        else if (porcentagem >= 0.4f)
        {
            imagemFome.sprite = fome50;
        }
        else if (porcentagem >= 0.3f)
        {
            imagemFome.sprite = fome40;
        }
        else if (porcentagem >= 0.2f)
        {
            imagemFome.sprite = fome30;
        }
        else if (porcentagem >= 0.1f)
        {
            imagemFome.sprite = fome20;
        }
        else
        {
            imagemFome.sprite = fome10;
        }
    }
}

[System.Serializable]
public class ItemComestivel
{
    public string itemId;
    public int restauraFome;
}