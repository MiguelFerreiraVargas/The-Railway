using UnityEngine;

/// <summary>
/// Pinta automaticamente as Terrain Layers do Terrain nativo da Unity,
/// baseado em ALTURA e INCLINAÇÃO — sem precisar pintar manualmente.
///
/// Funciona com o shader nativo "Universal Render Pipeline/Terrain/Lit",
/// então a iluminação (Point Light, Spot Light, etc.) continua funcionando
/// normalmente, já que não estamos trocando o shader do Terrain.
///
/// COMO USAR:
/// 1. Crie suas 6 Terrain Layers no Terrain (Edit Terrain Layers > Create Layer):
///    posição 0 = Terra, 1/2/3 = Grama A/B/C, 4/5 = Pedra A/B
///    (se a ordem for diferente, ajuste os índices abaixo no Inspector)
/// 2. Arraste este script pro mesmo objeto do Terrain (ou qualquer objeto).
/// 3. Arraste o Terrain no campo "Terrain".
/// 4. Ajuste os valores de altura/inclinação como quiser.
/// 5. Clique com o botão direito no cabeçalho do componente (nos 3 pontinhos)
///    e escolha "Pintar Terreno Automaticamente".
///
/// ATENÇÃO: isso APAGA qualquer pintura manual que já exista no Terrain.
/// </summary>
public class AutoTerrainPainter : MonoBehaviour
{
    [Header("Referência")]
    public Terrain terrain;

    [Header("Índices das Terrain Layers (na ordem que você criou)")]
    [Tooltip("Índice da layer de Terra na lista de Terrain Layers do Terrain.")]
    public int indiceTerra = 0;

    [Tooltip("Índices das 3 layers de Grama.")]
    public int[] indicesGrama = new int[] { 1, 2, 3 };

    [Tooltip("Índices das 2 layers de Pedra.")]
    public int[] indicesPedra = new int[] { 4, 5 };

    [Header("Altura (relativa à base do Terrain, em unidades de mundo)")]
    [Tooltip("Até essa altura, predomina Terra.")]
    public float alturaMaximaTerra = 5f;

    [Tooltip("Suavização da transição Terra -> Grama.")]
    public float suavizacaoTerraGrama = 3f;

    [Tooltip("Acima dessa altura, predomina Pedra (topo de montanhas).")]
    public float alturaMaximaGrama = 60f;

    [Tooltip("Suavização da transição Grama -> Pedra (por altura).")]
    public float suavizacaoGramaPedra = 8f;

    [Header("Inclinação (em graus, 0 = plano, 90 = parede vertical)")]
    [Tooltip("A partir desse ângulo, a Pedra passa a predominar independente da altura.")]
    [Range(0f, 90f)]
    public float limiteInclinacaoPedra = 30f;

    [Tooltip("Suavização da transição por inclinação.")]
    [Range(0.1f, 45f)]
    public float suavizacaoInclinacao = 10f;

    [Header("Variação (ruído) entre as 3 gramas e as 2 pedras")]
    [Tooltip("Quanto menor, maiores as manchas de cada variante de grama.")]
    public float escalaRuidoGrama = 0.02f;

    [Tooltip("Quanto menor, maiores as manchas de cada variante de pedra.")]
    public float escalaRuidoPedra = 0.015f;


    [ContextMenu("Pintar Terreno Automaticamente")]
    public void PintarTerrenoAutomaticamente()
    {
        if (terrain == null)
        {
            Debug.LogError("[AutoTerrainPainter] Nenhum Terrain foi configurado.", this);
            return;
        }

        TerrainData terrainData = terrain.terrainData;

        if (terrainData == null)
        {
            Debug.LogError("[AutoTerrainPainter] O Terrain não tem TerrainData.", this);
            return;
        }

        int numLayers = terrainData.terrainLayers.Length;

        if (numLayers < 6)
        {
            Debug.LogError(
                "[AutoTerrainPainter] Esse Terrain tem apenas " + numLayers +
                " Terrain Layers. Configure as 6 layers (Terra, 3x Grama, 2x Pedra) antes de pintar.",
                this
            );
            return;
        }

        int alphaWidth = terrainData.alphamapWidth;
        int alphaHeight = terrainData.alphamapHeight;

        float[,,] mapaAlpha = new float[alphaHeight, alphaWidth, numLayers];

        Vector3 tamanhoTerreno = terrainData.size;
        Vector3 posicaoTerreno = terrain.transform.position;

        // Usados só pra debug, pra você conferir se os valores de altura fazem sentido
        float menorAlturaEncontrada = float.MaxValue;
        float maiorAlturaEncontrada = float.MinValue;

        for (int y = 0; y < alphaHeight; y++)
        {
            for (int x = 0; x < alphaWidth; x++)
            {
                float normX = (float)x / (alphaWidth - 1);
                float normZ = (float)y / (alphaHeight - 1);

                // ---- Altura relativa ao Terrain (0 até size.y) ----
                float altura = terrainData.GetInterpolatedHeight(normX, normZ);

                if (altura < menorAlturaEncontrada) menorAlturaEncontrada = altura;
                if (altura > maiorAlturaEncontrada) maiorAlturaEncontrada = altura;

                // ---- Inclinação em graus (0 = plano, 90 = vertical) ----
                float inclinacaoGraus = terrainData.GetSteepness(normX, normZ);

                // ---- Posição real no mundo, usada só pro ruído (variedade visual) ----
                float mundoX = posicaoTerreno.x + normX * tamanhoTerreno.x;
                float mundoZ = posicaoTerreno.z + normZ * tamanhoTerreno.z;

                // =========================================================
                // PESOS BASE POR ALTURA
                // =========================================================

                float pesoTerra = 1f - SuavePasso(
                    alturaMaximaTerra - suavizacaoTerraGrama,
                    alturaMaximaTerra + suavizacaoTerraGrama,
                    altura
                );

                float pesoGramaPorAltura = SuavePasso(
                    alturaMaximaTerra - suavizacaoTerraGrama,
                    alturaMaximaTerra + suavizacaoTerraGrama,
                    altura
                ) * (1f - SuavePasso(
                    alturaMaximaGrama - suavizacaoGramaPedra,
                    alturaMaximaGrama + suavizacaoGramaPedra,
                    altura
                ));

                float pesoPedraPorAltura = SuavePasso(
                    alturaMaximaGrama - suavizacaoGramaPedra,
                    alturaMaximaGrama + suavizacaoGramaPedra,
                    altura
                );

                // =========================================================
                // INCLINAÇÃO: força Pedra em áreas íngremes, não importa a altura
                // =========================================================

                float pesoInclinacaoPedra = SuavePasso(
                    limiteInclinacaoPedra - suavizacaoInclinacao,
                    limiteInclinacaoPedra + suavizacaoInclinacao,
                    inclinacaoGraus
                );

                float pesoTerraFinal = pesoTerra * (1f - pesoInclinacaoPedra);
                float pesoGramaFinal = pesoGramaPorAltura * (1f - pesoInclinacaoPedra);
                float pesoPedraFinal = pesoPedraPorAltura + pesoInclinacaoPedra * (1f - pesoPedraPorAltura);

                // =========================================================
                // NORMALIZAR (soma tem que dar 1)
                // =========================================================

                float somaTotal = pesoTerraFinal + pesoGramaFinal + pesoPedraFinal;
                if (somaTotal <= 0.0001f) somaTotal = 1f;

                pesoTerraFinal /= somaTotal;
                pesoGramaFinal /= somaTotal;
                pesoPedraFinal /= somaTotal;

                // =========================================================
                // DISTRIBUIR ENTRE AS 3 VARIANTES DE GRAMA (ruído)
                // =========================================================

                float ruidoGrama = RuidoSuave(mundoX * escalaRuidoGrama, mundoZ * escalaRuidoGrama);

                float pesoGramaA = pesoGramaFinal * (1f - SuavePasso(0.25f, 0.5f, ruidoGrama));
                float pesoGramaB = pesoGramaFinal * (
                    SuavePasso(0.25f, 0.5f, ruidoGrama) * (1f - SuavePasso(0.6f, 0.85f, ruidoGrama))
                );
                float pesoGramaC = pesoGramaFinal * SuavePasso(0.6f, 0.85f, ruidoGrama);

                // =========================================================
                // DISTRIBUIR ENTRE AS 2 VARIANTES DE PEDRA (ruído)
                // =========================================================

                float ruidoPedra = RuidoSuave(mundoX * escalaRuidoPedra + 100f, mundoZ * escalaRuidoPedra + 100f);

                float pesoPedraA = pesoPedraFinal * (1f - SuavePasso(0.4f, 0.6f, ruidoPedra));
                float pesoPedraB = pesoPedraFinal * SuavePasso(0.4f, 0.6f, ruidoPedra);

                // =========================================================
                // ESCREVER NO MAPA DE ALPHA
                // =========================================================

                mapaAlpha[y, x, indiceTerra] = pesoTerraFinal;

                mapaAlpha[y, x, indicesGrama[0]] = pesoGramaA;
                mapaAlpha[y, x, indicesGrama[1]] = pesoGramaB;
                mapaAlpha[y, x, indicesGrama[2]] = pesoGramaC;

                mapaAlpha[y, x, indicesPedra[0]] = pesoPedraA;
                mapaAlpha[y, x, indicesPedra[1]] = pesoPedraB;
            }
        }

        terrainData.SetAlphamaps(0, 0, mapaAlpha);

        // Força o Terrain a redesenhar imediatamente (às vezes o Unity não atualiza sozinho no Editor)
        terrain.Flush();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(terrainData);
        UnityEditor.SceneView.RepaintAll();
#endif

        Debug.Log(
            "[AutoTerrainPainter] Terreno pintado com sucesso. " +
            "Altura mínima encontrada: " + menorAlturaEncontrada +
            " | Altura máxima encontrada: " + maiorAlturaEncontrada +
            " (compare esses valores com 'Altura Maxima Terra' e 'Altura Maxima Grama' no Inspector — " +
            "se sua Altura Maxima Grama estiver muito acima da altura máxima real do terreno, tudo vira Terra/Grama e a Pedra nunca aparece).",
            this
        );
    }


    // =========================================================
    // FUNÇÕES AUXILIARES
    // =========================================================

    private static float SuavePasso(float min, float max, float valor)
    {
        if (Mathf.Approximately(min, max))
            return valor < min ? 0f : 1f;

        float t = Mathf.Clamp01((valor - min) / (max - min));
        return t * t * (3f - 2f * t);
    }

    private static float RuidoSuave(float x, float z)
    {
        return Mathf.PerlinNoise(x, z);
    }
}