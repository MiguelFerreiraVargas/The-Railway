using UnityEngine;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviour
{
    public static HealthSystem Instance;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;

    private bool dead;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentHealth = maxHealth;

        UpdateUI();
    }

    public void TakeDamage(float damage)
    {
        if (dead)
            return;

        currentHealth -= damage;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );

        UpdateUI();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (dead)
            return;

        currentHealth += amount;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }
    }

    private void Die()
    {
        dead = true;

        Debug.Log("Player morreu.");

    }

    public float GetHealth()
    {
        return currentHealth;
    }
}