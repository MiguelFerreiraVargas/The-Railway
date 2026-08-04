using UnityEngine;

public class TRCamera : MonoBehaviour
{
    [Header("Movimento")]
    public float moveSpeed = 20f;
    public float dragSpeed = 0.5f;

    [Header("Rotação")]
    public float rotationSpeed = 100f;

    [Header("Zoom")]
    public Camera cam;
    public float zoomSpeed = 500f;
    public float minZoom = 15f;
    public float maxZoom = 80f;

    [Header("Limites do Mapa")]
    public float minX = -100f;
    public float maxX = 100f;
    public float minZ = -100f;
    public float maxZ = 100f;

    private Vector3 lastMousePosition;

    void Update()
    {
        MoveCamera();
        RotateCamera();
        ZoomCamera();
        DragCamera();
        ClampPosition();
    }

    void MoveCamera()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * v + right * h) * moveSpeed * Time.deltaTime;

        transform.position += move;
    }

    void RotateCamera()
    {
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);
        }

        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    void ZoomCamera()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            Vector3 pos = cam.transform.localPosition;

            pos += cam.transform.forward * scroll * zoomSpeed * Time.deltaTime;

            float distance = Vector3.Distance(
                cam.transform.localPosition,
                Vector3.zero
            );

            if (distance > maxZoom)
                pos = pos.normalized * maxZoom;

            if (distance < minZoom)
                pos = pos.normalized * minZoom;

            cam.transform.localPosition = pos;
        }
    }

    void DragCamera()
    {
        if (Input.GetMouseButtonDown(1))
        {
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;

            Vector3 move =
                (-transform.right * delta.x +
                -transform.forward * delta.y) *
                dragSpeed *
                Time.deltaTime;

            move.y = 0;

            transform.position += move;

            lastMousePosition = Input.mousePosition;
        }
    }

    void ClampPosition()
    {
        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        transform.position = pos;
    }
}
