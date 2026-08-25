using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCarry : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;

    [Header("Input")]
    [SerializeField] private InputActionReference grabAction;

    [Header("Settings")]
    [SerializeField] private float grabDistance = 3f;
    [SerializeField] private LayerMask grabbableLayer;

    private Rigidbody heldObject;
    private Collider heldCollider;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (grabAction != null)
        {
            grabAction.action.Enable();
            grabAction.action.performed += OnGrab;
        }
    }

    private void OnDisable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed -= OnGrab;
            grabAction.action.Disable();
        }
    }

    private void OnGrab(InputAction.CallbackContext context)
    {
        if (heldObject == null)
            TryGrab();

        else
            Drop();
    }

    private void TryGrab()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            grabDistance,
            grabbableLayer
        ))
            return;

        Rigidbody rb =
            hit.collider.GetComponentInParent<Rigidbody>();

        if (rb == null)
            return;

        heldObject = rb;
        heldCollider = rb.GetComponent<Collider>();

        heldObject.isKinematic = true;

        if (heldCollider != null)
            heldCollider.enabled = false;

        heldObject.transform.SetParent(holdPoint);

        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
    }

    public void Drop()
    {
        if (heldObject == null)
            return;

        heldObject.transform.SetParent(null);

        heldObject.isKinematic = false;

        if (heldCollider != null)
            heldCollider.enabled = true;

        heldObject = null;
        heldCollider = null;
    }

    public GameObject GetHeldObject()
    {
        return heldObject != null
            ? heldObject.gameObject
            : null;
    }
}