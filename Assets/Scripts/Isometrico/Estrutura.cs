using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uma estrutura em que se entra (casa, galpão, vagão...). Enquanto o Cândido estiver com o piso
/// dela embaixo dos pés, o jogo fica em primeira pessoa; ao sair, volta para a isométrica.
/// </summary>
public class Estrutura : MonoBehaviour
{
    [Tooltip("Colisores do piso interno. O jogador está dentro quando um deles está logo abaixo dele.")]
    public Collider[] pisos;
    [Tooltip("Distância (m) do centro da estrutura a partir da qual nem verifica.")]
    public float raioVerificacao = 12f;

    public bool JogadorDentro { get; private set; }
    /// <summary>O piso em que o jogador está pisando (andar atual), ou null.</summary>
    public Collider PisoAtual { get; private set; }

    private static readonly List<Estrutura> todas = new List<Estrutura>();
    private Transform jogador;

    /// <summary>Se o jogador está dentro de qualquer estrutura.</summary>
    public static bool AlgumaComJogador
    {
        get
        {
            foreach (var e in todas) if (e != null && e.JogadorDentro) return true;
            return false;
        }
    }

    /// <summary>A estrutura em que o jogador está agora (ou null).</summary>
    public static Estrutura ComJogador
    {
        get
        {
            foreach (var e in todas) if (e != null && e.JogadorDentro) return e;
            return null;
        }
    }

    private void OnEnable() => todas.Add(this);

    private void OnDisable()
    {
        todas.Remove(this);
        JogadorDentro = false;
    }

    private void Update()
    {
        if (jogador == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            jogador = p.transform;
        }

        bool dentro = false;
        Collider pisando = null;
        if ((jogador.position - transform.position).sqrMagnitude < raioVerificacao * raioVerificacao)
        {
            var origem = jogador.position + Vector3.up * 0.8f;
            var hits = Physics.RaycastAll(origem, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            float maisPerto = float.MaxValue;
            Collider primeiro = null;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(jogador)) continue;
                if (h.distance < maisPerto) { maisPerto = h.distance; primeiro = h.collider; }
            }
            if (primeiro != null)
                foreach (var piso in pisos) if (piso == primeiro) { dentro = true; pisando = piso; break; }
        }

        JogadorDentro = dentro;
        // na escada (rampa) continua valendo o andar de onde veio, até pisar no outro
        if (pisando != null && !pisando.name.StartsWith("Rampa")) PisoAtual = pisando;
        else if (!dentro) PisoAtual = null;
        else if (PisoAtual == null) PisoAtual = pisando;
    }
}
