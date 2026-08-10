using UnityEngine;
using UnityEngine.Splines;
using StarterAssets;

public class TrainInteraction : MonoBehaviour
{
    [Header("Trem")]
    public SplineAnimate splineAnimate;

    [Header("Player")]
    public Transform player;
    public Transform trainSeat;

    [Header("Saída")]
    public Transform exitPoint;

    [Header("Player Input")]
    public StarterAssetsInputs playerInputs;

    private bool insideTrain = false;

    private CharacterController characterController;
    private Vector3 lastSeatPosition;

    void Start()
    {
        if (player != null)
        {
            characterController =
                player.GetComponent<CharacterController>();
        }
    }

    void LateUpdate()
    {
        if (!insideTrain)
            return;

        if (player == null || trainSeat == null)
            return;

        // Bloqueia o movimento do Player
        if (playerInputs != null)
        {
            playerInputs.MoveInput(Vector2.zero);
            playerInputs.JumpInput(false);
            playerInputs.SprintInput(false);
        }

        // Movimento do trem desde o último frame
        Vector3 trainMovement =
            trainSeat.position - lastSeatPosition;

        // Faz o Player acompanhar o trem
        if (characterController != null &&
            characterController.enabled)
        {
            characterController.Move(trainMovement);
        }
        else
        {
            player.position += trainMovement;
        }

        lastSeatPosition = trainSeat.position;
    }

    public void ToggleTrain()
    {
        if (insideTrain)
            ExitTrain();
        else
            EnterTrain();
    }

    void EnterTrain()
    {
        if (player == null ||
            trainSeat == null ||
            splineAnimate == null)
        {
            Debug.LogWarning(
                "Configure Player, TrainSeat e Spline Animate."
            );
            return;
        }

        insideTrain = true;

        // Coloca o Player no Seat
        if (characterController != null)
            characterController.enabled = false;

        player.position = trainSeat.position;

        if (characterController != null)
            characterController.enabled = true;

        // Guarda a posição inicial do Seat
        lastSeatPosition = trainSeat.position;

        // Começa o trem
        splineAnimate.Play();
    }

    void ExitTrain()
    {
        if (player == null ||
            exitPoint == null ||
            splineAnimate == null)
        {
            Debug.LogWarning(
                "Configure o ExitPoint."
            );
            return;
        }

        // Para o trem
        splineAnimate.Pause();

        insideTrain = false;

        // Vai para o ExitPoint
        if (characterController != null)
            characterController.enabled = false;

        player.position = exitPoint.position;
        player.rotation = exitPoint.rotation;

        if (characterController != null)
            characterController.enabled = true;

        // Libera o movimento
        if (playerInputs != null)
        {
            playerInputs.MoveInput(Vector2.zero);
            playerInputs.JumpInput(false);
            playerInputs.SprintInput(false);
        }
    }
}