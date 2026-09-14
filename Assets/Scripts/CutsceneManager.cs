using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum TipoPasso
{
    Texto,       // mostra um texto na tela por um tempo
    Esperar,     // só espera X segundos
    MoverCamera, // anima a câmera de onde ela tá até um Transform de destino
    FadeTela,    // fade pra preto (ou de volta), usando uma Image full screen
    Tremor,      // shake de câmera (pra bomba, impacto, etc)
    TocarSom,    // toca um AudioClip (sirene, rádio, explosão)
    Objeto       // ativa/desativa um GameObject (ligar a TV, mostrar um prefab, etc)
}

[System.Serializable]
public class PassoCutscene
{
    public TipoPasso tipo;

    [Header("Texto")]
    public string texto;
    public float duracaoTexto = 3f;

    [Header("Esperar")]
    public float duracaoEspera = 1f;

    [Header("Mover Câmera")]
    public Transform pontoDestino;
    public float duracaoMovimento = 3f;

    [Header("Fade")]
    public bool fadeParaPreto = true; // false = clareia de volta
    public float duracaoFade = 1f;

    [Header("Tremor de Câmera")]
    public float intensidadeTremor = 0.3f;
    public float duracaoTremor = 1f;

    [Header("Som")]
    public AudioClip som;

    [Header("Ativar/Desativar Objeto")]
    public GameObject objetoAlvo;
    public bool ativarObjeto = true;
}

// Coloca esse script num GameObject na cena da cutscene. Monta a sequência
// no array "passos" no Inspector, na ordem que quiser — cada linha é um beat
// da cena. Quando termina, devolve o controle pro player.
public class CutsceneManager : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Camera cutsceneCamera;
    [SerializeField] private AudioSource audioSource;

    [Header("UI")]
    [SerializeField] private GameObject textoPanel;
    [SerializeField] private Text textoTela; // troca por TMP_Text se usar TextMeshPro
    [SerializeField] private Image telaFade; // Image preta full screen, começa com alpha 0

    [Header("Sequência (a ordem AQUI é a ordem da cutscene)")]
    [SerializeField] private PassoCutscene[] passos;

    [Header("Controle do Player")]
    [SerializeField] private GameObject cutsceneCameraObj; // objeto que contém a câmera da cutscene, desativado por padrão
    [SerializeField] private GameObject playerObj;          // objeto do player, desativado durante a cutscene
    [SerializeField] private Transform pontoFinalPlayer;     // onde o player aparece quando a cutscene termina (a estação)

    [Header("Rodar Automaticamente")]
    [SerializeField] private bool iniciarNoStart = true;

    private void Start()
    {
        if (iniciarNoStart)
            IniciarCutscene();
    }

    public void IniciarCutscene()
    {
        if (playerObj != null)
            playerObj.SetActive(false);

        if (cutsceneCameraObj != null)
            cutsceneCameraObj.SetActive(true);

        if (textoPanel != null)
            textoPanel.SetActive(false);

        StartCoroutine(RodarSequencia());
    }

    private IEnumerator RodarSequencia()
    {
        foreach (var passo in passos)
        {
            yield return StartCoroutine(ExecutarPasso(passo));
        }

        FinalizarCutscene();
    }

    private IEnumerator ExecutarPasso(PassoCutscene passo)
    {
        switch (passo.tipo)
        {
            case TipoPasso.Texto:
                yield return StartCoroutine(MostrarTexto(passo.texto, passo.duracaoTexto));
                break;

            case TipoPasso.Esperar:
                yield return new WaitForSeconds(passo.duracaoEspera);
                break;

            case TipoPasso.MoverCamera:
                yield return StartCoroutine(MoverCamera(passo.pontoDestino, passo.duracaoMovimento));
                break;

            case TipoPasso.FadeTela:
                yield return StartCoroutine(Fade(passo.fadeParaPreto, passo.duracaoFade));
                break;

            case TipoPasso.Tremor:
                yield return StartCoroutine(TremerCamera(passo.intensidadeTremor, passo.duracaoTremor));
                break;

            case TipoPasso.TocarSom:
                if (audioSource != null && passo.som != null)
                    audioSource.PlayOneShot(passo.som);
                break;

            case TipoPasso.Objeto:
                if (passo.objetoAlvo != null)
                    passo.objetoAlvo.SetActive(passo.ativarObjeto);
                break;
        }
    }

    private IEnumerator MostrarTexto(string texto, float duracao)
    {
        if (textoPanel != null)
            textoPanel.SetActive(true);

        if (textoTela != null)
            textoTela.text = texto;

        yield return new WaitForSeconds(duracao);

        if (textoPanel != null)
            textoPanel.SetActive(false);
    }

    private IEnumerator MoverCamera(Transform destino, float duracao)
    {
        if (cutsceneCamera == null || destino == null)
            yield break;

        Vector3 posInicial = cutsceneCamera.transform.position;
        Quaternion rotInicial = cutsceneCamera.transform.rotation;

        float timer = 0f;

        while (timer < duracao)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duracao);

            cutsceneCamera.transform.position = Vector3.Lerp(posInicial, destino.position, t);
            cutsceneCamera.transform.rotation = Quaternion.Slerp(rotInicial, destino.rotation, t);

            yield return null;
        }
    }

    private IEnumerator Fade(bool paraPreto, float duracao)
    {
        if (telaFade == null)
            yield break;

        float alphaInicial = paraPreto ? 0f : 1f;
        float alphaFinal = paraPreto ? 1f : 0f;
        Color cor = telaFade.color;

        float timer = 0f;

        while (timer < duracao)
        {
            timer += Time.deltaTime;
            float a = Mathf.Lerp(alphaInicial, alphaFinal, timer / duracao);
            telaFade.color = new Color(cor.r, cor.g, cor.b, a);
            yield return null;
        }

        telaFade.color = new Color(cor.r, cor.g, cor.b, alphaFinal);
    }

    private IEnumerator TremerCamera(float intensidade, float duracao)
    {
        if (cutsceneCamera == null)
            yield break;

        Vector3 posOriginal = cutsceneCamera.transform.localPosition;
        float timer = 0f;

        while (timer < duracao)
        {
            timer += Time.deltaTime;

            Vector3 offset = Random.insideUnitSphere * intensidade;
            offset.z = 0f;
            cutsceneCamera.transform.localPosition = posOriginal + offset;

            yield return null;
        }

        cutsceneCamera.transform.localPosition = posOriginal;
    }

    private void FinalizarCutscene()
    {
        if (cutsceneCameraObj != null)
            cutsceneCameraObj.SetActive(false);

        if (playerObj != null)
        {
            if (pontoFinalPlayer != null)
                playerObj.transform.position = pontoFinalPlayer.position;

            playerObj.SetActive(true);
        }
    }
}