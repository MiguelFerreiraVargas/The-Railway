using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Keyboard : MonoBehaviour
{
    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }
}