using System.Collections;
using UnityEngine;

public class Tree : MonoBehaviour, IInteractable
{
    [Header("Outline")]
    [SerializeField] private Outline outline;

    [Header("Vida")]
    [SerializeField] private int life = 3;

    [Header("Queda")]
    [SerializeField] private float fallDuration = 1.2f;
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeAmount = 3f;

    [Header("Direção da queda")]
    [SerializeField] private Transform player;

    [Header("Troncos")]
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private int logsToDrop = 3;

    [Header("Efeitos")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private GameObject fallEffect;

    [Header("Som")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip fallSound;
    [SerializeField] private AudioSource audioSource;

    private bool isFalling;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private void Awake()
    {
        if (outline == null)
            outline = GetComponent<Outline>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        originalPosition = transform.position;
        originalRotation = transform.rotation;

        HideOutline();
    }

    public bool Interact()
    {
        TakeDamage(1);
        return true;
    }

    public void TakeDamage(int damage)
    {
        if (isFalling)
            return;

        life -= damage;

        Debug.Log("Vida da árvore: " + life);

        PlayHitSound();

        if (hitEffect != null)
        {
            Instantiate(
                hitEffect,
                transform.position,
                Quaternion.identity
            );
        }

        if (life <= 0)
        {
            StartCoroutine(FallTree());
        }
        else
        {
            StartCoroutine(ShakeTree());
        }
    }

    public int GetLife()
    {
        return life;
    }

    private IEnumerator ShakeTree()
    {
        Quaternion startRotation = transform.rotation;

        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            float strength =
                Mathf.Sin(elapsed * 50f) * shakeAmount;

            transform.rotation =
                startRotation *
                Quaternion.Euler(0f, 0f, strength);

            yield return null;
        }

        transform.rotation = startRotation;
    }

    private IEnumerator FallTree()
    {
        isFalling = true;

        HideOutline();

        DisableColliders();

        PlayFallSound();

        if (fallEffect != null)
        {
            Instantiate(
                fallEffect,
                transform.position,
                Quaternion.identity
            );
        }

        // Encontra o jogador automaticamente
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        Vector3 fallDirection;

        if (player != null)
        {
            // Direção do jogador até a árvore
            Vector3 direction =
                transform.position - player.position;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
                fallDirection = direction.normalized;
            else
                fallDirection = -transform.forward;
        }
        else
        {
            fallDirection = -transform.forward;
        }

        // Decide se cai para frente ou para trás
        float dot =
            Vector3.Dot(
                transform.forward,
                fallDirection
            );

        bool fallForward = dot > 0f;

        float targetAngle =
            fallForward ? 75f : -75f;

        Quaternion startRotation =
            transform.rotation;

        Quaternion targetRotation =
            startRotation *
            Quaternion.Euler(
                targetAngle,
                0f,
                0f
            );

        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / fallDuration
                );

            // Queda acelerando no final
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t
                );

            yield return null;
        }

        transform.rotation = targetRotation;

        yield return new WaitForSeconds(0.3f);

        DropLogs();
    }

    private void DropLogs()
    {
        if (logPrefab == null)
        {
            Debug.LogWarning(
                "Log Prefab não foi configurado!"
            );

            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < logsToDrop; i++)
        {
            Vector3 position =
                transform.position +
                new Vector3(
                    Random.Range(-1f, 1f),
                    0.5f,
                    Random.Range(-1f, 1f)
                );

            GameObject log =
                Instantiate(
                    logPrefab,
                    position,
                    Random.rotation
                );

            Rigidbody rb =
                log.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.AddForce(
                    Random.insideUnitSphere * 1.5f,
                    ForceMode.Impulse
                );
            }
        }

        Destroy(gameObject);
    }

    private void DisableColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    private void PlayHitSound()
    {
        if (audioSource != null &&
            hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    private void PlayFallSound()
    {
        if (audioSource != null &&
            fallSound != null)
        {
            audioSource.PlayOneShot(fallSound);
        }
    }

    public void ShowOutline()
    {
        if (outline != null)
            outline.enabled = true;
    }

    public void HideOutline()
    {
        if (outline != null)
            outline.enabled = false;
    }
}