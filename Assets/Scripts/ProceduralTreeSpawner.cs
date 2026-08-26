using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera vegetação (árvores, grama, etc.) automaticamente ao redor do jogador.
/// Cada tipo de vegetação é uma "camada" (VegetationLayer) com densidade,
/// prefabs e regras próprias — assim dá pra ter grama bem densa e árvores
/// mais espaçadas ao mesmo tempo.
///
/// A vegetação é criada por chunks e destruída quando fica muito distante
/// do jogador.
///
/// EXCLUSÃO DE TRILHOS / TILES:
/// A checagem contra objetos do cenário (trilhos, pisos, etc.) agora usa uma
/// CAIXA (Physics.OverlapBox) do tamanho que você configurar, testada contra
/// uma Layer. Isso funciona mesmo se os trilhos forem gerados em tempo real
/// (tiling procedural), porque a checagem acontece no momento do spawn,
/// não depende de arrastar objetos fixos no Inspector.
///
/// CONFIGURAÇÃO NECESSÁRIA:
/// 1. Crie uma Layer nova (ex: "TrackTiles") em Edit > Project Settings > Tags and Layers.
/// 2. Coloque essa Layer em TODOS os prefabs/objetos dos trilhos e peças de chão que tiram (com collider).
/// 3. Nesse componente, em "Exclusão por Layer", marque essa Layer no campo Exclusion Layer Mask.
/// 4. Ajuste o Exclusion Box Size para cobrir a largura/altura real do trilho.
/// </summary>
public class ProceduralVegetationSpawner : MonoBehaviour
{
    // =========================================================
    // TIPOS
    // =========================================================

    [System.Serializable]
    public class VegetationLayer
    {
        [Tooltip("Nome só pra identificar no Inspector (ex: Árvores, Grama).")]
        public string nome = "Nova Camada";

        [Tooltip("Prefabs que podem aparecer para essa camada.")]
        public GameObject[] prefabs;

        [Range(0f, 1f)]
        [Tooltip("Densidade dessa camada (0 = nada, 1 = máximo).")]
        public float density = 0.5f;

        [Tooltip("Máximo de instâncias por chunk quando density = 1.")]
        public int maxPerChunkAtFullDensity = 15;

        [Tooltip("Distância mínima entre instâncias DESSA camada.")]
        public float minDistanceBetweenInstances = 6f;

        [Tooltip("Inclinação máxima do terreno onde pode nascer.")]
        [Range(0f, 90f)]
        public float maxSlopeAngle = 35f;

        [Header("Rotação")]
        public bool manterEmPe = true;
        public bool rotacaoYAleatoria = true;

        [Header("Variação de escala (deixa mais natural, ótimo pra grama)")]
        public bool variarEscala = false;
        public float escalaMin = 0.85f;
        public float escalaMax = 1.15f;

        [Header("Offset vertical (afunda/levanta um pouco em relação ao chão)")]
        public float offsetY = 0f;

        // Contador interno de quantas instâncias existem em cada chunk (não editar)
        [HideInInspector]
        public Dictionary<Vector2Int, List<GameObject>> chunksAtivos =
            new Dictionary<Vector2Int, List<GameObject>>();
    }


    // =========================================================
    // REFERÊNCIAS
    // =========================================================

    [Header("Referências")]
    [Tooltip("Jogador ou câmera que será usada como centro do sistema.")]
    public Transform player;

    [Tooltip("Terrain onde a vegetação será colocada.")]
    public Terrain terrain;


    // =========================================================
    // CAMADAS DE VEGETAÇÃO
    // =========================================================

    [Header("Camadas de Vegetação")]
    [Tooltip("Cada camada tem sua própria densidade e prefabs. Ex: uma camada 'Árvores' e outra 'Grama'.")]
    public List<VegetationLayer> camadas = new List<VegetationLayer>();


    // =========================================================
    // DISTÂNCIAS / CHUNKS
    // =========================================================

    [Header("Distâncias")]
    [Tooltip("Raio ao redor do jogador onde a vegetação será criada.")]
    public float spawnRadius = 100f;

    [Tooltip("Distância em que o chunk será destruído.")]
    public float despawnRadius = 140f;

    [Tooltip("Tamanho de cada chunk.")]
    public float chunkSize = 20f;


    // =========================================================
    // SPLINE / TREM (zona de exclusão por linha)
    // =========================================================

    [Header("Zona de Exclusão - Trilho (spline)")]
    [Tooltip("Pontos do spline/trilho do trem. Quanto mais pontos, mais preciso em curvas.")]
    public Transform[] splinePoints;

    [Tooltip("Distância mínima entre vegetação e o trilho.")]
    public float splineExclusionRadius = 10f;


    // =========================================================
    // EXCLUSÃO POR LAYER (resolve trilhos/tiles gerados em runtime)
    // =========================================================

    [Header("Exclusão por Layer (recomendado para tiles/trilhos procedurais)")]
    [Tooltip("Layers que impedem o nascimento de vegetação (ex: trilhos, plataformas).")]
    public LayerMask exclusionLayerMask;

    [Tooltip("Tamanho da caixa de checagem (largura, altura, profundidade). Ajuste para cobrir o trilho/tile real.")]
    public Vector3 exclusionBoxSize = new Vector3(4f, 3f, 4f);


    // =========================================================
    // OUTRAS ZONAS DE EXCLUSÃO (colliders fixos, opcional)
    // =========================================================

    [Header("Outras Zonas de Exclusão (objetos fixos na cena)")]
    [Tooltip("Colliders fixos onde vegetação não pode nascer. Não use isso para tiles gerados em runtime — use a Exclusão por Layer acima.")]
    public Collider[] exclusionZones;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    public bool desenharGizmos = true;


    // =========================================================
    // INTERNO
    // =========================================================

    private float checkTimer;
    private const float CHECK_INTERVAL = 0.5f;


    private void Start()
    {
        ValidarConfiguracao();
    }


    private void Update()
    {
        if (player == null)
            return;

        checkTimer -= Time.deltaTime;

        if (checkTimer <= 0f)
        {
            checkTimer = CHECK_INTERVAL;
            AtualizarChunks();
        }
    }


    private void ValidarConfiguracao()
    {
        if (player == null)
            Debug.LogWarning("[ProceduralVegetationSpawner] Player não foi configurado.", this);

        if (terrain == null)
            Debug.LogWarning("[ProceduralVegetationSpawner] Terrain não foi configurado.", this);

        if (camadas == null || camadas.Count == 0)
            Debug.LogWarning("[ProceduralVegetationSpawner] Nenhuma camada de vegetação configurada.", this);

        if (chunkSize <= 0f)
            chunkSize = 20f;

        if (despawnRadius < spawnRadius)
            despawnRadius = spawnRadius + chunkSize;
    }


    // =========================================================
    // ATUALIZAR CHUNKS (agora processa todas as camadas)
    // =========================================================

    private void AtualizarChunks()
    {
        if (player == null)
            return;

        Vector2Int chunkJogador = MundoParaChunk(player.position);
        int raioEmChunks = Mathf.CeilToInt(spawnRadius / chunkSize);

        for (int i = 0; i < camadas.Count; i++)
        {
            VegetationLayer camada = camadas[i];

            if (camada == null || camada.prefabs == null || camada.prefabs.Length == 0)
                continue;

            // ---- Criar novos chunks para essa camada ----
            for (int x = -raioEmChunks; x <= raioEmChunks; x++)
            {
                for (int z = -raioEmChunks; z <= raioEmChunks; z++)
                {
                    Vector2Int coord = new Vector2Int(chunkJogador.x + x, chunkJogador.y + z);

                    if (camada.chunksAtivos.ContainsKey(coord))
                        continue;

                    Vector3 centro = ChunkParaMundo(coord);
                    Vector2 distanciaXZ = new Vector2(player.position.x - centro.x, player.position.z - centro.z);

                    if (distanciaXZ.magnitude <= spawnRadius)
                    {
                        GerarChunk(camada, coord);
                    }
                }
            }

            // ---- Destruir chunks distantes dessa camada ----
            List<Vector2Int> paraRemover = null;

            foreach (KeyValuePair<Vector2Int, List<GameObject>> kvp in camada.chunksAtivos)
            {
                Vector3 centro = ChunkParaMundo(kvp.Key);
                Vector2 distanciaXZ = new Vector2(player.position.x - centro.x, player.position.z - centro.z);

                if (distanciaXZ.magnitude > despawnRadius)
                {
                    foreach (GameObject obj in kvp.Value)
                    {
                        if (obj != null) Destroy(obj);
                    }

                    if (paraRemover == null) paraRemover = new List<Vector2Int>();
                    paraRemover.Add(kvp.Key);
                }
            }

            if (paraRemover != null)
            {
                foreach (Vector2Int coord in paraRemover)
                    camada.chunksAtivos.Remove(coord);
            }
        }
    }


    // =========================================================
    // GERAR CHUNK PARA UMA CAMADA ESPECÍFICA
    // =========================================================

    private void GerarChunk(VegetationLayer camada, Vector2Int coord)
    {
        if (terrain == null)
            return;

        Vector3 centro = ChunkParaMundo(coord);

        List<GameObject> instanciasDoChunk = new List<GameObject>();
        List<Vector3> posicoesUsadas = new List<Vector3>();

        int alvo = Mathf.RoundToInt(camada.maxPerChunkAtFullDensity * camada.density);

        if (alvo <= 0)
        {
            camada.chunksAtivos[coord] = instanciasDoChunk;
            return;
        }

        int tentativasMax = alvo * 10;
        int colocadas = 0;

        for (int i = 0; i < tentativasMax && colocadas < alvo; i++)
        {
            Vector3 candidato = centro + new Vector3(
                Random.Range(-chunkSize * 0.5f, chunkSize * 0.5f),
                0f,
                Random.Range(-chunkSize * 0.5f, chunkSize * 0.5f)
            );

            Vector3 posFinal;
            Vector3 normal;

            if (!PosicaoValida(camada, candidato, posicoesUsadas, out posFinal, out normal))
                continue;

            GameObject prefab = camada.prefabs[Random.Range(0, camada.prefabs.Length)];
            if (prefab == null) continue;

            // ---- Rotação ----
            Quaternion rotacaoFinal;

            if (camada.manterEmPe)
            {
                rotacaoFinal = camada.rotacaoYAleatoria
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : Quaternion.identity;
            }
            else
            {
                Quaternion rotacaoTerreno = Quaternion.FromToRotation(Vector3.up, normal);
                Quaternion rotacaoY = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                rotacaoFinal = rotacaoTerreno * rotacaoY;
            }

            posFinal.y += camada.offsetY;

            // ---- Instanciar ----
            GameObject instancia = Instantiate(prefab, posFinal, rotacaoFinal, transform);
            instancia.name = prefab.name + "_" + camada.nome;

            // ---- Variação de escala ----
            if (camada.variarEscala)
            {
                float escala = Random.Range(camada.escalaMin, camada.escalaMax);
                instancia.transform.localScale *= escala;
            }

            instanciasDoChunk.Add(instancia);
            posicoesUsadas.Add(posFinal);
            colocadas++;
        }

        camada.chunksAtivos[coord] = instanciasDoChunk;
    }


    // =========================================================
    // VALIDAR POSIÇÃO
    // =========================================================

    private bool PosicaoValida(
        VegetationLayer camada,
        Vector3 posXZ,
        List<Vector3> existentes,
        out Vector3 posFinal,
        out Vector3 normal)
    {
        posFinal = Vector3.zero;
        normal = Vector3.up;

        if (terrain == null)
            return false;

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
            return false;

        Vector3 terrainLocal = posXZ - terrain.transform.position;
        float u = terrainLocal.x / terrainData.size.x;
        float v = terrainLocal.z / terrainData.size.z;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        float altura = terrain.SampleHeight(posXZ);
        posXZ.y = altura + terrain.transform.position.y;

        normal = terrainData.GetInterpolatedNormal(u, v);

        float inclinacao = Vector3.Angle(normal, Vector3.up);
        if (inclinacao > camada.maxSlopeAngle)
            return false;

        // ---- Distância mínima entre instâncias da mesma camada ----
        float distanciaMinimaSqr = camada.minDistanceBetweenInstances * camada.minDistanceBetweenInstances;

        foreach (Vector3 p in existentes)
        {
            Vector3 diferenca = p - posXZ;
            diferenca.y = 0f;

            if (diferenca.sqrMagnitude < distanciaMinimaSqr)
                return false;
        }

        // ---- Distância do trilho (spline) ----
        if (splinePoints != null && splinePoints.Length > 1)
        {
            for (int i = 0; i < splinePoints.Length - 1; i++)
            {
                if (splinePoints[i] == null || splinePoints[i + 1] == null)
                    continue;

                float distancia = DistanciaPontoSegmento(posXZ, splinePoints[i].position, splinePoints[i + 1].position);

                if (distancia < splineExclusionRadius)
                    return false;
            }
        }

        // ---- Zonas de exclusão fixas (colliders arrastados no Inspector) ----
        if (exclusionZones != null)
        {
            foreach (Collider col in exclusionZones)
            {
                if (col == null) continue;
                if (col.bounds.Contains(posXZ)) return false;
            }
        }

        // ---- Exclusão por Layer usando CAIXA (resolve trilhos/tiles procedurais) ----
        if (exclusionLayerMask.value != 0)
        {
            bool bloqueado = Physics.CheckBox(
                posXZ,
                exclusionBoxSize * 0.5f,
                Quaternion.identity,
                exclusionLayerMask
            );

            if (bloqueado)
                return false;
        }

        posFinal = posXZ;
        return true;
    }


    private static float DistanciaPontoSegmento(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float sqrLen = ab.sqrMagnitude;

        if (sqrLen < 0.0001f)
            return Vector3.Distance(p, a);

        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / sqrLen);
        Vector3 projecao = a + t * ab;

        return Vector3.Distance(p, projecao);
    }


    private Vector2Int MundoParaChunk(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize)
        );
    }


    private Vector3 ChunkParaMundo(Vector2Int coord)
    {
        return new Vector3(
            coord.x * chunkSize + chunkSize * 0.5f,
            0f,
            coord.y * chunkSize + chunkSize * 0.5f
        );
    }


    // =========================================================
    // LIMPAR TUDO (todas as camadas)
    // =========================================================

    public void LimparTudo()
    {
        foreach (VegetationLayer camada in camadas)
        {
            foreach (KeyValuePair<Vector2Int, List<GameObject>> kvp in camada.chunksAtivos)
            {
                foreach (GameObject obj in kvp.Value)
                {
                    if (obj != null) Destroy(obj);
                }
            }

            camada.chunksAtivos.Clear();
        }
    }


    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmos || player == null)
            return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(player.position, spawnRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(player.position, despawnRadius);

        if (splinePoints != null && splinePoints.Length > 1)
        {
            Gizmos.color = Color.yellow;

            for (int i = 0; i < splinePoints.Length - 1; i++)
            {
                if (splinePoints[i] != null && splinePoints[i + 1] != null)
                {
                    Gizmos.DrawLine(splinePoints[i].position, splinePoints[i + 1].position);
                }
            }
        }
    }
}