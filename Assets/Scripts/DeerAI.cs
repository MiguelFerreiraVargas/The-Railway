using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class DeerAI : MonoBehaviour
{
    private enum DeerState
    {
        Idle,
        Walking,
        Dead
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

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

    [Header("Death")]
    [SerializeField] private GameObject meatPrefab;
    [SerializeField] private float disappearTime = 2f;

    private DeerState currentState;
    private float stateTimer;
    private float currentHealth;
    private bool dead;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;

        agent.speed = walkSpeed;
        agent.stoppingDistance = 0.2f;
        agent.autoBraking = true;

        ChangeState(DeerState.Idle);
    }

    private void Update()
    {
        if (dead)
            return;

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

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (dead)
            return;

        dead = true;
        currentState = DeerState.Dead;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Desliga a animação de andar
        if (animator != null)
            animator.SetBool("IsWalking", false);

        // Dropa a carne
        if (meatPrefab != null)
        {
            Instantiate(
                meatPrefab,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );
        }

        StartCoroutine(Disappear());
    }

    private IEnumerator Disappear()
    {
        Vector3 originalScale = transform.localScale;

        float timer = 0f;

        while (timer < disappearTime)
        {
            timer += Time.deltaTime;

            float percentage = timer / disappearTime;

            transform.localScale =
                Vector3.Lerp(
                    originalScale,
                    Vector3.zero,
                    percentage
                );

            yield return null;
        }

        Destroy(gameObject);
    }

    private void ChangeState(DeerState newState)
    {
        if (dead)
            return;

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

        if (agent.pathPending)
            return;

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            ChangeState(DeerState.Idle);
            return;
        }

        if (stateTimer <= 0f)
        {
            agent.ResetPath();
            ChangeState(DeerState.Idle);
            return;
        }

        if (agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 direction = agent.velocity.normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction);

                transform.rotation =
                    Quaternion.Slerp(
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
            Random.Range(
                minWalkDistance,
                maxWalkDistance
            );

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
            animator.SetBool("IsWalking", walking);
    }
}