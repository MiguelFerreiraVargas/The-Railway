using UnityEngine;

public class MissionLocation : MonoBehaviour, IInteractable
{
    public Mission mission;

    private Outline outline;

    private void Awake()
    {
        outline = GetComponent<Outline>();

        if (outline != null)
            outline.enabled = false;
    }

    public void ShowOutline()
    {
        if (outline != null)
            outline.enabled = true;

        MissionUI.Instance.ShowMission(this);
    }

    public void HideOutline()
    {
        if (outline != null)
            outline.enabled = false;

        MissionUI.Instance.HideMission();
    }

    public void Interact()
    {
        MissionUI.Instance.ShowMission(this);
    }

    public bool CanStart()
    {
        return CompanyResources.Instance.coal >= mission.coalRequired &&
               CompanyResources.Instance.drivers >= mission.driverRequired;
    }

    public void StartMission()
    {
        if (!CanStart())
            return;

        CompanyResources.Instance.coal -= mission.coalRequired;
        CompanyResources.Instance.drivers -= mission.driverRequired;

        Debug.Log("Trem partiu para " + mission.destination);

        Invoke(nameof(FinishMission), 5f);
    }

    void FinishMission()
    {
        CompanyResources.Instance.money += mission.reward;
        CompanyResources.Instance.drivers += mission.driverRequired;

        Debug.Log("Missão concluída");
    }
}
