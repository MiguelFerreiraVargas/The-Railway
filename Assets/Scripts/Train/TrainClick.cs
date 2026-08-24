using UnityEngine;
using UnityEngine.InputSystem;

public class TrainClick : MonoBehaviour
{
    [Header("Câmera")]
    public Camera playerCamera;

    [Header("Distância")]
    public float interactionDistance = 5f;

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null)
            return;

        if (!UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (playerCamera == null)
            return;

        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(
                Screen.width / 2f,
                Screen.height / 2f,
                0f
            )
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance))
        {
            TrainInteraction train =
                hit.collider.GetComponentInParent<TrainInteraction>();

            if (train != null)
            {
                Debug.Log(
                    "TREM ENCONTRADO - E FUNCIONOU!"
                );

                train.ToggleTrain();
            }
            else
            {
                Debug.Log(
                    "E apertado, mas o objeto atingido " +
                    "não possui TrainInteraction."
                );
            }
        }
        else
        {
            Debug.Log(
                "E apertado, mas o Raycast não atingiu o trem."
            );
        }
    }
}