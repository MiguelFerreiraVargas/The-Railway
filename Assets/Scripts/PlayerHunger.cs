using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerHunger : MonoBehaviour
{
    [Header("Hunger")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float currentHunger = 100f;

    [SerializeField] private float hungerDecreasePerSecond = 0.5f;

    [Header("Starvation")]
    [SerializeField] private float damagePerSecond = 5f;

    [Header("UI")]
    [SerializeField] private TMP_Text hungerText;
    [SerializeField] private Image hungerImage;

    [Header("Effects")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float lowHungerThreshold = 60f;
    [SerializeField] private float cameraShakeAmount = 0.02f;
    [SerializeField] private float cameraShakeSpeed = 8f;

    private PlayerHealth playerHealth;

    private Vector3 originalCameraPosition;

    private void Start()
    {
        currentHunger = maxHunger;

        playerHealth = GetComponent<PlayerHealth>();

        if (playerCamera != null)
            originalCameraPosition = playerCamera.transform.localPosition;

        UpdateUI();
    }

    private void Update()
    {
        DecreaseHunger();
        HandleStarvationDamage();
        HandleLowHungerEffect();
    }

    private void DecreaseHunger()
    {
        currentHunger -= hungerDecreasePerSecond * Time.deltaTime;

        currentHunger = Mathf.Clamp(
            currentHunger,
            0f,
            maxHunger
        );

        UpdateUI();
    }

    private void HandleStarvationDamage()
    {
        if (currentHunger <= 0f && playerHealth != null)
        {
            playerHealth.TakeDamage(
                damagePerSecond * Time.deltaTime
            );
        }
    }

    private void HandleLowHungerEffect()
    {
        if (playerCamera == null)
            return;

        if (currentHunger <= lowHungerThreshold)
        {
            float intensity =
                1f - (currentHunger / lowHungerThreshold);

            float shakeX =
                Mathf.Sin(Time.time * cameraShakeSpeed) *
                cameraShakeAmount *
                intensity;

            float shakeY =
                Mathf.Cos(Time.time * cameraShakeSpeed * 1.3f) *
                cameraShakeAmount *
                intensity;

            playerCamera.transform.localPosition =
                originalCameraPosition +
                new Vector3(shakeX, shakeY, 0f);
        }
        else
        {
            playerCamera.transform.localPosition =
                Vector3.Lerp(
                    playerCamera.transform.localPosition,
                    originalCameraPosition,
                    Time.deltaTime * 8f
                );
        }
    }

    public void Eat(float amount)
    {
        currentHunger += amount;

        currentHunger = Mathf.Clamp(
            currentHunger,
            0f,
            maxHunger
        );

        UpdateUI();
    }

    private void UpdateUI()
    {
        float percentage = currentHunger / maxHunger;

        if (hungerText != null)
        {
            hungerText.text =
                Mathf.RoundToInt(percentage * 100f) + "%";
        }

        if (hungerImage != null)
        {
            hungerImage.fillAmount = percentage;
        }
    }

    public float GetHungerPercentage()
    {
        return currentHunger / maxHunger;
    }
}