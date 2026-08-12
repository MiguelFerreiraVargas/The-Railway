using UnityEngine;

public class Campfire : MonoBehaviour
{
    [SerializeField] private float cookingTime = 5f;

    private MeatItem currentMeat;
    private float timer;

    private void Update()
    {
        if (currentMeat == null)
            return;

        timer += Time.deltaTime;

        if (timer >= cookingTime)
        {
            currentMeat.Cook();

            currentMeat = null;
            timer = 0f;
        }
    }

    public bool PutMeat(MeatItem meat)
    {
        if (currentMeat != null)
            return false;

        if (meat == null || meat.IsCooked)
            return false;

        currentMeat = meat;
        timer = 0f;

        meat.transform.SetParent(transform);

        meat.transform.localPosition =
            Vector3.up * 0.5f;

        return true;
    }
}