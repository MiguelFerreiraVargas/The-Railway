using UnityEngine;
using UnityEngine.InputSystem;

public class Interaction : MonoBehaviour
{
    [SerializeField] private float _interactionRange = 100f;
    private Camera _mainCam;
    private IInteractable _target;//Objeto alvo do raycast
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _mainCam = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = _mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, _interactionRange))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
            {
                if (_target == interactable)
                    return;

                _target?.HideOutline();

                _target = interactable;
                _target.ShowOutline();
            }
            else
            {
                _target?.HideOutline();
                _target = null;
            }
        }
        else
        {
            _target?.HideOutline();
            _target = null;
        }
    }
    public void OnInteract(InputValue value)
    {
        if (_target == null)//Nullcheck
            return;

        _target.Interact();
    }
}
