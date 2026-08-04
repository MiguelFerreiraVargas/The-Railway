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
        currentMission = mission;

        panel.SetActive(true);

        destinationText.text =
            "Destino: " + mission.mission.destination;

        rewardText.text =
            "Recompensa: £" + mission.mission.reward;

        coalText.text =
            "Carvão: " +
            CompanyResources.Instance.coal +
            "/" +
            mission.mission.coalRequired;

        driverText.text =
            "Maquinistas: " +
            CompanyResources.Instance.drivers +
            "/" +
            mission.mission.driverRequired;

        statusText.text =
            mission.CanStart()
            ? "Pronto para partir"
            : "Recursos insuficientes";
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