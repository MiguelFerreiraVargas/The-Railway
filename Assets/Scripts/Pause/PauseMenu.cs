using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu de pausa no estilo do painel de LED da estação.
/// Esc (ou Start no controle) pausa/despausa. Enquanto pausado: tempo parado,
/// jogador e câmera travados, mouse livre. Também salva nos 3 slots.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private enum State { Closed, Main, Save, Options, Quit }

    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.state != State.Closed;

    [Header("Interface")]
    [SerializeField] private CanvasGroup root;
    [SerializeField] private CanvasGroup mainList;
    [SerializeField] private CanvasGroup saveList;
    [SerializeField] private CanvasGroup optionsList;
    [SerializeField] private CanvasGroup quitList;
    [SerializeField] private GameObject firstMainButton;
    [SerializeField] private GameObject firstSaveButton;
    [SerializeField] private GameObject firstOptionsButton;
    [SerializeField] private GameObject firstQuitButton;
    [SerializeField] private SaveBoardLine[] saveLines;
    [SerializeField] private TMP_Text message;

    [Header("Cenas")]
    [SerializeField] private string mainMenuScene = "Menu";

    [Header("Save")]
    [SerializeField] private string saveName = "Viagem";

    [Tooltip("Salva sozinho ao fechar o jogo (Alt+F4, X da janela, Sair). Se a cena tiver GameSaveController, é ele que salva.")]
    [SerializeField] private bool autoSaveOnQuit = true;
    [Tooltip("No editor, parar o Play também conta como fechar. Deixe desligado para não criar saves ao testar.")]
    [SerializeField] private bool autoSaveInEditor = false;

    [Header("Som")]
    [SerializeField] private AudioSource uiAudio;
    [SerializeField] private AudioClip confirmClip;
    [SerializeField] private AudioClip backClip;

    private State state = State.Closed;
    private float timeScaleBeforePause = 1f;
    private int closedFrame = -1;
    private Coroutine fadeRoutine, messageRoutine;

    // Estado do jogador guardado durante a pausa
    private FirstPersonController[] controllers = new FirstPersonController[0];
    private StarterAssetsInputs[] inputs = new StarterAssetsInputs[0];
    private bool[] controllerWasEnabled = new bool[0];
    private bool[] cursorLockedBefore = new bool[0];
    private bool[] lookBefore = new bool[0];

    private void Awake()
    {
        Instance = this;
        SetGroup(root, 0f, false);
        SetGroup(mainList, 1f, true);
        SetGroup(saveList, 0f, false);
        SetGroup(optionsList, 0f, false);
        SetGroup(quitList, 0f, false);
        if (message != null) message.alpha = 0f;
        EnsureEventSystem();
    }

    private void Start()
    {
        GameSettings.ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Nunca deixa o jogo congelado ao trocar de cena
            if (state != State.Closed) Time.timeScale = timeScaleBeforePause;
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        if (!autoSaveOnQuit || (Application.isEditor && !autoSaveInEditor))
            return;

        // Cena com GameSaveController: ele já salva ao fechar.
        if (FindFirstObjectByType<GameSaveController>() != null)
            return;

        int slot = GameSaveController.ChooseAutoSaveSlot();
        if (SaveCurrentGame(slot))
            Debug.Log("Salvamento automatico ao fechar (pausa): Slot " + (slot + 1));
    }

    private void Update()
    {
        // Durante a tela de carregamento o Esc não pausa (o mundo já está congelado).
        if (LoadingScreen.IsLoading || Leitura.Aberta || TelaDeMorte.Ativa)
            return;

        if (PausePressed())
        {
            if (state == State.Closed)
            {
                // Se outro painel do jogo (inventário, documento...) estiver aberto,
                // o Esc é dele: o UIManager fecha esse painel primeiro.
                bool otherPanelOpen = UIManager.Instance != null && UIManager.Instance.IsAnyPanelOpen;
                if (!otherPanelOpen && Time.frameCount != closedFrame)
                    Pause();
            }
            else if (state != State.Main)
            {
                BackToMain();
            }
            else
            {
                Resume();
            }
        }

        if (state != State.Closed)
            KeepSelection();
    }

    // ------------------------------------------------------------ Ações

    public void Pause()
    {
        if (state != State.Closed)
            return;

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        FreezePlayer(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowList(State.Main);
        Fade(1f, 0.12f, interactive: true);
    }

    public void Resume()
    {
        if (state == State.Closed)
            return;

        PlayClip(backClip);
        state = State.Closed;
        closedFrame = Time.frameCount;
        HideMessage();
        EventSystem.current?.SetSelectedGameObject(null);

        Fade(0f, 0.12f, interactive: false);

        Time.timeScale = timeScaleBeforePause;
        FreezePlayer(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenSave()
    {
        if (state != State.Main)
            return;

        PlayClip(confirmClip);
        foreach (var line in saveLines)
            if (line != null) line.Refresh();
        ShowList(State.Save);
    }

    public void OpenOptions()
    {
        if (state != State.Main)
            return;

        PlayClip(confirmClip);
        foreach (var line in optionsList != null ? optionsList.GetComponentsInChildren<OptionLine>(true) : new OptionLine[0])
            line.Refresh();
        ShowList(State.Options);
    }

    public void OpenQuit()
    {
        if (state != State.Main)
            return;

        PlayClip(confirmClip);
        ShowList(State.Quit);
    }

    public void BackToMain()
    {
        if (state == State.Closed || state == State.Main)
            return;

        PlayClip(backClip);
        HideMessage();
        ShowList(State.Main);
    }

    public void SaveToSlot(int slot)
    {
        if (state != State.Save)
            return;

        bool ok = SaveCurrentGame(slot);

        if (saveLines != null && slot >= 0 && slot < saveLines.Length && saveLines[slot] != null)
            saveLines[slot].Refresh();

        if (ok)
        {
            PlayClip(confirmClip);
            ShowMessage($"VIAGEM SALVA NO SLOT {slot + 1}");
        }
        else
        {
            PlayClip(backClip);
            ShowMessage("NÃO FOI POSSÍVEL SALVAR");
        }
    }

    public void GoToMainMenu()
    {
        if (state != State.Quit)
            return;

        PlayClip(confirmClip);
        Time.timeScale = 1f;
        // Continua "pausado" até a cena trocar: senão a câmera do trem (e outros
        // scripts do jogo) travam o mouse de novo no último frame antes do menu.
        timeScaleBeforePause = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void Quit()
    {
        if (state != State.Quit)
            return;

        // O salvamento automático ao fechar (GameSaveController) guarda a viagem.

        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ Save

    private bool SaveCurrentGame(int slot)
    {
        // Se a cena tem o controlador de save do jogo configurado, usa ele.
        var controller = FindFirstObjectByType<GameSaveController>();
        if (controller != null && controller.player != null && controller.playTimeTracker != null)
        {
            controller.SaveGame(slot);
            return SaveManager.HasSave(slot);
        }

        // Senão, salva direto: posição do jogador e tempo jogado.
        Transform player = controller != null && controller.player != null ? controller.player : FindPlayer();
        if (player == null)
            return false;

        var tracker = FindFirstObjectByType<PlayTimeTracker>();
        var data = new SaveData
        {
            saveName = saveName,
            playTimeSeconds = tracker != null ? tracker.currentPlayTime : Time.timeSinceLevelLoad,
            posX = player.position.x,
            posY = player.position.y,
            posZ = player.position.z,
            level = controller != null ? controller.currentLevel : 1,
        };
        SaveSnapshot.Capturar(data);

        SaveManager.Save(slot, data);
        GameSaveController.CurrentSlot = slot;
        return SaveManager.HasSave(slot);
    }

    private static Transform FindPlayer()
    {
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) return fpc.transform;

        var tagged = GameObject.FindWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    // ------------------------------------------------------------ Jogador

    private void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            controllers = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
            inputs = FindObjectsByType<StarterAssetsInputs>(FindObjectsSortMode.None);
            controllerWasEnabled = new bool[controllers.Length];
            cursorLockedBefore = new bool[inputs.Length];
            lookBefore = new bool[inputs.Length];

            for (int i = 0; i < controllers.Length; i++)
            {
                controllerWasEnabled[i] = controllers[i].enabled;
                controllers[i].enabled = false; // para a câmera, que não depende do tempo
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                cursorLockedBefore[i] = input.cursorLocked;
                lookBefore[i] = input.cursorInputForLook;
                input.cursorLocked = false;      // não trava o mouse ao voltar o foco
                input.cursorInputForLook = false;
                input.look = Vector2.zero;
                input.move = Vector2.zero;
                input.jump = false;
                input.sprint = false;
            }
        }
        else
        {
            for (int i = 0; i < controllers.Length; i++)
                if (controllers[i] != null) controllers[i].enabled = controllerWasEnabled[i];

            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] == null) continue;
                inputs[i].cursorLocked = cursorLockedBefore[i];
                inputs[i].cursorInputForLook = lookBefore[i];
                inputs[i].look = Vector2.zero;
            }
        }
    }

    // ------------------------------------------------------------ Interface

    private void ShowList(State next)
    {
        state = next;
        SetGroup(mainList, next == State.Main ? 1f : 0f, next == State.Main);
        SetGroup(saveList, next == State.Save ? 1f : 0f, next == State.Save);
        SetGroup(optionsList, next == State.Options ? 1f : 0f, next == State.Options);
        SetGroup(quitList, next == State.Quit ? 1f : 0f, next == State.Quit);
        Select(FirstButton(next));
    }

    private void KeepSelection()
    {
        var es = EventSystem.current;
        if (es == null) return;

        var list = ListOf(state);
        var selected = es.currentSelectedGameObject;

        if (selected == null || list == null || !selected.transform.IsChildOf(list.transform))
            Select(FirstButton(state));
    }

    private CanvasGroup ListOf(State s)
    {
        switch (s)
        {
            case State.Save: return saveList;
            case State.Options: return optionsList;
            case State.Quit: return quitList;
            default: return mainList;
        }
    }

    private GameObject FirstButton(State s)
    {
        switch (s)
        {
            case State.Save: return firstSaveButton;
            case State.Options: return firstOptionsButton;
            case State.Quit: return firstQuitButton;
            default: return firstMainButton;
        }
    }

    private void ShowMessage(string text)
    {
        if (message == null) return;
        message.text = text;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(BlinkMessage());
    }

    private IEnumerator BlinkMessage()
    {
        for (int i = 0; i < 2; i++)
        {
            message.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.18f);
            message.alpha = 0f;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        message.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.4f);

        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            message.alpha = 1f - t / 0.3f;
            yield return null;
        }

        message.alpha = 0f;
        messageRoutine = null;
    }

    private void HideMessage()
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = null;
        if (message != null) message.alpha = 0f;
    }

    private void Fade(float target, float duration, bool interactive, float delay = 0f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target, duration, interactive, delay));
    }

    private IEnumerator FadeRoutine(float target, float duration, bool interactive, float delay)
    {
        if (root == null) yield break;

        root.interactable = interactive;
        root.blocksRaycasts = interactive;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float start = root.alpha, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            root.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        root.alpha = target;
        fadeRoutine = null;
    }

    private static void SetGroup(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private static void Select(GameObject go)
    {
        if (go != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    private void PlayClip(AudioClip clip)
    {
        if (uiAudio != null && clip != null)
            uiAudio.PlayOneShot(clip);
    }

    private static bool PausePressed()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;

        return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
