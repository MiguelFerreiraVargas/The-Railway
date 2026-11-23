using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Listas para entender as estruturas: peças do House.fbx e o que está numa estrutura da cena.</summary>
public static class DiagEstruturas
{
    public static void PecasFbx(string caminho, StringBuilder log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            var m = mf != null ? mf.sharedMesh : null;
            var b = r.bounds;
            log.AppendLine($"{r.name}: centro {b.center:F2} tam {b.size:F2} tris {(m != null ? m.triangles.Length / 3 : 0)} sub {(m != null ? m.subMeshCount : 0)} mats [{string.Join(", ", r.sharedMaterials.Select(x => x != null ? x.name : "null"))}]");
        }
    }

    public static void Estrutura(string nome, StringBuilder log)
    {
        var cena = EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        var e = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).FirstOrDefault(x => x.name.ToLowerInvariant() == nome.ToLowerInvariant());
        if (e == null) { log.AppendLine("não achei " + nome); return; }
        var t = e.transform;
        log.AppendLine($"{e.name}: pos {t.position:F1} rot {t.eulerAngles:F0} escala {t.lossyScale:F3} pisos [{string.Join(", ", e.pisos.Select(p => p != null ? p.name : "null"))}]");
        foreach (var r in e.GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.parent != null && (r.transform.parent.name == "Suprimentos")) continue;
            var c = r.GetComponent<Collider>();
            var porta = r.GetComponent<PortaAutomatica>();
            var b = r.bounds;
            Vector3 lc = t.InverseTransformPoint(b.center);
            log.AppendLine($"  {Caminho(r.transform, t)}: local {lc:F1} tam {b.size:F2} col {(c != null ? c.GetType().Name + (c.isTrigger ? "(trig)" : "") : "-")}{(porta != null ? " PORTA" : "")} mat {(r.sharedMaterial != null ? r.sharedMaterial.name : "null")}");
        }
    }

    public static void Animais(StringBuilder log)
    {
        foreach (var caminho in new[] { "Assets/Packages/BearModel/source/Bear Animated.fbx", "Assets/Packages/Deer/animated-deer-3d-animal-model-free-download.fbx", "Assets/Prefabs/animated-deer-3d-animal-model-free-download.prefab", "Assets/Packages/simple-lowpoly-meat" })
        {
            log.AppendLine("== " + caminho);
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) log.AppendLine($"  clip {c.name} {c.length:0.00}s loop {c.isLooping}");
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (go == null) continue;
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                log.AppendLine($"  bounds {b.center:F2} {b.size:F2} escala raiz {go.transform.localScale:F3} renderers {rs.Length} animator {(go.GetComponentInChildren<Animator>() != null)} mats [{string.Join(", ", rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())}]");
            }
        }
        foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Packages/simple-lowpoly-meat" })) log.AppendLine("  carne: " + AssetDatabase.GUIDToAssetPath(g));
    }

    /// <summary>Fotos no editor da base do rádio e dos obstáculos nos trilhos.</summary>
    public static void FotosFerrovia(StringBuilder log)
    {
        EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        var camGo = new GameObject("Camera Foto");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 800f;
        var luz = new GameObject("Luz Foto").AddComponent<Light>();
        luz.type = LightType.Directional; luz.intensity = 1.2f; luz.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        try
        {
            void Foto(Vector3 alvo, Vector3 de, string nome)
            {
                camGo.transform.position = de; camGo.transform.LookAt(alvo);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine("Temp/TheRailway", nome), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                log.AppendLine("foto: " + nome);
            }
            var b = GameObject.Find("Base dos Sobreviventes");
            if (b != null)
            {
                var t = b.transform;
                Foto(t.position + Vector3.up * 1.5f, t.position + t.forward * 32f + Vector3.up * 9f, "foto_base_fora.png");
                Foto(t.position, t.position + Vector3.up * 40f + t.forward * 8f, "foto_base_cima.png");
            }
            var evs = GameObject.Find("Eventos da Ferrovia");
            if (evs != null)
                foreach (Transform e in evs.transform)
                {
                    if (e.name.Contains("Animais")) continue;
                    Foto(e.position + Vector3.up * 0.8f, e.position - e.forward * 12f + e.right * 4f + Vector3.up * 4f, "foto_" + new string(e.name.Where(char.IsLetterOrDigit).ToArray()) + ".png");
                }
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(luz.gameObject);
        }
    }

    private static string Caminho(Transform x, Transform raiz)
    {
        var s = x.name;
        while (x.parent != null && x.parent != raiz) { x = x.parent; s = x.name + "/" + s; }
        return s;
    }
}
