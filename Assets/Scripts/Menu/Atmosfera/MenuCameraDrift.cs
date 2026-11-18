using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimento sutil de câmera para o menu: respiração lenta (como câmera na mão)
/// e paralaxe leve seguindo o mouse.
/// </summary>
public class MenuCameraDrift : MonoBehaviour
{
    [Header("Respiração")]
    [Tooltip("Deslocamento máximo de posição, em metros.")]
    [SerializeField] private Vector3 positionAmplitude = new Vector3(0.06f, 0.04f, 0f);

    [Tooltip("Rotação máxima em graus (pitch, yaw, roll).")]
    [SerializeField] private Vector3 rotationAmplitude = new Vector3(0.35f, 0.5f, 0.25f);

    [SerializeField] private float driftSpeed = 0.18f;

    [Header("Paralaxe do mouse")]
    [Tooltip("Graus de rotação quando o mouse está na borda da tela.")]
    [SerializeField] private Vector2 mouseParallax = new Vector2(1.2f, 0.8f);

    [SerializeField] private float parallaxSmoothing = 2.5f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector2 smoothedMouse;
    private float seed;

    private void Awake()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
        seed = Random.Range(0f, 100f);
    }

    private void LateUpdate()
    {
        float t = Time.unscaledTime * driftSpeed;

        Vector3 offset = new Vector3(
            Noise(t, 0f) * positionAmplitude.x,
            Noise(t, 11f) * positionAmplitude.y,
            Noise(t, 23f) * positionAmplitude.z);

        Vector3 euler = new Vector3(
            Noise(t, 41f) * rotationAmplitude.x,
            Noise(t, 57f) * rotationAmplitude.y,
            Noise(t, 73f) * rotationAmplitude.z);

        Vector2 mouseTarget = Vector2.zero;

        if (Mouse.current != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();
            mouseTarget = new Vector2(
                Mathf.Clamp(mouse.x / Screen.width * 2f - 1f, -1f, 1f),
                Mathf.Clamp(mouse.y / Screen.height * 2f - 1f, -1f, 1f));
        }

        smoothedMouse = Vector2.Lerp(
            smoothedMouse,
            mouseTarget,
            1f - Mathf.Exp(-parallaxSmoothing * Time.unscaledDeltaTime));

        euler += new Vector3(
            -smoothedMouse.y * mouseParallax.y,
            smoothedMouse.x * mouseParallax.x,
            0f);

        transform.localPosition = startPosition + offset;
        transform.localRotation = startRotation * Quaternion.Euler(euler);
    }

    private float Noise(float t, float offset)
    {
        return Mathf.PerlinNoise(seed + offset, t) * 2f - 1f;
    }
}
