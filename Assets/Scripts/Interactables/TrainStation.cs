using UnityEngine;

public class TrainStation : MonoBehaviour, IInteractable
{
    private Outline outline;
    [SerializeField] private GameObject missionUI;

    private void Awake()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;

        missionUI.SetActive(false);
    }

    public void Interact()
    {
        Debug.Log("Interagiu");
    }

    public void ShowOutline()
    {
        outline.enabled = true;
        MissionUI.Instance.ShowMission(GetComponent<MissionLocation>());
    }

    public void HideOutline()
    {
        outline.enabled = false;
        MissionUI.Instance.HideMission();
    }
}