using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class MenuKeyboard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pressAnyKeyText;
    [SerializeField] private GameObject firstButton;

    [Header("Title Animation")]
    [SerializeField] private Animator titleAnimator;

    [Header("Buttons Animation")]
    [SerializeField] private Animator buttonsAnimator;
    [SerializeField] private float buttonsDelay = 1f;

    [Header("Texto pulsando")]
    [Tooltip("Faz o 'Aperte qualquer botão' pulsar suavemente.")]
    [SerializeField] private bool pulsePrompt = true;
    [SerializeField] private float pulseSpeed = 1.6f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseMinAlpha = 0.25f;

    [Tooltip("Tempo antes de aceitar entrada (deixa a abertura respirar).")]
    [SerializeField] private float inputDelay = 1.2f;

    [Header("Som (opcional)")]
    [SerializeField] private AudioSource uiAudio;
    [SerializeField] private AudioClip confirmClip;

    private bool hasStarted;
    private TMP_Text promptText;
    private CanvasGroup promptGroup;
    private float enabledTime;

    private void Start()
    {
        enabledTime = Time.unscaledTime;

        if (pressAnyKeyText != null)
        {
            promptGroup = pressAnyKeyText.GetComponent<CanvasGroup>();
            promptText = pressAnyKeyText.GetComponentInChildren<TMP_Text>();
        }
    }

    private void Update()
    {
        if (hasStarted)
            return;

        UpdatePromptPulse();

        if (Time.unscaledTime - enabledTime < inputDelay)
            return;

        if (AnyInputPressed())
            StartMenu();
    }

    private void UpdatePromptPulse()
    {
        if (!pulsePrompt || pressAnyKeyText == null)
            return;

        float wave = (Mathf.Sin((Time.unscaledTime - enabledTime) * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(pulseMinAlpha, 1f, wave);

        // Fica invisível até a entrada ser liberada, e depois aparece.
        float reveal = Mathf.Clamp01((Time.unscaledTime - enabledTime - inputDelay * 0.5f) / Mathf.Max(0.01f, inputDelay * 0.5f));
        alpha *= reveal;

        if (promptGroup != null)
            promptGroup.alpha = alpha;
        else if (promptText != null)
            promptText.alpha = alpha;
    }

    private static bool AnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame))
            return true;

        if (Gamepad.current != null)
        {
            Gamepad pad = Gamepad.current;

            if (pad.buttonSouth.wasPressedThisFrame ||
                pad.buttonEast.wasPressedThisFrame ||
                pad.buttonNorth.wasPressedThisFrame ||
                pad.buttonWest.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private void StartMenu()
    {
        hasStarted = true;

        if (uiAudio != null && confirmClip != null)
            uiAudio.PlayOneShot(confirmClip);

        if (pressAnyKeyText != null)
            pressAnyKeyText.SetActive(false);

        // Start title animation
        if (titleAnimator != null)
            titleAnimator.SetTrigger("ShowMenu");

        // Wait before showing buttons
        StartCoroutine(PlayButtonsAfterDelay());
    }

    private IEnumerator PlayButtonsAfterDelay()
    {
        yield return new WaitForSeconds(buttonsDelay);

        // Start buttons animation
        if (buttonsAnimator != null)
            buttonsAnimator.SetTrigger("ShowButtons");
    }

    // Called at the end of the buttons animation
    public void SelectFirstButton()
    {
        if (EventSystem.current != null && firstButton != null)
            EventSystem.current.SetSelectedGameObject(firstButton);
    }
}
