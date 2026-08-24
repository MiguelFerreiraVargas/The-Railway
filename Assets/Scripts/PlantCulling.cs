using UnityEngine;

public class PlantCulling : MonoBehaviour
{
    private Transform player;
    private Renderer[] plantRenderers;

    public float angle = 120f;
    public float maxDistance = 50f;

    void Start()
    {
        // Procura automaticamente o Player na cena
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogError("Não encontrei um objeto com a Tag 'Player'.");
        }

        plantRenderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (player == null)
            return;

        Vector3 direction = transform.position - player.position;
        float distance = direction.magnitude;

        if (distance > maxDistance)
        {
            SetRender(false);
            return;
        }

        direction.Normalize();

        float dot = Vector3.Dot(player.forward, direction);
        float limit = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);

        SetRender(dot > limit);
    }

    void SetRender(bool visible)
    {
        foreach (Renderer r in plantRenderers)
        {
            r.enabled = visible;
        }
    }
}
