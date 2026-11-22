using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HUD das necessidades: atualiza as barras e trata comer (F) e beber (G)
/// usando o que estiver no inventário.
/// </summary>
public class HudSobrevivencia : MonoBehaviour
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private BarraSegmentada vida;
    [SerializeField] private BarraSegmentada fome;
    [SerializeField] private BarraSegmentada sede;
    [SerializeField] private BarraSegmentada stamina;
    [SerializeField] private BarraSegmentada higiene;
    [SerializeField] private BarraSegmentada sanidade;
    [SerializeField] private TMP_Text dicasConsumo;
    [SerializeField] private TMP_Text avisoConsumo;

    private float avisoAte;

    private void Update()
    {
        var stats = PlayerStats.Instance;
        bool esconder = stats == null || LoadingScreen.IsLoading || PauseMenu.IsPaused || Leitura.Aberta || stats.Morto;
        if (grupo != null) grupo.alpha = Mathf.MoveTowards(grupo.alpha, esconder ? 0f : 1f, Time.unscaledDeltaTime * 4f);
        if (stats == null) return;

        vida?.Definir(stats.vida.Fracao);
        fome?.Definir(stats.fome.Fracao);
        sede?.Definir(stats.sede.Fracao);
        stamina?.Definir(stats.StaminaFracao);
        higiene?.Definir(stats.higiene.Fracao);
        sanidade?.Definir(stats.sanidade.Fracao);

        // Comer / beber
        int comidas = stats.ContarNoInventario(false);
        int bebidas = stats.ContarNoInventario(true);

        if (dicasConsumo != null)
        {
            string s = "";
            if (comidas > 0) s += $"<color=#FFB040>[F]</color> COMER ({comidas})   ";
            if (bebidas > 0) s += $"<color=#FFB040>[G]</color> BEBER ({bebidas})";
            dicasConsumo.text = s;
        }

        if (!esconder && Keyboard.current != null)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame) Usar(stats, false);
            if (Keyboard.current.gKey.wasPressedThisFrame) Usar(stats, true);
        }

        if (avisoConsumo != null)
            avisoConsumo.alpha = Mathf.Clamp01((avisoAte - Time.unscaledTime) * 2f);
    }

    private void Usar(PlayerStats stats, bool bebida)
    {
        var item = stats.ConsumirDoInventario(bebida);
        if (avisoConsumo == null) return;
        avisoConsumo.text = item != null ? (bebida ? "BEBEU " : "COMEU ") + item.nome : (bebida ? "NADA PARA BEBER" : "NADA PARA COMER");
        avisoAte = Time.unscaledTime + 1.6f;
    }
}
