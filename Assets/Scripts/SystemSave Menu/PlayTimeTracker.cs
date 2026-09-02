using UnityEngine;

public class PlayTimeTracker : MonoBehaviour
{
    public float currentPlayTime;

    void Update()
    {
        currentPlayTime += Time.deltaTime;
    }

    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentPlayTime / 3600);

        int minutes = Mathf.FloorToInt(
            (currentPlayTime % 3600) / 60
        );

        return $"{hours:00}h {minutes:00}m";
    }
}