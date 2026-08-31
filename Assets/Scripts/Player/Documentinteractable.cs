using System.Collections;
using UnityEngine;

// Coloca esse script em um objeto "documento" no cenário (junto com um Collider,
// na layer configurada como interactableLayer no PlayerArmActions).
public class DocumentInteractable : MonoBehaviour, IInteractable, IClosablePanel
{
    [Header("Documento")]
    [SerializeField] private GameObject documentPanel; // painel com a Image em tela cheia, desativado por padrão
    [SerializeField] private float delayBeforeShow = 1.2f; // dá tempo de ver a animação do braço (push) antes do documento aparecer

    private bool isOpen;
    private Coroutine openRoutine;

    private void Awake()
    {
        if (documentPanel != null)
            documentPanel.SetActive(false);
    }

    public void Interact()
    {
        if (isOpen)
            return;

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenAfterDelay());
    }

    private IEnumerator OpenAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeShow);

        if (documentPanel == null)
            yield break;

        documentPanel.SetActive(true);
        isOpen = true;

        UIManager.Instance?.OpenPanel(this, pauseGame: true); // libera o mouse e pausa o jogo
        openRoutine = null;
    }

    // chamado pelo UIManager quando aperta ESC
    public void ClosePanel()
    {
        if (!isOpen)
            return;

        if (documentPanel != null)
            documentPanel.SetActive(false);

        isOpen = false;
        UIManager.Instance?.ClosePanelInternal(this); // já cuida de travar o mouse de novo
    }
}