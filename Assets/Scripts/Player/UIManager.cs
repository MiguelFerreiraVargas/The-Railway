using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Qualquer painel que possa ser fechado (documento, inventário, rádio...) implementa isso.
public interface IClosablePanel
{
    void ClosePanel();
}

// Coloca esse script UMA VEZ num GameObject persistente na cena (ex: "GameManagers").
// Qualquer sistema de UI chama UIManager.Instance.OpenPanel(this, pauseGame) ao abrir e
// UIManager.Instance.ClosePanelInternal(this) ao fechar (o próprio painel decide COMO
// fechar visualmente, esse manager só cuida do cursor, da pausa e de quem fecha no ESC).
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Input")]
    [SerializeField] private InputActionReference closeAction; // binda em <Keyboard>/escape

    private class PanelEntry
    {
        public IClosablePanel Panel;
        public bool PauseGame;
    }

    private readonly Stack<PanelEntry> openPanels = new Stack<PanelEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (closeAction != null)
        {
            closeAction.action.Enable();
            closeAction.action.performed += OnCloseInput;
        }
    }

    private void OnDisable()
    {
        if (closeAction != null)
        {
            closeAction.action.performed -= OnCloseInput;
            closeAction.action.Disable();
        }
    }

    // O Input System continua recebendo eventos mesmo com Time.timeScale = 0,
    // então o ESC funciona normalmente mesmo com o jogo pausado pelo documento.
    private void OnCloseInput(InputAction.CallbackContext context)
    {
        if (openPanels.Count == 0)
            return;

        // fecha só o painel do topo (o mais recente aberto)
        openPanels.Peek().Panel.ClosePanel();
    }

    // pauseGame: true trava o Time.timeScale em 0 enquanto esse painel (ou outro
    // que também peça pausa) estiver na pilha. Documento usa true; coisas como
    // inventário podem usar false se você quiser continuar em tempo real.
    public void OpenPanel(IClosablePanel panel, bool pauseGame = false)
    {
        openPanels.Push(new PanelEntry { Panel = panel, PauseGame = pauseGame });
        UpdateCursorState();
        UpdatePauseState();
    }

    public void ClosePanelInternal(IClosablePanel panel)
    {
        if (openPanels.Count > 0 && ReferenceEquals(openPanels.Peek().Panel, panel))
        {
            openPanels.Pop();
        }
        else
        {
            // fallback: fechou fora de ordem (ex: painel de baixo fechado primeiro)
            var temp = new Stack<PanelEntry>();
            while (openPanels.Count > 0)
            {
                var entry = openPanels.Pop();
                if (!ReferenceEquals(entry.Panel, panel))
                    temp.Push(entry);
            }
            while (temp.Count > 0)
                openPanels.Push(temp.Pop());
        }

        UpdateCursorState();
        UpdatePauseState();
    }

    public bool IsAnyPanelOpen => openPanels.Count > 0;

    private void UpdateCursorState()
    {
        // com nada aberto, mouse some e trava de novo pro jogo em 1ª/3ª pessoa;
        // com qualquer painel aberto, mouse aparece livre
        bool shouldBeFree = openPanels.Count > 0;
        Cursor.lockState = shouldBeFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = shouldBeFree;
    }

    private void UpdatePauseState()
    {
        bool shouldPause = false;

        foreach (var entry in openPanels)
        {
            if (entry.PauseGame)
            {
                shouldPause = true;
                break;
            }
        }

        Time.timeScale = shouldPause ? 0f : 1f;
    }
}