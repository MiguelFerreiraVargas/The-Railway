using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Em primeira pessoa olha pelo centro da câmera; na isométrica escolhe o Interagivel mais
/// próximo à frente do Cândido. Mostra "[E] PEGAR CARVÃO" e usa ao apertar E (ou X no controle).
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float alcance = 2.8f;
    [SerializeField] private CanvasGroup aviso;
    [SerializeField] private TMP_Text avisoTexto;
    [SerializeField] private LayerMask camadas = ~0;
    [Tooltip("Na isométrica: raio em volta do Cândido em que as coisas podem ser usadas.")]
    [SerializeField] private float alcanceIsometrico = 2.1f;

    private Vector2 posAvisoOriginal;
    private readonly Collider[] pertos = new Collider[64];

    private void Awake()
    {
        if (aviso != null) posAvisoOriginal = ((RectTransform)aviso.transform).anchoredPosition;
    }

    public static Interagivel Atual { get; private set; }

    /// <summary>Disparado quando o jogador usa algo (a mão em primeira pessoa faz o gesto de pegar).</summary>
    public static event System.Action<Interagivel> Interagiu;

    private void Update()
    {
        Atual = null;
        bool bloqueado = PauseMenu.IsPaused || LoadingScreen.IsLoading || (PlayerStats.Instance != null && PlayerStats.Instance.Morto) || Leitura.Aberta;
        var cam = Camera.main;

        var modo = ModoCamera.Instance;
        bool iso = modo != null && modo.Isometrico;

        // sentado no trem, o E sempre desce (mesmo olhando pela janela)
        var tremSentado = !bloqueado ? TremInteragivel.ComJogador : null;
        if (tremSentado != null)
        {
            Atual = tremSentado;
        }
        else if (!bloqueado && cam != null && iso)
        {
            Atual = MaisProximo(modo.transform);
        }
        else if (!bloqueado && cam != null)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            Atual = PelaMira(ray);
        }

        if (aviso != null) aviso.alpha = Mathf.MoveTowards(aviso.alpha, Atual != null ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (Atual != null && avisoTexto != null) avisoTexto.text = $"<color=#FFB040>[E]</color>  {Atual.Texto}";
        PosicionarAviso(iso && cam != null ? modo.transform : null, cam);

        if (Atual != null && Apertou())
        {
            var alvoUsado = Atual;
            alvoUsado.Interagir();
            Interagiu?.Invoke(alvoUsado);
        }
    }

    // Primeira pessoa: o primeiro que a mira acerta; mas uma coisa específica (tênder, item)
    // logo atrás de algo grande e genérico (o trem inteiro) ganha dele.
    private readonly RaycastHit[] acertos = new RaycastHit[16];

    private Interagivel PelaMira(Ray ray)
    {
        int n = Physics.RaycastNonAlloc(ray, acertos, alcance, camadas, QueryTriggerInteraction.Collide);
        if (n == 0) return null;
        System.Array.Sort(acertos, 0, n, Comparer);
        Interagivel primeiro = null;
        float distPrimeiro = 0f;
        for (int i = 0; i < n; i++)
        {
            var col = acertos[i].collider;
            if (col == null || (ModoCamera.Instance != null && col.transform.IsChildOf(ModoCamera.Instance.transform))) continue;
            var it = col.GetComponentInParent<Interagivel>();
            if (primeiro == null)
            {
                if (it == null)
                {
                    if (!col.isTrigger) return null; // parede/chão na frente: nada
                    continue;                        // gatilho sem nada: atravessa
                }
                if (!it.Disponivel) return null;
                primeiro = it; distPrimeiro = acertos[i].distance;
                if (it.Prioridade >= 0f) return it;
                continue;
            }
            // já achou algo genérico: procura algo mais importante logo atrás
            if (acertos[i].distance - distPrimeiro > 1.2f) break;
            if (it != null && it != primeiro && it.Disponivel && it.Prioridade > primeiro.Prioridade) return it;
        }
        return primeiro;
    }

    private static readonly System.Collections.Generic.IComparer<RaycastHit> Comparer =
        System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    // Isométrica: o que estiver mais perto e mais de frente para o Cândido
    private Interagivel MaisProximo(Transform jogador)
    {
        Vector3 centro = jogador.position + Vector3.up * 0.9f;
        int n = Physics.OverlapSphereNonAlloc(centro, alcanceIsometrico, pertos, camadas, QueryTriggerInteraction.Collide);
        Interagivel melhor = null;
        float melhorNota = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var col = pertos[i];
            if (col == null || col.transform.IsChildOf(jogador)) continue;
            var it = col.GetComponentInParent<Interagivel>();
            if (it == null || !it.Disponivel) continue;
            Vector3 ponto = col.bounds.ClosestPoint(centro);
            Vector3 d = ponto - centro; d.y = 0f;
            float dist = d.magnitude;
            float frente = dist > 0.05f ? Vector3.Dot(jogador.forward, d / dist) : 1f;
            if (frente < -0.35f) continue; // atrás das costas
            if (!AVista(centro, col, it, jogador)) continue; // do outro lado da parede: tem que entrar no cômodo
            float nota = dist - frente * 0.7f - it.Prioridade;
            if (nota < melhorNota) { melhorNota = nota; melhor = it; }
        }
        return melhor;
    }

    private readonly RaycastHit[] noCaminho = new RaycastHit[16];

    // Se nada sólido (parede, porta fechada) fica entre o peito do Cândido e a coisa.
    // Vale se der para ver o centro dela ou o ponto mais perto dela.
    private bool AVista(Vector3 de, Collider col, Interagivel it, Transform jogador)
    {
        return Livre(de, col.bounds.center, it, jogador) || Livre(de, col.bounds.ClosestPoint(de), it, jogador);
    }

    private bool Livre(Vector3 de, Vector3 ate, Interagivel it, Transform jogador)
    {
        Vector3 d = ate - de;
        float dist = d.magnitude;
        if (dist < 0.05f) return true;
        int n = Physics.RaycastNonAlloc(de, d / dist, noCaminho, dist, camadas, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = noCaminho[i].collider;
            if (c == null || c.transform.IsChildOf(jogador) || c.transform.IsChildOf(it.transform)) continue;
            if (c is TerrainCollider) continue;
            // móvel baixo (mesa, balcão, caixa) na frente de um item não conta; parede é mais alta que o peito
            if (c.bounds.max.y < de.y) continue;
            return false;
        }
        return true;
    }

    // Na isométrica o aviso fica em cima da cabeça do Cândido; em primeira pessoa, embaixo da mira
    private void PosicionarAviso(Transform jogador, Camera cam)
    {
        if (aviso == null) return;
        var rt = (RectTransform)aviso.transform;
        if (jogador == null)
        {
            if (rt.anchoredPosition != posAvisoOriginal) rt.anchoredPosition = posAvisoOriginal;
            return;
        }
        Vector3 tela = cam.WorldToScreenPoint(jogador.position + Vector3.up * 2.15f);
        if (tela.z < 0f) return;
        var canvas = rt.GetComponentInParent<Canvas>();
        float escala = canvas != null ? canvas.scaleFactor : 1f;
        rt.position = new Vector3(tela.x, tela.y + 18f * escala, 0f);
    }

    private static bool Apertou()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        return Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
    }
}
