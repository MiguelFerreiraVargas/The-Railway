using UnityEngine;

public class RailwayCamera : MonoBehaviour
{
    [Header("Movimento")]
    public float moveSpeed = 50f;

    [Header("Zoom")]
    public float zoomSpeed = 300f;
    public float minY = 15f;
    public float maxY = 120f;

    [Header("Rotação")]
    public float rotateSpeed = 120f;

    [Header("Mapa")]
    public float minX = -500f;
    public float maxX = 500f;
    public float minZ = -500f;
    public float maxZ = 500f;

    void Update()
    {
        Move();
        Zoom();
        Rotate();
        ClampPosition();
    }

    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * v + right * h;

        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    void Zoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        Vector3 pos = transform.position;

        pos += transform.forward * scroll * zoomSpeed * Time.deltaTime;

        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }

    void Rotate()
    {
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X");

            transform.Rotate(
                Vector3.up,
                mouseX * rotateSpeed * Time.deltaTime,
                Space.World
            );
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