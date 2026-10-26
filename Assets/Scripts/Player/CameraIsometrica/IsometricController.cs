using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class IsometricController : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float velocidade = 5f;
    [SerializeField] private float velocidadeRotacao = 12f;

    [Header("Camera Isometrica")]
    [SerializeField] private Transform cameraIsometrica;

    [Header("Gravidade")]
    [SerializeField] private float gravidade = -20f;

    private CharacterController controller;
    private Vector3 velocidadeVertical;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraIsometrica == null && Camera.main != null)
            cameraIsometrica = Camera.main.transform;
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 frente = cameraIsometrica.forward;
        Vector3 direita = cameraIsometrica.right;

        frente.y = 0f;
        direita.y = 0f;

        frente.Normalize();
        direita.Normalize();

        Vector3 direcao =
            (direita * horizontal + frente * vertical).normalized;

        if (direcao.magnitude > 0.01f)
        {
            Quaternion rotacaoAlvo =
                Quaternion.LookRotation(direcao);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rotacaoAlvo,
                velocidadeRotacao * Time.deltaTime
            );
        }

        Vector3 movimento = direcao * velocidade;

        if (controller.isGrounded && velocidadeVertical.y < 0f)
            velocidadeVertical.y = -2f;

        velocidadeVertical.y += gravidade * Time.deltaTime;
        movimento.y = velocidadeVertical.y;

        controller.Move(movimento * Time.deltaTime);
    }
}
