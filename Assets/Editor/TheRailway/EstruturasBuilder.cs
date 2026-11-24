using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Espalha estruturas ao longo da ferrovia da CenaPrincipal (casas, a casa velha de dois andares,
/// o mercadinho e vagões abandonados), tira as árvores de dentro delas e mobilia o interior com
/// móveis do pacote do mercadinho e suprimentos que dá para pegar.
/// Pode rodar de novo: refaz o grupo "Estruturas" e os móveis da casa original.
/// </summary>
public static partial class EstruturasBuilder
{
    private const string HouseFbx = "Assets/Models/CASAS/old-stash-house/source/House.fbx";
    private const string TambooFbx = "Assets/Models/CASAS/tambo-minimarket-ps1-environment/source/tamboo.fbx";
    private const string PastaMat = "Assets/Materials/Estruturas";
    private const string Grupo = "Estruturas";
    private const string Pasta = "Temp/TheRailway";

    private enum Tipo { Casa, CasaVelha, Mercadinho, Vagao }

    private struct Plano
    {
        public float t; public int lado; public Tipo tipo; public float dist; public string nome;
        public Plano(float t, int lado, Tipo tipo, float dist, string nome) { this.t = t; this.lado = lado; this.tipo = tipo; this.dist = dist; this.nome = nome; }
    }

    // posição no trilho (0..1), lado, tipo, distância do trilho
    private static readonly Plano[] Planos =
    {
        new Plano(0.09f,  1, Tipo.Casa,       17f, "Casa da Curva"),
        new Plano(0.15f, -1, Tipo.Vagao,       7f, "Vagao Tombado 1"),
        new Plano(0.20f,  1, Tipo.CasaVelha,  20f, "Casa Velha 1"),
        new Plano(0.33f, -1, Tipo.Mercadinho, 22f, "Mercadinho"),
        new Plano(0.40f,  1, Tipo.Casa,       17f, "Casa do Morro"),
        new Plano(0.46f, -1, Tipo.Vagao,       7f, "Vagao Tombado 2"),
        new Plano(0.58f, -1, Tipo.CasaVelha,  20f, "Casa Velha 2"),
        new Plano(0.64f,  1, Tipo.Casa,       17f, "Casa do Lago"),
        new Plano(0.71f,  1, Tipo.Vagao,       7f, "Vagao Tombado 3"),
        new Plano(0.84f, -1, Tipo.Casa,       17f, "Casa da Serra"),
        new Plano(0.91f,  1, Tipo.CasaVelha,  20f, "Casa Velha 3"),
    };

    private static StringBuilder log;
    private static SplineContainer trilho;
    private static Terrain terreno;
    private static readonly List<Vector3> pontosTrilho = new List<Vector3>();
    private static readonly List<Bounds> ocupados = new List<Bounds>();
    private static Dictionary<string, Transform> pecasTamboo;
    private static readonly Collider[] buf = new Collider[64];
    private static readonly List<Transform> estruturasFeitas = new List<Transform>();

    // ================================================================== entrada

    public static void Construir(StringBuilder saida)
    {
        log = saida;
        var scene = EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        GarantirLegivel(TambooFbx);
        GarantirLegivel(HouseFbx);
        recipientes = 0;

        var trem = Object.FindFirstObjectByType<TrainInteraction>();
        var anim = trem != null ? trem.GetComponent<SplineAnimate>() : null;
        trilho = anim != null ? anim.Container : Object.FindFirstObjectByType<SplineContainer>();
        terreno = Terrain.activeTerrain;
        if (trilho == null || terreno == null) { log.AppendLine("ERRO: sem trilho ou terreno"); return; }
        log.AppendLine($"trilho: {trilho.CalculateLength():0} m; terreno {terreno.terrainData.size:F0} em {terreno.transform.position:F0}");

        pontosTrilho.Clear();
        for (int i = 0; i <= 800; i++) { trilho.Evaluate(i / 800f, out float3 p, out _, out _); pontosTrilho.Add(p); }

        var raizT = AssetDatabase.LoadAssetAtPath<GameObject>(TambooFbx);
        pecasTamboo = new Dictionary<string, Transform>();
        foreach (var t in raizT.GetComponentsInChildren<Transform>(true)) if (!pecasTamboo.ContainsKey(t.name)) pecasTamboo[t.name] = t;

        // refaz do zero
        var velho = GameObject.Find(Grupo);
        if (velho != null) Object.DestroyImmediate(velho);
        var grupo = new GameObject(Grupo).transform;
        estruturasFeitas.Clear();

        var domOriginal = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Dom");
        ocupados.Clear();
        if (domOriginal != null) ocupados.Add(Expandido(BoundsDe(domOriginal.transform), 10f));
        var kit = GameObject.Find("Survival");
        if (kit != null) ocupados.Add(new Bounds(kit.transform.position, new Vector3(40f, 10f, 40f)));
        var montes = GameObject.Find("Montes de Carvao");
        if (montes != null) foreach (Transform m in montes.transform) if (!m.name.Contains("(Vagao")) ocupados.Add(new Bounds(m.position, new Vector3(10f, 6f, 10f)));

        int semente = 11;
        foreach (var pl in Planos)
        {
            Transform t = null;
            switch (pl.tipo)
            {
                case Tipo.Casa: t = NovaCasa(domOriginal, grupo, pl.nome); break;
                case Tipo.CasaVelha: t = NovaCasaVelha(grupo, pl.nome); break;
                case Tipo.Mercadinho: t = NovoMercadinho(grupo, pl.nome); break;
                case Tipo.Vagao: t = NovoVagao(trem, grupo, pl.nome); break;
            }
            if (t == null) continue;
            if (!Posicionar(t, pl))
            {
                log.AppendLine($"AVISO: sem lugar para {pl.nome}, ficou de fora");
                Object.DestroyImmediate(t.gameObject);
                continue;
            }
            Physics.SyncTransforms();
            var rnd = new Random(semente++);
            if (pl.tipo == Tipo.Casa) TrancarComodo(t, pl.nome, rnd); // antes dos móveis: os cômodos ainda estão vazios
            if (pl.tipo == Tipo.Vagao) MobiliarVagao(t, rnd, pl.nome);
            else Mobiliar(t, pl.tipo, rnd, pl.nome);
            if (pl.tipo == Tipo.CasaVelha)
            {
                // o andar de cima também tem coisas
                var e2 = t.GetComponent<Estrutura>();
                var cima = e2.pisos.FirstOrDefault(c => c != null && c.name == "Piso de Cima A");
                if (cima != null) Mobiliar(t, pl.tipo, rnd, pl.nome + " Cima", cima, new[] { Colchao, Colchao, Armario, Estante, Caixa, Caixa, Caixa, Cadeira }, 4);
            }
            estruturasFeitas.Add(t);
        }

        // a casa original também ganha móveis (os documentos continuam onde estão)
        if (domOriginal != null)
        {
            foreach (var nome in new[] { "Moveis", "Suprimentos", "Entrada", "Dentro" })
            {
                var c = domOriginal.transform.Find(nome);
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
            MarcarPassagem(domOriginal.transform, new Vector3(-4.4f, 0f, 3.2f), new Vector3(-4.4f, 0.4f, -1.4f));
            Physics.SyncTransforms();
            Mobiliar(domOriginal.transform, Tipo.Casa, new Random(5), "Dom");
            estruturasFeitas.Add(domOriginal.transform);
        }

        ZonasSemArvores();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva: " + IsometricoBuilder.ScenePath);
    }

    // ================================================================== tipos de estrutura

    // Cópia da casa da cena (já tem colisores, portas automáticas e Estrutura configurados)
    private static Transform NovaCasa(GameObject domOriginal, Transform grupo, string nome)
    {
        if (domOriginal == null) { log.AppendLine("AVISO: sem a casa \"Dom\" para copiar"); return null; }
        var go = Object.Instantiate(domOriginal, grupo);
        go.name = nome;
        foreach (var j in go.GetComponentsInChildren<Interagivel>(true).ToList()) Object.DestroyImmediate(j.gameObject); // documentos são só da original
        foreach (var n in new[] { "Moveis", "Suprimentos", "Entrada", "Dentro" })
        {
            var c = go.transform.Find(n);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
        MarcarPassagem(go.transform, new Vector3(-4.4f, 0f, 3.2f), new Vector3(-4.4f, 0.4f, -1.4f));
        return go.transform;
    }

    // Casa velha de dois andares, um salão grande embaixo
    private static Transform NovaCasaVelha(Transform grupo, string nome)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HouseFbx);
        if (prefab == null) { log.AppendLine("AVISO: modelo da casa velha não achado"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = nome;
        var semColisor = new HashSet<string> { "Roof.001", "Plane.004", "Cylinder", "Cube.008", "Cube.003", "Cube.013" };
        Collider piso = null;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            // as cores do modelo são limpas demais: versão gasta, escura e sem brilho
            r.sharedMaterials = r.sharedMaterials.Select(Gasto).ToArray();
            if (r.name == "Door.001") { Porta(r); continue; }
            if (semColisor.Contains(r.name)) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            if (r.name == "Floor") piso = mc;
        }
        var est = go.AddComponent<Estrutura>();
        est.pisos = piso != null ? CasaVelhaPorDentro(go.transform, piso).ToArray() : new Collider[0];
        est.raioVerificacao = 14f;
        MarcarPassagem(go.transform, new Vector3(-2.6f, 0f, 7.2f), new Vector3(0f, 0.1f, 1.5f));
        return go.transform;
    }

    // O mercadinho do pacote "tambo": só o prédio e o que tem dentro (sem a rua e as árvores em volta)
    private static Transform NovoMercadinho(Transform grupo, string nome)
    {
        var raiz = new GameObject(nome).transform;
        raiz.SetParent(grupo, false);
        var fora = new[] { "arbol", "arbus", "cesped", "pasto", "pastito", "Plane.015", "escalones", "Tubo_grande", "ladrilleraxd", "veredas", "vereditaa", "vereditaxd", "Plane.009", "Plane.013", "Plane.014" };
        var semColisorPrefixo = new[] { "alfombra", "mantaXD", "graffiti", "publicidad", "luzRectangulo", "bombilla", "Rejilla", "aireAcondicionado", "puertaDeta", "puertaEntrada.001", "puertaEntrada.003", "puerta", "Cube.002", "snack", "cajistasImpu", "idk" };
        var portas = new[] { "puertaEntrada", "puertaEntrada.002", "Plane.006", "Plane.007", "puerta.001" };
        var vidroDaPorta = new Dictionary<string, string> { { "puertaEntrada.001", "puertaEntrada" }, { "puertaEntrada.003", "puertaEntrada.002" } };

        var criados = new Dictionary<string, MeshRenderer>();
        foreach (var src in pecasTamboo.Values)
        {
            var mr = src.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            if (fora.Any(f => src.name.StartsWith(f))) continue;
            // plantas de recorte (o fundo delas fica preto aqui) também saem
            if (mr.sharedMaterials.Any(m => m != null && m.name.Contains("Plantas"))) continue;
            var go = CopiaDe(src, raiz);
            var r = go.GetComponent<MeshRenderer>();
            var b = r.bounds;
            // só o que fica no terreno do prédio
            if (b.center.x < -16.5f || b.center.x > 6.6f || b.center.z < -9.5f || b.center.z > 17.5f || b.center.y < -1.3f || b.center.y > 7f || b.size.x > 24f || b.size.z > 24f)
            { Object.DestroyImmediate(go); continue; }
            criados[src.name] = r;
        }

        // portas que abrem sozinhas (o vidro vai junto com a folha)
        foreach (var par in vidroDaPorta)
            if (criados.TryGetValue(par.Key, out var vidro) && criados.TryGetValue(par.Value, out var folha))
                vidro.transform.SetParent(folha.transform, true);
        foreach (var p in portas) if (criados.TryGetValue(p, out var r)) Porta(r, p == "puertaEntrada.002");

        var pisos = new List<Collider>();
        foreach (var kv in criados)
        {
            var r = kv.Value;
            if (r == null || r.GetComponent<Collider>() != null) continue;
            if (semColisorPrefixo.Any(f => kv.Key.StartsWith(f)) && !portas.Contains(kv.Key)) continue;
            if (Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) < 0.25f) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            if (kv.Key == "piso" || kv.Key == "Plane.005") pisos.Add(mc);
        }
        // o piso primeiro (a isométrica usa o topo dele para cortar o teto)
        pisos = pisos.OrderBy(c => c.name == "piso" ? 0 : 1).ToList();
        var est = raiz.gameObject.AddComponent<Estrutura>();
        est.pisos = pisos.ToArray();
        est.raioVerificacao = 18f;
        MarcarPassagem(raiz, new Vector3(-3.6f, 0f, 14.5f), new Vector3(-3.6f, 0.1f, 9.1f));
        log.AppendLine($"{nome}: {criados.Count} peças do pacote");
        RecipientesDoMercadinho(raiz);
        return raiz;
    }

    // Vagão de carga abandonado ao lado da linha (cópia do vagão do trem, tombado)
    private static Transform NovoVagao(TrainInteraction trem, Transform grupo, string nome)
    {
        var src = trem != null ? trem.transform.Find("vagon") : null;
        if (src == null) { log.AppendLine("AVISO: sem vagão para copiar"); return null; }
        var go = Object.Instantiate(src.gameObject, grupo);
        go.name = nome;
        go.transform.localScale = src.lossyScale;
        go.transform.rotation = src.rotation;
        foreach (var ac in go.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().Name == "AddCollidersToMeshes").ToList()) Object.DestroyImmediate(ac);
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            if (r.GetComponent<Collider>() == null) r.gameObject.AddComponent<MeshCollider>();
        return go.transform;
    }

    private static void Porta(MeshRenderer r, bool dobradicaNoFim = false)
    {
        var go = r.gameObject;
        if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
        var porta = go.GetComponent<PortaAutomatica>();
        if (porta == null) porta = go.AddComponent<PortaAutomatica>();
        var b = r.bounds;
        bool aoLongoDeX = b.size.x >= b.size.z;
        Vector3 dob = aoLongoDeX
            ? new Vector3(dobradicaNoFim ? b.max.x - 0.03f : b.min.x + 0.03f, b.min.y, b.center.z)
            : new Vector3(b.center.x, b.min.y, dobradicaNoFim ? b.max.z - 0.03f : b.min.z + 0.03f);
        porta.dobradicaLocal = go.transform.parent.InverseTransformPoint(dob);
    }

    private static void MarcarPassagem(Transform estrutura, Vector3 entrada, Vector3 dentro)
    {
        foreach (var (n, p) in new[] { ("Entrada", entrada), ("Dentro", dentro) })
        {
            var g = new GameObject(n).transform;
            g.SetParent(estrutura, false);
            g.localPosition = p;
        }
    }

    // ================================================================== posição no mapa

    // Procura, perto do ponto planejado, um lugar plano ao lado da linha (o terreno é cheio de morros:
    // num declive a porta ficaria no alto, sem como subir).
    private static bool Posicionar(Transform t, Plano pl)
    {
        var rot0 = t.rotation;
        float melhorNota = float.MaxValue, melhorDt = 0f, melhorDist = 0f, melhorDesnivel = 0f;
        int melhorLado = pl.lado;
        for (int i = 0; i <= 24; i++)
        for (int troca = 0; troca < 2; troca++)
        {
            int lado = troca == 0 ? pl.lado : -pl.lado;
            float dt = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 0.004f; // 0, +-0.004, +-0.008... (~18 m cada)
            for (float dist = pl.dist; dist <= pl.dist + 24f; dist += 3f)
            {
                if (!Pousar(t, pl, lado, rot0, pl.t + dt, dist, out float desnivel, out _)) continue;
                // mais que um palmo de desnível na porta e o degrau fica alto demais para subir
                float nota = desnivel * 10f + (desnivel > 0.2f && pl.tipo != Tipo.Vagao ? 100f : 0f) + Mathf.Abs(dt) * 40f + (dist - pl.dist) * 0.04f + troca * 1.5f;
                if (nota < melhorNota) { melhorNota = nota; melhorDt = dt; melhorDist = dist; melhorDesnivel = desnivel; melhorLado = lado; }
            }
        }
        if (melhorNota == float.MaxValue) return false;
        Pousar(t, pl, melhorLado, rot0, pl.t + melhorDt, melhorDist, out _, out float yaw);
        ocupados.Add(Expandido(BoundsDe(t), 8f));
        log.AppendLine($"{pl.nome}: {t.position:F0} (trilho t={pl.t + melhorDt:0.000}, {melhorDist:0} m do trilho, giro {yaw:0}°, desnível {melhorDesnivel:0.00} m)");
        if (melhorDesnivel > 0.45f && pl.tipo != Tipo.Vagao) log.AppendLine($"AVISO: {pl.nome} ficou num terreno com {melhorDesnivel:0.00} m de desnível");
        return true;
    }

    // Põe a estrutura no ponto t do trilho, a "dist" metros do lado; diz o desnível do terreno embaixo e na porta.
    private static bool Pousar(Transform t, Plano pl, int ladoSinal, Quaternion rot0, float tt, float dist, out float desnivel, out float yaw)
    {
        desnivel = 0f;
        trilho.Evaluate(Mathf.Clamp01(tt), out float3 p3, out float3 tan3, out _);
        Vector3 p = p3, dir = tan3; dir.y = 0f; dir.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, dir).normalized * ladoSinal;
        if (pl.tipo == Tipo.Vagao)
        {
            // paralelo à linha, meio torto e tombado de lado
            yaw = Vector3.SignedAngle(Vector3.forward, dir, Vector3.up) + 9f * ladoSinal;
            t.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 7f * ladoSinal) * rot0;
        }
        else
        {
            // a entrada (+Z local) fica virada para a ferrovia
            yaw = Vector3.SignedAngle(Vector3.forward, -lado, Vector3.up);
            t.rotation = Quaternion.Euler(0f, yaw, 0f) * rot0;
        }
        var b = BoundsDe(t);
        Vector3 alvo = p + lado * dist;
        t.position += new Vector3(alvo.x - b.center.x, 0f, alvo.z - b.center.z);
        b = BoundsDe(t);

        if (!DentroDoTerreno(b)) return false;
        if (DistanciaAoTrilho(b) < (pl.tipo == Tipo.Vagao ? 3.2f : 7f)) return false;
        if (ocupados.Any(o => o.Intersects(b))) return false;

        // altura: o ponto mais alto do terreno embaixo dela (nada fica enterrado)
        float h = float.MinValue, baixo = float.MaxValue;
        for (int ix = 0; ix <= 4; ix++)
            for (int iz = 0; iz <= 4; iz++)
            {
                float a = Altura(new Vector3(Mathf.Lerp(b.min.x, b.max.x, ix / 4f), 0f, Mathf.Lerp(b.min.z, b.max.z, iz / 4f)));
                h = Mathf.Max(h, a); baixo = Mathf.Min(baixo, a);
            }
        var ent = t.Find("Entrada");
        if (ent != null) baixo = Mathf.Min(baixo, Altura(ent.position));
        desnivel = h - baixo;
        float baseY = pl.tipo == Tipo.Casa ? 0f : pl.tipo == Tipo.CasaVelha ? 0.03f : pl.tipo == Tipo.Mercadinho ? 0.1f : -0.25f;
        if (pl.tipo == Tipo.Vagao) h = Altura(b.center);
        t.position = new Vector3(t.position.x, h + baseY, t.position.z);
        return true;
    }

    private static float Altura(Vector3 p) => terreno.SampleHeight(p) + terreno.transform.position.y;

    private static bool DentroDoTerreno(Bounds b)
    {
        var tp = terreno.transform.position; var s = terreno.terrainData.size;
        return b.min.x > tp.x + 5f && b.max.x < tp.x + s.x - 5f && b.min.z > tp.z + 5f && b.max.z < tp.z + s.z - 5f;
    }

    private static float DistanciaAoTrilho(Bounds b)
    {
        float menor = float.MaxValue;
        var cantos = new List<Vector2>();
        for (int ix = 0; ix <= 4; ix++)
            for (int iz = 0; iz <= 4; iz++)
                cantos.Add(new Vector2(Mathf.Lerp(b.min.x, b.max.x, ix / 4f), Mathf.Lerp(b.min.z, b.max.z, iz / 4f)));
        for (int i = 1; i < pontosTrilho.Count; i++)
        {
            Vector2 a = new Vector2(pontosTrilho[i - 1].x, pontosTrilho[i - 1].z), c = new Vector2(pontosTrilho[i].x, pontosTrilho[i].z);
            foreach (var q in cantos)
            {
                Vector2 ab = c - a; float k = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                menor = Mathf.Min(menor, Vector2.Distance(q, a + ab * k));
            }
        }
        return menor;
    }

    private static Bounds BoundsDe(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Bounds Expandido(Bounds b, float m) { b.Expand(new Vector3(m * 2f, 0f, m * 2f)); return b; }

    // ================================================================== árvores

    private static void ZonasSemArvores()
    {
        var spawner = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
        var lista = spawner != null ? (spawner.exclusionZones ?? new Collider[0]).Where(c => c != null).ToList() : new List<Collider>();
        foreach (var t in estruturasFeitas)
        {
            var zona = t.Find("Zona sem Arvores");
            if (zona != null) { lista.Remove(zona.GetComponent<Collider>()); Object.DestroyImmediate(zona.gameObject); }
            var b = BoundsDe(t);
            var zg = new GameObject("Zona sem Arvores");
            zg.transform.SetParent(t, true);
            zg.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            var ls = zg.transform.lossyScale;
            var bc = zg.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            // a copa das árvores é larga: folga de 6 m em volta; bem alta para pegar qualquer altura do terreno
            var tam = new Vector3(b.size.x + 12f, 80f, b.size.z + 12f);
            bc.size = new Vector3(tam.x / Mathf.Max(1e-5f, Mathf.Abs(ls.x)), tam.y / Mathf.Max(1e-5f, Mathf.Abs(ls.y)), tam.z / Mathf.Max(1e-5f, Mathf.Abs(ls.z)));
            lista.Add(bc);
        }
        if (spawner != null)
        {
            spawner.exclusionZones = lista.Distinct().ToArray();
            EditorUtility.SetDirty(spawner);
            log.AppendLine($"zonas sem árvores no gerador: {spawner.exclusionZones.Length}");
        }
    }

    // ================================================================== móveis

    private class Movel
    {
        public string nome;
        public string[] pecas;
        public Vector3 frente;        // eixo da frente no espaço do pacote; zero = solto no meio do cômodo
        public bool superficie;       // dá para pôr suprimento em cima
        public bool colchao;
        public Movel(string nome, Vector3 frente, bool superficie, params string[] pecas) { this.nome = nome; this.frente = frente; this.superficie = superficie; this.pecas = pecas; }
    }

    private static readonly Movel Geladeira = new Movel("Geladeira", Vector3.left, false, "freezerPizza", "puerta");
    private static readonly Movel Armario = new Movel("Armario", Vector3.left, false, "alacenaB");
    private static readonly Movel Estante = new Movel("Estante", Vector3.forward, false, "estante");
    private static readonly Movel Freezer = new Movel("Freezer", Vector3.right, true, "heladera", "puertaHeladera+");
    private static readonly Movel Mesa = new Movel("Mesa", Vector3.zero, true, "mesa_comedor", "banca", "banca.001");
    private static readonly Movel MesaPequena = new Movel("Mesinha", Vector3.zero, true, "mesa_comedor");
    private static readonly Movel Banco = new Movel("Banco", Vector3.zero, false, "banca");
    private static readonly Movel Bancada = new Movel("Bancada", Vector3.right, true, "mesa_larga");
    private static readonly Movel Cadeira = new Movel("Cadeira", Vector3.zero, false, "silla_escritorio");
    private static readonly Movel Lixeira = new Movel("Lixeira", Vector3.forward, false, "tachoBasura", "tapa tacho");
    private static readonly Movel Colchao = new Movel("Colchao", Vector3.right, false) { colchao = true };
    private static readonly Movel Caixa = new Movel("Caixa", Vector3.forward, true);

    private static Movel[] KitDe(Tipo tipo)
    {
        switch (tipo)
        {
            case Tipo.Casa: return new[] { Geladeira, Armario, Colchao, MesaPequena, Banco, Banco, Caixa, Caixa, Lixeira, Caixa };
            case Tipo.CasaVelha: return new[] { Mesa, Bancada, Geladeira, Armario, Estante, Estante, Freezer, Colchao, Colchao, Cadeira, Caixa, Caixa, Caixa, Caixa, Lixeira };
            case Tipo.Mercadinho: return new[] { Caixa, Caixa, Caixa };
            default: return new Movel[0];
        }
    }

    private class Lugar { public Vector3 centro; public Vector2 meia; public Quaternion rot; public float topo; }

    // piso em que os móveis e itens da vez vão (casa de dois andares); null = qualquer piso da estrutura
    private static Collider pisoDaVez;

    private static void Mobiliar(Transform casa, Tipo tipo, Random rnd, string nome, Collider pisoEscolhido = null, Movel[] kit = null, int qtdSuprimentos = -1)
    {
        var est = casa.GetComponent<Estrutura>();
        if (est == null || est.pisos == null || est.pisos.Length == 0 || est.pisos[0] == null) { log.AppendLine($"AVISO: {nome} sem piso, não mobiliei"); return; }
        var piso = pisoEscolhido != null ? pisoEscolhido : est.pisos[0];
        pisoDaVez = tipo == Tipo.CasaVelha ? piso : null;
        float pisoY = piso.bounds.max.y;
        var moveis = new GameObject("Moveis").transform; moveis.SetParent(casa, false);
        var sup = new GameObject("Suprimentos").transform; sup.SetParent(casa, false);

        // lugares a deixar livres: portas, entrada, documentos
        var evitar = new List<(Vector3 p, float r)>();
        foreach (var porta in casa.GetComponentsInChildren<PortaAutomatica>()) evitar.Add((porta.GetComponent<Renderer>().bounds.center, 1.15f));
        foreach (var n in new[] { "Entrada", "Dentro" }) { var c = casa.Find(n); if (c != null) evitar.Add((c.position, 1.3f)); }
        foreach (var it in casa.GetComponentsInChildren<Interagivel>()) evitar.Add((it.transform.position, 0.9f));
        // casa velha: o pé da escada, o corredor ao lado da rampa e a chegada lá em cima ficam livres
        if (tipo == Tipo.CasaVelha)
        {
            bool emCima = pisoEscolhido != null && pisoEscolhido != est.pisos[0];
            var livres = emCima
                ? new[] { new Vector3(0.6f, 0f, -3.5f), new Vector3(0.2f, 0f, -2.3f), new Vector3(-0.8f, 0f, -3.2f), new Vector3(1.6f, 0f, -2.4f) }
                : new[] { new Vector3(5.6f, 0f, -3.5f), new Vector3(5.6f, 0f, -2.5f), new Vector3(4.7f, 0f, -2.4f), new Vector3(3.6f, 0f, -2.4f), new Vector3(2.5f, 0f, -2.4f), new Vector3(1.5f, 0f, -2.4f) };
            foreach (var l in livres) evitar.Add((casa.TransformPoint(l), 1.25f));
        }
        // o caminho da entrada até o meio da casa
        var en = casa.Find("Entrada"); var de = casa.Find("Dentro");
        if (en != null && de != null) for (int i = 1; i < 6; i++) evitar.Add((Vector3.Lerp(en.position, de.position, i / 6f), 0.9f));

        // pontos candidatos: grade no piso, só os que estão de fato dentro (piso embaixo, teto em cima)
        var pb = piso.bounds;
        var pontos = new List<Vector3>();
        for (float x = pb.min.x + 0.3f; x < pb.max.x - 0.3f; x += 0.25f)
            for (float z = pb.min.z + 0.3f; z < pb.max.z - 0.3f; z += 0.25f)
            {
                var q = new Vector3(x, pisoY, z);
                if (!DentroDe(casa, est, q)) continue;
                pontos.Add(q);
            }
        Embaralhar(pontos, rnd);

        var superficies = new List<Lugar>();
        int postos = 0;
        foreach (var m in kit ?? KitDe(tipo))
        {
            var go = CriarMovel(m, moveis, rnd);
            if (go == null) continue;
            if (ColocarMovel(go, m, casa, pontos, evitar, pisoY, rnd, out var lugar))
            {
                postos++;
                RecipienteDoMovel(go, m.nome, nome, postos, rnd);
                if (m.superficie) superficies.Add(lugar);
                evitar.Add((lugar.centro, 0.2f));
            }
            else Object.DestroyImmediate(go);
        }

        // no mercadinho, os balcões e mesas que já existem também servem
        if (tipo == Tipo.Mercadinho)
            foreach (var r in casa.GetComponentsInChildren<MeshRenderer>())
                if (r.name.StartsWith("mostrador") || r.name.StartsWith("mesa_comedor") || r.name.StartsWith("mesa_larga"))
                    superficies.Add(new Lugar { centro = r.bounds.center, meia = new Vector2(r.bounds.extents.x, r.bounds.extents.z), rot = Quaternion.identity, topo = r.bounds.max.y });

        int qtd = qtdSuprimentos >= 0 ? qtdSuprimentos : tipo == Tipo.Mercadinho ? 11 : tipo == Tipo.CasaVelha ? 8 : 5;
        int itens = Suprimentos(sup, casa, est, nome, qtd, superficies, pontos, evitar, pisoY, rnd);
        log.AppendLine($"{nome}: {postos} móveis, {itens} suprimentos");
        pisoDaVez = null;
    }

    private static bool DentroDe(Transform casa, Estrutura est, Vector3 q)
    {
        bool pisoOk = false;
        if (Physics.Raycast(q + Vector3.up * 1.0f, Vector3.down, out var h, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            pisoOk = pisoDaVez != null ? h.collider == pisoDaVez : est.pisos.Contains(h.collider);
        if (!pisoOk) return false;
        return Physics.Raycast(q + Vector3.up * 0.6f, Vector3.up, out var cima, 8f, ~0, QueryTriggerInteraction.Ignore) && cima.collider.transform.IsChildOf(casa);
    }

    private static GameObject CriarMovel(Movel m, Transform pai, Random rnd)
    {
        if (m.colchao) return CriarColchao(pai);
        var fontes = m.pecas.Length > 0
            ? m.pecas.Select(n => pecasTamboo.TryGetValue(n, out var t) ? t : null).Where(t => t != null && t.GetComponent<MeshFilter>() != null).ToList()
            : new List<Transform>();
        if (m == Caixa)
        {
            // uma ou duas caixas de papelão de tamanho médio, empilhadas
            var caixas = pecasTamboo.Values.Where(t => t.name.StartsWith("cajaCarton") && t.GetComponent<MeshFilter>() != null)
                .Where(t => { var s = Vector3.Scale(t.GetComponent<MeshFilter>().sharedMesh.bounds.size, t.lossyScale); float mx = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)); return mx > 0.35f && mx < 0.9f; })
                .ToList();
            if (caixas.Count == 0) return null;
            fontes = new List<Transform> { caixas[rnd.Next(caixas.Count)] };
        }
        if (fontes.Count == 0) { log.AppendLine($"AVISO: peças do móvel {m.nome} não achadas"); return null; }

        var raiz = new GameObject(m.nome).transform;
        var partes = fontes.Select(f => CopiaDe(f, null).transform).ToList();
        var b = partes[0].GetComponent<Renderer>().bounds;
        foreach (var p in partes) b.Encapsulate(p.GetComponent<Renderer>().bounds);
        raiz.position = new Vector3(b.center.x, b.min.y, b.center.z);
        foreach (var p in partes) p.SetParent(raiz, true);

        // segunda caixa por cima, às vezes
        if (m == Caixa && rnd.Next(3) == 0)
        {
            var de2 = CopiaDe(fontes[0], raiz);
            de2.transform.position += Vector3.up * b.size.y;
            de2.transform.RotateAround(raiz.position, Vector3.up, 20f + rnd.Next(40));
            de2.transform.localScale *= 0.85f;
            b.Encapsulate(de2.GetComponent<Renderer>().bounds);
        }

        var bc = raiz.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, b.size.y / 2f, 0f) + new Vector3(b.center.x - raiz.position.x, 0f, b.center.z - raiz.position.z);
        bc.size = b.size;
        raiz.SetParent(pai, true);
        return raiz.gameObject;
    }

    private static GameObject CriarColchao(Transform pai)
    {
        var raiz = new GameObject("Colchao").transform;
        var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(col.GetComponent<Collider>());
        col.name = "Espuma";
        col.transform.SetParent(raiz, false);
        col.transform.localScale = new Vector3(1.9f, 0.2f, 0.9f);
        col.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        col.GetComponent<Renderer>().sharedMaterial = Mat("Colchao Sujo", new Color(0.45f, 0.41f, 0.34f));
        var trav = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(trav.GetComponent<Collider>());
        trav.name = "Travesseiro";
        trav.transform.SetParent(raiz, false);
        trav.transform.localScale = new Vector3(0.35f, 0.1f, 0.6f);
        trav.transform.localPosition = new Vector3(-0.7f, 0.25f, 0f);
        trav.GetComponent<Renderer>().sharedMaterial = Mat("Travesseiro Encardido", new Color(0.62f, 0.58f, 0.5f));
        if (pecasTamboo.TryGetValue("mantaXD", out var manta))
        {
            var mt = CopiaDe(manta, raiz);
            mt.transform.localPosition += new Vector3(0.25f, 0.21f, 0f) - raiz.InverseTransformPoint(mt.GetComponent<Renderer>().bounds.center);
            mt.transform.localScale *= 1.3f;
        }
        var bc = raiz.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.15f, 0f); bc.size = new Vector3(1.9f, 0.3f, 0.9f);
        raiz.SetParent(pai, true);
        return raiz.gameObject;
    }

    private static bool ColocarMovel(GameObject go, Movel m, Transform casa, List<Vector3> pontos, List<(Vector3 p, float r)> evitar, float pisoY, Random rnd, out Lugar lugar)
    {
        lugar = null;
        var t = go.transform;
        var bc = go.GetComponent<BoxCollider>();
        Vector3 meia = bc.size / 2f;
        bool parede = m.frente != Vector3.zero;
        float raioMovel = new Vector2(meia.x, meia.z).magnitude;
        int giro0 = rnd.Next(4);

        foreach (var q in pontos)
        {
            if (evitar.Any(e => NoChao(e.p - q).magnitude < e.r + raioMovel)) continue;
            for (int k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, 90f * ((giro0 + k) % 4), 0f);
                Vector3 c = new Vector3(q.x, pisoY + 0.01f, q.z);
                Vector3 centro = c + rot * (bc.center);
                if (!Livre(centro + Vector3.up * 0.04f, meia - new Vector3(0.02f, 0.05f, 0.02f), rot, casa, null)) continue;

                if (parede)
                {
                    // costas na parede e um espaço livre na frente
                    Vector3 f = rot * m.frente;
                    float ext = Mathf.Abs(Vector3.Dot(meia, Abs(m.frente)));
                    float lat = Mathf.Abs(Vector3.Dot(meia, Abs(Vector3.Cross(Vector3.up, m.frente))));
                    Vector3 costas = centro - f * (ext + 0.1f);
                    var meiaC = Abs(Vector3.Cross(Vector3.up, m.frente)) * lat * 0.8f + Vector3.up * meia.y * 0.5f + Abs(m.frente) * 0.07f;
                    if (!TocaParede(costas, meiaC, rot, casa)) continue;
                    Vector3 frente = centro + f * (ext + 0.45f);
                    var meiaF = Abs(Vector3.Cross(Vector3.up, m.frente)) * lat + Vector3.up * 0.3f + Abs(m.frente) * 0.35f;
                    if (!Livre(new Vector3(frente.x, pisoY + 0.6f, frente.z), meiaF, rot, casa, null)) continue;
                }
                else
                {
                    // solto no cômodo: folga para passar em volta
                    var folga = meia + new Vector3(0.45f, 0f, 0.45f);
                    folga.y = 0.25f;
                    if (!Livre(new Vector3(centro.x, pisoY + 0.6f, centro.z), folga, rot, casa, null)) continue;
                }

                t.SetPositionAndRotation(c, rot * Quaternion.identity);
                Physics.SyncTransforms();
                lugar = new Lugar { centro = new Vector3(centro.x, 0f, centro.z), meia = new Vector2(meia.x, meia.z), rot = rot, topo = pisoY + 0.01f + bc.size.y };
                if (m == Mesa || m == MesaPequena)
                {
                    // o tampo da mesa (sem os bancos)
                    var tampo = go.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.StartsWith("mesa"));
                    if (tampo != null) lugar = new Lugar { centro = tampo.bounds.center, meia = new Vector2(tampo.bounds.extents.x * 0.7f, tampo.bounds.extents.z * 0.7f), rot = Quaternion.identity, topo = tampo.bounds.max.y };
                }
                return true;
            }
        }
        return false;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static Vector3 NoChao(Vector3 v) { v.y = 0f; return v; }

    private static bool Livre(Vector3 centro, Vector3 meia, Quaternion rot, Transform casa, Transform ignorar)
    {
        int n = Physics.OverlapBoxNonAlloc(centro, meia, buf, rot, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var c = buf[i];
            if (c == null || c is TerrainCollider) continue;
            if (ignorar != null && c.transform.IsChildOf(ignorar)) continue;
            if (c.isTrigger && c.GetComponentInParent<Interagivel>() == null) continue; // zonas sem árvores etc.
            return false;
        }
        return true;
    }

    private static bool TocaParede(Vector3 centro, Vector3 meia, Quaternion rot, Transform casa)
    {
        int n = Physics.OverlapBoxNonAlloc(centro, meia, buf, rot, ~0, QueryTriggerInteraction.Ignore);
        var moveis = casa.Find("Moveis");
        for (int i = 0; i < n; i++)
        {
            var c = buf[i];
            if (c == null || !c.transform.IsChildOf(casa)) continue;
            if (moveis != null && c.transform.IsChildOf(moveis)) continue;
            if (c.GetComponent<PortaAutomatica>() != null) continue;
            return true;
        }
        return false;
    }

    // ================================================================== vagão

    private static void MobiliarVagao(Transform vagao, Random rnd, string nome)
    {
        var sup = new GameObject("Suprimentos").transform; sup.SetParent(vagao, true);
        var moveis = new GameObject("Moveis").transform; moveis.SetParent(vagao, true);
        var b = BoundsDe(vagao);
        // do lado de fora, entre o vagão e a linha (quem passa de trem vê)
        Vector3 paraTrilho = Vector3.zero;
        float menor = float.MaxValue;
        foreach (var p in pontosTrilho) { float d = NoChao(p - b.center).sqrMagnitude; if (d < menor) { menor = d; paraTrilho = NoChao(p - b.center).normalized; } }
        Vector3 ao = Vector3.Cross(Vector3.up, paraTrilho);
        float sai = Mathf.Max(b.extents.x, b.extents.z) * 0.3f + 1.6f;

        var pontos = new List<Vector3>();
        for (float s = -3.5f; s <= 3.5f; s += 0.5f)
            for (float o = 0f; o <= 1.5f; o += 0.5f)
            {
                var q = b.center + paraTrilho * (sai + o) + ao * s;
                q.y = Altura(q);
                pontos.Add(q);
            }
        Embaralhar(pontos, rnd);

        int caixas = 0;
        var superficies = new List<Lugar>();
        for (int i = 0; i < 3; i++)
        {
            var go = CriarMovel(Caixa, moveis, rnd);
            if (go == null) continue;
            var bc = go.GetComponent<BoxCollider>();
            bool ok = false;
            foreach (var q in pontos)
            {
                var rot = Quaternion.Euler(0f, rnd.Next(360), 0f);
                if (!Livre(q + Vector3.up * (bc.size.y / 2f + 0.05f), bc.size / 2f, rot, vagao, null)) continue;
                go.transform.SetPositionAndRotation(q, rot);
                Physics.SyncTransforms();
                superficies.Add(new Lugar { centro = q, meia = new Vector2(bc.size.x, bc.size.z) * 0.35f, rot = rot, topo = q.y + bc.size.y });
                ok = true; caixas++;
                break;
            }
            if (!ok) Object.DestroyImmediate(go);
        }
        int itens = Suprimentos(sup, vagao, null, nome, 3, superficies, pontos, new List<(Vector3, float)>(), float.NaN, rnd);
        log.AppendLine($"{nome}: {caixas} caixas, {itens} suprimentos");
    }

    // ================================================================== suprimentos

    private class Suprimento { public string id, acao, modelo; public int qtd, peso; }

    private static readonly Suprimento[] Tabela =
    {
        new Suprimento { id = "lata_feijao", qtd = 1, peso = 30, acao = "PEGAR LATA DE COMIDA", modelo = "lata" },
        new Suprimento { id = "agua", qtd = 1, peso = 20, acao = "PEGAR GARRAFA D'ÁGUA", modelo = "Water" },
        new Suprimento { id = "soda", qtd = 1, peso = 14, acao = "PEGAR SODA", modelo = "Can_Soda" },
        new Suprimento { id = "biscoito", qtd = 1, peso = 16, acao = "PEGAR BISCOITO", modelo = "snack" },
        new Suprimento { id = "fosforos", qtd = 1, peso = 8, acao = "PEGAR FÓSFOROS", modelo = "match" },
        new Suprimento { id = "municao", qtd = 4, peso = 8, acao = "PEGAR MUNIÇÃO", modelo = "Ammunition" },
        new Suprimento { id = "lata_feijao", qtd = 1, peso = 6, acao = "PEGAR LATA", modelo = "Can" },
    };

    private static int Suprimentos(Transform pai, Transform casa, Estrutura est, string nome, int qtd, List<Lugar> superficies, List<Vector3> pontosChao, List<(Vector3 p, float r)> evitar, float pisoY, Random rnd)
    {
        int feitos = 0;
        int total = Tabela.Sum(s => s.peso);
        string chave = new string(nome.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        for (int i = 0; i < qtd * 4 && feitos < qtd; i++)
        {
            int sorte = rnd.Next(total);
            var s = Tabela.First(x => (sorte -= x.peso) < 0);
            var go = ModeloDoSuprimento(s.modelo, pai, rnd);
            if (go == null) continue;
            go.name = s.acao.Replace("PEGAR ", "").ToLowerInvariant();

            // em cima de um móvel (2 de cada 3) ou no chão
            bool posto = false;
            for (int tent = 0; tent < 12 && !posto; tent++)
            {
                Vector3 alvo; float y;
                if (superficies.Count > 0 && (rnd.Next(3) > 0 || pontosChao.Count == 0))
                {
                    var l = superficies[rnd.Next(superficies.Count)];
                    var off = new Vector3(((float)rnd.NextDouble() * 2f - 1f) * Mathf.Max(0f, l.meia.x - 0.12f), 0f, ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Max(0f, l.meia.y - 0.12f));
                    alvo = l.centro + l.rot * off; y = l.topo + 0.005f;
                }
                else if (pontosChao.Count > 0)
                {
                    alvo = pontosChao[rnd.Next(pontosChao.Count)];
                    y = float.IsNaN(pisoY) ? alvo.y : pisoY + 0.005f;
                    if (evitar.Any(e => NoChao(e.p - alvo).magnitude < e.r)) continue;
                }
                else break;

                AssentarEm(go.transform, new Vector3(alvo.x, y, alvo.z), rnd.Next(360));
                var r = go.GetComponentInChildren<Renderer>();
                var b = r.bounds;
                // não pode enfiar em nada (o próprio item é gatilho; o móvel embaixo fica abaixo da base)
                if (!Livre(b.center + Vector3.up * 0.01f, b.extents * 0.9f, Quaternion.identity, casa, go.transform)) continue;
                if (est != null && !DentroDe(casa, est, new Vector3(b.center.x, pisoDaVez != null ? pisoDaVez.bounds.max.y : est.pisos[0].bounds.max.y, b.center.z))) continue;
                posto = true;
            }
            if (!posto) { Object.DestroyImmediate(go); continue; }

            MundoBuilder.TornarColetavel(go, s.id, s.qtd, $"estrutura.{chave}.item{feitos + 1}", s.acao, "");
            Physics.SyncTransforms();
            feitos++;
        }
        return feitos;
    }

    private static void AssentarEm(Transform t, Vector3 base_, float giro)
    {
        t.rotation = Quaternion.Euler(0f, giro, 0f) * t.rotation;
        var b = t.GetComponentInChildren<Renderer>().bounds;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        t.position += new Vector3(base_.x - b.center.x, base_.y - b.min.y, base_.z - b.center.z);
    }

    private static GameObject ModeloDoSuprimento(string modelo, Transform pai, Random rnd)
    {
        GameObject src = null;
        if (modelo == "lata")
        {
            var comida = GameObject.Find("Comidaaa");
            src = comida != null ? comida.transform.Find("Soup Can (1)")?.gameObject : null;
        }
        else if (modelo == "snack")
        {
            var snacks = pecasTamboo.Values.Where(t => t.name.StartsWith("snack") && t.GetComponent<MeshFilter>() != null).ToList();
            if (snacks.Count == 0) return null;
            var go = CopiaDe(snacks[rnd.Next(snacks.Count)], pai);
            return go;
        }
        else
        {
            var kit = GameObject.Find("Survival");
            src = kit != null ? kit.transform.Find(modelo)?.gameObject : null;
        }
        if (src == null) { log.AppendLine($"AVISO: modelo de suprimento \"{modelo}\" não achado"); return null; }
        var copia = Object.Instantiate(src, pai);
        copia.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        copia.transform.localScale = Vector3.Scale(src.transform.lossyScale, new Vector3(1f / pai.lossyScale.x, 1f / pai.lossyScale.y, 1f / pai.lossyScale.z));
        copia.SetActive(true);
        return copia;
    }

    // ================================================================== utilidades

    // Copia uma peça de modelo (só a malha dela, sem filhos) com a pose que ela tem no arquivo
    private static GameObject CopiaDe(Transform src, Transform pai)
    {
        var mf = src.GetComponent<MeshFilter>(); var mr = src.GetComponent<MeshRenderer>();
        var go = new GameObject(src.name);
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
        go.transform.SetPositionAndRotation(src.position, src.rotation);
        go.transform.localScale = src.lossyScale;
        if (pai != null) go.transform.SetParent(pai, true);
        return go;
    }

    private static void GarantirLegivel(string caminho)
    {
        // o contorno dos itens (QuickOutline) lê os vértices da malha
        var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
        if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); log.AppendLine($"{Path.GetFileName(caminho)}: malhas legíveis (para o contorno dos itens)"); }
    }

    private static Material Mat(string nome, Color cor)
    {
        if (!AssetDatabase.IsValidFolder(PastaMat)) AssetDatabase.CreateFolder("Assets/Materials", "Estruturas");
        string path = $"{PastaMat}/{nome}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Gasto(Material m)
    {
        if (m == null) return null;
        var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var cor = Color.HSVToRGB(h, s * 0.45f, v * 0.5f);
        return Mat("Casa Velha " + m.name, cor);
    }

    private static void Embaralhar<T>(List<T> l, Random rnd)
    {
        for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
    }

    // ================================================================== fotos

    public static void Capturar(StringBuilder saida)
    {
        log = saida;
        var scene = EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        var camGo = new GameObject("Camera Captura");
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 3000f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.24f, 0.22f);
        var luzGo = new GameObject("Luz Captura");
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Directional; luz.intensity = 1.4f; luz.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        var rt = new RenderTexture(1280, 960, 24);
        cam.targetTexture = rt;
        var desligadas = new List<Renderer>();
        var marcas = new List<GameObject>();
        try
        {
            void Foto(string nome)
            {
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(Pasta, nome), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                log.AppendLine("foto: " + nome);
            }

            var todas = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).OrderBy(e => e.name).ToList();
            var grupo = GameObject.Find(Grupo);
            var vagoes = grupo != null ? grupo.transform.Cast<Transform>().Where(t => t.name.StartsWith("Vagao")).ToList() : new List<Transform>();

            // mapa: marcas vermelhas em cima de cada estrutura
            var terr = Terrain.activeTerrain;
            var matMarca = new Material(Shader.Find("Universal Render Pipeline/Unlit")); matMarca.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f));
            foreach (var t in todas.Select(e => e.transform).Concat(vagoes))
            {
                var mk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(mk.GetComponent<Collider>());
                mk.transform.position = BoundsDe(t).center + Vector3.up * 30f;
                mk.transform.localScale = new Vector3(18f, 1f, 18f);
                mk.GetComponent<Renderer>().sharedMaterial = matMarca;
                marcas.Add(mk);
            }
            var ts = terr.terrainData.size;
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(ts.x, ts.z) * 0.5f;
            cam.aspect = 1f;
            camGo.transform.SetPositionAndRotation(terr.transform.position + new Vector3(ts.x / 2f, 600f, ts.z / 2f), Quaternion.Euler(90f, 0f, 0f));
            Foto("estruturas_mapa.png");
            foreach (var mk in marcas) Object.DestroyImmediate(mk);
            marcas.Clear();
            cam.ResetAspect();

            int i = 0;
            foreach (var e in todas)
            {
                i++;
                string pre = $"estrutura_{i}_{new string(e.name.Where(char.IsLetterOrDigit).ToArray())}";
                var b = BoundsDe(e.transform);
                float pisoY = e.pisos[0].bounds.max.y;
                // de fora, olhando para a entrada
                var ent = e.transform.Find("Entrada");
                Vector3 frente = ent != null ? NoChao(ent.position - b.center).normalized : Vector3.forward;
                cam.orthographic = false; cam.fieldOfView = 50f;
                camGo.transform.position = b.center + frente * (b.extents.magnitude * 1.6f) + Vector3.up * b.extents.magnitude * 0.6f;
                camGo.transform.LookAt(b.center);
                Foto(pre + "_fora.png");

                // de cima, sem telhado nem teto (como a isométrica dentro)
                foreach (var r in e.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.bounds.center.y > pisoY + 2.3f) { r.enabled = false; desligadas.Add(r); }
                cam.orthographic = true; cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.z) * 1.05f;
                camGo.transform.SetPositionAndRotation(new Vector3(b.center.x, pisoY + 40f, b.center.z), Quaternion.LookRotation(Vector3.down, -frente));
                Foto(pre + "_planta.png");
                // perspectiva de cima (parecida com o jogo)
                cam.orthographic = false; cam.fieldOfView = 40f;
                camGo.transform.position = new Vector3(b.center.x, pisoY, b.center.z) + frente * Mathf.Max(b.extents.x, b.extents.z) * 0.9f + Vector3.up * Mathf.Max(b.extents.x, b.extents.z) * 2.1f;
                camGo.transform.LookAt(new Vector3(b.center.x, pisoY, b.center.z));
                Foto(pre + "_dentro.png");
                foreach (var r in desligadas) r.enabled = true;
                desligadas.Clear();
            }
            foreach (var v in vagoes)
            {
                var b = BoundsDe(v);
                cam.orthographic = false; cam.fieldOfView = 50f;
                camGo.transform.position = b.center + new Vector3(1f, 0.7f, 1f).normalized * b.extents.magnitude * 2.2f;
                camGo.transform.LookAt(b.center);
                Foto($"vagao_{new string(v.name.Where(char.IsDigit).ToArray())}.png");
            }
        }
        finally
        {
            foreach (var r in desligadas) if (r != null) r.enabled = true;
            foreach (var mk in marcas) if (mk != null) Object.DestroyImmediate(mk);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(luzGo);
        }
        // só coisas temporárias foram criadas (e já removidas)
        EditorSceneManager.SaveScene(scene);
    }
}
