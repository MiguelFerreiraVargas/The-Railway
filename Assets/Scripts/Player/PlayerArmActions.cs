using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public interface ICollectible
{
    void Collect();
}

public interface IInteractable
{
    void Interact();
}

public class PlayerArmActions : MonoBehaviour
{
    private enum Hand
    {
        Left,
        Right
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Camera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference interactAction;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Animation")]
    [SerializeField] private float interactionDelay = 0.35f;

    [Header("Guard")]
    [SerializeField] private float guardHoldTime = 3f;

    private static readonly int JabHash =
        Animator.StringToHash("Jab");

    private static readonly int GrabHash =
        Animator.StringToHash("Grab");

    private static readonly int PushHash =
        Animator.StringToHash("Push");

    private static readonly int UseRightHandHash =
        Animator.StringToHash("UseRightHand");

    private static readonly int GuardHash =
        Animator.StringToHash("Guard");

    private Hand nextPunchHand = Hand.Left;

    private Coroutine guardTimerRoutine;
    private bool isInteracting;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

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

        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteract;
        }
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.action.performed -= OnAttack;
            attackAction.action.Disable();
        }

        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteract;
            interactAction.action.Disable();
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (isInteracting)
            return;

        Punch();
    }

    private void Punch()
    {
        animator.SetBool(GuardHash, true);

        bool useRight = nextPunchHand == Hand.Right;

        animator.SetBool(
            UseRightHandHash,
            useRight
        );

        animator.SetTrigger(JabHash);

        nextPunchHand =
            useRight ? Hand.Left : Hand.Right;

        TryHitEnemy();

        RestartGuardTimer();
    }

    private void TryHitEnemy()
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
            enemyLayer))
        {
            DeerAI deer =
                hit.collider.GetComponentInParent<DeerAI>();

            if (deer != null)
                deer.TakeDamage(attackDamage);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (isInteracting)
            return;

        if (playerCamera == null)
            return;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactRange,
            interactableLayer))
            return;

        ICollectible collectible =
            hit.collider.GetComponentInParent<ICollectible>();

        if (collectible != null)
        {
            StartCoroutine(
                InteractionRoutine(
                    GrabHash,
                    collectible,
                    hit.point
                )
            );

            return;
        }

        IInteractable interactable =
            hit.collider.GetComponentInParent<IInteractable>();

        if (interactable != null)
        {
            StartCoroutine(
                InteractionRoutine(
                    PushHash,
                    interactable,
                    hit.point
                )
            );
        }
    }

    private IEnumerator InteractionRoutine(
        int animationHash,
        object interactionObject,
        Vector3 targetPoint)
    {
        isInteracting = true;

        bool useRight =
            IsOnRightSide(targetPoint);

        animator.SetBool(
            UseRightHandHash,
            useRight
        );

        animator.SetTrigger(animationHash);

        // deixa o braço fazer a animação
        yield return new WaitForSeconds(
            interactionDelay
        );

        if (interactionObject is ICollectible collectible)
        {
            collectible.Collect();
        }
        else if (interactionObject is IInteractable interactable)
        {
            interactable.Interact();
        }

        isInteracting = false;
    }

    private bool IsOnRightSide(Vector3 worldPoint)
    {
        Vector3 toTarget =
            worldPoint -
            playerCamera.transform.position;

        float side =
            Vector3.Dot(
                toTarget,
                playerCamera.transform.right
            );

        return side >= 0f;
    }

    private void RestartGuardTimer()
    {
        if (guardTimerRoutine != null)
            StopCoroutine(guardTimerRoutine);

        guardTimerRoutine =
            StartCoroutine(GuardTimeout());
    }

    private IEnumerator GuardTimeout()
    {
        yield return new WaitForSeconds(
            guardHoldTime
        );

        animator.SetBool(
            GuardHash,
            false
        );

        guardTimerRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null)
            return;

        Gizmos.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward *
            interactRange
        );
    }
}