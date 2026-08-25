using System.Collections;
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

    private bool hasStarted;

    private void Update()
    {
        if (hasStarted)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame)
        {
            StartMenu();
        }
    }

    private void StartMenu()
    {
        hasStarted = true;

        pressAnyKeyText.SetActive(false);

        // Start title animation
        titleAnimator.SetTrigger("ShowMenu");

        // Wait before showing buttons
        StartCoroutine(PlayButtonsAfterDelay());
    }

    private IEnumerator PlayButtonsAfterDelay()
    {
        yield return new WaitForSeconds(buttonsDelay);

        // Start buttons animation
        buttonsAnimator.SetTrigger("ShowButtons");
    }

    // Called at the end of the buttons animation
    public void SelectFirstButton()
    {
        EventSystem.current.SetSelectedGameObject(firstButton);
    }
}