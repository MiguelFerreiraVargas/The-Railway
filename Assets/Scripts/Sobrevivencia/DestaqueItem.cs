using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Contorno (QuickOutline) nos itens que dá para pegar e nos jornais: aparece suave quando o
/// jogador chega perto, pulsa devagar e fica mais forte quando a mira está no item.
/// Não atravessa paredes (só desenha a parte visível). É colocado sozinho ao carregar a cena.
/// </summary>
[DisallowMultipleComponent]
public class DestaqueItem : MonoBehaviour
{
    [Tooltip("A partir desta distância (m) o contorno começa a aparecer.")]
    public float distanciaMax = 9f;
    [Tooltip("Mais perto que isto o contorno fica na força máxima (sem mirar).")]
    public float distanciaCheia = 3f;
    public Color cor = new Color(1f, 0.86f, 0.55f, 1f);
    public Color corMirado = new Color(1f, 0.97f, 0.88f, 1f);
    [Range(0f, 1f)] public float forcaLonge = 0.85f;
    public float larguraLonge = 2.6f;
    public float larguraMirado = 4.5f;

    private Outline outline;
    private Interagivel interagivel;
    private float alfa, fase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AoIniciar()
    {
        Instalar();
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
    }

    private static void AoCarregar(Scene s, LoadSceneMode m) => Instalar();

    /// <summary>Coloca o destaque em todo item coletável e jornal da cena.</summary>
    public static void Instalar()
    {
        foreach (var i in FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((i is ItemColetavel || i is Jornal || i is MonteDeCarvao) && i.GetComponent<DestaqueItem>() == null)
                i.gameObject.AddComponent<DestaqueItem>();
    }

    private void Awake()
    {
        interagivel = GetComponent<Interagivel>();
        fase = Random.value * 10f;
        if (GetComponentInChildren<Renderer>() == null) { enabled = false; return; }

        outline = GetComponent<Outline>();
        if (outline == null)
        {
            try { outline = gameObject.AddComponent<Outline>(); }
            catch (System.Exception e)
            {
                // malha sem Read/Write: fica sem contorno, mas o jogo segue
                Debug.LogWarning($"[DestaqueItem] sem contorno em {name}: {e.Message}");
                enabled = false;
                return;
            }
        }
        outline.OutlineMode = Outline.Mode.OutlineVisible;
        Aplicar(0f, larguraLonge, cor);
    }

    private void Update()
    {
        if (outline == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        bool mirado = interagivel != null && PlayerInteractor.Atual == interagivel;
        float d = Vector3.Distance(cam.transform.position, transform.position);
        float perto = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(distanciaCheia, distanciaMax, d));
        float pulso = 0.82f + 0.18f * Mathf.Sin((Time.time + fase) * 2.4f);

        float alvo = mirado ? 1f : perto * forcaLonge * pulso;
        if (interagivel != null && !interagivel.Disponivel) alvo = 0f;
        alfa = Mathf.MoveTowards(alfa, alvo, Time.deltaTime * 4f);

        Aplicar(alfa, mirado ? larguraMirado : larguraLonge, mirado ? corMirado : cor);
    }

    private void Aplicar(float a, float largura, Color c)
    {
        c.a = a;
        if (outline.OutlineColor != c) outline.OutlineColor = c;
        float w = a > 0.002f ? largura : 0f;
        if (!Mathf.Approximately(outline.OutlineWidth, w)) outline.OutlineWidth = w;
    }
}
