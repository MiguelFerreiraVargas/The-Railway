using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerArmAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private InputActionReference attackAction;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (attackAction != null)
        {
            attackAction.action.Enable();
            attackAction.action.performed += OnAttack;
        }
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.action.performed -= OnAttack;
            attackAction.action.Disable();
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        animator.SetTrigger("AttackArm");
    }
}