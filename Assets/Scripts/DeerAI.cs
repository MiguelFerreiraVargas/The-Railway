using UnityEngine;
using UnityEngine.AI;

public class DeerAI : MonoBehaviour
{
    private enum DeerState
    {
        Idle,
        Walking
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Wandering")]
    [SerializeField] private float minWalkDistance = 5f;
    [SerializeField] private float maxWalkDistance = 12f;
    [SerializeField] private float idleMinTime = 2f;
    [SerializeField] private float idleMaxTime = 5f;
    [SerializeField] private float walkMinTime = 4f;
    [SerializeField] private float walkMaxTime = 10f;

    [Header("NavMesh")]
    [SerializeField] private float sampleDistance = 10f;

    private DeerState currentState;

    private float stateTimer;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent.speed = walkSpeed;
        agent.stoppingDistance = 0.2f;
        agent.autoBraking = true;

        ChangeState(DeerState.Idle);
    }

    private void Update()
    {
        switch (currentState)
        {
            case DeerState.Idle:
                UpdateIdle();
                break;

            case DeerState.Walking:
                UpdateWalking();
                break;
        }
    }

    private void ChangeState(DeerState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case DeerState.Idle:

                agent.isStopped = true;

                stateTimer = Random.Range(
                    idleMinTime,
                    idleMaxTime
                );

                SetWalkingAnimation(false);

                break;

            case DeerState.Walking:

                if (FindRandomDestination())
                {
                    agent.isStopped = false;

                    stateTimer = Random.Range(
                        walkMinTime,
                        walkMaxTime
                    );

                    SetWalkingAnimation(true);
                }
                else
                {
                    // Se não encontrou posição válida,
                    // continua no Idle.
                    ChangeState(DeerState.Idle);
                }

                break;
        }
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            ChangeState(DeerState.Walking);
        }
    }

    private void UpdateWalking()
    {
        stateTimer -= Time.deltaTime;

        // Espera o NavMesh terminar de calcular o caminho.
        if (agent.pathPending)
            return;

        // Chegou no destino.
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            ChangeState(DeerState.Idle);
            return;
        }

        // Tempo máximo andando acabou.
        if (stateTimer <= 0f)
        {
            agent.ResetPath();
            ChangeState(DeerState.Idle);
            return;
        }

        // Rotação suave.
        if (agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 direction = agent.velocity.normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    private bool FindRandomDestination()
    {
        Vector3 randomDirection =
            Random.insideUnitSphere *
            Random.Range(minWalkDistance, maxWalkDistance);

        randomDirection += transform.position;

        if (NavMesh.SamplePosition(
            randomDirection,
            out NavMeshHit hit,
            sampleDistance,
            NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            return true;
        }

        return false;
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", walking);
        }
    }
}