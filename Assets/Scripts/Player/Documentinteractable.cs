using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DocumentInteractable : MonoBehaviour, IInteractable, IClosablePanel
{
    [Header("Documento")]
    [SerializeField] private GameObject documentPanel;
    [SerializeField] private float delayBeforeShow = 1.2f;

    [Header("Botão Fechar")]
    [SerializeField] private Button closeButton;

    [Header("Outline")]
    [SerializeField] private Outline outline;

    private bool isOpen;
    private Coroutine openRoutine;

    private void Awake()
    {
        if (documentPanel != null)
            documentPanel.SetActive(false);

        if (outline == null)
            outline = GetComponent<Outline>();

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        HideOutline();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);
    }

    // Devolve false (e não faz NADA) se já tá aberto ou já tá no meio do delay
    // pra abrir — é isso que impede a animação de retriggar se você apertar E
    // várias vezes seguidas enquanto ele ainda tá esperando pra aparecer.
    public bool Interact()
    {
        if (isOpen || openRoutine != null)
            return false;

        HideOutline();
        openRoutine = StartCoroutine(OpenAfterDelay());
        return true;
    }

    private IEnumerator OpenAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeShow);

        if (documentPanel == null)
        {
            openRoutine = null;
            yield break;
        }

        documentPanel.SetActive(true);
        isOpen = true;

        UIManager.Instance?.OpenPanel(this, pauseGame: true);
        openRoutine = null;
    }

    public void ClosePanel()
    {
        if (!isOpen)
            return;

        if (documentPanel != null)
            documentPanel.SetActive(false);

        isOpen = false;
        UIManager.Instance?.ClosePanelInternal(this);
    }

    public void ShowOutline()
    {
        if (outline != null)
            outline.enabled = true;
    }

    public void HideOutline()
    {
        if (outline != null)
            outline.enabled = false;
    }
}