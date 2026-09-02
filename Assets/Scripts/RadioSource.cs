using System.Collections;
using UnityEngine;

[System.Serializable]
public class RadioLine
{
    public string text;
    [Tooltip("Segundos desde o início do áudio em que essa linha deve aparecer")]
    public float startTime;
}

// Coloca esse script no rádio (com um Collider na interactableLayer e um
// AudioSource configurado como 3D — Spatial Blend = 1 — pra só dar pra ouvir de perto).
// Ao interagir, toca a transmissão inteira do início ao fim. Se o player se afastar
// e não ouvir uma parte, ela simplesmente passa (igual rádio de verdade) — não pausa.
public class RadioSource : MonoBehaviour, IInteractable
{
    [Header("Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip broadcastClip;

    [Header("Legendas")]
    [SerializeField] private RadioLine[] lines;
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private UnityEngine.UI.Text subtitleText; // troca por TMP_Text se usar TextMeshPro

    private bool isPlaying;

    public void Interact()
    {
        if (isPlaying)
            return;

        StartCoroutine(PlayBroadcast());
    }

    private IEnumerator PlayBroadcast()
    {
        if (audioSource == null || broadcastClip == null)
            yield break;

        isPlaying = true;
        audioSource.clip = broadcastClip;
        audioSource.Play();

        float elapsed = 0f;
        int lineIndex = 0;

        while (elapsed < broadcastClip.length)
        {
            if (lines != null && lineIndex < lines.Length && elapsed >= lines[lineIndex].startTime)
            {
                ShowLine(lines[lineIndex].text);
                lineIndex++;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        HideLine();
        isPlaying = false;
    }

    private void ShowLine(string text)
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(true);

        if (subtitleText != null)
            subtitleText.text = text;
    }

    private void HideLine()
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }

    public void ShowOutline()
    {
        throw new System.NotImplementedException();
    }

    public void HideOutline()
    {
        throw new System.NotImplementedException();
    }
}