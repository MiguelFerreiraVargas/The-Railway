using UnityEngine;
using UnityEngine.UI;

public class TrainStation : MonoBehaviour, IInteractable
{
    private Outline outline;

    private void Awake()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;
    }

    public void Interact()
    {
        Debug.Log("Interagiu");
    }

    public void ShowOutline()
    {
        outline.enabled = true;
    }

    public void HideOutline()
    {
        outline.enabled = false;
    }
}