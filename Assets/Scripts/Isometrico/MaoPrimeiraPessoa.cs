using UnityEngine;

/// <summary>
/// Braços de primeira pessoa (ArmPrincipal): fazem o gesto de pegar (Grab) quando o jogador
/// usa alguma coisa com E. Só aparecem em primeira pessoa (o ModoCamera liga e desliga).
/// </summary>
public class MaoPrimeiraPessoa : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string gatilhoPegar = "Grab";
    [SerializeField] private string boolMaoDireita = "UseRightHand";

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable() => PlayerInteractor.Interagiu += AoInteragir;
    private void OnDisable() => PlayerInteractor.Interagiu -= AoInteragir;

    private void AoInteragir(Interagivel alvo)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (ModoCamera.Instance != null && ModoCamera.Instance.Isometrico) return;
        // a direita está com a lanterna: pega com a esquerda
        if (!string.IsNullOrEmpty(boolMaoDireita)) animator.SetBool(boolMaoDireita, !LanternaNaMao.MaoDireitaOcupada);
        if (!string.IsNullOrEmpty(gatilhoPegar)) animator.SetTrigger(gatilhoPegar);
    }
}
