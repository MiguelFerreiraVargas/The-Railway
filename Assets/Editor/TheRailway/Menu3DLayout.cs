using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Posições e valores da cena 3D do menu, em metros. Para testar outros valores
/// sem recompilar, crie Temp/TheRailway/layout.json só com os campos a mudar.
/// O trilho corre no eixo X, em z = 0; a plataforma fica do lado -Z.
/// </summary>
[Serializable]
public class Menu3DLayout
{
    public int seed = 23;

    // Céu e ambiente
    public string skybox = "Assets/AllSkyFree/Cold Sunset/Cold Sunset.mat";
    public Color fogColor = new Color(0.3f, 0.22f, 0.32f);
    public float fogDensity = 0.014f;
    public Color ambientSky = new Color(0.42f, 0.36f, 0.55f);
    public Color ambientEquator = new Color(0.32f, 0.24f, 0.3f);
    public Color ambientGround = new Color(0.06f, 0.05f, 0.06f);
    public Color moonColor = new Color(0.62f, 0.6f, 0.95f);
    public float moonIntensity = 1.3f;
    public Vector3 moonEuler = new Vector3(14f, 250f, 0f);

    // Chão e trilho
    public float groundSize = 500f;
    public float trackFrom = -140f;
    public float trackTo = 260f;

    // Trem (comprimentos alvo em metros)
    public float locoLength = 11f;
    public float locoX = 9f;
    [Tooltip("Inverte a locomotiva se a frente ficar virada para o lado errado.")]
    public bool locoFlip = true;
    public float trainScale = 1f;
    public float wagonLength = 9f;
    public float wagonGap = 0.6f;
    public float chimneyFromFront = 0.2f; // fração do comprimento, a partir da frente
    public float smokeScale = 1.6f;
    public float headlightIntensity = 60f;
    public float headlightRange = 55f;

    // Plataforma
    public float platformFrom = -45f;
    public float platformTo = 30f;
    public float platformWidth = 4.5f;
    public float platformHeight = 0.9f;
    public float platformGap = 1.6f;   // distância do centro do trilho até a borda
    public float lampSpacing = 11f;
    public float lampHeight = 5f;
    public float lampIntensity = 13f;
    public float lampRange = 13f;

    // Casa da estação
    public bool useHouse = true;
    public Vector3 housePos = new Vector3(-4f, 0f, -14f);
    public float houseYaw = 0f;
    public float houseWidth = 10f;

    // Câmera (posições absolutas e ângulos)
    public float fov = 50f;
    public Vector3 camIntroPos = new Vector3(-40f, 1.6f, -2.8f);
    public Vector3 camIntroEuler = new Vector3(2f, 80f, 0f);
    public Vector3 camMainPos = new Vector3(-17f, 2.55f, -3.1f);
    public Vector3 camMainEuler = new Vector3(4f, 92f, 0f);
    public float loadCamDistance = 3.6f;
    public float loadCamHeight = 0.05f;

    // Quadros (posição do centro; viram para a câmera principal)
    public Vector3 mainBoardPos = new Vector3(-11.2f, 3.0f, -4.6f);
    public float mainBoardYawOffset = 0f;
    public Vector3 loadBoardPos = new Vector3(-2.6f, 3.0f, -5.2f);
    public float loadBoardYawOffset = 0f;
    public float boardWidth = 3.0f;
    public float boardHeight = 1.9f;

    // Placa da estação (título)
    public Vector3 titleSignPos = new Vector3(6f, 6.6f, 4.8f);
    public float titleSignBaseY = 0f;
    public float titleSignWidth = 8f;

    // Bancos e caixotes na plataforma
    public int benchCount = 3;
    public int crateCount = 6;

    // Natureza e fundo
    public int treeCount = 90;
    public Vector2 treeHeight = new Vector2(7f, 13f);
    public int grassCount = 160;
    public bool useRuins = true;
    public Vector3 ruinsPos = new Vector3(40f, 0f, 120f);
    public float ruinsWidth = 140f;

    // Pós-processamento
    public float bloomIntensity = 1.8f;
    public float postExposure = 0.9f;

    private const string OverridePath = "Temp/TheRailway/layout.json";

    public static Menu3DLayout Load()
    {
        var layout = new Menu3DLayout();

        if (File.Exists(OverridePath))
        {
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(OverridePath), layout);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[The Railway] layout.json inválido: " + e.Message);
            }
        }

        return layout;
    }
}
