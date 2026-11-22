using System;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tela de leitura de jornais e documentos: o jogo para, o papel aparece
/// na tela e fecha com E, Esc ou clique.
/// </summary>
public class Leitura : MonoBehaviour
{
    public static Leitura Instance { get; private set; }
    public static bool Aberta => Instance != null && Instance.aberta;

    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private TMP_Text jornal;     // nome do jornal / cabeçalho
    [SerializeField] private TMP_Text data;
    [SerializeField] private TMP_Text manchete;
    [SerializeField] private TMP_Text corpo;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAbrir;
    [SerializeField] private AudioClip somFechar;

    private bool aberta;
    private float abertaEm;
    private float timeScaleAntes = 1f;
    private FirstPersonController[] travados = new FirstPersonController[0];
    private Action aoFechar;

    private void Awake()
    {
        Instance = this;
        if (grupo != null) { grupo.alpha = 0f; grupo.blocksRaycasts = false; }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (aberta) Time.timeScale = timeScaleAntes;
            Instance = null;
        }
    }

    public static void Abrir(string nomeJornal, string dataTexto, string titulo, string texto, Action depoisDeFechar = null)
    {
        if (Instance == null || Instance.aberta) return;
        Instance.AbrirInterno(nomeJornal, dataTexto, titulo, texto, depoisDeFechar);
    }

    private void AbrirInterno(string nomeJornal, string dataTexto, string titulo, string texto, Action depoisDeFechar)
    {
        if (jornal != null) jornal.text = nomeJornal;
        if (data != null) data.text = dataTexto;
        if (manchete != null) manchete.text = titulo;
        if (corpo != null) corpo.text = texto;
        aoFechar = depoisDeFechar;

        aberta = true;
        abertaEm = Time.unscaledTime;
        timeScaleAntes = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        travados = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (var f in travados) f.enabled = false;

        if (grupo != null) { grupo.alpha = 1f; grupo.blocksRaycasts = true; }
        if (audioSource != null && somAbrir != null) audioSource.PlayOneShot(somAbrir);
    }

    private void Update()
    {
        if (!aberta || Time.unscaledTime - abertaEm < 0.25f) return;

        bool fechar =
            (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame));

        if (fechar) Fechar();
    }

    public void Fechar()
    {
        if (!aberta) return;
        aberta = false;
        Time.timeScale = timeScaleAntes;
        foreach (var f in travados) if (f != null) f.enabled = true;
        foreach (var i in FindObjectsByType<StarterAssetsInputs>(FindObjectsSortMode.None)) { i.look = Vector2.zero; i.move = Vector2.zero; }

        if (grupo != null) { grupo.alpha = 0f; grupo.blocksRaycasts = false; }
        if (audioSource != null && somFechar != null) audioSource.PlayOneShot(somFechar);

        var cb = aoFechar;
        aoFechar = null;
        cb?.Invoke();
    }
}
