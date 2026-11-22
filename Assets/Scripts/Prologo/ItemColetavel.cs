using System.Collections;
using UnityEngine;

/// <summary>
/// Pega um item do chão e guarda no inventário. Some depois de pego
/// (e continua sumido ao carregar um save, pela marca em GameFlags).
/// </summary>
public class ItemColetavel : Interagivel
{
    [SerializeField] private string itemId = "carvao";
    [SerializeField] private int quantidade = 1;
    [Tooltip("Marca única deste item no mundo (ex.: \"prologo.item.carvao1\").")]
    [SerializeField] private string flag = "";
    [Tooltip("Comer/beber na hora em vez de guardar.")]
    [SerializeField] private bool consumirNaHora;
    [SerializeField] [TextArea] private string fala = "";
    [SerializeField] private AudioClip som;

    private IEnumerator Start()
    {
        yield return null; // espera o save ser aplicado
        if (!string.IsNullOrEmpty(flag) && GameFlags.Has(flag)) gameObject.SetActive(false);
    }

    public override void Interagir()
    {
        if (consumirNaHora && PlayerStats.Instance != null)
        {
            PlayerStats.Instance.Aplicar(PlayerStats.Instance.GetConsumivel(itemId));
        }
        else
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || !inv.AddItem(itemId, quantidade))
            {
                Narrador.Falar("Não cabe mais nada nos bolsos.");
                return;
            }
        }

        if (som != null) AudioSource.PlayClipAtPoint(som, transform.position);
        if (!string.IsNullOrEmpty(flag)) GameFlags.Set(flag);
        if (!string.IsNullOrEmpty(fala)) Narrador.Falar(fala);
        gameObject.SetActive(false);
    }
}
