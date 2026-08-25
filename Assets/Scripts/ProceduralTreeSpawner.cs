using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera árvores automaticamente ao redor do jogador.
/// As árvores são criadas por chunks e destruídas quando ficam
/// muito distantes do jogador.
///
/// As árvores ficam sempre em pé, com rotação Y aleatória.
/// A altura é ajustada automaticamente ao Terrain.
/// </summary>
public class ProceduralTreeSpawner : MonoBehaviour
{
    // =========================================================
    // REFERÊNCIAS
    // =========================================================

    [Header("Referências")]
    [Tooltip("Jogador ou câmera que será usada como centro do sistema.")]
    public Transform player;

    [Tooltip("Terrain onde as árvores serão colocadas.")]
    public Terrain terrain;


    // =========================================================
    // PREFABS
    // =========================================================

    [Header("Prefabs de Árvores")]
    [Tooltip("Coloque aqui os prefabs das árvores que podem aparecer.")]
    public GameObject[] treePrefabs;


    // =========================================================
    // DISTÂNCIAS
    // =========================================================

    [Header("Distâncias")]

    [Tooltip("Raio ao redor do jogador onde as árvores serão criadas.")]
    public float spawnRadius = 100f;

    [Tooltip("Distância em que o chunk será destruído.")]
    public float despawnRadius = 140f;

    [Tooltip("Distância mínima entre árvores.")]
    public float minDistanceBetweenTrees = 6f;

    [Tooltip("Tamanho de cada chunk.")]
    public float chunkSize = 20f;


    // =========================================================
    // TERRENO
    // =========================================================

    [Header("Terreno")]

    [Tooltip("Inclinação máxima onde uma árvore pode nascer.")]
    [Range(0f, 90f)]
    public float maxSlopeAngle = 35f;


    // =========================================================
    // SPLINE / TREM
    // =========================================================

    [Header("Zona de Exclusão - Trem")]

    [Tooltip("Pontos do spline/trilho do trem.")]
    public Transform[] splinePoints;

    [Tooltip("Distância mínima entre árvores e o trilho.")]
    public float splineExclusionRadius = 10f;


    // =========================================================
    // ZONAS DE EXCLUSÃO
    // =========================================================

    [Header("Outras Zonas de Exclusão")]

    [Tooltip("Colliders onde árvores não podem nascer.")]
    public Collider[] exclusionZones;

    [Tooltip("Layers que impedem o nascimento de árvores.")]
    public LayerMask exclusionLayerMask;


    // =========================================================
    // DENSIDADE
    // =========================================================

    [Header("Densidade")]

    [Range(0f, 1f)]
    [Tooltip("Quantidade de árvores geradas.")]
    public float density = 0.5f;

    [Tooltip("Máximo de árvores por chunk quando a densidade é 1.")]
    public int maxTreesPerChunk = 15;


    // =========================================================
    // ROTAÇÃO
    // =========================================================

    [Header("Rotação das Árvores")]

    [Tooltip("Se ativado, todas as árvores ficam perfeitamente em pé.")]
    public bool manterArvoreEmPe = true;

    [Tooltip("Gira cada árvore aleatoriamente no eixo Y.")]
    public bool rotacaoYAleatoria = true;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]

    public bool desenharGizmos = true;


    // =========================================================
    // SISTEMA INTERNO
    // =========================================================

    private readonly Dictionary<Vector2Int, List<GameObject>> chunksAtivos =
        new Dictionary<Vector2Int, List<GameObject>>();

    private float checkTimer;

    private const float CHECK_INTERVAL = 0.5f;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        ValidarConfiguracao();
    }


    // =========================================================
    // UPDATE
    // =========================================================

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


    // =========================================================
    // VALIDAÇÃO
    // =========================================================

    private void ValidarConfiguracao()
    {
        if (player == null)
        {
            Debug.LogWarning(
                "[ProceduralTreeSpawner] Player não foi configurado.",
                this
            );
        }

        if (terrain == null)
        {
            Debug.LogWarning(
                "[ProceduralTreeSpawner] Terrain não foi configurado.",
                this
            );
        }

        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogWarning(
                "[ProceduralTreeSpawner] Nenhum prefab de árvore foi colocado.",
                this
            );
        }

        if (chunkSize <= 0f)
        {
            chunkSize = 20f;
        }

        if (despawnRadius < spawnRadius)
        {
            despawnRadius = spawnRadius + chunkSize;
        }
    }


    // =========================================================
    // ATUALIZAR CHUNKS
    // =========================================================

    private void AtualizarChunks()
    {
        if (player == null)
            return;

        Vector2Int chunkJogador =
            MundoParaChunk(player.position);

        int raioEmChunks =
            Mathf.CeilToInt(spawnRadius / chunkSize);


        // -----------------------------------------------------
        // CRIAR NOVOS CHUNKS
        // -----------------------------------------------------

        for (int x = -raioEmChunks; x <= raioEmChunks; x++)
        {
            for (int z = -raioEmChunks; z <= raioEmChunks; z++)
            {
                Vector2Int coord = new Vector2Int(
                    chunkJogador.x + x,
                    chunkJogador.y + z
                );

                if (chunksAtivos.ContainsKey(coord))
                    continue;

                Vector3 centro = ChunkParaMundo(coord);

                Vector2 distanciaXZ = new Vector2(
                    player.position.x - centro.x,
                    player.position.z - centro.z
                );

                if (distanciaXZ.magnitude <= spawnRadius)
                {
                    GerarChunk(coord);
                }
            }
        }


        // -----------------------------------------------------
        // DESTRUIR CHUNKS DISTANTES
        // -----------------------------------------------------

        List<Vector2Int> paraRemover = null;

        foreach (KeyValuePair<Vector2Int, List<GameObject>> kvp in chunksAtivos)
        {
            Vector3 centro = ChunkParaMundo(kvp.Key);

            Vector2 distanciaXZ = new Vector2(
                player.position.x - centro.x,
                player.position.z - centro.z
            );

            if (distanciaXZ.magnitude > despawnRadius)
            {
                foreach (GameObject tree in kvp.Value)
                {
                    if (tree != null)
                    {
                        Destroy(tree);
                    }
                }

                if (paraRemover == null)
                {
                    paraRemover = new List<Vector2Int>();
                }

                paraRemover.Add(kvp.Key);
            }
        }


        if (paraRemover != null)
        {
            foreach (Vector2Int coord in paraRemover)
            {
                chunksAtivos.Remove(coord);
            }
        }
    }


    // =========================================================
    // GERAR CHUNK
    // =========================================================

    private void GerarChunk(Vector2Int coord)
    {
        if (terrain == null)
            return;

        if (treePrefabs == null || treePrefabs.Length == 0)
            return;


        Vector3 centro = ChunkParaMundo(coord);

        List<GameObject> arvoresDoChunk =
            new List<GameObject>();

        List<Vector3> posicoesUsadas =
            new List<Vector3>();


        int alvo = Mathf.RoundToInt(
            maxTreesPerChunk * density
        );

        if (alvo <= 0)
        {
            chunksAtivos[coord] = arvoresDoChunk;
            return;
        }


        int tentativasMax = alvo * 10;

        int colocadas = 0;


        // -----------------------------------------------------
        // TENTAR CRIAR ÁRVORES
        // -----------------------------------------------------

        for (
            int i = 0;
            i < tentativasMax && colocadas < alvo;
            i++
        )
        {
            Vector3 candidato = centro + new Vector3(
                Random.Range(
                    -chunkSize * 0.5f,
                    chunkSize * 0.5f
                ),

                0f,

                Random.Range(
                    -chunkSize * 0.5f,
                    chunkSize * 0.5f
                )
            );


            Vector3 posFinal;
            Vector3 normal;


            if (!PosicaoValida(
                candidato,
                posicoesUsadas,
                out posFinal,
                out normal))
            {
                continue;
            }


            // -------------------------------------------------
            // ESCOLHER PREFAB
            // -------------------------------------------------

            GameObject prefab =
                treePrefabs[
                    Random.Range(
                        0,
                        treePrefabs.Length
                    )
                ];


            if (prefab == null)
                continue;


            // -------------------------------------------------
            // ROTAÇÃO
            // -------------------------------------------------

            Quaternion rotacaoFinal;


            if (manterArvoreEmPe)
            {
                // IMPORTANTE:
                // A árvore NÃO acompanha a inclinação
                // do terreno.
                //
                // Ela permanece perfeitamente vertical.

                if (rotacaoYAleatoria)
                {
                    rotacaoFinal = Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    );
                }
                else
                {
                    rotacaoFinal = Quaternion.identity;
                }
            }
            else
            {
                // Se quiser que a árvore acompanhe
                // a inclinação do terreno.

                Quaternion rotacaoTerreno =
                    Quaternion.FromToRotation(
                        Vector3.up,
                        normal
                    );

                Quaternion rotacaoY =
                    Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    );

                rotacaoFinal =
                    rotacaoTerreno * rotacaoY;
            }


            // -------------------------------------------------
            // INSTANCIAR
            // -------------------------------------------------

            GameObject arvore = Instantiate(
                prefab,
                posFinal,
                rotacaoFinal,
                transform
            );


            arvore.name =
                prefab.name + "_Procedural";


            arvoresDoChunk.Add(arvore);

            posicoesUsadas.Add(posFinal);

            colocadas++;
        }


        // -----------------------------------------------------
        // SALVAR CHUNK
        // -----------------------------------------------------

        chunksAtivos[coord] = arvoresDoChunk;
    }


    // =========================================================
    // VALIDAR POSIÇÃO
    // =========================================================

    private bool PosicaoValida(
        Vector3 posXZ,
        List<Vector3> existentes,
        out Vector3 posFinal,
        out Vector3 normal)
    {
        posFinal = Vector3.zero;
        normal = Vector3.up;


        if (terrain == null)
            return false;


        TerrainData terrainData =
            terrain.terrainData;


        if (terrainData == null)
            return false;


        // -----------------------------------------------------
        // COORDENADAS LOCAIS DO TERRAIN
        // -----------------------------------------------------

        Vector3 terrainLocal =
            posXZ - terrain.transform.position;


        float u =
            terrainLocal.x /
            terrainData.size.x;

        float v =
            terrainLocal.z /
            terrainData.size.z;


        // -----------------------------------------------------
        // FORA DO TERRAIN
        // -----------------------------------------------------

        if (u < 0f ||
            u > 1f ||
            v < 0f ||
            v > 1f)
        {
            return false;
        }


        // -----------------------------------------------------
        // ALTURA
        // -----------------------------------------------------

        float altura =
            terrain.SampleHeight(posXZ);


        posXZ.y =
            altura +
            terrain.transform.position.y;


        // -----------------------------------------------------
        // NORMAL
        // -----------------------------------------------------

        normal =
            terrainData.GetInterpolatedNormal(
                u,
                v
            );


        // -----------------------------------------------------
        // INCLINAÇÃO
        // -----------------------------------------------------

        float inclinacao =
            Vector3.Angle(
                normal,
                Vector3.up
            );


        if (inclinacao > maxSlopeAngle)
            return false;


        // -----------------------------------------------------
        // DISTÂNCIA ENTRE ÁRVORES
        // -----------------------------------------------------

        float distanciaMinimaSqr =
            minDistanceBetweenTrees *
            minDistanceBetweenTrees;


        foreach (Vector3 p in existentes)
        {
            Vector3 diferenca =
                p - posXZ;

            diferenca.y = 0f;

            if (diferenca.sqrMagnitude <
                distanciaMinimaSqr)
            {
                return false;
            }
        }


        // -----------------------------------------------------
        // DISTÂNCIA DO TREM
        // -----------------------------------------------------

        if (
            splinePoints != null &&
            splinePoints.Length > 1
        )
        {
            for (
                int i = 0;
                i < splinePoints.Length - 1;
                i++
            )
            {
                if (
                    splinePoints[i] == null ||
                    splinePoints[i + 1] == null
                )
                {
                    continue;
                }


                float distancia =
                    DistanciaPontoSegmento(
                        posXZ,
                        splinePoints[i].position,
                        splinePoints[i + 1].position
                    );


                if (
                    distancia <
                    splineExclusionRadius
                )
                {
                    return false;
                }
            }
        }


        // -----------------------------------------------------
        // ZONAS DE EXCLUSÃO
        // -----------------------------------------------------

        if (exclusionZones != null)
        {
            foreach (Collider col in exclusionZones)
            {
                if (col == null)
                    continue;


                if (col.bounds.Contains(posXZ))
                {
                    return false;
                }
            }
        }


        // -----------------------------------------------------
        // LAYER DE EXCLUSÃO
        // -----------------------------------------------------

        if (exclusionLayerMask.value != 0)
        {
            bool bloqueado =
                Physics.CheckSphere(
                    posXZ,
                    minDistanceBetweenTrees * 0.5f,
                    exclusionLayerMask
                );


            if (bloqueado)
            {
                return false;
            }
        }


        // -----------------------------------------------------
        // POSIÇÃO APROVADA
        // -----------------------------------------------------

        posFinal = posXZ;

        return true;
    }


    // =========================================================
    // DISTÂNCIA PONTO / SEGMENTO
    // =========================================================

    private static float DistanciaPontoSegmento(
        Vector3 p,
        Vector3 a,
        Vector3 b)
    {
        Vector3 ab = b - a;

        float sqrLen =
            ab.sqrMagnitude;


        if (sqrLen < 0.0001f)
        {
            return Vector3.Distance(
                p,
                a
            );
        }


        float t =
            Mathf.Clamp01(
                Vector3.Dot(
                    p - a,
                    ab
                ) / sqrLen
            );


        Vector3 projecao =
            a + t * ab;


        return Vector3.Distance(
            p,
            projecao
        );
    }


    // =========================================================
    // MUNDO → CHUNK
    // =========================================================

    private Vector2Int MundoParaChunk(
        Vector3 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(
                pos.x / chunkSize
            ),

            Mathf.FloorToInt(
                pos.z / chunkSize
            )
        );
    }


    // =========================================================
    // CHUNK → MUNDO
    // =========================================================

    private Vector3 ChunkParaMundo(
        Vector2Int coord)
    {
        return new Vector3(
            coord.x * chunkSize +
            chunkSize * 0.5f,

            0f,

            coord.y * chunkSize +
            chunkSize * 0.5f
        );
    }


    // =========================================================
    // LIMPAR TUDO
    // =========================================================

    public void LimparTodasAsArvores()
    {
        foreach (
            KeyValuePair<Vector2Int, List<GameObject>> kvp
            in chunksAtivos)
        {
            foreach (GameObject tree in kvp.Value)
            {
                if (tree != null)
                {
                    Destroy(tree);
                }
            }
        }


        chunksAtivos.Clear();
    }


    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmos)
            return;

        if (player == null)
            return;


        // -----------------------------------------------------
        // RAIO DE SPAWN
        // -----------------------------------------------------

        Gizmos.color =
            new Color(
                0f,
                1f,
                0f,
                0.3f
            );


        Gizmos.DrawWireSphere(
            player.position,
            spawnRadius
        );


        // -----------------------------------------------------
        // RAIO DE DESPAWN
        // -----------------------------------------------------

        Gizmos.color =
            new Color(
                1f,
                0f,
                0f,
                0.3f
            );


        Gizmos.DrawWireSphere(
            player.position,
            despawnRadius
        );


        // -----------------------------------------------------
        // SPLINE DO TREM
        // -----------------------------------------------------

        if (
            splinePoints != null &&
            splinePoints.Length > 1
        )
        {
            Gizmos.color =
                Color.yellow;


            for (
                int i = 0;
                i < splinePoints.Length - 1;
                i++
            )
            {
                if (
                    splinePoints[i] != null &&
                    splinePoints[i + 1] != null
                )
                {
                    Gizmos.DrawLine(
                        splinePoints[i].position,
                        splinePoints[i + 1].position
                    );
                }
            }
        }
    }
}