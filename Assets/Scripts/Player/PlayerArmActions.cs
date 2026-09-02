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
    void Interact();
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

    // Objeto que está atualmente sendo mirado
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
        // Garante que o outline seja desligado
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
                // Se for o mesmo objeto, não faz nada
                if (_target == interactable)
                    return;

                // Desativa o anterior
                _target?.HideOutline();

                // Novo alvo
                _target = interactable;

                // Ativa outline
                _target.ShowOutline();

                // Mostra texto
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


    private void OnAttack(InputAction.CallbackContext context)
    {
        Punch();
    }

    private void Punch()
    {
        // Garante que está de guarda
        animator.SetBool(GuardHash, true);

        bool useRight = nextPunchHand == Hand.Right;

        animator.SetBool(UseRightHandHash, useRight);
        animator.SetTrigger(JabHash);

        // Alterna a mão
        nextPunchHand = useRight ? Hand.Left : Hand.Right;

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
            DeerAI deer = hit.collider.GetComponentInParent<DeerAI>();

            if (deer != null)
                deer.TakeDamage(attackDamage);
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

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!TryRaycastInteractable(out RaycastHit hit))
            return;

        IInteractable interactable =
            hit.collider.GetComponentInParent<IInteractable>();

        if (interactable != null)
        {
            PlayHandAnimation(PushHash, hit.point);
            interactable.Interact();
        }
    }

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

    private void PlayHandAnimation(int triggerHash, Vector3 targetPoint)
    {
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

        guardTimerRoutine = StartCoroutine(GuardTimeout());
    }

    private IEnumerator GuardTimeout()
    {
        yield return new WaitForSeconds(guardHoldTime);

        animator.SetBool(GuardHash, false);

        guardTimerRoutine = null;
    }

   

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null)
            return;

        Gizmos.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward * interactRange
        );
    }

    private void ShowInteractionText(IInteractable interactable)
    {
        if (interactionText != null)
            interactionText.SetActive(true);

        if (interactionTextTMP == null)
            return;

        // Alimentos/coletáveis
        if (interactable is ICollectible)
        {
            interactionTextTMP.text = "[E] Comer    [Q] Guardar";
        }
        else
        {
            // Outros objetos interagíveis
            interactionTextTMP.text = "[E] Interagir";
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