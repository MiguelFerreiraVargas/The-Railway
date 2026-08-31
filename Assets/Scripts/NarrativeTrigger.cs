using UnityEngine;

// Coloca num objeto com Collider marcado como "Is Trigger" no cenário.
// Quando o player entra, dispara uma fala via NarrativeManager.
public class NarrativeTrigger : MonoBehaviour
{
    [SerializeField] private NarrativeLine line;
    [SerializeField] private bool playOnce = true;
    [SerializeField] private string playerTag = "Player";

    private bool played;

    private void OnTriggerEnter(Collider other)
    {
        if (playOnce && played)
            return;

        if (!other.CompareTag(playerTag))
            return;

        NarrativeManager.Instance?.PlayLine(line);
        played = true;
    }
}