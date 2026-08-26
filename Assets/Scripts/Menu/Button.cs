using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private Animator fadeAnimator;

    [SerializeField] private float loadDelay = 1f;

    public void LoadNextScene()
    {
        StartCoroutine(LoadSceneRoutine());
    }

    private IEnumerator LoadSceneRoutine()
    {
        if (fadeAnimator != null)
        {
            fadeAnimator.SetTrigger("FadeOut");
        }

        yield return new WaitForSeconds(loadDelay);

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex + 1
        );
    }
}