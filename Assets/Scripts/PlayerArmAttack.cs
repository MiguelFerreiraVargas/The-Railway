using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerArmAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Camera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference attackAction;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask enemyLayer;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerCamera == null)
            playerCamera = Camera.main;
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
        if (animator != null)
            animator.SetTrigger("AttackArm");

        Attack();
    }

    private void Attack()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            attackRange,
            enemyLayer
        ))
        {
            DeerAI deer = hit.collider.GetComponentInParent<DeerAI>();

            if (deer != null)
            {
                deer.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null)
            return;

        Gizmos.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward * attackRange
        );
    }
}