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

    [Header("Ajuste do assento")]
    public Vector3 seatOffset = Vector3.zero;

    [Header("Saída")]
    public Transform exitPoint;

    [Header("Player Input")]
    public StarterAssetsInputs playerInputs;

    private bool insideTrain = false;

    private CharacterController characterController;

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

        // =====================================================
        // BLOQUEIA O MOVIMENTO DO PLAYER
        // =====================================================

        if (playerInputs != null)
        {
            playerInputs.MoveInput(Vector2.zero);
            playerInputs.JumpInput(false);
            playerInputs.SprintInput(false);
        }

        // =====================================================
        // PRENDE O PLAYER EXATAMENTE NO SEAT
        // =====================================================

        Vector3 seatPosition =
            trainSeat.TransformPoint(seatOffset);

        player.position = seatPosition;

        // IMPORTANTE:
        // NÃO copiamos a rotação do TrainSeat.
        // Isso deixa a câmera livre para olhar.
    }

    // =========================================================
    // E = ENTRAR / SAIR
    // =========================================================

    public void ToggleTrain()
    {
        if (insideTrain)
            ExitTrain();
        else
            EnterTrain();
    }

    // =========================================================
    // ENTRAR
    // =========================================================

    void EnterTrain()
    {
        if (player == null ||
            trainSeat == null ||
            splineAnimate == null)
        {
            Debug.LogWarning(
                "TrainInteraction: configure Player, TrainSeat e Spline Animate."
            );

            return;
        }

        insideTrain = true;

        // Desliga o CharacterController apenas durante
        // o posicionamento inicial.
        if (characterController != null)
            characterController.enabled = false;

        // Coloca exatamente no Seat
        player.position =
            trainSeat.TransformPoint(seatOffset);

        // Liga novamente
        if (characterController != null)
            characterController.enabled = true;

        // Zera o movimento
        StopPlayer();

        // Começa o trem
        splineAnimate.Play();
    }

    // =========================================================
    // SAIR
    // =========================================================

    void ExitTrain()
    {
        if (player == null ||
            exitPoint == null ||
            splineAnimate == null)
        {
            Debug.LogWarning(
                "TrainInteraction: configure o ExitPoint."
            );

            return;
        }

        // Para o trem
        splineAnimate.Pause();

        insideTrain = false;

        // Desliga temporariamente o CharacterController
        if (characterController != null)
            characterController.enabled = false;

        // Teleporta para o ponto de saída
        player.position = exitPoint.position;
        player.rotation = exitPoint.rotation;

        // Liga novamente
        if (characterController != null)
            characterController.enabled = true;

        StopPlayer();
    }

    // =========================================================
    // BLOQUEAR MOVIMENTO
    // =========================================================

    void StopPlayer()
    {
        if (playerInputs == null)
            return;

        playerInputs.MoveInput(Vector2.zero);
        playerInputs.JumpInput(false);
        playerInputs.SprintInput(false);
    }
}