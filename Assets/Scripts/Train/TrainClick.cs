using UnityEngine;
using UnityEngine.InputSystem;

public class TrainClick : MonoBehaviour
{
    public Camera playerCamera;
    public float interactionDistance = 5f;

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (playerCamera == null)
            return;

        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            TrainInteraction train =
                hit.collider.GetComponentInParent<TrainInteraction>();

            if (train != null)
            {
                train.ToggleTrain();
            }
        }
    }
}