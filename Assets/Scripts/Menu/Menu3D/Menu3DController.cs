using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Slot vazio")]
    [SerializeField] private string emptySlotMessage = "VOCÊ AINDA NÃO TEM NENHUMA PARTIDA";
    [Tooltip("Altura da mensagem no painel Carregar (pixels do painel, 0 = centro).")]
    [SerializeField] private float emptyMessageY = -195f;
    [SerializeField] private float emptyMessageFontSize = 44f;
    [SerializeField] private float emptyMessageDuration = 2.6f;

    [Header("Apagar save")]
    [SerializeField] private string loadHints = "ENTER  CARREGAR      DEL  APAGAR      ESC  VOLTAR";

    [Header("Tela")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private CanvasGroup prompt;
    [SerializeField] private CanvasGroup hints;

    [Header("Cena do jogo")]
    [SerializeField] private string gameScene = "CenaPrincipal";
    [Tooltip("Cena onde começa um jogo novo (JOGAR).")]
    [SerializeField] private string newGameScene = "Prologo";
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
        PlayerStats.LimparTransporte(); // no menu, nada vem da partida anterior

        // Com painel de LED, o quadro físico fica sempre visível; só os LEDs ligam.
        SetGroup(mainBoard, mainPower != null ? 1f : 0f, false);
        SetGroup(loadBoard, loadPower != null ? 1f : 0f, false);
        SetGroup(prompt, 0f, false);
        SetGroup(hints, 0f, false);
        SetGroup(fade, 1f, false);

        if (cameraRig != null && introPose != null)
            cameraRig.SetPositionAndRotation(introPose.position, introPose.rotation);

        targetPose = mainPose;
        GameSettings.ApplyVolume();

        // O menu sempre usa o mouse livre (ao voltar do jogo ele pode vir travado).
        FreeCursor();

        hintsText = hints != null ? hints.GetComponentInChildren<TMP_Text>(true) : null;
        mainHints = hintsText != null ? hintsText.text : "";
    }

    private static void FreeCursor()
    {
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
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
                FreeCursor();
                MoveCamera();
                KeepSelection();
                if (state == State.Load)
                    UpdateLoadBoard();
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

        // Jogo novo: zera progresso, inventário e slot atual
        SaveGameLoader.selectedSlot = -1;
        GameSaveController.CurrentSlot = -1;
        GameFlags.Clear();
        PlayerInventory.Instance?.LimparInventario();

        string cena = Application.CanStreamedLevelBeLoaded(newGameScene) ? newGameScene : gameScene;
        StartCoroutine(Leave(() => LoadingScreen.Load(cena)));
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
        RefreshDeleteButtons();
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

        // Enter no slot que está pedindo confirmação = confirma apagar
        if (confirmDeleteSlot == slot)
        {
            DeleteSlot(slot);
            return;
        }

        CancelDelete();

        if (!SaveManager.HasSave(slot))
        {
            ShowEmptySlotMessage();
            return;
        }

        PlayClip(confirmClip);
        SaveGameLoader.selectedSlot = slot;
        string cenaSave = SaveSnapshot.CenaDoSave(slot, gameScene);
        if (!Application.CanStreamedLevelBeLoaded(cenaSave)) cenaSave = gameScene;
        StartCoroutine(Leave(() => LoadingScreen.Load(cenaSave)));
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

    // ------------------------------------------------------- Slot vazio

    private TMP_Text emptyMessage;
    private Coroutine emptyMessageRoutine;

    private void ShowEmptySlotMessage()
    {
        if (loadPower != null) loadPower.PlayError();
        else PlayClip(backClip);

        ShowTimedMessage(emptySlotMessage);
    }

    private void ShowTimedMessage(string content)
    {
        var text = EnsureEmptyMessage();
        if (text == null)
            return;

        text.text = content;
        if (emptyMessageRoutine != null) StopCoroutine(emptyMessageRoutine);
        emptyMessageRoutine = StartCoroutine(BlinkEmptyMessage(text));
    }

    // ------------------------------------------------------- Apagar save

    private int confirmDeleteSlot = -1;
    private TMP_Text hintsText;
    private string mainHints;
    private readonly Dictionary<SaveBoardLine, Button> deleteButtons = new Dictionary<SaveBoardLine, Button>();

    private void UpdateLoadBoard()
    {
        var selectedLine = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            ? EventSystem.current.currentSelectedGameObject.GetComponent<SaveBoardLine>()
            : null;

        // Mudou de linha: cancela a confirmação
        if (confirmDeleteSlot >= 0 && (selectedLine == null || selectedLine.Slot != confirmDeleteSlot))
            CancelDelete();

        if (CancelPressed())
        {
            if (confirmDeleteSlot >= 0) { CancelDelete(); PlayClip(backClip); }
            else Back();
            return;
        }

        if (DeletePressed() && selectedLine != null)
            RequestDelete(selectedLine.Slot);

        // O "X" acompanha a cor do texto da linha (escuro quando a linha está acesa)
        foreach (var pair in deleteButtons)
        {
            if (pair.Value == null || pair.Key == null) continue;
            var xText = pair.Value.GetComponentInChildren<TMP_Text>();
            if (xText == null || pair.Key.Label == null) continue;
            xText.color = pair.Key.Label.color;
        }
    }

    /// <summary>Pede para apagar o slot; chamar de novo no mesmo slot confirma.</summary>
    public void RequestDelete(int slot)
    {
        if (state != State.Load)
            return;

        if (confirmDeleteSlot == slot)
        {
            DeleteSlot(slot);
            return;
        }

        if (!SaveManager.HasSave(slot))
        {
            ShowEmptySlotMessage();
            return;
        }

        confirmDeleteSlot = slot;
        if (loadPower != null) loadPower.PlayError();

        var text = EnsureEmptyMessage();
        if (text != null)
        {
            if (emptyMessageRoutine != null) StopCoroutine(emptyMessageRoutine);
            emptyMessageRoutine = null;
            text.text = $"APAGAR O SLOT {slot + 1}?  ENTER = SIM  ESC = NÃO";
            text.alpha = 1f;
        }
    }

    private void DeleteSlot(int slot)
    {
        confirmDeleteSlot = -1;
        SaveManager.DeleteSave(slot);

        if (GameSaveController.CurrentSlot == slot)
            GameSaveController.CurrentSlot = -1;

        if (saveLines != null)
            foreach (var line in saveLines)
                if (line != null) line.Refresh();

        RefreshDeleteButtons();
        PlayClip(backClip);
        ShowTimedMessage($"SLOT {slot + 1} APAGADO");
    }

    private void CancelDelete()
    {
        if (confirmDeleteSlot < 0)
            return;

        confirmDeleteSlot = -1;
        HideEmptyMessage();
    }

    /// <summary>Cria (uma vez) o "X" de apagar em cada slot e mostra só onde há save.</summary>
    private void RefreshDeleteButtons()
    {
        if (saveLines == null)
            return;

        foreach (var line in saveLines)
        {
            if (line == null) continue;

            if (!deleteButtons.TryGetValue(line, out var button) || button == null)
            {
                button = CreateDeleteButton(line);
                deleteButtons[line] = button;
            }

            button.gameObject.SetActive(SaveManager.HasSave(line.Slot));
        }
    }

    private Button CreateDeleteButton(SaveBoardLine line)
    {
        var go = new GameObject("Apagar", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(line.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-12f, 0f);
        rt.sizeDelta = new Vector2(80f, 80f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // área clicável invisível

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        int slot = line.Slot;
        button.onClick.AddListener(() => RequestDelete(slot));

        var textGo = new GameObject("X", typeof(RectTransform));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (line.Label != null) { tmp.font = line.Label.font; tmp.fontSize = line.Label.fontSize; }
        tmp.text = "X";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return button;
    }

    private static bool DeletePressed()
    {
        if (Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame)
            return true;

        return Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
    }

    /// <summary>Cria a linha da mensagem no painel Carregar, usando a mesma fonte de LED.</summary>
    private TMP_Text EnsureEmptyMessage()
    {
        if (emptyMessage != null || loadBoard == null)
            return emptyMessage;

        var leds = loadBoard.transform.Find("LEDs") ?? loadBoard.transform;
        var reference = leds.GetComponentInChildren<TMP_Text>(true);

        var go = new GameObject("Mensagem Slot Vazio", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(leds, false);
        rt.anchoredPosition = new Vector2(0f, emptyMessageY);
        rt.sizeDelta = new Vector2(((RectTransform)leds).rect.width - 60f, 80f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (reference != null) tmp.font = reference.font;
        tmp.text = emptySlotMessage;
        tmp.fontSize = emptyMessageFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = new Color(1f, 0.68f, 0.18f, 1f);
        tmp.raycastTarget = false;
        tmp.alpha = 0f;

        emptyMessage = tmp;
        return tmp;
    }

    private IEnumerator BlinkEmptyMessage(TMP_Text text)
    {
        // Duas piscadas, no mesmo ritmo dos dois bipes do erro
        for (int i = 0; i < 2; i++)
        {
            text.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.18f);
            text.alpha = 0f;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        text.alpha = 1f;
        yield return new WaitForSecondsRealtime(emptyMessageDuration);

        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            text.alpha = 1f - t / 0.3f;
            yield return null;
        }

        text.alpha = 0f;
        emptyMessageRoutine = null;
    }

    private void HideEmptyMessage()
    {
        if (emptyMessageRoutine != null) StopCoroutine(emptyMessageRoutine);
        emptyMessageRoutine = null;
        if (emptyMessage != null) emptyMessage.alpha = 0f;
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

        if (previous == State.Load && next != State.Load)
        {
            confirmDeleteSlot = -1;
            HideEmptyMessage();
        }

        if (hintsText != null)
            hintsText.text = next == State.Load ? loadHints : mainHints;

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
