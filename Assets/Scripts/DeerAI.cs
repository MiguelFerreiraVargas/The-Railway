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
    [SerializeField] private float wanderRadius = 15f;
    [SerializeField] private float minIdleTime = 2f;
    [SerializeField] private float maxIdleTime = 6f;

    [SerializeField] private float minWalkTime = 3f;
    [SerializeField] private float maxWalkTime = 8f;

    private DeerState currentState;
    private float stateTimer;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent.speed = walkSpeed;
        agent.angularSpeed = 360f;

        ChangeState(DeerState.Idle);
    }

    private void Update()
    {
        stateTimer -= Time.deltaTime;

        switch (currentState)
        {
            case DeerState.Idle:
                UpdateIdle();
                break;

            case DeerState.Walking:
                UpdateWalking();
                break;
        }

        UpdateAnimator();
    }

    private void ChangeState(DeerState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case DeerState.Idle:

                agent.isStopped = true;

                stateTimer = Random.Range(
                    minIdleTime,
                    maxIdleTime
                );

                break;

            case DeerState.Walking:

                agent.isStopped = false;

                stateTimer = Random.Range(
                    minWalkTime,
                    maxWalkTime
                );

                FindRandomDestination();

                break;
        }
    }

    private void UpdateIdle()
    {
        agent.isStopped = true;

        if (stateTimer <= 0f)
        {
            ChangeState(DeerState.Walking);
        }
    }

    private void UpdateWalking()
    {
        agent.isStopped = false;

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            ChangeState(DeerState.Idle);
            return;
        }

        if (stateTimer <= 0f)
        {
            ChangeState(DeerState.Idle);
        }
    }

    private void FindRandomDestination()
    {
        Vector3 randomDirection =
            Random.insideUnitSphere * wanderRadius;

        randomDirection += transform.position;

        if (NavMesh.SamplePosition(
            randomDirection,
            out NavMeshHit hit,
            wanderRadius,
            NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            ChangeState(DeerState.Idle);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        float speed = agent.velocity.magnitude;

        animator.SetFloat(
            "Speed",
            speed,
            0.15f,
            Time.deltaTime
        );
    }
}