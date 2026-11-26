using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Modelos de Assets/CalebeAssets3D no jogo: cada um vira um prefab com tamanho real, textura
/// e pivô no chão (Assets/Prefabs/Calebe). As armas, o carvão e os móveis usam esses prefabs.
/// </summary>
public static class AssetsCalebe
{
    public const string Pasta = "Assets/CalebeAssets3D";
    public const string PastaPrefabs = "Assets/Prefabs/Calebe";
    private const string PastaMats = PastaPrefabs + "/Materiais";
    private const string Saida = "Temp/TheRailway/calebe";

    private class Peca
    {
        public string nome, fbx, tex;
        public float maior;      // maior medida em metros (0 = como veio)
        public bool deitar;      // comprido em pé no arquivo: deita (armas e ferramentas no chão/mesa)
        public float liso = 0.25f, metal;
        public Peca(string nome, string fbx, string tex, float maior, bool deitar = false, float liso = 0.25f, float metal = 0f)
        { this.nome = nome; this.fbx = fbx; this.tex = tex; this.maior = maior; this.deitar = deitar; this.liso = liso; this.metal = metal; }
    }

    private const string Psx = Pasta + "/Furnitures/PSX-Derelict-Furniture-Standard";
    private const string Rua = Pasta + "/Furnitures/Street Furniture";

    private static readonly Peca[] Pecas =
    {
        // armas
        new Peca("Faca", Pasta + "/Facas/Fbx/Knife_001.fbx", Pasta + "/Facas/Texture/KnivesNormal/Knife_1_Normal.png", 0.3f, true, 0.6f, 0.5f),
        new Peca("Machado", Pasta + "/Machado/source/fireaxe.fbx", Pasta + "/Machado/textures/axe.png", 0.62f, true, 0.35f),
        new Peca("Rifle", Pasta + "/RetroWeaponsPack/Guns/Rifle_01/Fbx_Files/Rifle_01.fbx", Pasta + "/RetroWeaponsPack/Guns/Rifle_01/Textures/Rifle_01_Albedo.png", 0.95f, false, 0.35f, 0.3f),
        // móveis das casas
        new Peca("Cadeira Velha", Psx + "/fbx/chair.fbx", Psx + "/textures/chair128x128.png", 0f),
        new Peca("Sofa", Psx + "/fbx/couch.fbx", Psx + "/textures/couch256x256.png", 0f),
        new Peca("Geladeira Velha", Psx + "/fbx/fridge.fbx", Psx + "/textures/fridge256x256.png", 0f, false, 0.4f),
        new Peca("Colchao", Psx + "/fbx/mattress.fbx", Psx + "/textures/mattress256x256.png", 0f),
        new Peca("Estante Velha", Psx + "/fbx/shelf.fbx", Psx + "/textures/shelf256x256.png", 0f),
        new Peca("Vaso", Psx + "/fbx/vase.fbx", Psx + "/textures/vase128x128.png", 0.38f),
        // tralha
        new Peca("Barril", Rua + "/Barrel/Barrel.fbx", Rua + "/Barrel/barrel.png", 0.9f, false, 0.3f, 0.4f),
        new Peca("Garrafas", Rua + "/Bottles/Bottles.fbx", Rua + "/Bottles/Bottles.png", 0.3f, false, 0.7f),
        new Peca("Caixote", Rua + "/Box/Box.fbx", Rua + "/Box/Box.png", 0.6f),
        new Peca("Ar Condicionado", Rua + "/Conditioner/Conditioner.fbx", Rua + "/Conditioner/Conditioner.png", 0.95f, false, 0.3f, 0.3f),
        new Peca("Pe de Cabra", Rua + "/Crowbar/Crowbar.fbx", Rua + "/Crowbar/Crowbar.png", 0.75f, true, 0.4f, 0.6f),
        new Peca("Lanterna Velha", Rua + "/Flashlight/Flashlight.fbx", Rua + "/Flashlight/Flashlight.png", 0.22f, true, 0.4f, 0.4f),
        new Peca("Disquete", Rua + "/Floppy/Floppy.fbx", Rua + "/Floppy/Floppy.png", 0.09f),
        new Peca("Saco de Lixo", Rua + "/GarbageBag/GarbageBag.fbx", Rua + "/GarbageBag/GarbageBag.png", 0.75f, false, 0.55f),
        new Peca("Mascara de Gas", Rua + "/GasMask/GasMask.fbx", Rua + "/GasMask/GasMask.png", 0.3f),
        new Peca("Martelo", Rua + "/Hammer/Hammer.fbx", Rua + "/Hammer/Hammer.png", 0.33f, true, 0.35f, 0.3f),
        new Peca("Cadeado", Rua + "/OldLock/OldLock.fbx", Rua + "/OldLock/OldLock.png", 0.16f, false, 0.4f, 0.6f),
        new Peca("Cacamba", Rua + "/TrashCan/TrashCan.fbx", Rua + "/TrashCan/TrashCan.png", 2.0f, false, 0.3f, 0.4f),
        new Peca("Papelao", Rua + "/Сardboard/Сardboard.fbx", Rua + "/Сardboard/cardboard.png", 0.8f),
    };

    public static GameObject Prefab(string nome) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaPrefabs}/{nome}.prefab");

    // ================================================================== montar

    public static void Montar(StringBuilder log)
    {
        Pastas();
        foreach (var p in Pecas)
        {
            try { MontarPeca(p, log); }
            catch (System.Exception e) { log.AppendLine($"ERRO em {p.nome}: {e.Message}"); }
        }
        MalhaDoCarvao(log);
        AssetDatabase.SaveAssets();
    }

    private static void Pastas()
    {
        if (!AssetDatabase.IsValidFolder(PastaPrefabs)) AssetDatabase.CreateFolder("Assets/Prefabs", "Calebe");
        if (!AssetDatabase.IsValidFolder(PastaMats)) AssetDatabase.CreateFolder(PastaPrefabs, "Materiais");
    }

    private static void MontarPeca(Peca p, StringBuilder log)
    {
        var imp = AssetImporter.GetAtPath(p.fbx) as ModelImporter;
        if (imp == null) { log.AppendLine($"AVISO: {p.fbx} não achado"); return; }
        if (!imp.isReadable || imp.importCameras || imp.importLights || imp.importAnimation)
        {
            imp.isReadable = true; // o contorno dos itens e a mão leem os vértices
            imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
            imp.SaveAndReimport();
        }

        var mat = Material(p.nome, p.tex, p.liso, p.metal);
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(p.fbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var a in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        // malha com esqueleto (as armas do pacote têm ossos para animar) vira malha comum: a mão copia MeshFilters
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList())
        {
            var go = smr.gameObject; var malha = smr.sharedMesh; var mats = smr.sharedMaterials;
            Object.DestroyImmediate(smr);
            if (malha == null) continue;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }
        // a peça da ponta ganha "Lamina" no nome: a mão sabe qual lado vai para a frente
        if (p.nome == "Rifle")
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "FrontSight_Mesh") t.name = "Lamina (ponta)";
                if (t.name == "Sights_Mesh") t.name = "Mira (cima)";
            }
        var raiz = new GameObject(p.nome);
        inst.transform.SetParent(raiz.transform, false);
        inst.transform.localPosition = Vector3.zero;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        var b = Limites(inst);
        if (p.deitar)
        {
            // deitado: o eixo comprido em X, o mais fino para cima (a face larga aparece)
            var s = b.size;
            int longo = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
            int fino = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            if (fino == longo) fino = (longo + 1) % 3;
            Vector3 Eixo(int k) => k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward;
            // gira de modo que Eixo(longo) → X e Eixo(fino) → Y
            var de = Quaternion.LookRotation(Vector3.Cross(Eixo(longo), Eixo(fino)), Eixo(fino));
            var para = Quaternion.LookRotation(Vector3.Cross(Vector3.right, Vector3.up), Vector3.up);
            inst.transform.localRotation = para * Quaternion.Inverse(de) * inst.transform.localRotation;
            b = Limites(inst);
        }
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (p.maior > 0f) inst.transform.localScale *= p.maior / Mathf.Max(1e-4f, maior);
        b = Limites(inst);
        inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        b = Limites(inst);

        PrefabUtility.SaveAsPrefabAsset(raiz, $"{PastaPrefabs}/{p.nome}.prefab");
        Object.DestroyImmediate(raiz);
        log.AppendLine($"{p.nome}: {b.size.x:0.00}x{b.size.y:0.00}x{b.size.z:0.00} m");
    }

    public static Bounds Limites(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Material Material(string nome, string tex, float liso, float metal)
    {
        string caminho = $"{PastaMats}/{nome}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, caminho); }
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
        if (t == null) Debug.LogWarning("textura não achada: " + tex);
        mat.SetTexture("_BaseMap", t);
        mat.SetTexture("_MainTex", t);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", liso);
        mat.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ================================================================== carvão (escaneado, 200 mil triângulos → pedra leve)

    public const string MalhaCarvaoCaminho = PastaPrefabs + "/Pedra de Carvao.asset";
    public const string MatCarvaoCaminho = PastaMats + "/Carvao Escaneado.mat";
    private const string CoalObj = Pasta + "/CoalAssets/Coal.obj";
    private const string CoalTex = Pasta + "/CoalAssets/TexturaCoal.jpg";

    private static void MalhaDoCarvao(StringBuilder log)
    {
        var imp = AssetImporter.GetAtPath(CoalObj) as ModelImporter;
        if (imp == null) { log.AppendLine("AVISO: carvão escaneado não achado"); return; }
        if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
        var timp = AssetImporter.GetAtPath(CoalTex) as TextureImporter;
        if (timp != null && timp.maxTextureSize > 1024) { timp.maxTextureSize = 1024; timp.SaveAndReimport(); }

        var origem = AssetDatabase.LoadAllAssetsAtPath(CoalObj).OfType<Mesh>().OrderByDescending(m => m.vertexCount).FirstOrDefault();
        if (origem == null) { log.AppendLine("AVISO: carvão sem malha"); return; }
        var copia = Object.Instantiate(origem);
        MeshLodUtility.GenerateMeshLods(copia, 12);
        // o nível mais detalhado que ainda é leve
        int escolhido = 0; uint tris = 0;
        for (int l = 0; l < copia.lodCount; l++)
        {
            var r = copia.GetLod(0, l);
            escolhido = l; tris = r.indexCount / 3;
            if (tris <= 1400) break;
        }
        var faixa = copia.GetLod(0, escolhido);
        var idx = new int[faixa.indexCount];
        using (var dados = Mesh.AcquireReadOnlyMeshData(copia))
        {
            var d = dados[0];
            if (d.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32)
            { var buf = d.GetIndexData<uint>(); for (int i = 0; i < idx.Length; i++) idx[i] = (int)buf[(int)faixa.indexStart + i]; }
            else
            { var buf = d.GetIndexData<ushort>(); for (int i = 0; i < idx.Length; i++) idx[i] = buf[(int)faixa.indexStart + i]; }
        }

        // só os vértices usados, centrado e com 1 m na maior medida
        var vs = copia.vertices; var ns = copia.normals; var uvs = copia.uv;
        var mapa = new Dictionary<int, int>();
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>();
        var ni = new int[idx.Length];
        for (int i = 0; i < idx.Length; i++)
        {
            if (!mapa.TryGetValue(idx[i], out int k))
            {
                k = nv.Count; mapa[idx[i]] = k;
                nv.Add(vs[idx[i]]);
                if (ns.Length == vs.Length) nn.Add(ns[idx[i]]);
                if (uvs.Length == vs.Length) nu.Add(uvs[idx[i]]);
            }
            ni[i] = k;
        }
        var lb = new Bounds(nv[0], Vector3.zero); foreach (var v in nv) lb.Encapsulate(v);
        float esc = 1f / Mathf.Max(lb.size.x, lb.size.y, lb.size.z);
        for (int i = 0; i < nv.Count; i++) nv[i] = (nv[i] - lb.center) * esc;
        int niveis = copia.lodCount;
        Object.DestroyImmediate(copia);

        var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaCarvaoCaminho);
        bool nova = malha == null;
        if (nova) malha = new Mesh();
        malha.Clear();
        malha.name = "Pedra de Carvao";
        malha.SetVertices(nv);
        if (nn.Count == nv.Count) malha.SetNormals(nn);
        if (nu.Count == nv.Count) malha.SetUVs(0, nu);
        malha.SetTriangles(ni, 0);
        if (nn.Count != nv.Count) malha.RecalculateNormals();
        malha.RecalculateBounds();
        malha.RecalculateTangents();
        if (nova) AssetDatabase.CreateAsset(malha, MalhaCarvaoCaminho);
        EditorUtility.SetDirty(malha);

        var mat = Material("Carvao Escaneado", CoalTex, 0.45f, 0f);
        mat.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.57f));
        log.AppendLine($"carvão: {origem.triangles.Length / 3} → {ni.Length / 3} triângulos (nível {escolhido} de {niveis}), {nv.Count} vértices");
    }

    public static Mesh MalhaCarvao => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaCarvaoCaminho);
    public static Material MatCarvao => AssetDatabase.LoadAssetAtPath<Material>(MatCarvaoCaminho);

    /// <summary>Troca o cubo de uma pedra pelo carvão escaneado (mesma escala: a malha tem 1 m).</summary>
    public static bool VirarCarvao(GameObject pedra)
    {
        var malha = MalhaCarvao; var mat = MatCarvao;
        var mf = pedra.GetComponent<MeshFilter>(); var mr = pedra.GetComponent<MeshRenderer>();
        if (malha == null || mat == null || mf == null || mr == null) return false;
        // o cubo enchia mais: a pedra irregular vem um pouco maior
        if (mf.sharedMesh != null && mf.sharedMesh.name == "Cube") pedra.transform.localScale *= 1.25f;
        mf.sharedMesh = malha;
        mr.sharedMaterial = mat;
        return true;
    }

    /// <summary>Pedras dos montes de carvão da cena aberta viram carvão de verdade.</summary>
    public static int CarvaoNaCena()
    {
        int n = 0;
        foreach (var mc in Object.FindObjectsByType<MonteDeCarvao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var mf in mc.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() is Collider c && c != null && !(c is BoxCollider)) continue;
                if (mf.sharedMesh.name != "Cube" && mf.sharedMesh != MalhaCarvao) continue;
                if (VirarCarvao(mf.gameObject)) { n++; EditorUtility.SetDirty(mf.gameObject); }
            }
        return n;
    }

    // ================================================================== prévia

    public static void Previa(StringBuilder log, IEnumerable<string> caminhos)
    {
        Directory.CreateDirectory(Saida);
        var lista = caminhos.ToList();
        const int lado = 300;
        int colunas = 6, linhas = Mathf.CeilToInt(lista.Count / (float)colunas);
        var folha = new Texture2D(colunas * lado, linhas * lado, TextureFormat.RGB24, false);
        int i = 0;
        foreach (var c in lista)
        {
            var tex = Foto(c, lado, log, i);
            if (tex != null)
            {
                int x = (i % colunas) * lado, y = (linhas - 1 - i / colunas) * lado;
                folha.SetPixels(x, y, lado, lado, tex.GetPixels());
                Object.DestroyImmediate(tex);
            }
            i++;
        }
        folha.Apply();
        File.WriteAllBytes(Path.Combine(Saida, "folha.png"), folha.EncodeToPNG());
        Object.DestroyImmediate(folha);
        log.AppendLine($"folha.png: {lista.Count} modelos, {colunas} por linha");
    }

    public static IEnumerable<string> PrefabsMontados() =>
        Pecas.Select(p => $"{PastaPrefabs}/{p.nome}.prefab").Concat(new[] { "carvao" });

    private static Texture2D Foto(string caminho, int lado, StringBuilder log, int indice)
    {
        var ps = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject go;
            if (caminho == "carvao")
            {
                go = new GameObject("carvao");
                for (int k = 0; k < 6; k++)
                {
                    var p = new GameObject("p" + k);
                    p.transform.SetParent(go.transform, false);
                    p.AddComponent<MeshFilter>().sharedMesh = MalhaCarvao;
                    p.AddComponent<MeshRenderer>().sharedMaterial = MatCarvao;
                    p.transform.localPosition = new Vector3((k % 3) * 0.6f, 0f, (k / 3) * 0.6f);
                    p.transform.localRotation = Quaternion.Euler(k * 50f, k * 70f, 0f);
                    p.transform.localScale = Vector3.one * (0.4f + 0.05f * k);
                }
                SceneManager.MoveGameObjectToScene(go, ps);
            }
            else
            {
                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
                if (modelo == null) { log.AppendLine($"[{indice}] {caminho}: não carregou"); return null; }
                go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, ps);
            }
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) { log.AppendLine($"[{indice}] {caminho}: sem malha"); return null; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            int tris = 0;
            foreach (var r in rs)
            {
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m != null) tris += (int)(Enumerable.Range(0, m.subMeshCount).Sum(k => (long)m.GetIndexCount(k)) / 3);
            }
            log.AppendLine($"[{indice}] {Path.GetFileNameWithoutExtension(caminho)}: {tris} tris, {b.size.x:0.00}x{b.size.y:0.00}x{b.size.z:0.00} m");

            var luz = new GameObject("luz").AddComponent<Light>();
            luz.type = LightType.Directional; luz.transform.rotation = Quaternion.Euler(40f, 150f, 0f); luz.intensity = 1.3f;
            SceneManager.MoveGameObjectToScene(luz.gameObject, ps);
            var luz2 = new GameObject("luz2").AddComponent<Light>();
            luz2.type = LightType.Directional; luz2.transform.rotation = Quaternion.Euler(20f, -40f, 0f); luz2.intensity = 0.6f;
            SceneManager.MoveGameObjectToScene(luz2.gameObject, ps);
            var camGo = new GameObject("cam");
            SceneManager.MoveGameObjectToScene(camGo, ps);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = ps; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.33f, 0.36f, 0.4f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.005f; cam.farClipPlane = 1000f;
            float raio = b.extents.magnitude;
            camGo.transform.position = b.center + new Vector3(-1f, 0.8f, -1.2f).normalized * (raio / Mathf.Sin(15f * Mathf.Deg2Rad));
            camGo.transform.LookAt(b.center);
            var rt = new RenderTexture(lado, lado, 24);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(lado, lado, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, lado, lado), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            return tex;
        }
        finally { EditorSceneManager.ClosePreviewScene(ps); }
    }
}
