using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Ciclo do GDD na CenaPrincipal: carvão da locomotiva (abastecer no tênder), montes de carvão
/// ao longo da ferrovia, itens do chão viram coletáveis e documentos dentro da casa.
/// Pode rodar de novo: reaproveita o que já existe (pelos nomes) e não mexe no resto.
/// </summary>
public static class MundoBuilder
{
    private const string MatCarvao = "Assets/Scenes/Prologo/Carvao.mat";
    private const string MatPapel = "Assets/Scenes/Prologo/Papel.mat";

    public static void Preparar(StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(IsometricoBuilder.ScenePath, OpenSceneMode.Single);
        var trem = Object.FindFirstObjectByType<TrainInteraction>();

        Tender(trem, log);
        Montes(trem, log);
        ItensDoChao(log);
        Documentos(log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva: " + IsometricoBuilder.ScenePath);
    }

    // ------------------------------------------------------------ tênder

    private static void Tender(TrainInteraction trem, StringBuilder log)
    {
        if (trem == null) { log.AppendLine("AVISO: sem trem na cena"); return; }
        var t = trem.transform.Find("Tender (carvao)");
        if (t == null)
        {
            t = new GameObject("Tender (carvao)").transform;
            t.SetParent(trem.transform, false);
        }
        // lado esquerdo da cabine (o direito é por onde se sobe)
        var cabine = trem.trainSeat != null ? trem.trainSeat.position : trem.transform.position;
        var saida = trem.exitPoint != null ? trem.exitPoint.position : cabine + trem.transform.forward * 2f;
        Vector3 ladoSaida = saida - cabine; ladoSaida.y = 0f;
        t.position = new Vector3(cabine.x, trem.transform.position.y + 1.2f, cabine.z) - ladoSaida.normalized * 1.4f;
        t.rotation = trem.transform.rotation;
        // a raiz do trem vem do .fbx com escala minúscula: compensa para o colisor ter o tamanho em metros
        var ls = trem.transform.lossyScale;
        t.localScale = new Vector3(1f / Mathf.Max(1e-5f, ls.x), 1f / Mathf.Max(1e-5f, ls.y), 1f / Mathf.Max(1e-5f, ls.z));
        var bc = t.GetComponent<BoxCollider>();
        if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(1.2f, 1.8f, 1.6f);
        var c = t.GetComponent<CombustivelTrem>();
        if (c == null) c = t.gameObject.AddComponent<CombustivelTrem>();
        var so = new SerializedObject(c);
        so.FindProperty("acao").stringValue = "CARVÃO DO TREM";
        so.FindProperty("somCarvao").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AllSounds/PapelFechando.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"tênder (abastecer) em {t.position:F1}");
    }

    // ------------------------------------------------------------ montes de carvão

    private static void Montes(TrainInteraction trem, StringBuilder log)
    {
        var anim = trem != null ? trem.GetComponent<SplineAnimate>() : null;
        var container = anim != null ? anim.Container : Object.FindFirstObjectByType<SplineContainer>();
        if (container == null) { log.AppendLine("AVISO: sem spline para os montes de carvão"); return; }

        var grupo = GameObject.Find("Montes de Carvao");
        if (grupo == null) grupo = new GameObject("Montes de Carvao");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatCarvao);
        var terreno = Terrain.activeTerrain;
        var spawner = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
        var zonas = spawner != null ? (spawner.exclusionZones ?? new Collider[0]).Where(z => z != null).ToList() : new List<Collider>();
        var rnd = new System.Random(77);

        float[] ts = { 0.04f, 0.27f, 0.52f, 0.78f };
        for (int i = 0; i < ts.Length; i++)
        {
            container.Evaluate(ts[i], out float3 p, out float3 tan, out float3 up);
            Vector3 pos = p, dir = ((Vector3)tan).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, dir).normalized * (i % 2 == 0 ? 1f : -1f);
            Vector3 onde = pos + lado * 4.5f;
            if (terreno != null) onde.y = terreno.SampleHeight(onde) + terreno.transform.position.y;
            CriarMonte(grupo.transform, $"Monte de Carvao {i + 1}", onde, $"mundo.carvao{i + 1}", 5, mat, rnd, zonas);
            log.AppendLine($"Monte de Carvao {i + 1} em {onde:F0} (t={ts[i]:0.00}, 5 pedras)");
        }
        MontesNosVagoes(grupo.transform, mat, rnd, zonas, log);
        if (spawner != null) { spawner.exclusionZones = zonas.Where(z => z != null).Distinct().ToArray(); EditorUtility.SetDirty(spawner); }
    }

    /// <summary>Os vagões tombados derramaram carvão: um monte menor ao lado de cada um.</summary>
    public static void MontesNosVagoes(StringBuilder log)
    {
        var grupo = GameObject.Find("Montes de Carvao");
        if (grupo == null) grupo = new GameObject("Montes de Carvao");
        var spawner = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
        var zonas = spawner != null ? (spawner.exclusionZones ?? new Collider[0]).Where(z => z != null).ToList() : new List<Collider>();
        MontesNosVagoes(grupo.transform, AssetDatabase.LoadAssetAtPath<Material>(MatCarvao), new System.Random(91), zonas, log);
        if (spawner != null) { spawner.exclusionZones = zonas.Where(z => z != null).Distinct().ToArray(); EditorUtility.SetDirty(spawner); }
    }

    private static void MontesNosVagoes(Transform grupo, Material mat, System.Random rnd, List<Collider> zonas, StringBuilder log)
    {
        var estruturas = GameObject.Find("Estruturas");
        if (estruturas == null) { log.AppendLine("sem estruturas: nenhum monte nos vagões"); return; }
        var terreno = Terrain.activeTerrain;
        Physics.SyncTransforms();
        int n = 0;
        foreach (Transform vagao in estruturas.transform)
        {
            if (!vagao.name.StartsWith("Vagao")) continue;
            n++;
            string nome = $"Monte de Carvao (Vagao {n})";
            var antigo = grupo.Find(nome);
            if (antigo != null) Object.DestroyImmediate(antigo.gameObject);
            Physics.SyncTransforms();

            var rs = vagao.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<Interagivel>() == null).ToArray();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            // carvão saindo pelas pontas e pelos lados do vagão; o primeiro lugar livre ganha
            Vector3 comprido = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            Vector3 largo = Vector3.Cross(Vector3.up, comprido);
            float meioComp = Vector3.Dot(b.extents, comprido), meioLarg = Vector3.Dot(b.extents, largo);
            var lugares = new[] { comprido * (meioComp + 2f), -comprido * (meioComp + 2f), largo * (meioLarg + 2f), -largo * (meioLarg + 2f) };
            Vector3? escolhido = null;
            foreach (var l in lugares)
            {
                var onde = new Vector3(b.center.x, 0f, b.center.z) + l;
                if (terreno != null) onde.y = terreno.SampleHeight(onde) + terreno.transform.position.y;
                bool livre = !Physics.OverlapBox(onde + Vector3.up * 0.7f, new Vector3(1.1f, 0.6f, 1.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !(c is TerrainCollider));
                if (livre) { escolhido = onde; break; }
            }
            if (escolhido == null) { log.AppendLine($"{vagao.name}: sem lugar livre para o carvão"); continue; }
            CriarMonte(grupo, nome, escolhido.Value, $"mundo.carvao_vagao{n}", 4, mat, rnd, zonas);
            log.AppendLine($"{nome} em {escolhido.Value:F0} (4 pedras)");
        }
    }

    private static void CriarMonte(Transform grupo, string nome, Vector3 onde, string flag, int pedras, Material mat, System.Random rnd, List<Collider> zonas)
    {
        var existente = grupo.Find(nome);
        if (existente != null) Object.DestroyImmediate(existente.gameObject);

        var monte = new GameObject(nome).transform;
        monte.SetParent(grupo, false);
        monte.position = onde;
        for (int k = 0; k < 8; k++)
        {
            var lump = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lump.GetComponent<Collider>());
            lump.name = "Pedra";
            lump.transform.SetParent(monte, false);
            float s = 0.35f + (float)rnd.NextDouble() * 0.45f;
            lump.transform.localScale = Vector3.one * s;
            lump.transform.localPosition = new Vector3(((float)rnd.NextDouble() - 0.5f) * 1.4f, s * 0.35f + (float)rnd.NextDouble() * 0.2f, ((float)rnd.NextDouble() - 0.5f) * 1.4f);
            lump.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
            if (mat != null) lump.GetComponent<Renderer>().sharedMaterial = mat;
        }
        var mc = monte.gameObject.AddComponent<MonteDeCarvao>();
        var so = new SerializedObject(mc);
        so.FindProperty("acao").stringValue = "PEGAR CARVÃO";
        so.FindProperty("flagEncerra").stringValue = "";
        so.FindProperty("estoque").intValue = pedras;
        so.FindProperty("flagMonte").stringValue = flag;
        so.FindProperty("flagPegouAlgum").stringValue = "mundo.carvao_pegou";
        so.FindProperty("falaPrimeira").stringValue = "Carvão! É disso que a velha precisa. Uma pedra de cada vez até o trem.";
        so.FindProperty("avisarPerto").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        // gatilho para a mira/proximidade e zona sem árvores
        var col = new GameObject("Colisor do Monte");
        col.transform.SetParent(monte, false);
        col.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        var bc = col.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.size = new Vector3(1.8f, 1f, 1.8f);
        var zona = new GameObject("Zona sem Arvores");
        zona.transform.SetParent(monte, false);
        var zb = zona.AddComponent<BoxCollider>();
        zb.isTrigger = true; zb.size = new Vector3(6f, 4f, 6f);
        zonas.Add(zb);
    }

    // ------------------------------------------------------------ itens do chão

    private static readonly Dictionary<string, (string id, int qtd, string acao, string fala)> KitSobrevivencia = new Dictionary<string, (string, int, string, string)>
    {
        { "Water", ("agua", 1, "PEGAR GARRAFA D'ÁGUA", "Água. [G] pra beber.") },
        { "Can_Soda", ("soda", 1, "PEGAR SODA", "Uma soda. Isso dá um gás.") },
        { "Can", ("lata_feijao", 1, "PEGAR LATA", "Comida em lata. [F] pra comer.") },
        { "match", ("fosforos", 1, "PEGAR FÓSFOROS", "Fósforos. Vão servir pra uma fogueira.") },
        { "Ammunition", ("municao", 8, "PEGAR MUNIÇÃO", "Munição... alguém se preparou pra algo.") },
        { "Bullet", ("municao", 1, "PEGAR BALA", "") },
        { "branch", ("galho", 1, "PEGAR GALHO", "Galhos secos. Bom pra fogueira.") },
        { "branch_01", ("galho", 1, "PEGAR GALHO", "") },
    };

    private static void ItensDoChao(StringBuilder log)
    {
        int n = 0;
        var kit = GameObject.Find("Survival");
        if (kit != null)
            foreach (Transform t in kit.transform)
                if (KitSobrevivencia.TryGetValue(t.name, out var d))
                {
                    if (TornarColetavel(t.gameObject, d.id, d.qtd, $"mundo.item.{t.name}", d.acao, d.fala)) n++;
                }
        log.AppendLine($"kit de sobrevivência: {n} itens pegáveis");

        int latas = 0;
        var comida = GameObject.Find("Comidaaa");
        if (comida != null)
        {
            // a primeira lata tem a malha solta ao lado (Can/Label irmãos): junta para sumirem juntas
            var primeira = comida.transform.Find("Soup Can");
            if (primeira != null && primeira.childCount == 0)
                foreach (var nome in new[] { "Can", "Label" })
                {
                    var solto = comida.transform.Find(nome);
                    if (solto == null) continue;
                    try { solto.SetParent(primeira, true); }
                    catch (System.Exception e) { log.AppendLine($"AVISO: não deu para juntar {nome} na lata: {e.Message}"); }
                }
            int i = 0;
            foreach (Transform t in comida.transform)
            {
                if (!t.name.StartsWith("Soup Can")) continue;
                i++;
                if (TornarColetavel(t.gameObject, "lata_feijao", 1, $"mundo.lata{i}", "PEGAR LATA DE COMIDA", latas == 0 ? "Comida em lata. Ainda fechada." : "")) latas++;
            }
        }
        log.AppendLine($"latas de comida pegáveis: {latas}");
    }

    public static bool TornarColetavel(GameObject go, string id, int qtd, string flag, string acao, string fala)
    {
        var item = go.GetComponent<ItemColetavel>();
        if (item == null) item = go.AddComponent<ItemColetavel>();
        if (go.GetComponent<Collider>() == null)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                // tamanho mínimo para dar para mirar/chegar perto
                var tam = go.transform.InverseTransformVector(r.bounds.size);
                tam = new Vector3(Mathf.Max(Mathf.Abs(tam.x), 0.25f / Mathf.Max(0.01f, go.transform.lossyScale.x)),
                                  Mathf.Max(Mathf.Abs(tam.y), 0.25f / Mathf.Max(0.01f, go.transform.lossyScale.y)),
                                  Mathf.Max(Mathf.Abs(tam.z), 0.25f / Mathf.Max(0.01f, go.transform.lossyScale.z)));
                bc.center = go.transform.InverseTransformPoint(r.bounds.center);
                bc.size = tam;
            }
        }
        var so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = id;
        so.FindProperty("quantidade").intValue = qtd;
        so.FindProperty("flag").stringValue = flag;
        so.FindProperty("acao").stringValue = acao;
        so.FindProperty("fala").stringValue = fala;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // ------------------------------------------------------------ documentos

    private static void Documentos(StringBuilder log)
    {
        var casa = Object.FindObjectsByType<Estrutura>(FindObjectsSortMode.None).FirstOrDefault(e => e.name == "Dom");
        if (casa == null) { log.AppendLine("AVISO: sem casa (Estrutura) para os documentos"); return; }
        var papel = AssetDatabase.LoadAssetAtPath<Material>(MatPapel);

        Documento(casa.transform, papel, "Documento Stephenson", new Vector3(-4.6f, 0.42f, -2.9f), 20f,
            "Almanaque Ferroviário", "Edição comemorativa, 1998",
            "George Stephenson, o pai das ferrovias",
            "George Stephenson nasceu em 1781, em Wylam, na Inglaterra, filho de um mineiro. Só aprendeu a ler aos 18 anos, estudando à noite.\n\n" +
            "Começou cuidando de máquinas nas minas de carvão e passou a construir locomotivas a vapor. Em 1825, a sua Locomotion nº 1 puxou o primeiro trem público a vapor, na ferrovia Stockton–Darlington.\n\n" +
            "Em 1829, a locomotiva Rocket, feita com o filho Robert, venceu as provas de Rainhill. A bitola que ele usou, 1.435 mm, ainda é a mais comum do mundo.",
            "mundo.doc.stephenson1",
            "Stephenson... eu conheço essa história. Eu lia isso na estação, nos dias de chuva.");

        Documento(casa.transform, papel, "Folheto Sintomas", new Vector3(-0.9f, 0.42f, -2.6f), -35f,
            "Secretaria de Saúde", "Maio de 2020",
            "Vírus Violeta: conheça os sintomas",
            "• Dedo mínimo arroxeado (o primeiro sinal)\n• Tremores nas mãos\n• Febre baixa e cansaço\n• Confusão e perda de memória\n\n" +
            "Ao notar qualquer sintoma, procure o posto de saúde mais próximo. Não se automedique. Evite aglomerações.",
            "mundo.doc.sintomas",
            "Perda de memória... olhei pras minhas mãos sem querer.");
    }

    private static void Documento(Transform casa, Material papel, string nome, Vector3 local, float giro,
        string jornal, string data, string manchete, string corpo, string flag, string fala)
    {
        var velho = casa.Find(nome);
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var go = new GameObject(nome);
        go.transform.SetParent(casa, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.size = new Vector3(0.5f, 0.15f, 0.4f);
        var folha = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(folha.GetComponent<Collider>());
        folha.name = "Folha";
        folha.transform.SetParent(go.transform, false);
        folha.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        folha.transform.localScale = new Vector3(0.3f, 0.01f, 0.4f);
        if (papel != null) folha.GetComponent<Renderer>().sharedMaterial = papel;
        var j = go.AddComponent<Jornal>();
        var so = new SerializedObject(j);
        so.FindProperty("acao").stringValue = "LER";
        so.FindProperty("nomeJornal").stringValue = jornal;
        so.FindProperty("data").stringValue = data;
        so.FindProperty("manchete").stringValue = manchete;
        so.FindProperty("corpo").stringValue = corpo;
        so.FindProperty("flag").stringValue = flag;
        so.FindProperty("falaDepois").stringValue = fala;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
