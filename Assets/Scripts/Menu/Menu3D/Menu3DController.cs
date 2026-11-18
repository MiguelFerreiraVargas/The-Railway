using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla o menu 3D da estação: abertura com a câmera chegando na plataforma,
/// "aperte qualquer botão", quadro de partidas (menu principal) e quadro de saves.
/// </summary>
public class Menu3DController : MonoBehaviour
{
    public enum State { Intro, WaitKey, Main, Load, Leaving }

    public static Menu3DController Instance { get; private set; }

    [Header("Câmera")]
    [SerializeField] private Transform cameraRig;
    [SerializeField] private Transform introPose;
    [SerializeField] private Transform mainPose;
    [SerializeField] private Transform loadPose;
    [SerializeField] private float introDuration = 7f;
    [Tooltip("Quanto mais alto, mais rápido a câmera chega na nova posição.")]
    [SerializeField] private float moveSmoothing = 1.8f;

    [Header("Quadros")]
    [SerializeField] private CanvasGroup mainBoard;
    [SerializeField] private CanvasGroup loadBoard;
    [SerializeField] private GameObject firstMainButton;
    [SerializeField] private GameObject firstLoadButton;
    [SerializeField] private SaveBoardLine[] saveLines;

    [Header("Painéis de LED (opcional)")]
    [SerializeField] private LedBoardPower mainPower;
    [SerializeField] private LedBoardPower loadPower;
    [Tooltip("Espera a câmera chegar antes de ligar o outro painel.")]
    [SerializeField] private float switchPowerDelay = 0.55f;

    [Header("Tela")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private CanvasGroup prompt;
    [SerializeField] private CanvasGroup hints;

    [Header("Cena do jogo")]
    [SerializeField] private string gameScene = "CenaPrincipal";
    [SerializeField] private float fadeOutTime = 1.4f;

    [Header("Som")]
    [SerializeField] private AudioSource uiAudio;
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip confirmClip;
    [SerializeField] private AudioClip backClip;

    private State state = State.Intro;
    private float stateTime;
    private Transform targetPose;

    public State CurrentState => state;

    private void Awake()
    {
        Instance = this;

        // Com painel de LED, o quadro físico fica sempre visível; só os LEDs ligam.
        SetGroup(mainBoard, mainPower != null ? 1f : 0f, false);
        SetGroup(loadBoard, loadPower != null ? 1f : 0f, false);
        SetGroup(prompt, 0f, false);
        SetGroup(hints, 0f, false);
        SetGroup(fade, 1f, false);

        if (cameraRig != null && introPose != null)
            cameraRig.SetPositionAndRotation(introPose.position, introPose.rotation);

        targetPose = mainPose;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        stateTime += Time.unscaledDeltaTime;

        switch (state)
        {
            case State.Intro:
                UpdateIntro();
                break;

            case State.WaitKey:
                UpdatePrompt();
                if (AnyInputPressed())
                    EnterMain();
                break;

            case State.Main:
            case State.Load:
                MoveCamera();
                KeepSelection();
                if (state == State.Load && CancelPressed())
                    Back();
                break;

            case State.Leaving:
                MoveCamera();
                break;
        }

        float step = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        UpdateBoard(mainBoard, mainPower, state == State.Main, step);
        UpdateBoard(loadBoard, loadPower, state == State.Load, step);
        FadeGroup(hints, state == State.Main || state == State.Load ? 1f : 0f, step);
    }

    // ------------------------------------------------------------- Abertura

    private void UpdateIntro()
    {
        float t = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, introDuration));
        float eased = t * t * (3f - 2f * t);

        if (cameraRig != null && introPose != null && mainPose != null)
        {
            cameraRig.SetPositionAndRotation(
                Vector3.Lerp(introPose.position, mainPose.position, eased),
                Quaternion.Slerp(introPose.rotation, mainPose.rotation, eased));
        }

        if (fade != null)
            fade.alpha = 1f - Mathf.Clamp01(stateTime / 2.5f);

        // Pular a abertura apertando qualquer botão.
        if (stateTime > 0.5f && AnyInputPressed())
        {
            if (fade != null) fade.alpha = 0f;
            EnterMain();
            return;
        }

        if (t >= 1f)
            SetState(State.WaitKey);
    }

    private void UpdatePrompt()
    {
        if (prompt == null)
            return;

        float wave = (Mathf.Sin(stateTime * 2.4f) + 1f) * 0.5f;
        prompt.alpha = Mathf.Min(1f, stateTime * 1.5f) * Mathf.Lerp(0.3f, 1f, wave);
    }

    private void EnterMain()
    {
        SetGroup(prompt, 0f, false);
        PlayClip(confirmClip);
        targetPose = mainPose;
        SetState(State.Main);
        Select(firstMainButton);
    }

    // --------------------------------------------------------------- Ações

    public void Play()
    {
        if (state != State.Main)
            return;

        PlayClip(confirmClip);
        SaveGameLoader.selectedSlot = -1;
        StartCoroutine(Leave(() => SceneManager.LoadScene(gameScene)));
    }

    public void OpenLoad()
    {
        if (state != State.Main)
            return;

        PlayClip(confirmClip);

        if (saveLines != null)
        {
            foreach (var line in saveLines)
                if (line != null) line.Refresh();
        }

        targetPose = loadPose != null ? loadPose : mainPose;
        SetState(State.Load);
        Select(firstLoadButton);
    }

    public void Back()
    {
        if (state != State.Load)
            return;

        PlayClip(backClip != null ? backClip : confirmClip);
        targetPose = mainPose;
        SetState(State.Main);
        Select(firstMainButton);
    }

    public void LoadSlot(int slot)
    {
        if (state != State.Load)
            return;

        if (!SaveManager.HasSave(slot))
        {
            PlayClip(backClip);
            return;
        }

        PlayClip(confirmClip);
        SaveGameLoader.selectedSlot = slot;
        StartCoroutine(Leave(() => SceneManager.LoadScene(gameScene)));
    }

    public void Quit()
    {
        if (state != State.Main)
            return;

        PlayClip(confirmClip);
        StartCoroutine(Leave(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }));
    }

    private IEnumerator Leave(Action onFaded)
    {
        SetState(State.Leaving);
        EventSystem.current?.SetSelectedGameObject(null);

        float t = 0f;
        float start = fade != null ? fade.alpha : 0f;

        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            if (fade != null)
                fade.alpha = Mathf.Lerp(start, 1f, t / fadeOutTime);
            yield return null;
        }

        onFaded?.Invoke();
    }

    // ----------------------------------------------------------- Utilidades

    public void PlayMove()
    {
        if (state == State.Main || state == State.Load)
            PlayClip(moveClip);
    }

    private void MoveCamera()
    {
        if (cameraRig == null || targetPose == null)
            return;

        float k = 1f - Mathf.Exp(-moveSmoothing * Time.unscaledDeltaTime);
        cameraRig.SetPositionAndRotation(
            Vector3.Lerp(cameraRig.position, targetPose.position, k),
            Quaternion.Slerp(cameraRig.rotation, targetPose.rotation, k));
    }

    private void KeepSelection()
    {
        var es = EventSystem.current;

        if (es == null)
            return;

        var board = state == State.Load ? loadBoard : mainBoard;
        var selected = es.currentSelectedGameObject;

        if (selected == null || board == null || !selected.transform.IsChildOf(board.transform))
            Select(state == State.Load ? firstLoadButton : firstMainButton);
    }

    private void SetState(State next)
    {
        State previous = state;
        state = next;
        stateTime = 0f;

        // Liga/desliga os painéis de LED nas trocas de tela
        if (previous == State.Main && next != State.Main) mainPower?.PowerOff();
        if (previous == State.Load && next != State.Load) loadPower?.PowerOff();
        if (next == State.Main && previous != State.Main) mainPower?.PowerOn(previous == State.Load ? switchPowerDelay : 0f);
        if (next == State.Load && previous != State.Load) loadPower?.PowerOn(switchPowerDelay);
    }

    private static void UpdateBoard(CanvasGroup group, LedBoardPower power, bool active, float step)
    {
        if (group == null)
            return;

        if (power == null)
        {
            FadeGroup(group, active ? 1f : 0f, step);
            return;
        }

        group.alpha = 1f;
        group.interactable = active;
        group.blocksRaycasts = active;
    }

    private static void Select(GameObject go)
    {
        if (go != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    private static void SetGroup(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private static void FadeGroup(CanvasGroup group, float target, float step)
    {
        if (group == null)
            return;

        group.alpha = Mathf.Lerp(group.alpha, target, step);
        bool on = target > 0.5f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    private void PlayClip(AudioClip clip)
    {
        if (uiAudio != null && clip != null)
            uiAudio.PlayOneShot(clip);
    }

    private static bool AnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        var pad = Gamepad.current;
        return pad != null &&
               (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
    }

    private static bool CancelPressed()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame))
            return true;

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            return true;

        return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
    }
}
