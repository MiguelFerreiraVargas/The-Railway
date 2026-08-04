using UnityEngine;

public class CompanyResources : MonoBehaviour
{
    public static CompanyResources Instance;

    public int coal = 100;
    public int drivers = 3;
    public int money = 500;

    private void Awake()
    {
        Instance = this;
    }
}