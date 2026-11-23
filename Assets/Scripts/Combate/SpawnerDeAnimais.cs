using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Põe bichos no mato em volta do Cândido (longe o bastante para não nascerem na cara dele):
/// cervos são comuns; lobos vêm em bando; urso é raro. Nada nasce dentro ou perto de casas,
/// nem nos primeiros minutos de jogo. Os que ficam longe somem sozinhos (Animal).
/// </summary>
public class SpawnerDeAnimais : MonoBehaviour
{
    [System.Serializable]
    public class Especie
    {
        public string nome = "cervo";
        public GameObject prefab;
        public float peso = 1f;
        public int bandoMin = 1, bandoMax = 1;
        public bool hostil;
    }

    public List<Especie> especies = new List<Especie>();
    public int maximoVivos = 6;
    public int maximoBandosHostis = 1;
    public float intervalo = 6f;
    public float distanciaMin = 50f, distanciaMax = 85f;
    [Tooltip("Segundos de jogo antes de aparecer bicho perigoso.")]
    public float folgaHostil = 120f;
    [Tooltip("Distância mínima de casas e estruturas.")]
    public float longeDeCasas = 25f;

    private float proximo;
    private float inicio;
    private ProceduralVegetationSpawner vegetacao;
    private readonly List<Bounds> casas = new List<Bounds>();

    private void Start()
    {
        inicio = Time.time;
        proximo = Time.time + 10f;
        vegetacao = FindFirstObjectByType<ProceduralVegetationSpawner>();
        foreach (var e in FindObjectsByType<Estrutura>(FindObjectsSortMode.None))
        {
            var rs = e.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            casas.Add(b);
        }
    }

    private void Update()
    {
        if (Time.time < proximo) return;
        proximo = Time.time + intervalo;
        var p = GameObject.FindWithTag("Player");
        if (p == null || Estrutura.AlgumaComJogador) return;
        if (PlayerStats.Instance != null && PlayerStats.Instance.Morto) return;

        int vivos = 0, bandosHostis = 0;
        var hostisContados = new HashSet<string>();
        foreach (var a in Animal.Todos)
        {
            if (a == null || !a.Vivo) continue;
            vivos++;
            if (a.comportamento != Animal.Comportamento.Passivo && hostisContados.Add(a.especie)) bandosHostis++;
        }
        if (vivos >= maximoVivos) return;

        bool podeHostil = Time.time - inicio > folgaHostil && bandosHostis < maximoBandosHostis;
        float total = 0f;
        foreach (var e in especies) if (e.prefab != null && (podeHostil || !e.hostil)) total += e.peso;
        if (total <= 0f) return;
        float sorte = Random.value * total;
        Especie escolhida = null;
        foreach (var e in especies)
        {
            if (e.prefab == null || (!podeHostil && e.hostil)) continue;
            if ((sorte -= e.peso) <= 0f) { escolhida = e; break; }
        }
        if (escolhida == null) return;

        if (!AcharLugar(p.transform.position, out Vector3 onde)) return;
        int n = Random.Range(escolhida.bandoMin, escolhida.bandoMax + 1);
        for (int i = 0; i < n && vivos < maximoVivos + 2; i++)
        {
            var off = Random.insideUnitCircle * 3f;
            var pos = onde + new Vector3(off.x, 0f, off.y);
            var ter = Terrain.activeTerrain;
            if (ter != null) pos.y = ter.SampleHeight(pos) + ter.transform.position.y;
            var go = Instantiate(escolhida.prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            go.name = escolhida.nome;
            go.SetActive(true);
            vivos++;
        }
    }

    /// <summary>Põe um bicho num ponto (eventos da ferrovia usam).</summary>
    public GameObject Criar(string especie, Vector3 pos, Quaternion rot)
    {
        var e = especies.Find(x => x.nome == especie);
        if (e == null || e.prefab == null) return null;
        var go = Instantiate(e.prefab, pos, rot, transform);
        go.name = e.nome;
        go.SetActive(true);
        return go;
    }

    private bool AcharLugar(Vector3 jogador, out Vector3 onde)
    {
        var ter = Terrain.activeTerrain;
        onde = jogador;
        for (int tent = 0; tent < 12; tent++)
        {
            var dir = Random.insideUnitCircle.normalized;
            var p = jogador + new Vector3(dir.x, 0f, dir.y) * Random.Range(distanciaMin, distanciaMax);
            if (ter != null)
            {
                var tp = ter.transform.position; var ts = ter.terrainData.size;
                if (p.x < tp.x + 5f || p.z < tp.z + 5f || p.x > tp.x + ts.x - 5f || p.z > tp.z + ts.z - 5f) continue;
                p.y = ter.SampleHeight(p) + tp.y;
            }
            bool perto = false;
            foreach (var b in casas)
            {
                var bb = b; bb.Expand(longeDeCasas * 2f);
                if (bb.Contains(new Vector3(p.x, b.center.y, p.z))) { perto = true; break; }
            }
            if (perto) continue;
            onde = p;
            return true;
        }
        return false;
    }
}
