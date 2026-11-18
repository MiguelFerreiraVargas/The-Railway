using UnityEngine;

/// <summary>Mantém um quad (brilho de farol, por exemplo) virado para a câmera.</summary>
public class Billboard : MonoBehaviour
{
    private Camera cam;

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
