using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using StarterAssets;

public class TrainInteraction : MonoBehaviour
{
    [Header("Spline")]
    public SplineAnimate splineAnimate;

    [Header("Velocidade")]
    public float maxSpeed = 15f;
    public float accelerationTime = 8f;
    public float decelerationTime = 5f;

    [Header("Player")]
    public Transform player;
    public Transform trainSeat;

    [Header("Ajuste do assento")]
    public Vector3 seatOffset = Vector3.zero;

    [Header("Saída")]
    public Transform exitPoint;

    [Header("Player Input")]
    public StarterAssetsInputs playerInputs;

    private SplineContainer splineContainer;
    private CharacterController characterController;

    private bool insideTrain = false;

    // Permite que outros scripts saibam se o jogador está no trem
    public bool IsInsideTrain => insideTrain;

    private bool accelerating = false;
    private bool braking = false;
    private bool waitingToExit = false;

    private float currentSpeed = 0f;
    private float splinePosition = 0f;
    private float splineLength = 0f;

    void Awake()
    {
        if (splineAnimate == null)
        {
            splineAnimate = GetComponent<SplineAnimate>();
        }

        if (splineAnimate != null)
        {
            splineContainer = splineAnimate.Container;
        }

        if (player != null)
        {
            characterController =
                player.GetComponent<CharacterController>();
        }
    }

    void Start()
    {
        if (splineContainer == null)
        {
            Debug.LogError(
                "TrainInteraction: não encontrou o Spline Container!"
            );

            return;
        }

        splineLength =
            splineContainer.CalculateLength();

        if (splineAnimate != null)
        {
            splineAnimate.Pause();
        }
    }

    void Update()
    {
        if (splineContainer == null)
            return;

        if (!insideTrain && !braking)
            return;

        // =====================================================
        // ACELERAÇÃO
        // =====================================================

        if (accelerating)
        {
            float acceleration =
                maxSpeed /
                Mathf.Max(
                    accelerationTime,
                    0.01f
                );

            currentSpeed =
                Mathf.MoveTowards(
                    currentSpeed,
                    maxSpeed,
                    acceleration * Time.deltaTime
                );

            if (currentSpeed >= maxSpeed)
            {
                currentSpeed = maxSpeed;
                accelerating = false;
            }
        }

        // =====================================================
        // FREIO
        // =====================================================

        if (braking)
        {
            float deceleration =
                maxSpeed /
                Mathf.Max(
                    decelerationTime,
                    0.01f
                );

            currentSpeed =
                Mathf.MoveTowards(
                    currentSpeed,
                    0f,
                    deceleration * Time.deltaTime
                );

            // =================================================
            // TREM PAROU
            // =================================================

            if (currentSpeed <= 0.001f)
            {
                currentSpeed = 0f;
                braking = false;

                SetTrainPosition();

                if (waitingToExit)
                {
                    waitingToExit = false;

                    ExitPlayer();
                }
            }
        }

        // =====================================================
        // MOVIMENTO
        // =====================================================

        if (currentSpeed > 0f &&
            splineLength > 0f)
        {
            float movement =
                (currentSpeed / splineLength) *
                Time.deltaTime;

            splinePosition += movement;

            splinePosition =
                Mathf.Clamp01(
                    splinePosition
                );

            SetTrainPosition();

            // =================================================
            // FINAL DO SPLINE
            // =================================================

            if (splinePosition >= 1f)
            {
                splinePosition = 1f;

                currentSpeed = 0f;
                accelerating = false;
                braking = false;

                SetTrainPosition();

                Debug.Log(
                    "Trem chegou ao final do spline."
                );

                if (waitingToExit)
                {
                    waitingToExit = false;

                    ExitPlayer();
                }
            }
        }
    }

    void SetTrainPosition()
    {
        if (splineContainer == null)
            return;

        if (!splineContainer.Evaluate(
            splinePosition,
            out float3 position,
            out float3 tangent,
            out float3 upVector))
        {
            return;
        }

        if (math.lengthsq(tangent) < 0.0001f)
            return;

        transform.position =
            (Vector3)position;

        Quaternion rotation =
            Quaternion.LookRotation(
                ((Vector3)tangent).normalized,
                ((Vector3)upVector).normalized
            );

        rotation *=
            Quaternion.Euler(
                0f,
                -90f,
                0f
            );

        transform.rotation =
            rotation;
    }

    void LateUpdate()
    {
        if (!insideTrain)
            return;

        if (player == null ||
            trainSeat == null)
            return;

        if (playerInputs != null)
        {
            playerInputs.MoveInput(
                Vector2.zero
            );

            playerInputs.JumpInput(
                false
            );

            playerInputs.SprintInput(
                false
            );
        }

        Vector3 seatPosition =
            trainSeat.TransformPoint(
                seatOffset
            );

        player.position =
            seatPosition;
    }

    public void ToggleTrain()
    {
        Debug.Log(
            "ToggleTrain() FOI CHAMADO!"
        );

        if (insideTrain)
        {
            RequestExitTrain();
        }
        else
        {
            EnterTrain();
        }
    }

    void EnterTrain()
    {
        Debug.Log(
            "ENTRANDO NO TREM!"
        );

        if (splineContainer == null)
        {
            Debug.LogError(
                "ERRO: Spline Container não encontrado!"
            );

            return;
        }

        if (player == null)
        {
            Debug.LogError(
                "ERRO: Player não configurado!"
            );

            return;
        }

        if (trainSeat == null)
        {
            Debug.LogError(
                "ERRO: Train Seat não configurado!"
            );

            return;
        }

        insideTrain = true;
        accelerating = true;
        braking = false;
        waitingToExit = false;

        currentSpeed = 0f;

        SetTrainPosition();

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        player.position =
            trainSeat.TransformPoint(
                seatOffset
            );

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        StopPlayer();

        Debug.Log(
            "Trem começou a acelerar!"
        );
    }

    // =========================================================
    // PEDIDO PARA SAIR
    // =========================================================

    void RequestExitTrain()
    {
        Debug.Log(
            "SAÍDA SOLICITADA - TREM VAI PARAR PRIMEIRO!"
        );

        if (currentSpeed <= 0.001f)
        {
            currentSpeed = 0f;
            braking = false;
            accelerating = false;

            ExitPlayer();

            return;
        }

        accelerating = false;
        braking = true;

        waitingToExit = true;

        Debug.Log(
            "Jogador continua dentro do trem enquanto ele freia."
        );
    }

    // =========================================================
    // SAIR DE VERDADE
    // =========================================================

    void ExitPlayer()
    {
        Debug.Log(
            "TREM PAROU - AGORA O PLAYER VAI SAIR!"
        );

        insideTrain = false;
        accelerating = false;
        braking = false;
        waitingToExit = false;

        if (exitPoint == null)
        {
            Debug.LogError(
                "ERRO: ExitPoint não configurado!"
            );

            return;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        player.position =
            exitPoint.position;

        player.rotation =
            exitPoint.rotation;

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        StopPlayer();
    }

    void StopPlayer()
    {
        if (playerInputs == null)
            return;

        playerInputs.MoveInput(
            Vector2.zero
        );

        playerInputs.JumpInput(
            false
        );

        playerInputs.SprintInput(
            false
        );
    }
}