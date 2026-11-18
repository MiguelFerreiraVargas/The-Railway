using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Configurações do jogador (volume, sensibilidade do mouse, tela cheia),
/// guardadas no PlayerPrefs e aplicadas ao abrir qualquer cena com menu.
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "TheRailway.Volume";
    private const string SensitivityKey = "TheRailway.Sensitivity";
    private const string FullscreenKey = "TheRailway.Fullscreen";

    // Velocidade original de cada jogador, para a sensibilidade ser um multiplicador.
    private static readonly Dictionary<FirstPersonController, float> baseSpeed = new Dictionary<FirstPersonController, float>();

    /// <summary>Volume geral de 0 a 1.</summary>
    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); ApplyVolume(); }
    }

    /// <summary>Multiplicador da sensibilidade do mouse (1 = padrão).</summary>
    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat(SensitivityKey, 1f);
        set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.2f, 3f)); PlayerPrefs.Save(); ApplySensitivity(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); ApplyFullscreen(); }
    }

    public static void ApplyAll()
    {
        ApplyVolume();
        ApplySensitivity();
        ApplyFullscreen();
    }

    public static void ApplyVolume()
    {
        AudioListener.volume = Volume;
    }

    public static void ApplySensitivity()
    {
        foreach (var fpc in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (!baseSpeed.ContainsKey(fpc)) baseSpeed[fpc] = fpc.RotationSpeed;
            fpc.RotationSpeed = baseSpeed[fpc] * Sensitivity;
        }
    }

    public static void ApplyFullscreen()
    {
        // No editor a "tela cheia" não se aplica; só no jogo compilado.
        if (!Application.isEditor && PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreen = Fullscreen;
    }
}
