using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// "VOCÊ MORREU": aparece quando o PlayerStats avisa a morte.
/// Opções: carregar o último save, recomeçar do início ou ir ao menu.
/// </summary>
public class TelaDeMorte : MonoBehaviour
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private CanvasGroup vermelho;   // tela avermelhando antes do painel
    [SerializeField] private TMP_Text causa;
    [SerializeField] private GameObject botaoCarregar;
    [SerializeField] private GameObject botaoRecomecar;
    [SerializeField] private string cenaMenu = "Menu";
    [SerializeField] private string cenaInicio = "Prologo";
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somMorte;

    private bool ativa;
    private PlayerStats inscrito;

    public static bool Ativa { get; private set; }

    private void Awake()
    {
        SetGrupo(grupo, 0f, false);
        if (vermelho != null) vermelho.alpha = 0f;
        Ativa = false;
    }

    private void Update()
    {
        if (inscrito == null && PlayerStats.Instance != null)
        {
            inscrito = PlayerStats.Instance;
            inscrito.OnMorte += AoMorrer;
        }

        if (ativa) ManterSelecao();
    }

    private void OnDestroy()
    {
        if (inscrito != null) inscrito.OnMorte -= AoMorrer;
        if (ativa) Time.timeScale = 1f;
        Ativa = false;
    }

    private void AoMorrer(string motivo)
    {
        if (ativa) return;
        ativa = true;
        Ativa = true;
        StartCoroutine(Mostrar(motivo));
    }

    private IEnumerator Mostrar(string motivo)
    {
        if (audioSource != null && somMorte != null) audioSource.PlayOneShot(somMorte);
        Narrador.Calar();

        foreach (var f in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)) f.enabled = false;

        // A tela avermelha e escurece
        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.unscaledDeltaTime;
            if (vermelho != null) vermelho.alpha = Mathf.Clamp01(t / 1.5f);
            Time.timeScale = Mathf.Lerp(1f, 0.2f, t / 1.5f);
            yield return null;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (causa != null) causa.text = "VOCÊ MORREU " + motivo;
        if (botaoCarregar != null) botaoCarregar.SetActive(TemSave(out _));

        SetGrupo(grupo, 1f, true);
        Selecionar();
    }

    // ------------------------------------------------------------ Botões

    public void CarregarUltimoSave()
    {
        if (!TemSave(out int slot)) return;
        SaveGameLoader.selectedSlot = slot;
        Sair(() => LoadingScreen.Load(SaveSnapshot.CenaDoSave(slot, SceneManager.GetActiveScene().name)));
    }

    public void RecomecarDoInicio()
    {
        GameFlags.Clear();
        PlayerInventory.Instance?.LimparInventario();
        SaveGameLoader.selectedSlot = -1;
        GameSaveController.CurrentSlot = -1;
        Sair(() => LoadingScreen.Load(cenaInicio));
    }

    public void IrAoMenu()
    {
        Sair(() => SceneManager.LoadScene(cenaMenu));
    }

    private void Sair(System.Action carregar)
    {
        Time.timeScale = 1f;
        ativa = false;
        Ativa = false;
        carregar();
    }

    private static bool TemSave(out int slot)
    {
        slot = GameSaveController.CurrentSlot;
        if (slot >= 0 && SaveManager.HasSave(slot)) return true;

        // Sem slot atual: usa o save mais recente que existir
        System.DateTime melhor = System.DateTime.MinValue;
        slot = -1;
        for (int i = 0; i < 3; i++)
        {
            var d = SaveManager.Load(i);
            if (d == null) continue;
            System.DateTime.TryParseExact(d.lastPlayedDate, "dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var data);
            if (slot < 0 || data > melhor) { melhor = data; slot = i; }
        }
        return slot >= 0;
    }

    private void ManterSelecao()
    {
        var es = EventSystem.current;
        if (es == null || grupo == null) return;
        var sel = es.currentSelectedGameObject;
        if (sel == null || !sel.activeInHierarchy || !sel.transform.IsChildOf(grupo.transform)) Selecionar();
    }

    private void Selecionar()
    {
        var es = EventSystem.current;
        if (es == null) return;
        var alvo = botaoCarregar != null && botaoCarregar.activeInHierarchy ? botaoCarregar : botaoRecomecar;
        if (alvo != null) es.SetSelectedGameObject(alvo);
    }

    private static void SetGrupo(CanvasGroup g, float a, bool i)
    {
        if (g == null) return;
        g.alpha = a; g.interactable = i; g.blocksRaycasts = i;
    }
}
