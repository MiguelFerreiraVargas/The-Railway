using UnityEngine;
using UnityEngine.UI;

public class BarraSanidade : MonoBehaviour
{
    public static BarraSanidade Instance { get; private set; }

    [Header("Imagem da Barra")]
    public Image imagemSanidade;

    [Header("Sprites da Sanidade - 10 estágios")]
    public Sprite sanidade100;
    public Sprite sanidade90;
    public Sprite sanidade80;
    public Sprite sanidade70;
    public Sprite sanidade60;
    public Sprite sanidade50;
    public Sprite sanidade40;
    public Sprite sanidade30;
    public Sprite sanidade20;
    public Sprite sanidade10;

    [Header("Configuração da Sanidade")]
    public int sanidadeMaxima = 100;
    public int sanidadeAtual = 100;

    [Header("Perda por Fome Zerada")]
    public BarraFome barraFome;              // arrasta a barra de fome do player aqui
    public float intervaloPerdaPorFome = 3f; // mais devagar que o dano de vida da fome, de propósito
    public int perdaPorFome = 1;

    // Disparado quando a sanidade chega a 0. O PlayerDeath escuta esse evento.
    public event System.Action OnMorte;
    private bool morreu;

    private float perdaFomeTimer;

    void Start()
    {
        Instance = this;
        AtualizarBarra();
    }

    void Update()
    {
        if (barraFome != null && barraFome.fomeAtual <= 0)
        {
            perdaFomeTimer += Time.deltaTime;

            if (perdaFomeTimer >= intervaloPerdaPorFome)
            {
                perdaFomeTimer = 0f;
                PerderSanidade(perdaPorFome);
            }
        }
        else
        {
            perdaFomeTimer = 0f;
        }
    }

    public void PerderSanidade(int quantidade)
    {
        sanidadeAtual -= quantidade;

        if (sanidadeAtual < 0)
            sanidadeAtual = 0;

        AtualizarBarra();

        if (sanidadeAtual <= 0 && !morreu)
        {
            morreu = true;
            OnMorte?.Invoke();
        }
    }

    public void RecuperarSanidade(int quantidade)
    {
        sanidadeAtual += quantidade;

        if (sanidadeAtual > sanidadeMaxima)
            sanidadeAtual = sanidadeMaxima;

        AtualizarBarra();

        if (sanidadeAtual > 0)
            morreu = false; // permite morrer de novo depois de reviver
    }

    void AtualizarBarra()
    {
        float porcentagem = (float)sanidadeAtual / sanidadeMaxima;

        if (porcentagem >= 0.9f)
        {
            imagemSanidade.sprite = sanidade100;
        }
        else if (porcentagem >= 0.8f)
        {
            imagemSanidade.sprite = sanidade90;
        }
        else if (porcentagem >= 0.7f)
        {
            imagemSanidade.sprite = sanidade80;
        }
        else if (porcentagem >= 0.6f)
        {
            imagemSanidade.sprite = sanidade70;
        }
        else if (porcentagem >= 0.5f)
        {
            imagemSanidade.sprite = sanidade60;
        }
        else if (porcentagem >= 0.4f)
        {
            imagemSanidade.sprite = sanidade50;
        }
        else if (porcentagem >= 0.3f)
        {
            imagemSanidade.sprite = sanidade40;
        }
        else if (porcentagem >= 0.2f)
        {
            imagemSanidade.sprite = sanidade30;
        }
        else if (porcentagem >= 0.1f)
        {
            imagemSanidade.sprite = sanidade20;
        }
        else
        {
            imagemSanidade.sprite = sanidade10;
        }
    }
}