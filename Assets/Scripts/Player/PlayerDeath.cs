using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Coloca esse script UMA VEZ num objeto do player. Escuta os eventos OnMorte da
// BarraVida e da BarraSanidade — não precisa chamar Morrer() manualmente pra
// esses dois casos, só arrasta as referências abaixo.
public class PlayerDeath : MonoBehaviour, IClosablePanel
{
    [Header("Fontes de Morte")]
    [SerializeField] private BarraVida barraVida;
    [SerializeField] private BarraSanidade barraSanidade;

    [Header("Referências do Player")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private MonoBehaviour[] scriptsParaDesativar; // movimento, mira, ataque etc — arrasta aqui

    [Header("Queda da Câmera")]
    [SerializeField] private float duracaoQueda = 1.2f;
    [SerializeField] private float anguloInclinacao = 80f;

    [Header("UI de Morte")]
    [SerializeField] private GameObject painelMorte;
    [SerializeField] private Text tituloTexto;
    [SerializeField] private Text motivoTexto;
    [SerializeField] private Button botaoReviver;
    [SerializeField] private Button botaoMenu;
    [SerializeField] private string cenaMenu = "Menu";

    [Header("Mochila Dropada")]
    [SerializeField] private GameObject backpackPrefab; // precisa ter o componente DroppedBackpack

    [Header("Reviver")]
    [SerializeField] private Vector3 pontoRevive;
    [SerializeField] private int vidaAoReviver = 100;
    [SerializeField] private int fomeAoReviver = 100;
    [SerializeField] private int sanidadeAoReviver = 100;

    private bool morto;

    private void Awake()
    {
        if (painelMorte != null)
            painelMorte.SetActive(false);
    }

    private void OnEnable()
    {
        if (barraVida != null)
            barraVida.OnMorte += OnMorteVida;

        if (barraSanidade != null)
            barraSanidade.OnMorte += OnMorteSanidade;

        if (botaoReviver != null)
            botaoReviver.onClick.AddListener(Reviver);

        if (botaoMenu != null)
            botaoMenu.onClick.AddListener(IrParaMenu);
    }

    private void OnDisable()
    {
        if (barraVida != null)
            barraVida.OnMorte -= OnMorteVida;

        if (barraSanidade != null)
            barraSanidade.OnMorte -= OnMorteSanidade;

        if (botaoReviver != null)
            botaoReviver.onClick.RemoveListener(Reviver);

        if (botaoMenu != null)
            botaoMenu.onClick.RemoveListener(IrParaMenu);
    }

    private void OnMorteVida(string motivo)
    {
        string texto = motivo == "Fome" ? "Você morreu de fome." : $"Você morreu. ({motivo})";
        Morrer("Você morreu", texto);
    }

    private void OnMorteSanidade()
    {
        Morrer("Sua mente não aguentou", "Você perdeu a sanidade.");
    }

    public void Morrer(string titulo, string motivo)
    {
        if (morto)
            return;

        morto = true;
        StartCoroutine(SequenciaMorte(titulo, motivo));
    }

    private IEnumerator SequenciaMorte(string titulo, string motivo)
    {
        foreach (var script in scriptsParaDesativar)
        {
            if (script != null)
                script.enabled = false;
        }

        DroparMochila();

        yield return StartCoroutine(QuedaCamera());

        if (tituloTexto != null)
            tituloTexto.text = titulo;

        if (motivoTexto != null)
            motivoTexto.text = motivo;

        if (painelMorte != null)
            painelMorte.SetActive(true);

        UIManager.Instance?.OpenPanel(this, pauseGame: true);
    }

    private IEnumerator QuedaCamera()
    {
        if (playerCamera == null)
            yield break;

        Quaternion rotacaoInicial = playerCamera.localRotation;
        float lado = Random.value > 0.5f ? 1f : -1f;
        Quaternion rotacaoFinal = rotacaoInicial * Quaternion.Euler(anguloInclinacao * 0.5f, 0f, anguloInclinacao * lado);

        float timer = 0f;

        while (timer < duracaoQueda)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duracaoQueda);
            playerCamera.localRotation = Quaternion.Slerp(rotacaoInicial, rotacaoFinal, t);
            yield return null;
        }
    }

    private void DroparMochila()
    {
        if (backpackPrefab == null || playerTransform == null || PlayerInventory.Instance == null)
            return;

        var itens = new List<InventorySlot>();

        foreach (var slot in PlayerInventory.Instance.Slots)
        {
            if (!string.IsNullOrEmpty(slot.itemId) && slot.quantity > 0)
                itens.Add(slot);
        }

        if (itens.Count == 0)
            return;

        GameObject mochila = Instantiate(backpackPrefab, playerTransform.position, Quaternion.identity);
        DroppedBackpack componente = mochila.GetComponent<DroppedBackpack>();

        if (componente != null)
            componente.Configurar(itens);
    }

    // esse painel só fecha via Reviver ou Menu, não pelo ESC — por isso fica vazio
    public void ClosePanel() { }

    private void Reviver()
    {
        if (painelMorte != null)
            painelMorte.SetActive(false);

        UIManager.Instance?.ClosePanelInternal(this);

        foreach (var script in scriptsParaDesativar)
        {
            if (script != null)
                script.enabled = true;
        }

        if (playerTransform != null)
            playerTransform.position = pontoRevive;

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.identity;

        if (barraVida != null)
            barraVida.Curar(vidaAoReviver);

        if (BarraFome.Instance != null)
            BarraFome.Instance.Comer(fomeAoReviver);

        if (barraSanidade != null)
            barraSanidade.RecuperarSanidade(sanidadeAoReviver);

        PlayerInventory.Instance?.LimparInventario(); // já dropou tudo na mochila, zera

        morto = false;
    }

    private void IrParaMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(cenaMenu);
    }
}