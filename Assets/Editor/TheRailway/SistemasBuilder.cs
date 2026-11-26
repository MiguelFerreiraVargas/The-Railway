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
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Liga na CenaPrincipal os sistemas de sobrevivência: mochila (a do kit vira pegável), armas
/// (raras, espalhadas pelas estruturas), animais (prefabs + spawner), eventos nos trilhos,
/// a base do rádio no fim da linha, desmaio e efeitos de estado. Pode rodar de novo.
/// </summary>
public static class SistemasBuilder
{
    private const string PastaAnimais = "Assets/Prefabs/Animais";
    private const string Urso = "Assets/Packages/BearModel/source/Bear Animated.fbx";
    private const string Cervo = "Assets/Packages/Deer/animated-deer-3d-animal-model-free-download.fbx";
    private const string SurvivalFbx = "Assets/Models/Survival/Models/Survival.fbx";
    private static StringBuilder log;

    public static void Construir(StringBuilder saida)
    {
        try { Montar(saida); }
        catch
        {
            // não deixa a cena pela metade: recarrega do disco
            EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
            throw;
        }
    }

    private static void Montar(StringBuilder saida)
    {
        log = saida;
        Legivel(SurvivalFbx);
        var cena = EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        var fonte = FonteDoHud();
        var kit = GameObject.Find("Survival");
        var modo = Object.FindFirstObjectByType<ModoCamera>();
        if (modo == null) throw new System.Exception("sem jogador (ModoCamera)");
        var jogador = modo.gameObject;

        // ---------------------------------------------------------------- mochila
        var mochila = kit != null ? kit.transform.Find("Backpack") : null;
        if (mochila != null)
        {
            MundoBuilder.TornarColetavel(mochila.gameObject, "mochila", 1, "mundo.item.mochila", "PEGAR MOCHILA", "Uma mochila! Agora cabe bem mais coisa. [Tab] abre.");
            log.AppendLine("mochila do kit: pegável");
        }
        var mochila2 = kit != null ? kit.transform.Find("Backpack.001") : null;
        if (mochila2 != null) mochila2.gameObject.SetActive(false);

        var sistemas = GameObject.Find("Sistemas do Jogo");
        if (sistemas == null) sistemas = new GameObject("Sistemas do Jogo");
        var tela = Pegar<TelaMochila>(sistemas); tela.fonte = fonte; EditorUtility.SetDirty(tela);
        Pegar<EfeitosDeEstado>(sistemas);

        // ---------------------------------------------------------------- armas
        // armas dos modelos da pasta CalebeAssets3D (Assets/Prefabs/Calebe); sem eles, as feitas aqui / do kit
        AssetsCalebe.Montar(log);
        GameObject faca = AssetsCalebe.Prefab("Faca") ?? FacaNova();
        GameObject machado = AssetsCalebe.Prefab("Machado") ?? MachadoNovo();
        if (machado == null && kit != null && kit.transform.Find("Axe") != null) machado = kit.transform.Find("Axe").gameObject;
        GameObject rifle = AssetsCalebe.Prefab("Rifle") ?? (kit != null ? kit.transform.Find("hunting_rifle")?.gameObject : null);
        var rifleVelho = kit != null ? kit.transform.Find("hunting_rifle")?.gameObject : null;
        var machadoVelho = kit != null ? kit.transform.Find("Axe")?.gameObject : null;
        var armas = Pegar<ArmasDoJogador>(jogador);
        armas.modeloFaca = faca; armas.modeloMachado = machado; armas.modeloRifle = rifle; armas.fonte = fonte;
        EditorUtility.SetDirty(armas);
        Pegar<Desmaio>(jogador);
        // as armas do kit saem de vista: armas são achados raros
        var facaVelha = kit != null && kit.transform.Find("knife") != null ? kit.transform.Find("knife").gameObject : null;
        foreach (var a in new[] { faca, machado, rifle, facaVelha, rifleVelho, machadoVelho }) if (a != null && a.scene.IsValid()) { a.SetActive(false); EditorUtility.SetDirty(a); }
        ArmasNasEstruturas(faca, machado, rifle);

        // ---------------------------------------------------------------- pedras dos montes: carvão escaneado
        log.AppendLine($"carvão escaneado em {AssetsCalebe.CarvaoNaCena()} pedras dos montes");

        // ---------------------------------------------------------------- carvão da locomotiva
        foreach (var ct in Object.FindObjectsByType<CombustivelTrem>(FindObjectsSortMode.None))
        {
            var sc = new SerializedObject(ct);
            sc.FindProperty("metrosPorCarvao").floatValue = 170f;
            sc.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("carvão: 170 m por pedra (a viagem pede ~26 pedras; o mapa tem 35)");
        }

        // ---------------------------------------------------------------- braços numa câmera própria (não atravessam paredes)
        if (Camera.main != null && Camera.main.GetComponent<CameraDosBracos>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraDosBracos>();
            log.AppendLine("câmera dos braços na câmera principal (camada FirstPerson)");
        }

        // ---------------------------------------------------------------- lanterna e pilhas pelas casas
        var lanternaModelo = Camera.main != null ? Camera.main.transform.Find("Lantern") : null;
        var pilhaModelo = kit != null ? kit.transform.Find("Battery") : null;
        Achados(lanternaModelo != null ? lanternaModelo.gameObject : null, pilhaModelo != null ? pilhaModelo.gameObject : null);

        // ---------------------------------------------------------------- animais
        Directory.CreateDirectory(PastaAnimais);
        var cervo = PrefabCervo();
        var urso = PrefabUrso("Urso", 1f, null);
        var lobo = PrefabUrso("Lobo", 0.42f, new Color(0.33f, 0.33f, 0.36f));
        var sp = Object.FindFirstObjectByType<SpawnerDeAnimais>();
        if (sp == null) sp = new GameObject("Animais").AddComponent<SpawnerDeAnimais>();
        sp.especies = new List<SpawnerDeAnimais.Especie>
        {
            new SpawnerDeAnimais.Especie { nome = "cervo", prefab = cervo, peso = 5f, bandoMin = 1, bandoMax = 3 },
            new SpawnerDeAnimais.Especie { nome = "lobo", prefab = lobo, peso = 3f, bandoMin = 2, bandoMax = 3, hostil = true },
            new SpawnerDeAnimais.Especie { nome = "urso", prefab = urso, peso = 1.2f, bandoMin = 1, bandoMax = 1, hostil = true },
        };
        EditorUtility.SetDirty(sp);
        log.AppendLine("animais: cervo, lobo (modelo do urso, menor e cinza), urso");

        // ---------------------------------------------------------------- ferrovia: eventos e base
        var trem = Object.FindFirstObjectByType<TrainInteraction>();
        var anim = trem != null ? trem.GetComponent<SplineAnimate>() : null;
        var trilho = anim != null ? anim.Container : Object.FindFirstObjectByType<SplineContainer>();
        if (trilho == null) throw new System.Exception("sem trilho");
        var zonas = new List<Collider>();
        Eventos(trilho, zonas);
        Base(trilho, kit, fonte, zonas);
        var veg = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
        if (veg != null)
        {
            var l = (veg.exclusionZones ?? new Collider[0]).Where(z => z != null).ToList();
            l.AddRange(zonas);
            veg.exclusionZones = l.Distinct().ToArray();
            EditorUtility.SetDirty(veg);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        log.AppendLine("cena salva");
    }

    // ================================================================== armas raras

    private static void ArmasNasEstruturas(GameObject faca, GameObject machado, GameObject rifle)
    {
        var estruturas = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).OrderBy(e => e.name).ToList();
        foreach (var e in estruturas)
            foreach (var velha in e.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Arma: ")).ToList())
                if (velha != null) Object.DestroyImmediate(velha.gameObject);

        var tipos = new (string id, GameObject modelo, float chance, string garantida, string acao, string fala)[]
        {
            ("faca", faca, 0.30f, "Casa da Curva", "PEGAR FACA", "Uma faca. Melhor que os punhos. [2] pra usar."),
            ("machado", machado, 0.18f, "Casa Velha 1", "PEGAR MACHADO", "Um machado! Isso corta árvore caída... e o que mais vier. [3]"),
            ("rifle", rifle, 0.10f, "Mercadinho", "PEGAR RIFLE", "Um rifle. Pesado... mas agora eu tenho uma chance. [4]"),
        };
        foreach (var tp in tipos)
        {
            if (tp.modelo == null) { log.AppendLine($"AVISO: sem modelo de {tp.id} no kit"); continue; }
            int postas = 0;
            foreach (var e in estruturas)
            {
                var rnd = new Random((e.name + tp.id).Aggregate(7, (h, c) => h * 31 + c));
                bool garantida = e.name == tp.garantida;
                if (!garantida && rnd.NextDouble() > tp.chance) continue;
                if (PorArma(e, tp.id, tp.modelo, tp.acao, tp.fala, rnd)) postas++;
            }
            log.AppendLine($"{tp.id}: {postas} pelo mapa (garantida em {tp.garantida})");
        }
    }

    // ao lado de um suprimento da estrutura (no mesmo móvel ou no chão)
    private static bool PorArma(Estrutura e, string id, GameObject modelo, string acao, string fala, Random rnd)
    {
        var itens = e.GetComponentsInChildren<ItemColetavel>(true).ToList();
        if (itens.Count == 0) return false;
        var ref_ = itens[rnd.Next(itens.Count)];
        var rb = ref_.GetComponentInChildren<Renderer>().bounds;
        var copia = Object.Instantiate(modelo, ref_.transform.parent);
        copia.name = "Arma: " + id;
        copia.SetActive(true);
        foreach (var c in copia.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        float ang = (float)rnd.NextDouble() * 360f;
        copia.transform.rotation = Quaternion.Euler(0f, ang, 0f) * modelo.transform.rotation;
        var rs = copia.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        Vector3 lado = Quaternion.Euler(0f, ang, 0f) * Vector3.right;
        Vector3 alvo = new Vector3(rb.center.x, rb.min.y, rb.center.z) + lado * (Mathf.Max(b.extents.x, b.extents.z) * 0.5f + 0.25f);
        copia.transform.position += new Vector3(alvo.x - b.center.x, alvo.y - b.min.y + 0.005f, alvo.z - b.center.z);
        string chave = new string(e.name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        MundoBuilder.TornarColetavel(copia, id, 1, $"estrutura.{chave}.arma.{id}", acao, fala);
        return true;
    }

    // ================================================================== lanterna e pilhas (achados nas casas)

    private static void Achados(GameObject lanterna, GameObject pilha)
    {
        var estruturas = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).OrderBy(e => e.name).ToList();
        foreach (var e in estruturas)
            foreach (var velha in e.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Achado: ")).ToList())
                if (velha != null) Object.DestroyImmediate(velha.gameObject);
        int lanternas = 0, pilhas = 0;
        foreach (var e in estruturas)
        {
            bool casa = e.name.StartsWith("Casa") || e.name == "Dom";
            var rnd = new Random((e.name + "lanterna").Aggregate(11, (h, c) => h * 31 + c));
            // lanterna: só em casas (de um e de dois andares); garantida na casa do começo
            if (lanterna != null && casa && (e.name == "Dom" || rnd.NextDouble() < 0.4))
                if (PorAchado(e, "lanterna", lanterna, "PEGAR LANTERNA", "Uma lanterna! Agora dá pra enxergar alguma coisa. [L] liga.", rnd, 0.22f, "lanterna")) lanternas++;
            // pilhas: casas e mercadinho
            if (pilha != null)
            {
                int n = e.name == "Mercadinho" ? 3 : e.name == "Dom" ? 1 : rnd.NextDouble() < 0.6 ? 1 + rnd.Next(2) : 0;
                for (int i = 0; i < n; i++)
                    if (PorAchado(e, "pilha", pilha, "PEGAR PILHA", i == 0 ? "Pilha. Pra lanterna." : "", rnd, 0.07f, "pilha" + (i + 1))) pilhas++;
            }
        }
        log.AppendLine($"lanternas: {lanternas} (garantida na Dom), pilhas: {pilhas}");
    }

    private static bool PorAchado(Estrutura e, string id, GameObject modelo, string acao, string fala, Random rnd, float tamanho, string chaveItem)
    {
        var itens = e.GetComponentsInChildren<ItemColetavel>(true).Where(i => !i.name.StartsWith("Achado: ")).ToList();
        if (itens.Count == 0) return false;
        var ref_ = itens[rnd.Next(itens.Count)];
        var rb = ref_.GetComponentInChildren<Renderer>().bounds;
        var copia = Object.Instantiate(modelo, ref_.transform.parent);
        copia.name = "Achado: " + id;
        copia.SetActive(true);
        foreach (var mb in copia.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
        foreach (var c in copia.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var l in copia.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        float ang = (float)rnd.NextDouble() * 360f;
        copia.transform.rotation = Quaternion.Euler(0f, ang, 0f) * Quaternion.Euler(0f, 0f, 90f);
        var rs = copia.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Object.DestroyImmediate(copia); return false; }
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        copia.transform.localScale *= tamanho / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        // deitada; ao lado de um suprimento
        Vector3 lado = Quaternion.Euler(0f, ang + 90f, 0f) * Vector3.right;
        Vector3 alvo = new Vector3(rb.center.x, rb.min.y, rb.center.z) + lado * (Mathf.Max(b.extents.x, b.extents.z) + 0.2f);
        copia.transform.position += new Vector3(alvo.x - b.center.x, alvo.y - b.min.y + 0.005f, alvo.z - b.center.z);
        string chave = new string(e.name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        MundoBuilder.TornarColetavel(copia, id, 1, $"estrutura.{chave}.achado.{chaveItem}", acao, fala);
        return true;
    }

    // ================================================================== faca nova (modelada aqui)

    private const string PastaFaca = "Assets/Models/Faca";

    // faca de caça de 26 cm, deitada (lâmina no +X, gume no -Z, espessura no Y)
    private static GameObject FacaNova()
    {
        Directory.CreateDirectory(PastaFaca);
        var aco = MaterialFaca("Faca_Aco", new Color(0.82f, 0.84f, 0.86f), 0.55f, 0.7f); // metal demais fica preto à noite
        var escuro = MaterialFaca("Faca_Guarda", new Color(0.16f, 0.15f, 0.14f), 0.85f, 0.55f);
        var madeira = MaterialFaca("Faca_Cabo", new Color(0.45f, 0.27f, 0.14f), 0f, 0.35f);

        var raiz = new GameObject("Faca");
        Peca(raiz, "Lamina", MalhaLamina(), aco);
        Peca(raiz, "Guarda", Caixa(new Vector3(0.004f, 0f, -0.003f), new Vector3(0.008f, 0.014f, 0.044f)), escuro);
        Peca(raiz, "Cabo", MalhaCabo(), madeira);
        Peca(raiz, "Pomo", Caixa(new Vector3(-0.114f, 0f, 0f), new Vector3(0.01f, 0.02f, 0.03f)), escuro);
        // deitada no chão: a parte de baixo na origem
        foreach (Transform f in raiz.transform) f.localPosition = new Vector3(0f, 0.012f, 0f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, PastaFaca + "/Faca.prefab");
        Object.DestroyImmediate(raiz);
        log.AppendLine("faca nova: lâmina de aço, guarda e cabo de madeira (26 cm)");
        return prefab;
    }

    private static Material MaterialFaca(string nome, Color cor, float metal, float liso)
    {
        string caminho = PastaFaca + "/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
        m.SetColor("_BaseColor", cor); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", liso);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void Peca(GameObject raiz, string nome, Mesh malha, Material mat)
    {
        string caminho = PastaFaca + "/" + nome + ".asset";
        var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
        if (velha != null) { velha.Clear(); EditorUtility.CopySerialized(malha, velha); malha = velha; }
        else AssetDatabase.CreateAsset(malha, caminho);
        var go = new GameObject(nome);
        go.transform.SetParent(raiz.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = malha;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // lâmina em cunha: lombo grosso em cima (+Z), fio no gume (-Z), ponta subindo para o lombo
    private static Mesh MalhaLamina()
    {
        const int n = 24; const float x0 = 0.008f, comp = 0.142f;
        var v = new List<Vector3>(); var tris = new List<int>();
        Vector3 Lombo(float t, float lado) { float zs = t < 0.68f ? 0.011f : 0.011f - 0.013f * Mathf.Pow((t - 0.68f) / 0.32f, 1.6f); float h = 0.0021f * (1f - Mathf.Pow(t, 6f)); return new Vector3(x0 + comp * t, h * lado, zs); }
        Vector3 Fio(float t) { float ze = -0.017f + 0.015f * Mathf.Pow(t, 2.6f); return new Vector3(x0 + comp * t, 0f, ze); }
        // três faces (de cima, de baixo e o lombo), cada uma com seus vértices (arestas vivas)
        void Faixa(System.Func<float, Vector3> a, System.Func<float, Vector3> b)
        {
            int i0 = v.Count;
            for (int i = 0; i <= n; i++) { float t = i / (float)n; v.Add(a(t)); v.Add(b(t)); }
            for (int i = 0; i < n; i++)
            {
                int k = i0 + i * 2;
                tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }
        }
        Faixa(t => Lombo(t, 1f), Fio);            // de cima
        Faixa(Fio, t => Lombo(t, -1f));           // de baixo
        Faixa(t => Lombo(t, -1f), t => Lombo(t, 1f)); // lombo
        // fecha a base da lâmina (encostada na guarda)
        int b0 = v.Count;
        v.Add(Lombo(0f, 1f)); v.Add(Lombo(0f, -1f)); v.Add(Fio(0f));
        tris.AddRange(new[] { b0, b0 + 2, b0 + 1 });
        var m = new Mesh { name = "Lamina" };
        m.SetVertices(v); m.SetTriangles(tris, 0);
        Consertar(m);
        return m;
    }

    // cabo: prisma de 10 lados, oval, mais gordo no meio
    private static Mesh MalhaCabo()
    {
        const int lados = 10, aneis = 9; const float x0 = -0.11f, x1 = 0f;
        var v = new List<Vector3>(); var tris = new List<int>();
        for (int a = 0; a <= aneis; a++)
        {
            float t = a / (float)aneis;
            float bojo = 1f + 0.14f * Mathf.Sin(t * Mathf.PI) + (t < 0.12f ? 0.12f * (1f - t / 0.12f) : 0f);
            for (int l = 0; l < lados; l++)
            {
                float ang = l * Mathf.PI * 2f / lados;
                v.Add(new Vector3(Mathf.Lerp(x0, x1, t), Mathf.Sin(ang) * 0.0095f * bojo, Mathf.Cos(ang) * 0.0125f * bojo));
            }
        }
        for (int a = 0; a < aneis; a++)
            for (int l = 0; l < lados; l++)
            {
                int i = a * lados + l, j = a * lados + (l + 1) % lados;
                tris.AddRange(new[] { i, j, i + lados, j, j + lados, i + lados });
            }
        int c0 = v.Count; v.Add(new Vector3(x0, 0f, 0f));
        for (int l = 0; l < lados; l++) tris.AddRange(new[] { c0, (l + 1) % lados, l });
        var m = new Mesh { name = "Cabo" };
        m.SetVertices(v); m.SetTriangles(tris, 0);
        Consertar(m);
        return m;
    }

    private static Mesh Caixa(Vector3 centro, Vector3 tam)
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(tmp);
        var vs = m.vertices;
        for (int i = 0; i < vs.Length; i++) vs[i] = centro + Vector3.Scale(vs[i], tam);
        m.vertices = vs;
        m.RecalculateBounds();
        return m;
    }

    // vira as faces para fora (pelo centro da peça) e calcula normais
    private static void Consertar(Mesh m)
    {
        m.RecalculateBounds();
        var vs = m.vertices; var t = m.triangles; var c = m.bounds.center;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = vs[t[i]], b = vs[t[i + 1]], d = vs[t[i + 2]];
            Vector3 nrm = Vector3.Cross(b - a, d - a);
            Vector3 meio = (a + b + d) / 3f;
            Vector3 fora = meio - c; fora.x *= 0.05f; // a peça é comprida em X: "fora" é mais para longe do eixo
            if (Vector3.Dot(nrm, fora) < 0f) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
        }
        m.triangles = t;
        m.RecalculateNormals();
        m.RecalculateTangents();
    }

    // ================================================================== machado novo

    private const string PastaMachado = "Assets/Models/Machado";

    private static GameObject MachadoNovo()
    {
        string obj = PastaMachado + "/Machado.obj";
        if (!File.Exists(obj)) { log.AppendLine("AVISO: machado novo não encontrado"); return null; }
        var imp = AssetImporter.GetAtPath(obj) as ModelImporter;
        if (imp != null && (!imp.isReadable || imp.materialImportMode != ModelImporterMaterialImportMode.None))
        {
            imp.isReadable = true; // a mão lê os vértices para saber qual ponta é a cabeça
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
        }
        var nimp = AssetImporter.GetAtPath(PastaMachado + "/Machado_Normal.png") as TextureImporter;
        if (nimp != null && nimp.textureType != TextureImporterType.NormalMap) { nimp.textureType = TextureImporterType.NormalMap; nimp.SaveAndReimport(); }

        string caminhoMat = PastaMachado + "/Machado.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(caminhoMat);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, caminhoMat); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PastaMachado + "/Machado_Cor.png"));
        mat.SetColor("_BaseColor", Color.white);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PastaMachado + "/Machado_Normal.png");
        if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
        var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(PastaMachado + "/Machado_Metalico.png");
        if (metal != null) { mat.SetTexture("_MetallicGlossMap", metal); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        mat.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(mat);

        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(obj);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var raiz = new GameObject("Machado");
        inst.transform.SetParent(raiz.transform, false);
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = mat;
        // 60 cm, deitado no chão (no prefab o eixo comprido fica na horizontal)
        var rs = inst.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
        inst.transform.localScale *= 0.6f / Mathf.Max(0.001f, maior);
        if (b.size.y >= b.size.x && b.size.y >= b.size.z) inst.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        rs = inst.GetComponentsInChildren<Renderer>();
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, PastaMachado + "/Machado.prefab");
        Object.DestroyImmediate(raiz);
        log.AppendLine($"machado novo: {maior:0.00} → 0,6 m");
        return prefab;
    }

    // ================================================================== animais

    private static GameObject PrefabCervo()
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Cervo);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Cervo";
        var a = go.AddComponent<Animal>();
        a.especie = "cervo"; a.causaMorte = "CHIFRADO POR UM CERVO";
        a.comportamento = Animal.Comportamento.Passivo;
        a.vida = 45f; a.velAndar = 1.3f; a.velCorrer = 8f; a.distVer = 20f; a.carne = 2;
        a.velAnimAndar = 1.2f; a.velAnimCorrer = 7f;
        var clips = Clipes(Cervo);
        a.clipParado = Clipe(clips, "idle_Skeleton"); a.clipAndar = Clipe(clips, "walk_Skeleton");
        a.clipCorrer = Clipe(clips, "run_Skeleton"); a.clipFerido = Clipe(clips, "wound_Skeleton");
        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 2; col.center = new Vector3(0f, 1.0f, 0.2f); col.radius = 0.45f; col.height = 2.1f;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return Salvar(go, "Cervo");
    }

    private static GameObject PrefabUrso(string nome, float escala, Color? tinta)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Urso);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = nome;
        var raiz = new GameObject(nome);
        go.transform.SetParent(raiz.transform, false);
        go.transform.localScale = modelo.transform.localScale * escala;
        if (go.GetComponent<Animator>() == null) go.AddComponent<Animator>();
        if (tinta.HasValue)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => Tingido(m, tinta.Value, nome)).ToArray();

        var a = raiz.AddComponent<Animal>();
        var clips = Clipes(Urso);
        a.clipParado = Clipe(clips, "Stand_Idle_01"); a.clipAndar = Clipe(clips, "Walk"); a.clipCorrer = Clipe(clips, "Run");
        a.clipAtacar = Clipe(clips, "Attack_Run_01_AttackF"); a.clipFerido = Clipe(clips, "Hit_Stand_F01"); a.clipMorrer = Clipe(clips, "Death_Stand_R01");
        bool lobo = nome == "Lobo";
        a.especie = lobo ? "lobo" : "urso";
        a.causaMorte = lobo ? "ATACADO POR LOBOS" : "ATACADO POR UM URSO";
        a.comportamento = Animal.Comportamento.Agressivo;
        a.vida = lobo ? 70f : 260f;
        a.velAndar = lobo ? 1.6f : 1.4f; a.velCorrer = lobo ? 8.2f : 6.8f;
        a.distVer = lobo ? 26f : 22f; a.distAtaque = lobo ? 1.7f : 2.6f;
        a.dano = lobo ? 11f : 28f; a.intervaloAtaque = lobo ? 1.1f : 1.8f;
        a.carne = lobo ? 1 : 5;
        a.velAnimAndar = 1.5f * escala; a.velAnimCorrer = 6.5f * escala;

        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var col = raiz.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.center = raiz.transform.InverseTransformPoint(b.center);
        col.radius = Mathf.Min(b.extents.x, b.extents.y) * 0.9f;
        col.height = b.size.z;
        return Salvar(raiz, nome);
    }

    private static Material Tingido(Material m, Color cor, string nome)
    {
        if (m == null) return null;
        string caminho = $"{PastaAnimais}/{nome} {m.name}.mat";
        var t = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (t == null) { t = new Material(m); AssetDatabase.CreateAsset(t, caminho); }
        else t.CopyPropertiesFromMaterial(m);
        foreach (var p in new[] { "_BaseColor", "_Color" }) if (t.HasProperty(p)) t.SetColor(p, t.GetColor(p) * cor);
        EditorUtility.SetDirty(t);
        return t;
    }

    private static GameObject Salvar(GameObject go, string nome)
    {
        // o FBX do cervo traz luzes direcionais (e câmeras) da cena de origem: cada bicho acendia três "sóis"
        // (no URP a luz tem um componente extra que impede tirar só ela: some o objeto inteiro da luz)
        int tirados = 0;
        var fora = go.GetComponentsInChildren<Light>(true).Select(l => l.gameObject)
            .Concat(go.GetComponentsInChildren<Camera>(true).Select(c => c.gameObject)).Distinct().ToList();
        foreach (var obj in fora)
        {
            if (obj == null) continue;
            if (obj == go || obj.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                // tem malha junto: tira os componentes extras primeiro, depois a luz/câmera
                foreach (var comp in obj.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter) && !(c is Animator) && !(c is Light) && !(c is Camera) && !(c is MonoBehaviour && c.GetType().Namespace == null)).ToList())
                    if (comp.GetType().Name.StartsWith("UniversalAdditional")) Object.DestroyImmediate(comp);
                foreach (var l in obj.GetComponents<Light>()) Object.DestroyImmediate(l);
                foreach (var c in obj.GetComponents<Camera>()) Object.DestroyImmediate(c);
            }
            else Object.DestroyImmediate(obj);
            tirados++;
        }
        int sobrou = go.GetComponentsInChildren<Light>(true).Length + go.GetComponentsInChildren<Camera>(true).Length;
        if (sobrou > 0) log.AppendLine($"AVISO: {nome} ainda tem {sobrou} luzes/câmeras");
        if (tirados > 0) log.AppendLine($"{nome}: {tirados} luzes/câmeras do modelo removidas");
        var p = PrefabUtility.SaveAsPrefabAsset(go, $"{PastaAnimais}/{nome}.prefab");
        Object.DestroyImmediate(go);
        return p;
    }

    private static AnimationClip[] Clipes(string caminho) =>
        AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();

    private static AnimationClip Clipe(AnimationClip[] clips, string fim)
    {
        var c = clips.FirstOrDefault(x => x.name == fim) ?? clips.FirstOrDefault(x => x.name.EndsWith(fim));
        if (c == null) log.AppendLine($"AVISO: clipe {fim} não achado");
        return c;
    }

    // ================================================================== eventos nos trilhos

    private static void Eventos(SplineContainer trilho, List<Collider> zonas)
    {
        var velho = GameObject.Find("Eventos da Ferrovia");
        if (velho != null) Object.DestroyImmediate(velho);
        var grupo = new GameObject("Eventos da Ferrovia").transform;
        var ter = Terrain.activeTerrain;
        var veg = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
        var arvore = AcharArvore(veg);
        var matPedra = MatPedra();

        var planos = new (float t, EventoFerrovia.Tipo tipo)[]
        {
            (0.12f, EventoFerrovia.Tipo.ArvoreCaida),
            (0.27f, EventoFerrovia.Tipo.Animais),
            (0.47f, EventoFerrovia.Tipo.Pedras),
            (0.62f, EventoFerrovia.Tipo.ArvoreCaida),
            (0.80f, EventoFerrovia.Tipo.Pedras),
        };
        int n = 0;
        foreach (var (t, tipo) in planos)
        {
            n++;
            trilho.Evaluate(t, out float3 p, out float3 tan, out _);
            Vector3 pos = p, frente = ((Vector3)tan); frente.y = 0f; frente.Normalize();
            var go = new GameObject($"Evento {n}: {tipo}");
            go.transform.SetParent(grupo, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(frente, Vector3.up));
            var ev = go.AddComponent<EventoFerrovia>();
            ev.tipo = tipo; ev.t = t; ev.flag = $"ferrovia.evento{n}";
            var so = new SerializedObject(ev);
            so.FindProperty("acao").stringValue = tipo == EventoFerrovia.Tipo.ArvoreCaida ? "TIRAR A ÁRVORE" : tipo == EventoFerrovia.Tipo.Pedras ? "TIRAR AS PEDRAS" : "ESPANTAR OS BICHOS";
            so.ApplyModifiedPropertiesWithoutUndo();

            var rnd = new Random(n * 13);
            if (tipo == EventoFerrovia.Tipo.ArvoreCaida)
            {
                GameObject a = arvore != null ? Object.Instantiate(arvore, go.transform) : null;
                if (a != null)
                {
                    a.SetActive(true);
                    foreach (var c in a.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    foreach (var mb in a.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
                    if (a.GetComponentsInChildren<Renderer>(true).Length == 0) { Object.DestroyImmediate(a); a = null; }
                }
                if (a == null)
                {
                    // sem modelo de árvore: um tronco grosso
                    a = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(a.GetComponent<Collider>());
                    a.name = "Tronco";
                    a.transform.SetParent(go.transform, false);
                    a.transform.localScale = new Vector3(0.7f, 4f, 0.7f);
                    var casca = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Survival/Textures/Trunk.png");
                    var mt = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (casca != null) mt.SetTexture("_BaseMap", casca);
                    mt.SetColor("_BaseColor", new Color(0.45f, 0.35f, 0.25f));
                    a.GetComponent<Renderer>().sharedMaterial = mt;
                }
                // deitada atravessando os trilhos: a base do tronco fica no chão de um lado e o tronco
                // passa por cima dos trilhos, encostado neles (a copa cai do outro lado)
                a.transform.rotation = Quaternion.identity;
                var rs = a.GetComponentsInChildren<Renderer>(true);
                var b0 = rs[0].bounds; foreach (var r in rs) b0.Encapsulate(r.bounds);
                Vector3 baseOff = new Vector3(b0.center.x, b0.min.y, b0.center.z) - a.transform.position;
                var deitada = Quaternion.FromToRotation(Vector3.up, go.transform.right) * Quaternion.Euler(0f, rnd.Next(360), 0f);
                Vector3 baseNoChao = pos - go.transform.right * 2.2f + Vector3.up * 0.4f;
                a.transform.rotation = deitada;
                a.transform.position = baseNoChao - deitada * baseOff;
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.9f, 0f); bc.size = new Vector3(7f, 1.8f, 2.2f);
            }
            else if (tipo == EventoFerrovia.Tipo.Pedras)
            {
                var partes = new List<Transform>();
                for (int i = 0; i < 6; i++)
                {
                    var pedra = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                    Object.DestroyImmediate(pedra.GetComponent<Collider>());
                    pedra.name = "Pedra " + (i + 1);
                    pedra.transform.SetParent(go.transform, false);
                    float s = 0.7f + (float)rnd.NextDouble() * 0.7f;
                    pedra.transform.localScale = new Vector3(s * 1.2f, s * 0.8f, s);
                    pedra.transform.localPosition = new Vector3(((float)rnd.NextDouble() - 0.5f) * 4.5f, s * 0.3f, ((float)rnd.NextDouble() - 0.5f) * 2.2f);
                    pedra.transform.localRotation = Quaternion.Euler(rnd.Next(360), rnd.Next(360), rnd.Next(360));
                    pedra.GetComponent<Renderer>().sharedMaterial = matPedra;
                    partes.Add(pedra.transform);
                }
                ev.partes = partes.ToArray();
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.6f, 0f); bc.size = new Vector3(5.5f, 1.2f, 3f);
            }
            else
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true; bc.center = new Vector3(0f, 1f, 0f); bc.size = new Vector3(6f, 2f, 6f);
            }
            // sem árvores em volta do obstáculo (a de verdade é só a caída)
            var z = new GameObject("Zona sem Arvores");
            z.transform.SetParent(go.transform, false);
            var zb = z.AddComponent<BoxCollider>(); zb.isTrigger = true; zb.size = new Vector3(14f, 30f, 14f);
            zonas.Add(zb);
            log.AppendLine($"evento {n}: {tipo} em t={t:0.00} {pos:F0}");
        }
    }

    private static GameObject AcharArvore(ProceduralVegetationSpawner veg)
    {
        if (veg == null) return null;
        var so = new SerializedObject(veg);
        var it = so.GetIterator();
        while (it.Next(true))
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is GameObject g && g.name.ToLowerInvariant().Contains("tree")
                && g.GetComponentsInChildren<Renderer>(true).Length > 0)
                return g;
        return null;
    }

    private static Material MatPedra()
    {
        const string caminho = "Assets/Materials/Estruturas/Pedra do Deslizamento.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Survival/Textures/Rock.jpg");
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", new Color(0.55f, 0.52f, 0.48f));
        m.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(m, caminho);
        return m;
    }

    // ================================================================== a base no fim da linha

    private static void Base(SplineContainer trilho, GameObject kit, TMP_FontAsset fonte, List<Collider> zonas)
    {
        var velho = GameObject.Find("Base dos Sobreviventes");
        if (velho != null) Object.DestroyImmediate(velho);
        var ter = Terrain.activeTerrain;
        trilho.Evaluate(1f, out float3 pf, out float3 tf, out _);
        Vector3 fim = pf, frente = ((Vector3)tf); frente.y = 0f; frente.Normalize();
        // a base fica logo depois do fim dos trilhos; o portão olha para o trem
        Vector3 centro = fim + frente * 20f;
        if (ter != null) { var tp = ter.transform.position; var ts = ter.terrainData.size; centro.x = Mathf.Clamp(centro.x, tp.x + 20f, tp.x + ts.x - 20f); centro.z = Mathf.Clamp(centro.z, tp.z + 20f, tp.z + ts.z - 20f); }
        float Y(Vector3 v) => ter != null ? ter.SampleHeight(v) + ter.transform.position.y : v.y;
        centro.y = Y(centro);

        var raiz = new GameObject("Base dos Sobreviventes").transform;
        raiz.SetPositionAndRotation(centro, Quaternion.LookRotation(-frente, Vector3.up)); // +Z local = portão, virado para o trem
        var madeira = AssetDatabase.FindAssets("Casa Velha", new[] { "Assets/Materials/Estruturas" }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name.Contains("Beams")) ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Prologo/Carvao.mat");

        // cerca 28 x 28 m, com o portão (3 m) no meio da frente
        const float lado = 14f;
        int tabuas = 0;
        for (int s = 0; s < 4; s++)
        {
            Vector3 dir = Quaternion.Euler(0f, 90f * s, 0f) * Vector3.right;
            Vector3 normal = Quaternion.Euler(0f, 90f * s, 0f) * Vector3.forward;
            for (float x = -lado; x < lado; x += 2f)
            {
                Vector3 meioLocal = normal * lado + dir * (x + 1f);
                if (s == 0 && Mathf.Abs(x + 1f) < 2f) continue; // vão do portão
                Vector3 mundo = raiz.TransformPoint(meioLocal);
                var tab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tab.name = "Cerca";
                tab.transform.SetParent(raiz, true);
                tab.transform.position = new Vector3(mundo.x, Y(mundo) + 1.1f, mundo.z);
                tab.transform.rotation = raiz.rotation * Quaternion.LookRotation(normal);
                tab.transform.localScale = new Vector3(2.05f, 2.4f, 0.18f);
                tab.GetComponent<Renderer>().sharedMaterial = madeira;
                tabuas++;
            }
        }
        // portão: dois mourões altos e a placa
        Vector3 portaoLocal = Vector3.forward * lado;
        foreach (float sx in new[] { -2f, 2f })
        {
            var m = raiz.TransformPoint(portaoLocal + Vector3.right * sx);
            var poste = GameObject.CreatePrimitive(PrimitiveType.Cube);
            poste.name = "Mourao"; poste.transform.SetParent(raiz, true);
            poste.transform.position = new Vector3(m.x, Y(m) + 1.6f, m.z); poste.transform.rotation = raiz.rotation;
            poste.transform.localScale = new Vector3(0.35f, 3.4f, 0.35f);
            poste.GetComponent<Renderer>().sharedMaterial = madeira;
            var luz = new GameObject("Lampiao").AddComponent<Light>();
            luz.transform.SetParent(poste.transform, false); luz.transform.localPosition = new Vector3(0f, 0.55f, 0.6f);
            luz.type = LightType.Point; luz.color = new Color(1f, 0.7f, 0.4f); luz.range = 22f; luz.intensity = 40f;
        }
        var pm = raiz.TransformPoint(portaoLocal);
        var placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placa.name = "Placa"; placa.transform.SetParent(raiz, true);
        placa.transform.position = new Vector3(pm.x, Y(pm) + 3.3f, pm.z); placa.transform.rotation = raiz.rotation;
        placa.transform.localScale = new Vector3(4.6f, 0.8f, 0.12f);
        placa.GetComponent<Renderer>().sharedMaterial = madeira;
        var txt = new GameObject("Texto da Placa").AddComponent<TextMeshPro>();
        txt.transform.SetParent(raiz, true);
        txt.transform.position = placa.transform.position + raiz.forward * 0.08f;
        txt.transform.rotation = raiz.rotation * Quaternion.Euler(0f, 180f, 0f);
        txt.text = "BASE  -132 432 -22"; txt.fontSize = 4f; txt.alignment = TextAlignmentOptions.Center; txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.color = new Color(0.95f, 0.9f, 0.75f); txt.rectTransform.sizeDelta = new Vector2(4.4f, 0.8f);
        if (fonte != null) txt.font = fonte;

        // dentro: barracas, fogueira acesa e lanternas
        // barracas de lona em volta da fogueira
        var lona = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lona" };
        lona.SetColor("_BaseColor", new Color(0.32f, 0.36f, 0.24f));
        const string caminhoLona = "Assets/Materials/Estruturas/Lona da Base.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(caminhoLona) == null) AssetDatabase.CreateAsset(lona, caminhoLona);
        lona = AssetDatabase.LoadAssetAtPath<Material>(caminhoLona);
        foreach (var (local, giro) in new[] { (new Vector3(-7f, 0f, -6f), 30f), (new Vector3(6.5f, 0f, -7f), -20f), (new Vector3(-8f, 0f, 5f), 100f), (new Vector3(7.5f, 0f, 6f), 200f) })
        {
            var w = raiz.TransformPoint(local);
            var barraca = new GameObject("Barraca").transform;
            barraca.SetParent(raiz, false);
            barraca.position = new Vector3(w.x, Y(w), w.z);
            barraca.rotation = raiz.rotation * Quaternion.Euler(0f, giro, 0f);
            foreach (float lado2 in new[] { -1f, 1f })
            {
                var pano = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pano.name = "Lona";
                pano.transform.SetParent(barraca, false);
                pano.transform.localRotation = Quaternion.Euler(0f, 0f, 50f * lado2);
                pano.transform.localPosition = new Vector3(0.62f * lado2, 0.78f, 0f);
                pano.transform.localScale = new Vector3(0.06f, 2.0f, 3.2f);
                pano.GetComponent<Renderer>().sharedMaterial = lona;
            }
        }

        // fogueira, lampião e troncos para sentar (as peças do kit são pequenas: ajusta o tamanho)
        var dentro = new (string peca, Vector3 local, float giro, float tamanho)[]
        {
            ("bonfire", new Vector3(0f, 0f, -1f), 0f, 1.3f), ("Farol", new Vector3(3.5f, 0f, 2.5f), 0f, 1.4f), ("Farol", new Vector3(-3.5f, 0f, -5f), 0f, 1.4f),
            ("Trunk", new Vector3(1.9f, 0f, -1.6f), 80f, 2.2f), ("Trunk_01", new Vector3(-1.8f, 0f, -0.4f), 10f, 2.0f), ("Trunk", new Vector3(0.2f, 0f, 1.2f), 0f, 2.0f),
        };
        foreach (var (peca, local, giro, tamanho) in dentro)
        {
            var src = kit != null ? kit.transform.Find(peca) : null;
            if (src == null) continue;
            var c = Object.Instantiate(src.gameObject, raiz);
            c.SetActive(true);
            foreach (var mb in c.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            c.transform.rotation = raiz.rotation * Quaternion.Euler(0f, giro, 0f) * src.rotation;
            var rs = c.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var w = raiz.TransformPoint(local);
            float escala = tamanho / Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.y, b.size.z));
            c.transform.localScale *= escala;
            rs = c.GetComponentsInChildren<Renderer>();
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            c.transform.position += new Vector3(w.x - b.center.x, Y(w) - b.min.y, w.z - b.center.z);
            if (peca == "Farol")
            {
                var lamp = new GameObject("Luz do Lampiao").AddComponent<Light>();
                lamp.transform.SetParent(c.transform, false);
                lamp.transform.position = new Vector3(w.x, Y(w) + 1.2f, w.z);
                lamp.type = LightType.Point; lamp.color = new Color(1f, 0.8f, 0.5f); lamp.range = 12f; lamp.intensity = 25f;
            }
            if (peca == "bonfire")
            {
                var fogo = new GameObject("Fogo").AddComponent<Light>();
                fogo.transform.SetParent(c.transform, false);
                fogo.transform.position = new Vector3(w.x, Y(w) + 0.8f, w.z);
                fogo.type = LightType.Point; fogo.color = new Color(1f, 0.55f, 0.2f); fogo.range = 18f; fogo.intensity = 60f;
            }
        }

        // o portão (entrar = fim do jogo)
        var portao = new GameObject("Portao da Base");
        portao.transform.SetParent(raiz, false);
        portao.transform.position = new Vector3(pm.x, Y(pm) + 1.2f, pm.z);
        portao.transform.rotation = raiz.rotation;
        var bc = portao.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.size = new Vector3(4f, 2.4f, 1.5f);
        var cb = portao.AddComponent<ChegadaBase>();
        cb.fonte = fonte;

        var zona = new GameObject("Zona sem Arvores");
        zona.transform.SetParent(raiz, false);
        var zb = zona.AddComponent<BoxCollider>(); zb.isTrigger = true; zb.size = new Vector3(40f, 60f, 40f);
        zonas.Add(zb);
        log.AppendLine($"base dos sobreviventes em {centro:F0} ({tabuas} tábuas de cerca), portão virado para o trem");
    }

    // ================================================================== utilitários

    private static T Pegar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private static TMP_FontAsset FonteDoHud()
    {
        var obj = Object.FindFirstObjectByType<Objetivos>(FindObjectsInactive.Include);
        var t = obj != null ? new SerializedObject(obj).FindProperty("texto").objectReferenceValue as TMP_Text : null;
        return t != null ? t.font : TMP_Settings.defaultFontAsset;
    }

    private static void Legivel(string caminho)
    {
        var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
        if (imp == null || imp.isReadable) return;
        imp.isReadable = true;
        imp.SaveAndReimport();
        log.AppendLine(Path.GetFileName(caminho) + ": malhas legíveis");
    }
}
