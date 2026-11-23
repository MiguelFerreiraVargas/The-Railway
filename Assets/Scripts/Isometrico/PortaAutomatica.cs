using UnityEngine;

/// <summary>
/// Porta que abre sozinha quando o Cândido chega perto (girando para longe dele) e fecha
/// depois que ele se afasta. Gira em volta de uma dobradiça na borda da folha, sem precisar
/// mudar a hierarquia (funciona dentro de prefabs de modelo). O pai deve estar "em pé" (Y para cima).
/// O colisor só fica ligado com a porta fechada: aberta ou se mexendo ela não empurra nem prende
/// o Cândido. Trancada, não abre (ver PortaTrancada).
/// </summary>
public class PortaAutomatica : MonoBehaviour
{
    [Tooltip("Ponto da dobradiça, no espaço local do pai da porta.")]
    public Vector3 dobradicaLocal;
    public float angulo = 95f;
    public float distanciaAbrir = 1.8f;
    public float velocidade = 240f;
    public float esperaFechar = 1.2f;
    public AudioClip somAbrir;
    [Tooltip("Trancada não abre sozinha.")]
    public bool trancada;

    private Vector3 posFechada, centroLocal, normalLocal;
    private Quaternion rotFechada;
    private float atual, alvo, sinal = 1f, ultimoPerto = -99f;
    private Transform jogador;
    private Collider[] colisores;

    public bool Aberta => Mathf.Abs(atual) > angulo * 0.6f;
    public bool Fechada => Mathf.Approximately(atual, 0f) && alvo == 0f;

    private void Awake()
    {
        posFechada = transform.localPosition;
        rotFechada = transform.localRotation;
        var r = GetComponent<Renderer>();
        var pai = transform.parent;
        Vector3 centroMundo = r != null ? r.bounds.center : transform.position;
        centroLocal = pai != null ? pai.InverseTransformPoint(centroMundo) : centroMundo;
        Vector3 aoLongo = centroLocal - dobradicaLocal; aoLongo.y = 0f;
        normalLocal = Vector3.Cross(Vector3.up, aoLongo.normalized); // perpendicular à folha fechada
        colisores = GetComponents<Collider>();
    }

    private void Update()
    {
        if (jogador == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            jogador = p.transform;
        }

        var pai = transform.parent;
        Vector3 jogadorLocal = pai != null ? pai.InverseTransformPoint(jogador.position) : jogador.position;
        Vector3 d = jogadorLocal - centroLocal; d.y = 0f;
        bool perto = d.magnitude < distanciaAbrir;
        if (perto) ultimoPerto = Time.time;

        if (perto && alvo == 0f && !trancada)
        {
            // gira para o lado oposto ao do jogador (a folha "foge" dele)
            sinal = Vector3.Dot(d, normalLocal) > 0f ? -1f : 1f;
            alvo = angulo;
            if (somAbrir != null) AudioSource.PlayClipAtPoint(somAbrir, transform.position, 0.6f);
        }
        else if (!perto && alvo != 0f && Time.time - ultimoPerto > esperaFechar)
        {
            alvo = 0f;
        }

        float meta = alvo * sinal;
        if (!Mathf.Approximately(atual, meta))
        {
            atual = Mathf.MoveTowards(atual, meta, velocidade * Time.deltaTime);
            Girar(atual);
        }

        // só bloqueia fechada; ela abre antes de o Cândido encostar
        bool solida = Fechada;
        foreach (var c in colisores) if (c != null && c.enabled != solida) c.enabled = solida;
    }

    private void Girar(float graus)
    {
        var giro = Quaternion.AngleAxis(graus, Vector3.up);
        transform.localPosition = dobradicaLocal + giro * (posFechada - dobradicaLocal);
        transform.localRotation = giro * rotFechada;
    }

    /// <summary>Tranco: a porta balança um pouco no lugar (forçando a fechadura).</summary>
    public void Sacudir(float graus) => Girar(graus);

    /// <summary>Destranca e abre na hora, para longe de quem forçou.</summary>
    public void Destrancar()
    {
        trancada = false;
        Girar(0f);
        atual = 0f;
        alvo = 0f; // abre no próximo quadro, se o Cândido estiver perto
    }
}
