using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Um indicador de necessidade no HUD. Funciona de três jeitos (pode usar mais de um):
///  - gomos: N imagens que apagam conforme o valor cai;
///  - quadros: troca o sprite por quadros da arte (ex.: o cantil esvaziando);
///  - máscara: uma janela (RectMask2D) que encolhe e revela a barra desenhada.
/// Pisca quando está baixa.
/// </summary>
public class BarraSegmentada : MonoBehaviour
{
    [SerializeField] private Image[] segmentos;
    [SerializeField] private TMP_Text rotulo;
    [SerializeField] private Color cor = new Color(0.9f, 0.3f, 0.25f);
    [SerializeField] private Color corVazia = new Color(1f, 1f, 1f, 0.08f);
    [Tooltip("Abaixo disso (0-1) a barra pisca.")]
    [SerializeField] private float limiteAlerta = 0.2f;

    [Header("Arte em quadros (quadro 0 = cheio, último = vazio)")]
    [SerializeField] private Image imagemQuadros;
    [SerializeField] private Sprite[] quadros;

    [Header("Preenchimento por máscara")]
    [SerializeField] private RectTransform mascara;
    [SerializeField] private Graphic arteMascarada;
    [SerializeField] private Vector2 tamanhoCheio;
    [SerializeField] private bool vertical;
    [SerializeField] private int passos = 10;

    private float mostrado = -1f;

    public void Definir(float fracao)
    {
        fracao = Mathf.Clamp01(fracao);
        mostrado = mostrado < 0f ? fracao : Mathf.MoveTowards(mostrado, fracao, Time.unscaledDeltaTime * 1.5f);

        bool alerta = fracao <= limiteAlerta;
        float pisca = alerta ? (Mathf.Sin(Time.unscaledTime * 8f) > 0f ? 1f : 0.35f) : 1f;

        if (segmentos != null && segmentos.Length > 0)
        {
            float porGomo = 1f / segmentos.Length;
            for (int i = 0; i < segmentos.Length; i++)
            {
                if (segmentos[i] == null) continue;
                // gomo parcialmente cheio vira cor mais fraca
                float cheio = Mathf.Clamp01((mostrado - i * porGomo) / porGomo);
                var c = Color.Lerp(corVazia, cor, cheio);
                c.a *= cheio > 0f ? pisca : 1f;
                segmentos[i].color = c;
            }
        }

        if (imagemQuadros != null && quadros != null && quadros.Length > 0)
        {
            int n = quadros.Length;
            int i = Mathf.Clamp(Mathf.RoundToInt((1f - mostrado) * (n - 1)), 0, n - 1);
            if (mostrado > 0.001f && i == n - 1 && n > 1) i = n - 2; // só mostra vazio quando zerar
            if (quadros[i] != null) imagemQuadros.sprite = quadros[i];
            imagemQuadros.color = new Color(cor.r, cor.g, cor.b, pisca);
        }

        if (mascara != null)
        {
            float k = mostrado <= 0.001f ? 0f : Mathf.Ceil(mostrado * passos - 0.001f) / passos;
            mascara.sizeDelta = vertical ? new Vector2(tamanhoCheio.x, tamanhoCheio.y * k) : new Vector2(tamanhoCheio.x * k, tamanhoCheio.y);
            if (arteMascarada != null) arteMascarada.color = new Color(cor.r, cor.g, cor.b, pisca);
        }

        if (rotulo != null)
            rotulo.color = alerta ? Color.Lerp(cor, Color.white, pisca > 0.5f ? 0.4f : 0f) : new Color(0.85f, 0.82f, 0.75f, 0.85f);
    }
}
