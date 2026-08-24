using UnityEngine;
using UnityEngine.InputSystem;

public class MenuKeyboard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pressAnyKeyText;

    [Header("Animation")]
    [SerializeField] private Animator menuAnimator;

    private bool hasStarted;

    private void Update()
    {
        if (hasStarted)
            return;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            StartMenu();
        }
    }

    private void StartMenu()
    {
        hasStarted = true;

        pressAnyKeyText.SetActive(false);

        menuAnimator.SetTrigger("ShowMenu");
    }
}