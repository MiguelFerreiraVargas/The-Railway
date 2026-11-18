using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Valores ajustáveis da atmosfera do menu. Os padrões ficam aqui; para testar
/// outros valores sem recompilar, crie Temp/TheRailway/ajustes.json com os campos
/// que quiser mudar (mesmos nomes).
/// </summary>
[Serializable]
public class MenuTuning
{
    // Neblina: densidade fixa, ou -1 para calcular pela distância até o trem.
    public float fogDensity = -1f;
    [Tooltip("Quanto do trem ainda aparece através da neblina (0-1) no modo automático.")]
    public float fogVisibilityAtTrain = 0.7f;
    public Color fogColor = new Color(0.055f, 0.07f, 0.1f);

    // Luz ambiente
    public Color ambientSky = new Color(0.07f, 0.09f, 0.15f);
    public Color ambientEquator = new Color(0.045f, 0.05f, 0.07f);
    public Color ambientGround = new Color(0.015f, 0.014f, 0.02f);
    public float ambientMultiplier = 2f;

    // Luzes
    public bool enableMoon = true; // a luz direcional estava desativada na cena original
    public float moonIntensity = 0.8f;
    public Vector3 moonEuler = new Vector3(-1f, 0f, 0f); // x < 0 = não mexer na rotação
    public float spotIntensity = 12f;
    public float spotRange = 14f;
    public float spotAngle = 120f;
    // As lâmpadas dos postes ficam ~10,7 m acima do chão: o alcance precisa passar disso.
    public float pointIntensity = 22f;
    public float pointRange = 20f;

    // Pós-processamento
    public float postExposure = 0.4f;
    public float bloomIntensity = 1.4f;
    public float bloomThreshold = 0.85f;
    public float contrast = 22f;
    public float saturation = -18f;
    public float vignette = 0.4f;

    // UI
    public float titleFontSize = 100f;
    public float titleCharacterSpacing = 4f;
    public string subtitleSpacing = "0.6em";
    public float[] buttonY = { -30f, -130f, -230f };
    public float savePanelAlpha = 0.97f;
    public float buttonFontSize = 34f;
    public float previewTitleY = 346f; // posição final da animação "Anim title"

    private const string OverridePath = "Temp/TheRailway/ajustes.json";

    public static MenuTuning Load()
    {
        var tuning = new MenuTuning();

        if (File.Exists(OverridePath))
        {
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(OverridePath), tuning);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[The Railway] ajustes.json inválido: " + e.Message);
            }
        }

        return tuning;
    }
}
