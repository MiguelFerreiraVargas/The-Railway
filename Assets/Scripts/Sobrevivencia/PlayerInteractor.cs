using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Olha pelo centro da câmera; se achar um Interagivel perto, mostra
/// "[E] PEGAR CARVÃO" e usa ao apertar E (ou X no controle).
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float alcance = 2.8f;
    [SerializeField] private CanvasGroup aviso;
    [SerializeField] private TMP_Text avisoTexto;
    [SerializeField] private LayerMask camadas = ~0;

    public static Interagivel Atual { get; private set; }

    private void Update()
    {
        Atual = null;
        bool bloqueado = PauseMenu.IsPaused || LoadingScreen.IsLoading || (PlayerStats.Instance != null && PlayerStats.Instance.Morto) || Leitura.Aberta;
        var cam = Camera.main;

        if (!bloqueado && cam != null)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, alcance, camadas, QueryTriggerInteraction.Collide))
            {
                var alvo = hit.collider.GetComponentInParent<Interagivel>();
                if (alvo != null && alvo.Disponivel) Atual = alvo;
            }
        }

        if (aviso != null) aviso.alpha = Mathf.MoveTowards(aviso.alpha, Atual != null ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (Atual != null && avisoTexto != null) avisoTexto.text = $"<color=#FFB040>[E]</color>  {Atual.Texto}";

        if (Atual != null && Apertou())
            Atual.Interagir();
    }

    private static bool Apertou()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        return Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
    }
}
