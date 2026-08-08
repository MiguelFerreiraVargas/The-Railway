using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HungerSystem : MonoBehaviour
{
    public static HungerSystem Instance;

    [Header("Hunger")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float currentHunger = 100f;

    [SerializeField] private float hungerDrainPerSecond = 1f;

    [Header("Damage At 0%")]
    [SerializeField] private float damagePerSecond = 5f;

    [Header("UI")]
    [SerializeField] private TMP_Text hungerText;
    [SerializeField] private Image foodImage;

    [Header("Food Image")]
    [SerializeField] private Color fullFoodColor = Color.white;
    [SerializeField] private Color emptyFoodColor = Color.gray;

    [Header("Camera Sickness")]
    [SerializeField] private Transform cameraTransform;

    [SerializeField] private float sicknessStart = 60f;

    [SerializeField] private float maxSicknessIntensity = 2f;

    [SerializeField] private float sicknessSpeed = 2f;

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentHunger = maxHunger;

        if (cameraTransform != null)
        {
            originalCameraPosition = cameraTransform.localPosition;
            originalCameraRotation = cameraTransform.localRotation;
        }

        UpdateUI();
    }

    private void Update()
    {
        ConsumeHunger();
        HandleZeroHungerDamage();
        HandleCameraSickness();
        UpdateUI();
    }

    private void ConsumeHunger()
    {
        currentHunger -= hungerDrainPerSecond * Time.deltaTime;

        currentHunger = Mathf.Clamp(
            currentHunger,
            0f,
            maxHunger
        );
    }

    private void HandleZeroHungerDamage()
    {
        if (currentHunger > 0f)
            return;

        if (HealthSystem.Instance != null)
        {
            HealthSystem.Instance.TakeDamage(
                damagePerSecond * Time.deltaTime
            );
        }
    }

    private void HandleCameraSickness()
    {
        if (cameraTransform == null)
            return;

        if (currentHunger >= sicknessStart)
        {
            cameraTransform.localPosition =
                Vector3.Lerp(
                    cameraTransform.localPosition,
                    originalCameraPosition,
                    Time.deltaTime * 5f
                );

            cameraTransform.localRotation =
                Quaternion.Slerp(
                    cameraTransform.localRotation,
                    originalCameraRotation,
                    Time.deltaTime * 5f
                );

            return;
        }

        // 60% = 0 intensidade
        // 0% = intensidade máxima

        float sickness =
            1f - (currentHunger / sicknessStart);

        sickness = Mathf.Clamp01(sickness);

        float intensity =
            sickness * maxSicknessIntensity;

        float time = Time.time * sicknessSpeed;

        // Movimento suave de enjoo
        float offsetX =
            Mathf.Sin(time) * intensity * 0.01f;

        float offsetY =
            Mathf.Cos(time * 1.3f) * intensity * 0.01f;

        float rotation =
            Mathf.Sin(time * 0.8f) * intensity;

        Vector3 targetPosition =
            originalCameraPosition +
            new Vector3(offsetX, offsetY, 0f);

        Quaternion targetRotation =
            originalCameraRotation *
            Quaternion.Euler(0f, 0f, rotation);

        cameraTransform.localPosition =
            Vector3.Lerp(
                cameraTransform.localPosition,
                targetPosition,
                Time.deltaTime * 5f
            );

        cameraTransform.localRotation =
            Quaternion.Slerp(
                cameraTransform.localRotation,
                targetRotation,
                Time.deltaTime * 5f
            );
    }

    private void UpdateUI()
    {
        float percentage =
            currentHunger / maxHunger * 100f;

        if (hungerText != null)
        {
            hungerText.text =
                Mathf.RoundToInt(percentage) + "%";
        }

        if (foodImage != null)
        {
            float normalized =
                currentHunger / maxHunger;

            foodImage.color =
                Color.Lerp(
                    emptyFoodColor,
                    fullFoodColor,
                    normalized
                );
        }
    }

    public void AddHunger(float amount)
    {
        currentHunger += amount;

        currentHunger =
            Mathf.Clamp(
                currentHunger,
                0f,
                maxHunger
            );

        UpdateUI();
    }

    public float GetHunger()
    {
        return currentHunger;
    }
}