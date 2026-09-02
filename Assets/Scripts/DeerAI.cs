using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class DeerAI : MonoBehaviour
{
    private enum DeerState
    {
        Idle,
        Walking,
        Running,
        Dead
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Wandering")]
    [SerializeField] private float minWalkDistance = 5f;
    [SerializeField] private float maxWalkDistance = 12f;
    [SerializeField] private float idleMinTime = 2f;
    [SerializeField] private float idleMaxTime = 5f;
    [SerializeField] private float walkMinTime = 4f;
    [SerializeField] private float walkMaxTime = 10f;

    [Header("Fuga (ao tomar dano sem morrer)")]
    [SerializeField] private float fleeDistance = 10f;
    [SerializeField] private float fleeDuration = 4f;

    [Header("NavMesh")]
    [SerializeField] private float sampleDistance = 10f;

    [Header("Death - Ragdoll")]
    [SerializeField] private Rigidbody[] ragdollBodies;        // Rigidbodies criados pela Ragdoll Wizard (ossos)
    [SerializeField] private Collider[] ragdollColliders;      // Colliders dos ossos do ragdoll
    [SerializeField] private Collider mainCollider;            // o Collider principal do veado (Capsule, usado enquanto vivo)

    [Header("Death - Timing")]
    [SerializeField] private float bodyStayTime = 20f;         // quanto tempo o corpo fica largado antes de sumir, se ninguém coletar
    [SerializeField] private float sinkDuration = 2f;          // tempo do fade ao sumir
    [SerializeField] private GameObject deathParticlesPrefab;  // opcional: poeira/partículas ao morrer

    [Header("Death - Coleta de Carne")]
    [SerializeField] private string corpseLayerName = "Interactable"; // layer que os ossos do corpo recebem pra virar interagível
    [SerializeField] private string rawMeatItemId = "carne_crua";
    [SerializeField] private int meatAmount = 2;

    private DeerState currentState;
    private float stateTimer;
    private float currentHealth;
    private bool dead;
    private bool meatCollected;
    private Vector3 fleeFromPosition;

    private Renderer[] renderers;
    private MaterialPropertyBlock propBlock;
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (ragdollBodies == null || ragdollBodies.Length == 0)
            ragdollBodies = GetComponentsInChildren<Rigidbody>(true);

        if (ragdollColliders == null || ragdollColliders.Length == 0)
            ragdollColliders = GetComponentsInChildren<Collider>(true);

        renderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();

        // ragdoll começa desligado: só o mainCollider e a Animator controlam o veado vivo
        foreach (var rb in ragdollBodies)
        {
            if (rb != null) rb.isKinematic = true;
        }

        foreach (var col in ragdollColliders)
        {
            if (col != null && col != mainCollider) col.enabled = false;
        }

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
            case DeerState.Running:
                UpdateMoving();
                break;
        }
    }

    public void TakeDamage(float damage, Vector3? damageSourcePosition = null)
    {
        if (dead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            Flee(damageSourcePosition);
        }
    }

    private void Flee(Vector3? sourcePosition)
    {
        fleeFromPosition = sourcePosition ?? (transform.position - transform.forward);
        ChangeState(DeerState.Running);
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
            agent.enabled = false;
        }

        if (mainCollider != null)
            mainCollider.enabled = false;

        EnableRagdoll();

        if (deathParticlesPrefab != null)
        {
            Instantiate(deathParticlesPrefab, transform.position, Quaternion.identity);
        }

        StartCoroutine(DeathSequence());
    }

    // Desliga a Animator e libera a física em cada osso do ragdoll (criado antes com
    // GameObject > 3D Object > Ragdoll no editor), e muda a layer dos ossos pra que
    // o corpo vire algo "interagível" (pro raycast do PlayerArmActions achar ele).
    private void EnableRagdoll()
    {
        if (animator != null)
            animator.enabled = false;

        int corpseLayer = LayerMask.NameToLayer(corpseLayerName);

        if (ragdollBodies != null)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (ragdollColliders != null)
        {
            foreach (var col in ragdollColliders)
            {
                if (col == null) continue;
                col.enabled = true;

                if (corpseLayer != -1)
                    col.gameObject.layer = corpseLayer;
            }
        }
    }

    // Chamado pelo PlayerArmActions (via raycast na interactableLayer) quando o
    // player interage com o corpo caído.
    public void Interact()
    {
        if (!dead || meatCollected)
            return;

        meatCollected = true;

        PlayerInventory.Instance?.AddItem(rawMeatItemId, meatAmount);

        // já coletou a carne, não precisa mais esperar o timer normal — some agora
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    private IEnumerator DeathSequence()
    {
        yield return StartCoroutine(WaitForRagdollToSettle());

        // se ninguém coletar a carne nesse tempo, o corpo some sozinho
        yield return new WaitForSeconds(bodyStayTime);

        if (!meatCollected)
            yield return StartCoroutine(FadeOut());
    }

    private IEnumerator WaitForRagdollToSettle()
    {
        const float velocityThreshold = 0.05f;
        const float maxWaitTime = 5f;
        float timer = 0f;

        while (timer < maxWaitTime)
        {
            bool allSettled = true;

            if (ragdollBodies != null)
            {
                foreach (var rb in ragdollBodies)
                {
                    if (rb != null && !rb.IsSleeping() && rb.linearVelocity.sqrMagnitude > velocityThreshold)
                    {
                        allSettled = false;
                        break;
                    }
                }
            }

            if (allSettled)
                yield break;

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        float timer = 0f;

        while (timer < sinkDuration)
        {
            timer += Time.deltaTime;
            SetFade(1f - (timer / sinkDuration));
            yield return null;
        }

        SetFade(0f);
        Destroy(gameObject);
    }

    // Funciona com shaders que tenham propriedade "_Alpha" (URP/Lit transparente, shader custom, etc).
    private void SetFade(float alpha)
    {
        if (renderers == null) return;

        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propBlock);
            propBlock.SetFloat(AlphaID, alpha);
            rend.SetPropertyBlock(propBlock);
        }
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
                stateTimer = Random.Range(idleMinTime, idleMaxTime);
                SetMovementAnimation(false, false);
                break;

            case DeerState.Walking:
                agent.speed = walkSpeed;

                if (FindRandomDestination(minWalkDistance, maxWalkDistance))
                {
                    agent.isStopped = false;
                    stateTimer = Random.Range(walkMinTime, walkMaxTime);
                    SetMovementAnimation(true, false);
                }
                else
                {
                    ChangeState(DeerState.Idle);
                }
                break;

            case DeerState.Running:
                agent.speed = runSpeed;

                if (FindDestinationAwayFrom(fleeFromPosition, fleeDistance))
                {
                    agent.isStopped = false;
                    stateTimer = fleeDuration;
                    SetMovementAnimation(false, true);
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

    private void UpdateMoving()
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
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private bool FindRandomDestination(float minDist, float maxDist)
    {
        Vector3 randomDirection = Random.insideUnitSphere * Random.Range(minDist, maxDist);
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            return true;
        }

        return false;
    }

    private bool FindDestinationAwayFrom(Vector3 source, float distance)
    {
        Vector3 awayDirection = (transform.position - source).normalized;

        if (awayDirection == Vector3.zero)
            awayDirection = -transform.forward;

        Vector3 target = transform.position + awayDirection * distance;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            return true;
        }

        return false;
    }

    private void SetMovementAnimation(bool walking, bool running)
    {
        if (animator == null) return;
        animator.SetBool("IsWalking", walking);
        animator.SetBool("IsRunning", running);
    }
}