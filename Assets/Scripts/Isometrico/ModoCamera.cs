using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Alterna o jogador entre a primeira pessoa (lado de fora) e a câmera isométrica estilo
/// Project Zomboid (dentro de casas e estruturas). Na isométrica o WASD anda em relação à tela,
/// o Cândido vira para onde anda, o corpo aparece e o mouse fica livre.
/// Usa o mesmo FirstPersonController (gravidade, stamina, passos continuam iguais).
/// </summary>
[DefaultExecutionOrder(-50)] // antes do FirstPersonController ler o input
public class ModoCamera : MonoBehaviour
{
    public enum Modo { Isometrico, PrimeiraPessoa }

    [SerializeField] private Modo modoInicial = Modo.PrimeiraPessoa;
    [Tooltip("Modo dentro de uma Estrutura (casa, galpão...). Fora dela vale o outro.")]
    [SerializeField] private Modo modoDentro = Modo.Isometrico;
    [SerializeField] private CameraIsometrica cameraIsometrica;
    [Tooltip("Câmera de primeira pessoa (PlayerFollowCamera).")]
    [SerializeField] private CinemachineVirtualCameraBase cameraPrimeiraPessoa;
    [SerializeField] private Transform corpo;
    [Tooltip("Renderers que só fazem sentido em primeira pessoa (cápsula, lanterna na câmera...).")]
    [SerializeField] private Renderer[] soEmPrimeiraPessoa;
    [Tooltip("Graus por segundo que o Cândido gira para a direção em que anda.")]
    [SerializeField] private float velocidadeGiro = 720f;
    [SerializeField] private float tempoDeTransicao = 0.8f;
    [Tooltip("Troca sozinho ao entrar e sair das Estruturas.")]
    [SerializeField] private bool automatico = true;
    [Tooltip("Segundos que o jogador precisa ficar dentro/fora antes de trocar (evita piscar na porta).")]
    [SerializeField] private float tempoParaTrocar = 0.15f;
    [Tooltip("Tecla de teste para alternar o modo à mão (vazio = desligado).")]
    [SerializeField] private Key teclaAlternar = Key.V;

    public static ModoCamera Instance { get; private set; }

    /// <summary>Só para testes automáticos: substitui o WASD (mesmo formato do input).</summary>
    public static Vector2? MoverTeste;
    public Modo Atual { get; private set; }
    public bool Isometrico => Atual == Modo.Isometrico;

    private StarterAssetsInputs inputs;
    private FirstPersonController fpc;
    private InputAction acaoMover;
    private Renderer[] rendCorpo;
    private bool dentroAntes;
    private TrainInteraction[] trens = new TrainInteraction[0];
    private float mudouEm;
    private float distanciaFora = -1f;

    private void Awake()
    {
        Instance = this;
        inputs = GetComponent<StarterAssetsInputs>();
        fpc = GetComponent<FirstPersonController>();
        var pi = GetComponent<PlayerInput>();
        acaoMover = pi != null ? pi.actions.FindAction("Move") : null;
        rendCorpo = corpo != null ? corpo.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, tempoDeTransicao);
        trens = FindObjectsByType<TrainInteraction>(FindObjectsSortMode.None);
        Aplicar(modoInicial, instantaneo: true);
        dentroAntes = Estrutura.AlgumaComJogador;
        mudouEm = -1f;
    }

    public void Definir(Modo modo)
    {
        if (modo != Atual) Aplicar(modo, instantaneo: false);
    }

    public void Alternar() => Definir(Isometrico ? Modo.PrimeiraPessoa : Modo.Isometrico);

    private void Aplicar(Modo modo, bool instantaneo)
    {
        Atual = modo;
        bool iso = modo == Modo.Isometrico;

        if (cameraIsometrica != null)
        {
            var vc = cameraIsometrica.GetComponent<CinemachineCamera>();
            vc.Priority = iso ? 30 : 0;
            if (iso) cameraIsometrica.Recentrar();
            if (instantaneo && Camera.main != null && Camera.main.TryGetComponent<CinemachineBrain>(out var brain))
                brain.ResetState(); // começa já na câmera certa, sem transição
        }

        foreach (var r in rendCorpo)
            if (r != null) r.shadowCastingMode = iso ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        foreach (var r in soEmPrimeiraPessoa)
            if (r != null) r.enabled = !iso;

        if (inputs != null)
        {
            inputs.look = Vector2.zero;
            inputs.cursorInputForLook = !iso;
            inputs.cursorLocked = !iso;
        }

        // a mira do centro da tela só faz sentido em primeira pessoa
        foreach (var img in FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (img.name == "Mira") img.enabled = !iso;

        if (!iso && !PauseMenu.IsPaused && !Leitura.Aberta)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (automatico)
        {
            bool dentro = Estrutura.AlgumaComJogador;
            if (dentro != dentroAntes) { dentroAntes = dentro; mudouEm = Time.time; }
            else if (mudouEm >= 0f && Time.time - mudouEm >= tempoParaTrocar)
            {
                mudouEm = -1f;
                Modo fora = modoDentro == Modo.Isometrico ? Modo.PrimeiraPessoa : Modo.Isometrico;
                if (cameraIsometrica != null)
                {
                    if (dentro && !cameraIsometrica.Interior) { distanciaFora = cameraIsometrica.distancia; cameraIsometrica.distancia = cameraIsometrica.distanciaInterior; }
                    else if (!dentro && cameraIsometrica.Interior && distanciaFora > 0f) cameraIsometrica.distancia = distanciaFora;
                    cameraIsometrica.Interior = dentro;
                }
                Definir(dentro ? modoDentro : fora);
            }
        }

        // no trem o V é da câmera de fora da locomotiva (TrainCameraView)
        bool noTrem = false;
        if (trens != null) foreach (var t in trens) if (t != null && t.IsInsideTrain) { noTrem = true; break; }
        if (teclaAlternar != Key.None && Keyboard.current != null && Keyboard.current[teclaAlternar].wasPressedThisFrame
            && !PauseMenu.IsPaused && !Leitura.Aberta && !noTrem)
            Alternar();

        if (inputs == null) return;
        Vector2 cru = MoverTeste ?? (acaoMover != null ? acaoMover.ReadValue<Vector2>() : inputs.move);

        if (!Isometrico)
        {
            inputs.move = cru;
            return;
        }

        // Isométrico: o mouse não gira a câmera nem o corpo
        inputs.look = Vector2.zero;

        bool sentado = false;
        foreach (var t in trens) if (t != null && t.IsInsideTrain) { sentado = true; break; }

        if (cru.sqrMagnitude < 0.01f || fpc == null || !fpc.enabled || sentado)
        {
            inputs.move = Vector2.zero;
            return;
        }

        // Direção em relação à tela (a câmera isométrica olha na diagonal)
        var camT = Camera.main != null ? Camera.main.transform : transform;
        Vector3 frente = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
        Vector3 direita = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;
        Vector3 desejada = (frente * cru.y + direita * cru.x).normalized;

        var alvo = Quaternion.LookRotation(desejada, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, velocidadeGiro * Time.deltaTime);

        // Anda para a frente do corpo; numa virada grande, freia um pouco até alinhar
        float alinhado = Mathf.Clamp01(Vector3.Dot(transform.forward, desejada) * 0.6f + 0.4f);
        inputs.move = new Vector2(0f, Mathf.Min(1f, cru.magnitude) * alinhado);
    }

    private void LateUpdate()
    {
        // Mouse livre na isométrica (outros scripts travam ele ao fechar painéis)
        if (Isometrico && !PauseMenu.IsPaused && !Leitura.Aberta)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
    }
}
