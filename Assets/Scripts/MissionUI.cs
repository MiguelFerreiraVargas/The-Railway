using TMPro;
using UnityEngine;

public class MissionUI : MonoBehaviour
{
    public static MissionUI Instance;

    public GameObject panel;

    public TMP_Text destinationText;
    public TMP_Text rewardText;
    public TMP_Text coalText;
    public TMP_Text driverText;
    public TMP_Text statusText;

    private MissionLocation currentMission;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowMission(MissionLocation mission)
    {
        Debug.Log(panel);
        Debug.Log(destinationText);
        Debug.Log(rewardText);
        Debug.Log(coalText);
        Debug.Log(driverText);
        Debug.Log(statusText);
        Debug.Log(mission);
        Debug.Log(mission.mission);

        currentMission = mission;
    }

    public void HideMission()
    {
        panel.SetActive(false);
    }

    public void OnDepartButton()
    {
        if (currentMission == null)
            return;

        currentMission.StartMission();

        ShowMission(currentMission);
    }
}