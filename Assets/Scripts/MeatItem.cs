using UnityEngine;

public class MeatItem : MonoBehaviour
{
    [Header("Food")]
    [SerializeField] private bool cooked = false;

    [SerializeField] private float rawHunger = 10f;
    [SerializeField] private float rawDamage = 5f;

    [SerializeField] private float cookedHunger = 35f;
    [SerializeField] private float cookedDamage = 0f;

    [Header("Visual")]
    [SerializeField] private Renderer meatRenderer;
    [SerializeField] private Material rawMaterial;
    [SerializeField] private Material cookedMaterial;

    public bool IsCooked => cooked;

    private void Start()
    {
        UpdateVisual();
    }

    public float GetHungerValue()
    {
        return cooked ? cookedHunger : rawHunger;
    }

    public float GetDamageValue()
    {
        return cooked ? cookedDamage : rawDamage;
    }

    public void Cook()
    {
        if (cooked)
            return;

        cooked = true;

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (meatRenderer == null)
            return;

        if (cooked && cookedMaterial != null)
            meatRenderer.material = cookedMaterial;

        else if (!cooked && rawMaterial != null)
            meatRenderer.material = rawMaterial;
    }
}