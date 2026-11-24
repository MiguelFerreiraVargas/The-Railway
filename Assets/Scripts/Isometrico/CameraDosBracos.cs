using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Braços, lanterna, arma e carvão da primeira pessoa numa câmera própria (Overlay do URP) por cima
/// do mundo: assim nunca atravessam paredes, portas e móveis quando o Cândido chega perto.
/// Fica na câmera principal; tudo que está preso nela (e o que for posto com PorNaCamada) vai
/// para a camada "PrimeiraPessoa", que a câmera principal não desenha.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraDosBracos : MonoBehaviour
{
    public const string NomeCamada = "FirstPerson";
    public static int Camada => LayerMask.NameToLayer(NomeCamada);

    private Camera principal, bracos;

    private void Awake()
    {
        principal = GetComponent<Camera>();
        int c = Camada;
        if (c < 0) { Debug.LogWarning("[CameraDosBracos] camada FirstPerson não existe"); enabled = false; return; }

        var go = new GameObject("Câmera dos Braços");
        go.transform.SetParent(transform, false);
        bracos = go.AddComponent<Camera>();
        bracos.cullingMask = 1 << c;
        bracos.clearFlags = CameraClearFlags.Depth;
        bracos.nearClipPlane = 0.01f;
        bracos.farClipPlane = 6f;
        bracos.fieldOfView = principal.fieldOfView;
        var dados = bracos.GetUniversalAdditionalCameraData();
        dados.renderType = CameraRenderType.Overlay;
        dados.renderShadows = false;
        var dadosPrincipal = principal.GetUniversalAdditionalCameraData();
        dados.renderPostProcessing = dadosPrincipal.renderPostProcessing;
        if (!dadosPrincipal.cameraStack.Contains(bracos)) dadosPrincipal.cameraStack.Add(bracos);
        principal.cullingMask &= ~(1 << c);

        // o que já está preso na câmera (braços, lanterna) vai para a camada
        foreach (Transform filho in transform)
            if (filho != go.transform && filho.GetComponentInChildren<Renderer>(true) != null) PorNaCamada(filho.gameObject);
    }

    private void LateUpdate()
    {
        if (bracos == null) return;
        bracos.fieldOfView = principal.fieldOfView;
        bracos.enabled = principal.enabled;
    }

    /// <summary>Põe o objeto (e os filhos) na camada da primeira pessoa.</summary>
    public static void PorNaCamada(GameObject go)
    {
        int c = Camada;
        if (c < 0 || go == null) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = c;
    }
}
