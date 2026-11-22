using UnityEngine;

/// <summary>
/// Mantém o jogador colado no chão ao descer degraus e rampas (sem isso o
/// CharacterController "voa" um pouco a cada degrau). Só puxa para baixo
/// distâncias pequenas: ao sair de uma beirada alta, cai normalmente.
/// Vai no mesmo objeto do CharacterController do jogador.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(10)] // depois do FirstPersonController mover
public class AderirAoChao : MonoBehaviour
{
    [Tooltip("Maior queda que ainda conta como degrau/rampa (metros).")]
    [SerializeField] private float distanciaMaxima = 0.45f;

    private CharacterController cc;
    private bool estavaNoChao;

    private void Awake() => cc = GetComponent<CharacterController>();

    private void Update()
    {
        if (cc == null || !cc.enabled) return;

        // Subindo (pulo) ou já no chão: nada a fazer
        if (estavaNoChao && !cc.isGrounded && cc.velocity.y <= 0.1f)
        {
            float escala = transform.lossyScale.y;
            float raio = cc.radius * escala * 0.95f;
            var centroBaixo = transform.TransformPoint(cc.center) + Vector3.down * (cc.height * escala * 0.5f - cc.radius * escala);
            if (Physics.SphereCast(centroBaixo, raio, Vector3.down, out var hit, distanciaMaxima + cc.skinWidth, ~0, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= Mathf.Cos(cc.slopeLimit * Mathf.Deg2Rad))
            {
                float descer = hit.distance - cc.skinWidth * 0.5f;
                if (descer > 0.005f) cc.Move(Vector3.down * descer);
            }
        }

        estavaNoChao = cc.isGrounded;
    }
}
