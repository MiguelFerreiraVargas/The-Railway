using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DocumentInteractable : MonoBehaviour, IInteractable
{
    [Header("Documento")]
    [SerializeField] private GameObject documentPanel;

    [Header("Configuração")]
    [SerializeField] private float openDelay = 0.35f;

    private bool isOpen;
    private bool isOpening;

    private void Awake()
    {
        if (documentPanel != null)
            documentPanel.SetActive(false);
    }

    public void Interact()
    {
        if (isOpen || isOpening)
            return;

        StartCoroutine(OpenDocumentRoutine());
    }

    private IEnumerator OpenDocumentRoutine()
    {
        isOpening = true;

        yield return new WaitForSeconds(openDelay);

        OpenDocument();

        isOpening = false;
    }

    private void OpenDocument()
    {
        if (documentPanel == null)
            return;

        documentPanel.SetActive(true);

        isOpen = true;

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;
    }

    private void CloseDocument()
    {
        if (documentPanel == null)
            return;

        documentPanel.SetActive(false);

        isOpen = false;

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isOpen)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseDocument();
        }
    }
}