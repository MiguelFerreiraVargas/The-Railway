using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Câmera de fora da locomotiva. Dentro do trem, V (ou segurar Ctrl) troca entre a cabine e uma
/// câmera atrás e acima do trem, que acompanha as curvas. O mouse gira em volta e a roda dá zoom.
/// Ao descer do trem volta sozinha para a câmera do jogador.
/// </summary>
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
    public float startingDistance = 22f;
    public float minDistance = 8f;
    public float maxDistance = 60f;

    [Header("Zoom")]
    public float zoomSpeed = 8f;

    [Header("Mouse")]
    public float mouseSensitivity = 0.15f;

    [Header("Ângulo")]
    public float startingPitch = 18f;
    public float minPitch = -10f;
    public float maxPitch = 75f;
    [Tooltip("Ângulo inicial em volta do trem (0 = olhando por trás, para a frente do trem).")]
    public float startingYaw = 20f;
    [Tooltip("Quão rápido a câmera acompanha a curva do trem (maior = mais grudada).")]
    public float seguirCurva = 2.5f;

    [Header("Altura do alvo")]
    public float targetHeight = 2f;

    [Header("Colisão")]
    public LayerMask collisionMask;
    public float collisionRadius = 0.5f;
    public float collisionPadding = 0.3f;

    [Header("Teclas")]
    public Key teclaTrocar = Key.V;

    /// <summary>Se a câmera de fora do trem está ativa.</summary>
    public static bool Ativa { get; private set; }

    private bool viewingTrain;
    private bool alternada;      // ligada pelo V (o Ctrl só vale enquanto segura)
    private float yaw;           // em volta do trem, relativo à frente dele
    private float pitch;
    private float distance;
    private float yawTrem;       // direção do trem suavizada
    private float velYaw;
    private Vector3 ultimaPosTrem, rumoTrem;

    private readonly Dictionary<Renderer, bool> estadoJogador = new Dictionary<Renderer, bool>();
    private AudioListener ouvidoJogador, ouvidoTrem;
    private CanvasGroup dica;
    private TMP_Text textoDica;

    void Start()
    {
        distance = startingDistance;
        pitch = startingPitch;
        yaw = startingYaw;
        Ativa = false;

        if (playerCamera != null) playerCamera.enabled = true;
        if (trainCamera != null) trainCamera.enabled = false;

        // só um ouvido ligado por vez (senão o Unity reclama de dois AudioListeners)
        ouvidoJogador = playerCamera != null ? playerCamera.GetComponent<AudioListener>() : null;
        ouvidoTrem = trainCamera != null ? trainCamera.GetComponent<AudioListener>() : null;
        if (ouvidoTrem != null) ouvidoTrem.enabled = false;
        if (ouvidoJogador != null) ouvidoJogador.enabled = true;

        MontarDica();
    }

    void Update()
    {
        bool dentro = trainInteraction != null && trainInteraction.IsInsideTrain;
        AtualizarDica(dentro);

        // Com o jogo pausado (Esc), não mexe na câmera
        if (PauseMenu.IsPaused || Keyboard.current == null)
            return;

        if (!dentro) alternada = false;
        else if (Keyboard.current[teclaTrocar].wasPressedThisFrame && !Leitura.Aberta && !MapaDoMundo.Aberto)
            alternada = !alternada;

        bool ctrl = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        bool querVer = dentro && (alternada || ctrl);

        if (querVer && !viewingTrain) EnterTrainCamera();
        else if (!querVer && viewingTrain) ExitTrainCamera();

        if (viewingTrain)
        {
            UpdateMouse();
            UpdateZoom();
        }
    }

    // A câmera acompanha o trem depois que ele já andou neste quadro
    void LateUpdate()
    {
        // mede sempre para onde o trem anda, para a câmera já nascer atrás dele
        if (train != null)
        {
            Vector3 d = train.position - ultimaPosTrem; d.y = 0f;
            if (d.sqrMagnitude > 0.0004f && d.sqrMagnitude < 100f) rumoTrem = d.normalized;
            ultimaPosTrem = train.position;
        }
        if (!viewingTrain) return;
        UpdateTrainCamera(false);
    }

    void EnterTrainCamera()
    {
        if (playerCamera == null || trainCamera == null || train == null)
            return;

        viewingTrain = true;
        Ativa = true;
        playerCamera.enabled = false;
        trainCamera.enabled = true;
        if (ouvidoJogador != null) ouvidoJogador.enabled = false;
        if (ouvidoTrem != null) ouvidoTrem.enabled = true;

        HidePlayer();
        UpdateTrainCamera(true);
    }

    void ExitTrainCamera()
    {
        viewingTrain = false;
        Ativa = false;
        if (trainCamera != null) trainCamera.enabled = false;
        if (playerCamera != null) playerCamera.enabled = true;
        if (ouvidoTrem != null) ouvidoTrem.enabled = false;
        if (ouvidoJogador != null) ouvidoJogador.enabled = true;
        ShowPlayer();
    }

    void OnDisable()
    {
        if (viewingTrain) ExitTrainCamera();
    }

    void UpdateMouse()
    {
        if (Mouse.current == null) return;
        Vector2 mouse = Mouse.current.delta.ReadValue();
        yaw += mouse.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouse.y * mouseSensitivity, minPitch, maxPitch);
    }

    void UpdateZoom()
    {
        if (Mouse.current == null) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomSpeed * 0.25f, minDistance, maxDistance);
    }

    void UpdateTrainCamera(bool instantaneo)
    {
        if (trainCamera == null || train == null) return;

        // direção em que o trem anda (o eixo do modelo não é a frente dele), suavizada nas curvas
        Vector3 frente = rumoTrem.sqrMagnitude > 0.0001f ? rumoTrem : Vector3.ProjectOnPlane(train.forward, Vector3.up);
        float alvoYaw = frente.sqrMagnitude > 0.0001f ? Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg : yawTrem;
        yawTrem = instantaneo ? alvoYaw : Mathf.SmoothDampAngle(yawTrem, alvoYaw, ref velYaw, 1f / Mathf.Max(0.1f, seguirCurva));

        Vector3 target = train.position + Vector3.up * targetHeight;
        Quaternion rotation = Quaternion.Euler(pitch, yawTrem + yaw, 0f);
        Vector3 direction = rotation * Vector3.forward;
        Vector3 desiredPosition = target - direction * distance;
        trainCamera.transform.position = CheckCollision(target, desiredPosition);

        Vector3 lookDirection = target - trainCamera.transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
            trainCamera.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    Vector3 CheckCollision(Vector3 target, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - target;
        float distanceToCamera = direction.magnitude;
        if (distanceToCamera <= 0.01f) return desiredPosition;
        direction.Normalize();
        if (Physics.SphereCast(target, collisionRadius, direction, out RaycastHit hit, distanceToCamera, collisionMask, QueryTriggerInteraction.Ignore))
            return hit.point - direction * collisionPadding;
        return desiredPosition;
    }

    // Esconde o jogador e lembra o que estava ligado (a cápsula de debug e o corpo da isométrica ficam como estavam)
    void HidePlayer()
    {
        estadoJogador.Clear();
        if (player == null) return;
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            estadoJogador[r] = r.enabled;
            r.enabled = false;
        }
    }

    void ShowPlayer()
    {
        foreach (var kv in estadoJogador)
            if (kv.Key != null) kv.Key.enabled = kv.Value;
        estadoJogador.Clear();
    }

    // ------------------------------------------------------------ dica na tela

    void MontarDica()
    {
        var raiz = new GameObject("Dica da Camera do Trem");
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var escala = raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 1f;
        dica = raiz.AddComponent<CanvasGroup>();
        dica.alpha = 0f; dica.blocksRaycasts = false; dica.interactable = false;

        var go = new GameObject("Texto", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(raiz.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 36f);
        rt.sizeDelta = new Vector2(1200f, 40f);
        textoDica = go.AddComponent<TextMeshProUGUI>();
        var obj = FindFirstObjectByType<Objetivos>();
        var fonteHud = obj != null ? obj.GetComponentInChildren<TMP_Text>(true) : null;
        if (fonteHud != null) textoDica.font = fonteHud.font;
        textoDica.fontSize = 24f;
        textoDica.alignment = TextAlignmentOptions.Center;
        textoDica.color = new Color(0.93f, 0.86f, 0.7f);
        textoDica.raycastTarget = false;
    }

    void AtualizarDica(bool dentro)
    {
        if (dica == null) return;
        dica.alpha = Mathf.MoveTowards(dica.alpha, dentro && !PauseMenu.IsPaused ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (dica.alpha <= 0f) return;
        textoDica.text = viewingTrain
            ? $"<color=#E8B04A>[{teclaTrocar}]</color> voltar para a cabine    <color=#E8B04A>[mouse]</color> girar    <color=#E8B04A>[roda]</color> zoom"
            : $"<color=#E8B04A>[{teclaTrocar}]</color> câmera de fora do trem";
    }
}
