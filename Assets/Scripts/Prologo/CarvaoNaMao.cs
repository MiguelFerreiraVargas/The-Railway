using UnityEngine;

/// <summary>
/// Mostra a pedra de carvão na mão (preso à câmera, canto inferior esquerdo; o direito é da lanterna) enquanto houver
/// carvão no inventário. Sobe ao pegar, balança de leve ao andar e desce ao colocar na fornalha.
/// </summary>
public class CarvaoNaMao : MonoBehaviour
{
    [SerializeField] private string itemId = "carvao";
    [SerializeField] private Vector3 posicao = new Vector3(-0.17f, -0.12f, 0.42f);
    [SerializeField] private Vector3 rotacao = new Vector3(28f, -42f, -22f);
    [SerializeField] private Vector3 tamanho = new Vector3(0.085f, 0.065f, 0.075f);

    private static CarvaoNaMao instancia;
    private Transform pedra;
    private Transform cam;
    private float mostrar; // 0 escondido, 1 na mão
    private Vector3 ultimaPos;
    private float passo;

    /// <summary>Cria (uma vez por cena) a pedra na mão, copiando a malha e o material do monte.</summary>
    public static void Garantir(Renderer modelo)
    {
        if (instancia != null) return;
        var go = new GameObject("Carvao na Mao");
        instancia = go.AddComponent<CarvaoNaMao>();
        instancia.Montar(modelo);
    }

    public static bool Visivel => instancia != null && instancia.pedra != null && instancia.pedra.gameObject.activeSelf;

    private void Montar(Renderer modelo)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = "Pedra";
        Destroy(p.GetComponent<Collider>());
        var mf = modelo != null ? modelo.GetComponent<MeshFilter>() : null;
        if (mf != null && mf.sharedMesh != null) p.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var r = p.GetComponent<MeshRenderer>();
        if (modelo != null) r.sharedMaterial = modelo.sharedMaterial;
        // Na mão fica um pouco mais clara e brilhante que no monte, senão vira só um borrão preto
        var m = new Material(r.sharedMaterial);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.13f, 0.13f, 0.14f));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        pedra = p.transform;
        pedra.SetParent(transform, false);
        pedra.localScale = tamanho;
        p.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void LateUpdate()
    {
        if (pedra == null) return;
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
            transform.SetParent(cam, false);
            ultimaPos = cam.position;
        }

        int qtd = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetQuantity(itemId) : 0;
        bool naMao = qtd > 0 && !Leitura.Aberta;
        mostrar = Mathf.MoveTowards(mostrar, naMao ? 1f : 0f, Time.deltaTime * (naMao ? 4f : 6f));
        pedra.gameObject.SetActive(mostrar > 0.001f);
        if (mostrar <= 0.001f) { ultimaPos = cam.position; return; }

        // balanço de caminhada
        Vector3 delta = cam.position - ultimaPos; delta.y = 0f;
        ultimaPos = cam.position;
        float vel = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        passo += Time.deltaTime * Mathf.Lerp(1.2f, 9f, Mathf.Clamp01(vel / 4f));
        float amp = Mathf.Lerp(0.004f, 0.016f, Mathf.Clamp01(vel / 4f));
        Vector3 balanco = new Vector3(Mathf.Sin(passo) * amp, -Mathf.Abs(Mathf.Cos(passo)) * amp, 0f);

        float e = Mathf.SmoothStep(0f, 1f, mostrar);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        pedra.localPosition = posicao + balanco + new Vector3(0f, (1f - e) * -0.35f, 0f);
        pedra.localRotation = Quaternion.Euler(rotacao + new Vector3((1f - e) * 40f, 0f, 0f));
    }
}
