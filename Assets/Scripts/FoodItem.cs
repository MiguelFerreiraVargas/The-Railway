using UnityEngine;
using UnityEngine.InputSystem;

public class Food : MonoBehaviour
{
    [Header("Food")]
    [SerializeField] private float hungerAmount = 25f;

    [Header("Interaction")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Outline")]
    [SerializeField] private Outline outline;

    private bool playerInRange;

    private void Start()
    {
        if (outline == null)
            outline = GetComponentInChildren<Outline>();

        if (outline != null)
            outline.enabled = false;
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += Eat;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= Eat;
            interactAction.action.Disable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;

        if (outline != null)
            outline.enabled = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;

        if (outline != null)
            outline.enabled = false;
    }

    private void Eat(InputAction.CallbackContext context)
    {
        if (!playerInRange)
            return;

        if (HungerSystem.Instance == null)
            return;

        HungerSystem.Instance.AddHunger(hungerAmount);

        Destroy(gameObject);
    }
}