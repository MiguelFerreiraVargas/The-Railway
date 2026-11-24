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
        // pega com a mão que vai segurar: o carvão vai primeiro para a direita (se a lanterna não estiver
        // nela) e o segundo para a esquerda; o resto, com a mão que estiver livre
        bool pegouCarvao = alvo is MonteDeCarvao || (alvo is ItemColetavel ic && ic.ItemId == "carvao");
        int carvao = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity("carvao") : 0;
        bool direita = !LanternaNaMao.MaoDireitaOcupada && (pegouCarvao ? carvao <= 1 : !CarvaoNaMao.NaDireita);
        if (!string.IsNullOrEmpty(boolMaoDireita)) animator.SetBool(boolMaoDireita, direita);
        if (!string.IsNullOrEmpty(gatilhoPegar)) animator.SetTrigger(gatilhoPegar);
    }
}
