using System.Collections.Generic;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configurações do jogador (volume, sensibilidade do mouse, tela cheia, campo de visão),
/// guardadas no PlayerPrefs e aplicadas ao abrir qualquer cena com menu.
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "TheRailway.Volume";
    private const string SensitivityKey = "TheRailway.Sensitivity";
    private const string FullscreenKey = "TheRailway.Fullscreen";
    private const string FovKey = "TheRailway.FieldOfView";

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

    /// <summary>Campo de visão vertical da câmera do jogador, em graus (padrão 60; o antigo era 40).</summary>
    public static float FieldOfView
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(FovKey, FovPadrao), FovMin, FovMax);
        set { PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, FovMin, FovMax)); PlayerPrefs.Save(); ApplyFieldOfView(); }
    }

    public const float FovPadrao = 60f, FovMin = 45f, FovMax = 90f;

    public static void ApplyAll()
    {
        ApplyVolume();
        ApplySensitivity();
        ApplyFullscreen();
        ApplyFieldOfView();
    }

    // Aplica o FOV em toda cena carregada, mesmo nas que não têm menu de pausa.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AoIniciar()
    {
        ApplyFieldOfView();
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo) => ApplyFieldOfView();

    /// <summary>
    /// Só a câmera em primeira pessoa (a que segue o PlayerCameraRoot); a câmera de fora do trem fica como está.
    /// </summary>
    public static void ApplyFieldOfView()
    {
        float fov = FieldOfView;
        foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EhCameraDoJogador(cam.gameObject, cam.Follow)) cam.Lens.FieldOfView = fov;
#pragma warning disable CS0618 // as cenas ainda usam a CinemachineVirtualCamera antiga
        foreach (var cam in Object.FindObjectsByType<CinemachineVirtualCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EhCameraDoJogador(cam.gameObject, cam.Follow)) cam.m_Lens.FieldOfView = fov;
#pragma warning restore CS0618
    }

    private static bool EhCameraDoJogador(GameObject go, Transform follow)
    {
        if (go.name.Contains("PlayerFollowCamera")) return true;
        return follow != null && follow.name.Contains("PlayerCameraRoot");
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
