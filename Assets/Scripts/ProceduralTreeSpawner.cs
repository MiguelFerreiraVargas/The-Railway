using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class ProceduralVegetationSpawner : MonoBehaviour
{
    [System.Serializable]
    public class VegetationLayer
    {
        public string nome = "Nova Camada";
        public GameObject[] prefabs;

        [Range(0f, 1f)]
        public float density = 0.5f;

        public int maxPerChunkAtFullDensity = 15;
        public float minDistanceBetweenInstances = 6f;

        [Range(0f, 90f)]
        public float maxSlopeAngle = 35f;

        public bool manterEmPe = true;
        public bool rotacaoYAleatoria = true;

        public bool variarEscala = false;
        public float escalaMin = 0.85f;
        public float escalaMax = 1.15f;

        public float offsetY = 0f;

        [HideInInspector]
        public Dictionary<Vector2Int, List<GameObject>> chunksAtivos =
            new Dictionary<Vector2Int, List<GameObject>>();
    }

    [Header("Referências")]
    public Transform player;
    public Terrain terrain;

    [Header("Vegetação")]
    public List<VegetationLayer> camadas =
        new List<VegetationLayer>();

    [Header("Chunks")]
    public float spawnRadius = 100f;
    public float despawnRadius = 140f;
    public float chunkSize = 20f;

    [Header("Spline / Trilho")]
    public SplineContainer splineContainer;

    [Tooltip("Distância que ficará totalmente sem árvores ao redor do trilho.")]
    public float splineExclusionRadius = 10f;

    [Tooltip("Quantidade de segmentos usados para verificar o spline.")]
    [Range(50, 1000)]
    public int splineSamples = 500;

    [Header("Exclusão por Layer")]
    public LayerMask exclusionLayerMask;
    public Vector3 exclusionBoxSize =
        new Vector3(4f, 3f, 4f);

    [Header("Outras exclusões")]
    public Collider[] exclusionZones;

    private float checkTimer;
    private const float CHECK_INTERVAL = 0.5f;


    private void Start()
    {
        if (chunkSize <= 0f)
            chunkSize = 20f;

        if (despawnRadius < spawnRadius)
            despawnRadius = spawnRadius + chunkSize;

        if (splineSamples < 50)
            splineSamples = 50;
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


    private void AtualizarChunks()
    {
        Vector2Int chunkJogador =
            MundoParaChunk(player.position);

        int raio =
            Mathf.CeilToInt(spawnRadius / chunkSize);


        foreach (VegetationLayer camada in camadas)
        {
            if (camada == null ||
                camada.prefabs == null ||
                camada.prefabs.Length == 0)
                continue;


            for (int x = -raio; x <= raio; x++)
            {
                for (int z = -raio; z <= raio; z++)
                {
                    Vector2Int coord =
                        new Vector2Int(
                            chunkJogador.x + x,
                            chunkJogador.y + z
                        );


                    if (camada.chunksAtivos.ContainsKey(coord))
                        continue;


                    Vector3 centro =
                        ChunkParaMundo(coord);


                    Vector2 distancia =
                        new Vector2(
                            player.position.x - centro.x,
                            player.position.z - centro.z
                        );


                    if (distancia.magnitude <= spawnRadius)
                        GerarChunk(camada, coord);
                }
            }


            List<Vector2Int> remover =
                new List<Vector2Int>();


            foreach (var chunk in camada.chunksAtivos)
            {
                Vector3 centro =
                    ChunkParaMundo(chunk.Key);


                Vector2 distancia =
                    new Vector2(
                        player.position.x - centro.x,
                        player.position.z - centro.z
                    );


                if (distancia.magnitude > despawnRadius)
                {
                    foreach (GameObject obj in chunk.Value)
                    {
                        if (obj != null)
                            Destroy(obj);
                    }

                    remover.Add(chunk.Key);
                }
            }


            foreach (Vector2Int coord in remover)
                camada.chunksAtivos.Remove(coord);
        }
    }


    private void GerarChunk(
        VegetationLayer camada,
        Vector2Int coord)
    {
        if (terrain == null)
            return;


        Vector3 centro =
            ChunkParaMundo(coord);


        List<GameObject> objetos =
            new List<GameObject>();

        List<Vector3> posicoes =
            new List<Vector3>();


        int alvo =
            Mathf.RoundToInt(
                camada.maxPerChunkAtFullDensity *
                camada.density
            );


        if (alvo <= 0)
        {
            camada.chunksAtivos[coord] =
                objetos;

            return;
        }


        int tentativasMax =
            Mathf.Max(alvo * 20, 20);

        int criadas = 0;


        for (
            int i = 0;
            i < tentativasMax && criadas < alvo;
            i++)
        {
            Vector3 candidato =
                centro +
                new Vector3(
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
                camada,
                candidato,
                posicoes,
                out posFinal,
                out normal))
            {
                continue;
            }


            GameObject prefab =
                camada.prefabs[
                    Random.Range(
                        0,
                        camada.prefabs.Length
                    )
                ];


            if (prefab == null)
                continue;


            Quaternion rotacao;


            if (camada.manterEmPe)
            {
                rotacao =
                    camada.rotacaoYAleatoria
                    ?
                    Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    )
                    :
                    Quaternion.identity;
            }
            else
            {
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


                rotacao =
                    rotacaoTerreno *
                    rotacaoY;
            }


            posFinal.y += camada.offsetY;


            GameObject objeto =
                Instantiate(
                    prefab,
                    posFinal,
                    rotacao,
                    transform
                );


            objeto.name =
                prefab.name +
                "_" +
                camada.nome;


            if (camada.variarEscala)
            {
                float escala =
                    Random.Range(
                        camada.escalaMin,
                        camada.escalaMax
                    );

                objeto.transform.localScale *= escala;
            }


            objetos.Add(objeto);
            posicoes.Add(posFinal);

            criadas++;
        }


        camada.chunksAtivos[coord] =
            objetos;
    }


    private bool PosicaoValida(
        VegetationLayer camada,
        Vector3 pos,
        List<Vector3> existentes,
        out Vector3 posFinal,
        out Vector3 normal)
    {
        posFinal = Vector3.zero;
        normal = Vector3.up;


        if (terrain == null)
            return false;


        TerrainData data =
            terrain.terrainData;


        if (data == null)
            return false;


        Vector3 local =
            pos - terrain.transform.position;


        float u =
            local.x / data.size.x;

        float v =
            local.z / data.size.z;


        if (u < 0f ||
            u > 1f ||
            v < 0f ||
            v > 1f)
        {
            return false;
        }


        pos.y =
            terrain.SampleHeight(pos) +
            terrain.transform.position.y;


        normal =
            data.GetInterpolatedNormal(
                u,
                v
            );


        if (
            Vector3.Angle(
                normal,
                Vector3.up
            )
            >
            camada.maxSlopeAngle
        )
        {
            return false;
        }


        float distanciaMinima =
            camada.minDistanceBetweenInstances *
            camada.minDistanceBetweenInstances;


        foreach (Vector3 p in existentes)
        {
            Vector3 diferenca =
                p - pos;

            diferenca.y = 0f;


            if (
                diferenca.sqrMagnitude <
                distanciaMinima
            )
            {
                return false;
            }
        }


        if (
            splineContainer != null &&
            PertoDoSpline(pos)
        )
        {
            return false;
        }


        if (exclusionZones != null)
        {
            foreach (Collider col in exclusionZones)
            {
                if (
                    col != null &&
                    col.bounds.Contains(pos)
                )
                {
                    return false;
                }
            }
        }


        if (
            exclusionLayerMask.value != 0 &&
            Physics.CheckBox(
                pos,
                exclusionBoxSize * 0.5f,
                Quaternion.identity,
                exclusionLayerMask
            )
        )
        {
            return false;
        }


        posFinal = pos;

        return true;
    }


    private bool PertoDoSpline(Vector3 pos)
    {
        if (splineContainer == null)
            return false;


        Spline spline =
            splineContainer.Spline;


        if (spline == null)
            return false;


        float raio =
            splineExclusionRadius;


        if (raio <= 0f)
            return false;


        float raioSqr =
            raio * raio;


        Vector3 anterior =
            splineContainer.transform.TransformPoint(
                spline.EvaluatePosition(0f)
            );


        for (
            int i = 1;
            i <= splineSamples;
            i++)
        {
            float t =
                i / (float)splineSamples;


            Vector3 atual =
                splineContainer.transform.TransformPoint(
                    spline.EvaluatePosition(t)
                );


            if (
                DistanciaSegmentoXZ(
                    pos,
                    anterior,
                    atual
                )
                <= raioSqr
            )
            {
                return true;
            }


            anterior = atual;
        }


        return false;
    }


    private float DistanciaSegmentoXZ(
        Vector3 ponto,
        Vector3 a,
        Vector3 b)
    {
        Vector3 p =
            new Vector3(
                ponto.x,
                0f,
                ponto.z
            );


        Vector3 A =
            new Vector3(
                a.x,
                0f,
                a.z
            );


        Vector3 B =
            new Vector3(
                b.x,
                0f,
                b.z
            );


        Vector3 AB =
            B - A;


        float comprimento =
            AB.sqrMagnitude;


        if (comprimento < 0.0001f)
            return (p - A).sqrMagnitude;


        float t =
            Vector3.Dot(
                p - A,
                AB
            ) / comprimento;


        t =
            Mathf.Clamp01(t);


        Vector3 pontoMaisProximo =
            A + AB * t;


        return (
            p - pontoMaisProximo
        ).sqrMagnitude;
    }


    private Vector2Int MundoParaChunk(Vector3 pos)
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


    public void LimparTudo()
    {
        foreach (VegetationLayer camada in camadas)
        {
            foreach (var chunk in camada.chunksAtivos)
            {
                foreach (GameObject obj in chunk.Value)
                {
                    if (obj != null)
                        Destroy(obj);
                }
            }

            camada.chunksAtivos.Clear();
        }
    }
}