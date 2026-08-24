using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [SerializeField] private GameObject pausePanel;
    [SerializeField] private FirstPersonController firstPersonController;

    private bool isPaused;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        pausePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        isPaused = true;

        pausePanel.SetActive(true);

        Time.timeScale = 0f;

        firstPersonController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;

        pausePanel.SetActive(false);

        Time.timeScale = 1f;

        firstPersonController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}