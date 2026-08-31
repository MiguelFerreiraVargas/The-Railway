using System.Collections;
using UnityEngine;

[System.Serializable]
public class NarrativeLine
{
    public string text;
    public AudioClip voiceClip; // opcional: se tiver, a legenda dura o tempo do áudio
    public float displayTime = 3f; // usado só se não tiver voiceClip
}

// Coloca UMA VEZ num GameObject persistente (junto com UIManager/PlayerInventory).
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private UnityEngine.UI.Text subtitleText; // troca por TMP_Text se usar TextMeshPro

    [Header("Áudio")]
    [SerializeField] private AudioSource voiceSource;

    private Coroutine currentLineRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }

    public void PlayLine(NarrativeLine line)
    {
        if (line == null)
            return;

        if (currentLineRoutine != null)
            StopCoroutine(currentLineRoutine);

        currentLineRoutine = StartCoroutine(PlayLineRoutine(line));
    }

    private IEnumerator PlayLineRoutine(NarrativeLine line)
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(true);

        if (subtitleText != null)
            subtitleText.text = line.text;

        float duration = line.displayTime;

        if (line.voiceClip != null && voiceSource != null)
        {
            voiceSource.clip = line.voiceClip;
            voiceSource.Play();
            duration = line.voiceClip.length;
        }

        yield return new WaitForSeconds(duration);

        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);

        currentLineRoutine = null;
    }
}