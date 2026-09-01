using System.Collections;
using UnityEngine;

public class PanelAnimation : MonoBehaviour
{
    public CanvasGroup canvasGroup;

    public float animationTime = 0.2f;

    public Vector3 closedScale = new Vector3(0.9f, 0.9f, 0.9f);

    private Vector3 openScale;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        openScale = transform.localScale;
    }

    public void ClosePanel()
    {
        StartCoroutine(CloseAnimation());
    }

    IEnumerator CloseAnimation()
    {
        float time = 0;

        Vector3 startScale = transform.localScale;
        float startAlpha = canvasGroup.alpha;

        while (time < animationTime)
        {
            time += Time.deltaTime;

            float t = time / animationTime;

            // Suaviza a animação
            t = Mathf.SmoothStep(0, 1, t);

            transform.localScale = Vector3.Lerp(
                startScale,
                closedScale,
                t
            );

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                0,
                t
            );

            yield return null;
        }

        transform.localScale = closedScale;
        canvasGroup.alpha = 0;

        gameObject.SetActive(false);
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(OpenAnimation());
    }

    IEnumerator OpenAnimation()
    {
        float time = 0;

        transform.localScale = closedScale;
        canvasGroup.alpha = 0;

        while (time < animationTime)
        {
            time += Time.deltaTime;

            float t = time / animationTime;

            t = Mathf.SmoothStep(0, 1, t);

            transform.localScale = Vector3.Lerp(
                closedScale,
                openScale,
                t
            );

            canvasGroup.alpha = Mathf.Lerp(
                0,
                1,
                t
            );

            yield return null;
        }

        transform.localScale = openScale;
        canvasGroup.alpha = 1;
    }
}