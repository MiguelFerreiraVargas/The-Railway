using UnityEngine;
using UnityEngine.UI;

public class BarraVida : MonoBehaviour
{
    [Header("Imagem da Barra")]
    public Image imagemVida;
    [Header("Sprites da Vida - 10 estágios")]
    public Sprite vida100;
    public Sprite vida90;
    public Sprite vida80;
    public Sprite vida70;
    public Sprite vida60;
    public Sprite vida50;
    public Sprite vida40;
    public Sprite vida30;
    public Sprite vida20;
    public Sprite vida10;
    [Header("Configuração da Vida")]
    public int vidaMaxima = 100;
    public int vidaAtual = 100;

   
    public event System.Action<string> OnMorte;
    private bool morto;

    void Start()
    {
        AtualizarBarra();
    }

    public void TomarDano(int dano, string motivo = "Ferimentos")
    {
        vidaAtual -= dano;
        if (vidaAtual < 0)
            vidaAtual = 0;
        AtualizarBarra();

        if (vidaAtual <= 0 && !morto)
        {
            morto = true;
            OnMorte?.Invoke(motivo);
        }
    }

    public void Curar(int quantidade)
    {
        vidaAtual += quantidade;
        if (vidaAtual > vidaMaxima)
            vidaAtual = vidaMaxima;
        AtualizarBarra();

        if (vidaAtual > 0)
            morto = false;
    }

    void AtualizarBarra()
    {
        float porcentagem = (float)vidaAtual / vidaMaxima;
        if (porcentagem >= 0.9f)
        {
            imagemVida.sprite = vida100;
        }
        else if (porcentagem >= 0.8f)
        {
            imagemVida.sprite = vida90;
        }
        else if (porcentagem >= 0.7f)
        {
            imagemVida.sprite = vida80;
        }
        else if (porcentagem >= 0.6f)
        {
            imagemVida.sprite = vida70;
        }
        else if (porcentagem >= 0.5f)
        {
            imagemVida.sprite = vida60;
        }
        else if (porcentagem >= 0.4f)
        {
            imagemVida.sprite = vida50;
        }
        else if (porcentagem >= 0.3f)
        {
            imagemVida.sprite = vida40;
        }
        else if (porcentagem >= 0.2f)
        {
            imagemVida.sprite = vida30;
        }
        else if (porcentagem >= 0.1f)
        {
            imagemVida.sprite = vida20;
        }
        else
        {
            imagemVida.sprite = vida10;
        }
    }
}