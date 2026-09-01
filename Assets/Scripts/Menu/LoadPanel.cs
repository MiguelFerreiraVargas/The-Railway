using UnityEngine;

public class LoadPanel : MonoBehaviour
{
    public GameObject loadPanel;

    public void OpenLoadPanel()
    {
        loadPanel.SetActive(true);
    }

    public void CloseLoadPanel()
    {
        loadPanel.SetActive(false);
    }
}