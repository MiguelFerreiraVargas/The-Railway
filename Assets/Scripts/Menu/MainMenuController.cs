using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pressAnyKeyText;
    [SerializeField] private GameObject menuButtons;

    [Header("Animation")]
    [SerializeField] private Animator menuAnimator;

    private bool hasStarted;

    private void Start()
    {
        menuButtons.SetActive(false);
    }

    private void Update()
    {
        if (hasStarted)
            return;

        if (Input.anyKeyDown)
        {
            StartMenu();
        }
    }

    private void StartMenu()
    {
        hasStarted = true;

        // Hide the "Press any key" text
        pressAnyKeyText.SetActive(false);

        // Activate the menu buttons
        menuButtons.SetActive(true);

        // Play the menu animation
        menuAnimator.SetTrigger("ShowMenu");
    }
}