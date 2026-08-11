using UnityEngine;

public class RailPieceCulling : MonoBehaviour
{
    [Header("Distância para renderizar")]
    public float renderDistance = 100f;

    private Transform target;
    private Renderer[] renderers;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>(true);

        // Procura automaticamente a câmera principal
        if (Camera.main != null)
            target = Camera.main.transform;
    }

    void Update()
    {
        if (target == null)
        {
            if (Camera.main != null)
                target = Camera.main.transform;

            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            target.position
        );

        bool visible = distance <= renderDistance;

        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = visible;
        }
    }
}