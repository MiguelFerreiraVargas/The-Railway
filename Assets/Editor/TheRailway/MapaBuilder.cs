using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Mapa da CenaPrincipal: tira uma foto de cima do terreno, deixa com cara de mapa de papel,
/// desenha o trilho e as construções e liga o MapaDoMundo (tecla M) na cena.
/// Também põe carvão derramado ao lado dos vagões tombados.
/// </summary>
public static class MapaBuilder
{
    public const string Imagem = "Assets/Textures/Mapa/MapaDoMundo.png";
    private const int Lado = 1024;

    private static readonly Color Papel = new Color(0.86f, 0.78f, 0.6f);
    private static readonly Color Tinta = new Color(0.3f, 0.22f, 0.14f);
    private static readonly Color TintaForte = new Color(0.17f, 0.11f, 0.06f);

    public static void Construir(StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        MundoBuilder.MontesNosVagoes(log);

        var terr = Terrain.activeTerrain;
        if (terr == null) throw new System.Exception("sem terreno na cena");
        var ts = terr.terrainData.size;
        var min = new Vector2(terr.transform.position.x, terr.transform.position.z);
        var tam = new Vector2(ts.x, ts.z);

        var foto = Fotografar(terr);
        Pintar(foto, min, tam, log);

        Directory.CreateDirectory(Path.GetDirectoryName(Imagem));
        File.WriteAllBytes(Imagem, foto.EncodeToPNG());
        File.WriteAllBytes(Path.Combine("Temp/TheRailway", "mapa_mundo.png"), foto.EncodeToPNG());
        Object.DestroyImmediate(foto);
        AssetDatabase.ImportAsset(Imagem, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(Imagem);
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = Lado;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        log.AppendLine("imagem do mapa: " + Imagem);

        // o mapa na cena
        var mapa = Object.FindFirstObjectByType<MapaDoMundo>(FindObjectsInactive.Include);
        if (mapa == null) mapa = new GameObject("Mapa do Mundo").AddComponent<MapaDoMundo>();
        mapa.imagem = AssetDatabase.LoadAssetAtPath<Texture2D>(Imagem);
        mapa.mundoMin = min;
        mapa.mundoTamanho = tam;
        var obj = Object.FindFirstObjectByType<Objetivos>(FindObjectsInactive.Include);
        var textoObj = obj != null ? new SerializedObject(obj).FindProperty("texto").objectReferenceValue as TMP_Text : null;
        mapa.fonte = textoObj != null ? textoObj.font : TMP_Settings.defaultFontAsset;
        EditorUtility.SetDirty(mapa);
        log.AppendLine($"MapaDoMundo: terreno {min} + {tam}, fonte {(mapa.fonte != null ? mapa.fonte.name : "padrão")}, montes {Object.FindObjectsByType<MonteDeCarvao>(FindObjectsSortMode.None).Length}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva");
    }

    // foto de cima só do terreno (sem trem, casas e árvores: o resto é desenhado por cima)
    private static Texture2D Fotografar(Terrain terr)
    {
        var ts = terr.terrainData.size;
        var camGo = new GameObject("Camera Mapa");
        var luzGo = new GameObject("Luz Mapa");
        var desligados = new List<Renderer>();
        bool nevoa = RenderSettings.fog;
        var rt = new RenderTexture(Lado, Lado, 24);
        try
        {
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = ts.z * 0.5f; cam.aspect = ts.x / ts.z;
            cam.nearClipPlane = 1f; cam.farClipPlane = ts.y + 1200f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Papel;
            camGo.transform.SetPositionAndRotation(terr.transform.position + new Vector3(ts.x / 2f, ts.y + 600f, ts.z / 2f), Quaternion.Euler(90f, 0f, 0f));
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional; luz.intensity = 1.5f; luz.shadows = LightShadows.None;
            luz.transform.rotation = Quaternion.Euler(50f, -45f, 0f);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled) { r.enabled = false; desligados.Add(r); }
            RenderSettings.fog = false;

            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(Lado, Lado, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            return tex;
        }
        finally
        {
            foreach (var r in desligados) if (r != null) r.enabled = true;
            RenderSettings.fog = nevoa;
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(luzGo); Object.DestroyImmediate(rt);
        }
    }

    private static void Pintar(Texture2D tex, Vector2 min, Vector2 tam, StringBuilder log)
    {
        var px = tex.GetPixels();
        var rnd = new System.Random(5);
        // papel: o relevo vira sombra de tinta, um pouco da cor do terreno fica
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                var c = px[y * Lado + x];
                float claro = Mathf.Clamp01((c.grayscale - 0.06f) / 0.45f);
                var cor = Color.Lerp(Tinta, Papel, Mathf.Lerp(0.25f, 1f, claro));
                cor = Color.Lerp(cor, c * 1.7f, 0.18f);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(Lado / 2f, Lado / 2f)) / (Lado * 0.5f);
                cor *= 1f - 0.3f * Mathf.SmoothStep(0.75f, 1.15f, d);
                cor += Color.white * (float)(rnd.NextDouble() - 0.5) * 0.03f;
                cor.a = 1f;
                px[y * Lado + x] = cor;
            }
        // quadriculado a cada 100 m
        for (float m = 100f; m < tam.x; m += 100f)
        {
            int gx = Mathf.RoundToInt(m / tam.x * Lado);
            for (int y = 0; y < Lado; y++) Misturar(px, gx, y, Tinta, 0.12f);
        }
        for (float m = 100f; m < tam.y; m += 100f)
        {
            int gy = Mathf.RoundToInt(m / tam.y * Lado);
            for (int x = 0; x < Lado; x++) Misturar(px, x, gy, Tinta, 0.12f);
        }

        Vector2 P(Vector3 w) => new Vector2((w.x - min.x) / tam.x * Lado, (w.z - min.y) / tam.y * Lado);

        // construções (pela pegada no chão)
        int casas = 0;
        foreach (var e in Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).Select(e => e.transform)
                     .Concat(GameObject.Find("Estruturas") != null ? GameObject.Find("Estruturas").transform.Cast<Transform>().Where(t => t.name.StartsWith("Vagao")) : Enumerable.Empty<Transform>()))
        {
            var rs = e.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<Interagivel>() == null).ToArray();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var a = P(b.min); var z = P(b.max);
            Vector2 centro = (a + z) * 0.5f, meio = Vector2.Max((z - a) * 0.5f, new Vector2(3f, 3f));
            Retangulo(px, centro, meio + Vector2.one * 1.5f, Papel, 1f);
            Retangulo(px, centro, meio, TintaForte, 1f);
            casas++;
        }

        // trilho: linha grossa com dormentes
        var trem = Object.FindFirstObjectByType<TrainInteraction>();
        var anim = trem != null ? trem.GetComponent<SplineAnimate>() : null;
        var container = anim != null ? anim.Container : Object.FindFirstObjectByType<SplineContainer>();
        if (container != null)
        {
            const int n = 4000;
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++) { container.Evaluate(i / (float)n, out float3 p, out _, out _); pts[i] = P((Vector3)p); }
            for (int i = 0; i < n; i++) Linha(px, pts[i], pts[i + 1], 2.2f, TintaForte);
            float andado = 0f;
            for (int i = 0; i < n; i++)
            {
                andado += Vector2.Distance(pts[i], pts[i + 1]);
                if (andado < 7f) continue;
                andado = 0f;
                var dir = (pts[i + 1] - pts[i]).normalized;
                var nor = new Vector2(-dir.y, dir.x);
                Linha(px, pts[i] - nor * 4.5f, pts[i] + nor * 4.5f, 0.8f, TintaForte);
            }
            log.AppendLine($"trilho desenhado ({n} pontos), {casas} construções");
        }
        tex.SetPixels(px); tex.Apply();
    }

    private static void Misturar(Color[] px, int x, int y, Color c, float a)
    {
        if (x < 0 || y < 0 || x >= Lado || y >= Lado) return;
        int i = y * Lado + x;
        px[i] = Color.Lerp(px[i], c, a);
    }

    private static void Retangulo(Color[] px, Vector2 centro, Vector2 meio, Color c, float a)
    {
        for (int y = Mathf.FloorToInt(centro.y - meio.y); y <= Mathf.CeilToInt(centro.y + meio.y); y++)
            for (int x = Mathf.FloorToInt(centro.x - meio.x); x <= Mathf.CeilToInt(centro.x + meio.x); x++)
                Misturar(px, x, y, c, a);
    }

    // linha com espessura (raio em pixels) e borda suave
    private static void Linha(Color[] px, Vector2 a, Vector2 b, float raio, Color c)
    {
        int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - raio - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + raio + 1);
        int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - raio - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + raio + 1);
        Vector2 ab = b - a; float l2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d = Vector2.Distance(p, a + ab * t);
                float cobre = Mathf.Clamp01(raio + 0.5f - d);
                if (cobre > 0f && x >= 0 && y >= 0 && x < Lado && y < Lado)
                {
                    int i = y * Lado + x;
                    px[i] = Color.Lerp(px[i], c, Mathf.Max(cobre, 0f) * 0.9f);
                }
            }
    }
}
