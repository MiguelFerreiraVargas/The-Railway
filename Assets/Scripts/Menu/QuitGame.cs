using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class QuitGame : MonoBehaviour
{
    public void Quit()
    {
        Debug.Log("Saindo do jogo...");

        Application.Quit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
}