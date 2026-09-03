using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Implementa isso em objetos que podem ser coletados
public interface ICollectible
{
    void Collect();
}

// Implementa isso em objetos com que dá pra interagir
public interface IInteractable
{
    bool Interact();
    void ShowOutline();
    void HideOutline();
}

public class PlayerArmActions : MonoBehaviour
{
    private enum Hand
    {
        Left,
        Right
    }

    [Header("Interaction UI")]
    [SerializeField] private GameObject interactionText;
    [SerializeField] private TMP_Text interactionTextTMP;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Camera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference storeAction;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Guard")]
    [SerializeField] private float guardHoldTime = 3f;

    private static readonly int JabHash = Animator.StringToHash("Jab");
    private static readonly int GrabHash = Animator.StringToHash("Grab");
    private static readonly int PushHash = Animator.StringToHash("Push");
    private static readonly int UseRightHandHash = Animator.StringToHash("UseRightHand");
    private static readonly int GuardHash = Animator.StringToHash("Guard");

    private Hand nextPunchHand = Hand.Left;
    private Coroutine guardTimerRoutine;

    // Objeto que está sendo mirado
    private IInteractable _target;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        HideInteractionText();
    }

    private void Update()
    {
        UpdateOutline();
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

        if (storeAction != null)
        {
            storeAction.action.Enable();
            storeAction.action.performed += OnStore;
        }
    }

    private void OnDisable()
    {
        _target?.HideOutline();
        _target = null;

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

        if (storeAction != null)
        {
            storeAction.action.performed -= OnStore;
            storeAction.action.Disable();
        }
    }

    private void UpdateOutline()
    {
        if (playerCamera == null)
            return;

        if (Physics.Raycast(
            playerCamera.transform.position,
            playerCamera.transform.forward,
            out RaycastHit hit,
            interactRange,
            interactableLayer))
        {
            IInteractable interactable =
                hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                if (_target == interactable)
                    return;

                _target?.HideOutline();

                _target = interactable;

                _target.ShowOutline();

                ShowInteractionText(interactable);
            }
            else
            {
                ClearTarget();
            }
        }
        else
        {
            ClearTarget();
        }
    }

    // Botão de ataque
    private void OnAttack(InputAction.CallbackContext context)
    {
        Punch();
    }

    private void Punch()
    {
        if (animator == null)
            return;

        animator.SetBool(GuardHash, true);

        bool useRight = nextPunchHand == Hand.Right;

        animator.SetBool(UseRightHandHash, useRight);
        animator.SetTrigger(JabHash);

        nextPunchHand = useRight ? Hand.Left : Hand.Right;

        TryHitTarget();

        RestartGuardTimer();
    }

    // Procura inimigos e árvores
    private void TryHitTarget()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        // Primeiro procura inimigos
        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            attackRange,
            enemyLayer))
        {
            DeerAI deer =
                hit.collider.GetComponentInParent<DeerAI>();

            if (deer != null)
            {
                deer.TakeDamage(attackDamage);
                return;
            }
        }

        // Depois procura árvores
        if (Physics.Raycast(
            ray,
            out hit,
            attackRange,
            interactableLayer))
        {
            Tree tree =
                hit.collider.GetComponentInParent<Tree>();

            if (tree != null)
            {
                tree.TakeDamage(1);

                Debug.Log(
                    "Bateu na árvore! Vida restante: " +
                    tree.GetLife()
                );
            }
        }
    }

    private bool TryRaycastInteractable(out RaycastHit hit)
    {
        hit = default;

        if (playerCamera == null)
            return false;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        return Physics.Raycast(
            ray,
            out hit,
            interactRange,
            interactableLayer
        );
    }

    // Botão E
    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!TryRaycastInteractable(out RaycastHit hit))
            return;

        IInteractable interactable =
            hit.collider.GetComponentInParent<IInteractable>();

        if (interactable == null)
            return;

        bool aceitou = interactable.Interact();

        if (aceitou)
            PlayHandAnimation(PushHash, hit.point);
    }

    // Botão Q
    private void OnStore(InputAction.CallbackContext context)
    {
        if (!TryRaycastInteractable(out RaycastHit hit))
            return;

        ICollectible collectible =
            hit.collider.GetComponentInParent<ICollectible>();

        if (collectible != null)
        {
            PlayHandAnimation(GrabHash, hit.point);
            collectible.Collect();
        }
    }

    private void PlayHandAnimation(
        int triggerHash,
        Vector3 targetPoint)
    {
        if (animator == null)
            return;

        bool useRight = IsOnRightSide(targetPoint);

        animator.SetBool(UseRightHandHash, useRight);
        animator.SetTrigger(triggerHash);
    }

    private bool IsOnRightSide(Vector3 worldPoint)
    {
        Vector3 toTarget =
            worldPoint - playerCamera.transform.position;

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
        yield return new WaitForSeconds(guardHoldTime);

        if (animator != null)
            animator.SetBool(GuardHash, false);

        guardTimerRoutine = null;
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

    private void ShowInteractionText(
        IInteractable interactable)
    {
        if (interactionText != null)
            interactionText.SetActive(true);

        if (interactionTextTMP == null)
            return;

        if (interactable is ICollectible)
        {
            interactionTextTMP.text =
                "[E] Consumir    [Q] Guardar";
        }
        else if (interactable is Tree)
        {
            interactionTextTMP.text =
                "[Clique] Cortar árvore";
        }
        else
        {
            interactionTextTMP.text =
                "[E] Interagir";
        }
    }

    private void HideInteractionText()
    {
        if (interactionText != null)
            interactionText.SetActive(false);
    }

    private void ClearTarget()
    {
        _target?.HideOutline();
        _target = null;

        HideInteractionText();
    }
}