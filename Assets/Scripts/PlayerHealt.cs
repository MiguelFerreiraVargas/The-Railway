using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("UI")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthImage;

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateUI();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateUI();
    }

    private void UpdateUI()
    {
        float percentage = currentHealth / maxHealth;

        if (healthText != null)
        {
            healthText.text = Mathf.RoundToInt(percentage * 100f) + "%";
        }

        if (healthImage != null)
        {
            healthImage.fillAmount = percentage;
        }
    }

    private void Die()
    {
        Debug.Log("Player morreu");

    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
}