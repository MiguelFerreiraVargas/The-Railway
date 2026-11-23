using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Na câmera isométrica, esconde o que fica entre a câmera e o Cândido (copas de árvore,
/// paredes, telhados): o objeto vira "só sombra" enquanto tampa e volta ao normal depois.
/// Usa os colisores do cenário (as árvores têm MeshCollider).
/// </summary>
public class OcultarObstaculos : MonoBehaviour
{
    public Transform alvo;
    [Tooltip("Altura do ponto do jogador que precisa ficar visível.")]
    public float alturaAlvo = 1.0f;
    public LayerMask camadas = ~0;

    private readonly Dictionary<Renderer, ShadowCastingMode> escondidos = new Dictionary<Renderer, ShadowCastingMode>();
    private readonly HashSet<Renderer> agora = new HashSet<Renderer>();
    private readonly Collider[] candidatos = new Collider[256];
    [Tooltip("Dentro de casa, some o que estiver acima desta altura do piso (telhado, teto).")]
    public float alturaParede = 2.3f;
    private TrainInteraction[] trens;
    private Estrutura cacheCasa;
    private Renderer[] rendCasa = new Renderer[0];
    [Tooltip("Folga (m) em volta do que não é da casa (copa de árvore) para sumir quando tampa o Cândido.")]
    public float folgaFora = 1.6f;
    private Transform tremComJogador;
    [Tooltip("Distância lateral (m) da linha de visão em que os objetos são verificados.")]
    public float raioBusca = 6f;

    private void LateUpdate()
    {
        bool ativo = alvo != null && ModoCamera.Instance != null && ModoCamera.Instance.Isometrico;
        agora.Clear();

        tremComJogador = null;
        if (trens == null) trens = FindObjectsByType<TrainInteraction>(FindObjectsSortMode.None);
        foreach (var t in trens) if (t != null && t.IsInsideTrain) { tremComJogador = t.transform; break; }

        // casa em que o Cândido está: some o que fica acima da altura das paredes (telhado, teto)
        Estrutura casaAtual = ativo ? Estrutura.ComJogador : null;
        if (casaAtual != null)
        {
            // o andar em que o Cândido está (casa de dois andares: no térreo some o de cima)
            var piso = casaAtual.PisoAtual != null ? casaAtual.PisoAtual : casaAtual.pisos != null && casaAtual.pisos.Length > 0 ? casaAtual.pisos[0] : null;
            float pisoTopo = piso != null ? piso.bounds.max.y : alvo.position.y;
            if (casaAtual.PisoAtual != null && casaAtual.PisoAtual.bounds.size.y < 0.6f) pisoTopo = Mathf.Max(pisoTopo, casaAtual.PisoAtual.bounds.max.y);
            if (cacheCasa != casaAtual) { cacheCasa = casaAtual; rendCasa = casaAtual.GetComponentsInChildren<Renderer>(); }
            foreach (var r in rendCasa)
                if (r != null && r.bounds.center.y > pisoTopo + alturaParede) agora.Add(r);

            // paredes em pedaços (casa velha): some o pedaço que fica entre a câmera e o Cândido
            if (Camera.main != null)
            {
                Vector3 de = Camera.main.transform.position;
                Vector3 ate = alvo.position + Vector3.up * alturaAlvo;
                var raio = new Ray(de, (ate - de).normalized);
                float dist = Vector3.Distance(de, ate);
                foreach (var r in rendCasa)
                {
                    if (r == null || agora.Contains(r) || r.transform.parent == null || r.transform.parent.name != "Paredes") continue;
                    var b = r.bounds;
                    b.Expand(0.4f);
                    if (b.IntersectRay(raio, out float t) && t < dist - 0.9f) agora.Add(r);
                }
            }
        }

        if (ativo && Camera.main != null)
        {
            Vector3 de = Camera.main.transform.position;
            Vector3 ate = alvo.position + Vector3.up * alturaAlvo;
            Vector3 dir = ate - de;
            float dist = dir.magnitude;
            var raioVisao = new Ray(de, dir / dist);

            // Candidatos: tudo com colisor perto da linha câmera→jogador projetada no chão
            // (o colisor da árvore é só o tronco; a copa é testada pelo volume do desenho)
            Vector3 chaoCam = new Vector3(de.x, alvo.position.y + 1f, de.z);
            int n = Physics.OverlapCapsuleNonAlloc(alvo.position + Vector3.up, chaoCam, raioBusca, candidatos, camadas, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = candidatos[i];
                if (col == null || col.transform.IsChildOf(alvo) || col is TerrainCollider) continue;
                if (tremComJogador != null && col.transform.IsChildOf(tremComJogador)) continue; // dirigindo: o trem fica à vista
                if (casaAtual != null && col.transform.IsChildOf(casaAtual.transform)) continue;  // as paredes da casa ficam (o telhado já saiu)
                var raizObj = col.attachedRigidbody != null ? col.attachedRigidbody.transform : col.transform.parent != null && col.transform.parent.GetComponent<Collider>() == null && col.transform.parent.childCount < 12 ? col.transform.parent : col.transform;
                foreach (var r in raizObj.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || agora.Contains(r)) continue;
                    var b = r.bounds;
                    // copas de árvore e o que é de fora: com folga (perto da câmera elas tampam muito)
                    b.Expand(casaAtual != null ? folgaFora : -0.2f);
                    if (b.Contains(de) || (b.IntersectRay(raioVisao, out float t) && t < dist - 0.6f))
                        agora.Add(r);
                }
            }
        }

        // esconde os novos
        foreach (var r in agora)
            if (r != null && !escondidos.ContainsKey(r))
            {
                escondidos[r] = r.shadowCastingMode;
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

        // devolve os que não tampam mais
        if (escondidos.Count > agora.Count)
        {
            var volta = new List<Renderer>();
            foreach (var kv in escondidos) if (!agora.Contains(kv.Key)) volta.Add(kv.Key);
            foreach (var r in volta)
            {
                if (r != null) r.shadowCastingMode = escondidos[r];
                escondidos.Remove(r);
            }
        }
    }

    private void OnDisable()
    {
        foreach (var kv in escondidos) if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
        escondidos.Clear();
    }

    public int QuantosEscondidos => escondidos.Count;
}
