using UnityEngine;
using UnityEngine.InputSystem;

public class TrainCameraView : MonoBehaviour
{
    [Header("Câmeras")]
    public Camera playerCamera;
    public Camera trainCamera;

    [Header("Trem")]
    public Transform train;
    public TrainInteraction trainInteraction;

    [Header("Player")]
    public Transform player;

    [Header("Distância")]
    public float startingDistance = 30f;
    public float minDistance = 5f;
    public float maxDistance = 60f;

    [Header("Zoom")]
    public float zoomSpeed = 8f;

    [Header("Mouse")]
    public float mouseSensitivity = 0.15f;

    [Header("Ângulo")]
    public float startingPitch = 20f;
    public float minPitch = -10f;
    public float maxPitch = 75f;

    [Header("Altura do alvo")]
    public float targetHeight = 2f;

    [Header("Colisão")]
    public LayerMask collisionMask;
    public float collisionRadius = 0.5f;
    public float collisionPadding = 0.3f;

    private bool viewingTrain = false;

    private float yaw = 180f;
    private float pitch;

    private float distance;

    private Renderer[] playerRenderers;

    void Start()
    {
        distance = startingDistance;
        pitch = startingPitch;

        if (playerCamera != null)
            playerCamera.enabled = true;

        if (trainCamera != null)
            trainCamera.enabled = false;

        // Pega TODOS os Renderers do Player.
        // Isso inclui capsule, braços, corpo, roupas etc.
        if (player != null)
        {
            playerRenderers =
                player.GetComponentsInChildren<Renderer>(true);
        }

        // Mouse sempre travado
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        // =====================================================
        // CTRL
        // =====================================================

        bool ctrlPressed =
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed;

        // =====================================================
        // SÓ FUNCIONA DENTRO DO TREM
        // =====================================================

        bool playerInsideTrain =
            trainInteraction != null &&
            trainInteraction.IsInsideTrain;

        if (ctrlPressed && playerInsideTrain)
        {
            if (!viewingTrain)
            {
                EnterTrainCamera();
            }

            UpdateMouse();
            UpdateZoom();
        }
        else
        {
            if (viewingTrain)
            {
                ExitTrainCamera();
            }
        }

        // Mouse permanece travado
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // =========================================================
    // IMPORTANTE:
    // A câmera acompanha o trem no LateUpdate.
    // O trem já terminou de se mover neste frame.
    // =========================================================

    void LateUpdate()
    {
        if (!viewingTrain)
            return;

        UpdateTrainCamera();
    }

    // =========================================================
    // ENTRAR NA CÂMERA
    // =========================================================

    void EnterTrainCamera()
    {
        if (playerCamera == null ||
            trainCamera == null ||
            train == null)
            return;

        viewingTrain = true;

        playerCamera.enabled = false;
        trainCamera.enabled = true;

        HidePlayer();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetCameraPosition();
    }

    // =========================================================
    // SAIR DA CÂMERA
    // =========================================================

    void ExitTrainCamera()
    {
        viewingTrain = false;

        if (trainCamera != null)
            trainCamera.enabled = false;

        if (playerCamera != null)
            playerCamera.enabled = true;

        ShowPlayer();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // =========================================================
    // MOUSE
    // =========================================================

    void UpdateMouse()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouse =
            Mouse.current.delta.ReadValue();

        yaw +=
            mouse.x *
            mouseSensitivity;

        pitch -=
            mouse.y *
            mouseSensitivity;

        pitch =
            Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );
    }

    // =========================================================
    // ZOOM
    // =========================================================

    void UpdateZoom()
    {
        if (Mouse.current == null)
            return;

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -=
                scroll *
                zoomSpeed;

            distance =
                Mathf.Clamp(
                    distance,
                    minDistance,
                    maxDistance
                );
        }
    }

    // =========================================================
    // CÂMERA
    // =========================================================

    void UpdateTrainCamera()
    {
        if (trainCamera == null ||
            train == null)
            return;

        // Ponto que a câmera observa
        Vector3 target =
            train.position +
            Vector3.up * targetHeight;

        // Rotação controlada pelo mouse
        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        Vector3 direction =
            rotation * Vector3.forward;

        // Posição desejada
        Vector3 desiredPosition =
            target -
            direction * distance;

        // Verifica colisão
        Vector3 finalPosition =
            CheckCollision(
                target,
                desiredPosition
            );

        // SEM LERP
        // A câmera acompanha exatamente o trem.
        trainCamera.transform.position =
            finalPosition;

        // Olha para o trem
        Vector3 lookDirection =
            target -
            trainCamera.transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            trainCamera.transform.rotation =
                Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up
                );
        }
    }

    // =========================================================
    // COLISÃO
    // =========================================================

    Vector3 CheckCollision(
        Vector3 target,
        Vector3 desiredPosition)
    {
        Vector3 direction =
            desiredPosition - target;

        float distanceToCamera =
            direction.magnitude;

        if (distanceToCamera <= 0.01f)
            return desiredPosition;

        direction.Normalize();

        if (Physics.SphereCast(
            target,
            collisionRadius,
            direction,
            out RaycastHit hit,
            distanceToCamera,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            return
                hit.point -
                direction *
                collisionPadding;
        }

        return desiredPosition;
    }

    // =========================================================
    // POSIÇÃO INICIAL
    // =========================================================

    void SetCameraPosition()
    {
        UpdateTrainCamera();
    }

    // =========================================================
    // ESCONDER PLAYER
    // =========================================================

    void HidePlayer()
    {
        if (playerRenderers == null)
            return;

        foreach (Renderer renderer in playerRenderers)
        {
            renderer.enabled = false;
        }
    }

    // =========================================================
    // MOSTRAR PLAYER
    // =========================================================

    void ShowPlayer()
    {
        if (playerRenderers == null)
            return;

        foreach (Renderer renderer in playerRenderers)
        {
            renderer.enabled = true;
        }
    }
}