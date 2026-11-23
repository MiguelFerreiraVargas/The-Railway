using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Interiores das estruturas: a casa velha de dois andares (paredes em pedaços por andar e lado,
/// escada e piso de cima com vão), cômodos trancados nas casas comuns e móveis que dá para vasculhar.
/// </summary>
public static partial class EstruturasBuilder
{
    // ================================================================== casa velha: dois andares de verdade

    // alturas no espaço do modelo (House.fbx)
    private const float AndarDeCima = 3.07f;     // topo do piso do 2º andar
    private const float Sotao = 5.55f;           // piso do sótão (acima disso é empena/telhado)
    private const float EspessuraPiso = 0.12f;

    /// <summary>
    /// Corta as malhas que pegam a casa inteira (paredes, janelas, vigas) em pedaços por andar e por lado,
    /// troca o piso solto do 2º andar por um piso com vão e põe a escada. Devolve os pisos (térreo primeiro).
    /// </summary>
    private static List<Collider> CasaVelhaPorDentro(Transform casa, Collider terreo)
    {
        var pisos = new List<Collider> { terreo };
        var paredes = new GameObject("Paredes").transform;
        paredes.SetParent(casa, false);

        var panels = casa.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Panels");
        // caixa das paredes no espaço da casa: define os quatro lados
        Bounds caixa = panels != null ? NoLocal(casa, panels.bounds) : new Bounds(Vector3.zero, new Vector3(12f, 9f, 9f));

        int pedacos = 0;
        foreach (var nome in new[] { "Panels", "Plane.002", "Cube.006" })
        {
            var r = casa.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.name == nome);
            if (r == null) continue;
            pedacos += Fatiar(r, casa, paredes, caixa, new[] { AndarDeCima - 0.03f, Sotao });
            Object.DestroyImmediate(r.gameObject);
        }

        // piso do 2º andar (o do modelo é um plano só, sem lugar para a escada)
        Material matPiso = null;
        foreach (var nome in new[] { "Floor.001", "Floor.004" })
        {
            var r = casa.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.name == nome);
            if (r == null) continue;
            if (matPiso == null) matPiso = r.sharedMaterial;
            Object.DestroyImmediate(r.gameObject);
        }
        var matDegrau = casa.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Cube.008")).Select(r => r.sharedMaterial).FirstOrDefault() ?? matPiso;

        // a escada sobe junto da parede dos fundos (z negativo), de leste (x+) para oeste
        const float xBaixo = 5.3f, xAlto = 1.1f, zMin = -4.05f, zMax = -3.05f;
        float topo = AndarDeCima, baixo = AndarDeCima - EspessuraPiso;
        // piso A: do lado oeste até o fim da escada; piso B: o resto, na frente do vão
        pisos.Add(CaixaSolida("Piso de Cima A", casa, new Vector3(-6.04f, baixo, -4.38f), new Vector3(xAlto, topo, 4.38f), matPiso, true));
        pisos.Add(CaixaSolida("Piso de Cima B", casa, new Vector3(xAlto, baixo, zMax + 0.1f), new Vector3(6.04f, topo, 4.38f), matPiso, true));
        // corrimão em volta do vão (não deixa cair lá de cima)
        CaixaSolida("Corrimao", casa, new Vector3(xAlto + 0.4f, topo, zMax + 0.05f), new Vector3(6.0f, topo + 0.95f, zMax + 0.12f), matDegrau, true);

        // degraus (só visual) e a rampa (colisor liso por cima deles)
        int n = 15;
        float alt = AndarDeCima / n, passo = (xBaixo - xAlto) / n;
        for (int i = 0; i < n; i++)
        {
            float x1 = xBaixo - i * passo, x0 = x1 - passo;
            CaixaSolida($"Degrau {i + 1}", casa, new Vector3(x0, 0f, zMin), new Vector3(x1, (i + 1) * alt, zMax), matDegrau, false);
        }
        var rampa = new GameObject("Rampa da Escada");
        rampa.transform.SetParent(casa, false);
        float comp = Mathf.Sqrt((xBaixo - xAlto) * (xBaixo - xAlto) + AndarDeCima * AndarDeCima);
        float ang = Mathf.Atan2(AndarDeCima, xBaixo - xAlto) * Mathf.Rad2Deg;
        var normal = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad), 0f);
        rampa.transform.localPosition = new Vector3((xBaixo + xAlto) / 2f, AndarDeCima / 2f, (zMin + zMax) / 2f) - normal * 0.05f;
        rampa.transform.localRotation = Quaternion.Euler(0f, 0f, -ang);
        var bc = rampa.AddComponent<BoxCollider>();
        bc.size = new Vector3(comp + 0.1f, 0.1f, zMax - zMin);
        pisos.Add(bc);

        log.AppendLine($"{casa.name}: paredes em {pedacos} pedaços, escada de {n} degraus ({ang:0}°), piso de cima com vão");
        return pisos;
    }

    private static Bounds NoLocal(Transform t, Bounds b)
    {
        var r = new Bounds(t.InverseTransformPoint(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            r.Encapsulate(t.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return r;
    }

    // caixa sólida entre dois cantos (no espaço da casa)
    private static Collider CaixaSolida(string nome, Transform casa, Vector3 de, Vector3 ate, Material mat, bool colisor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nome;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(casa, false);
        go.transform.localPosition = (de + ate) / 2f;
        go.transform.localScale = new Vector3(Mathf.Abs(ate.x - de.x), Mathf.Abs(ate.y - de.y), Mathf.Abs(ate.z - de.z));
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return colisor ? go.AddComponent<BoxCollider>() : null;
    }

    private struct Vert { public Vector3 p, n; public Vector2 uv; }

    private class Pedaco
    {
        public readonly List<Vector3> p = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> tri = new List<int>();
    }

    /// <summary>Corta a malha em faixas de altura (cortes no espaço da casa) e em quatro lados. Devolve quantos pedaços criou.</summary>
    private static int Fatiar(MeshRenderer r, Transform casa, Transform pai, Bounds caixa, float[] cortes)
    {
        var mf = r.GetComponent<MeshFilter>();
        var m = mf != null ? mf.sharedMesh : null;
        if (m == null) return 0;
        var paraCasa = casa.worldToLocalMatrix * r.transform.localToWorldMatrix;
        var vs = m.vertices; var ns = m.normals; var uvs = m.uv;
        var tris = m.triangles;
        var verts = new Vert[vs.Length];
        for (int i = 0; i < vs.Length; i++)
            verts[i] = new Vert
            {
                p = paraCasa.MultiplyPoint3x4(vs[i]),
                n = ns.Length == vs.Length ? paraCasa.MultiplyVector(ns[i]).normalized : Vector3.up,
                uv = uvs.Length == vs.Length ? uvs[i] : Vector2.zero,
            };

        var faixas = new List<(float de, float ate)>();
        float ant = float.NegativeInfinity;
        foreach (var c in cortes) { faixas.Add((ant, c)); ant = c; }
        faixas.Add((ant, float.PositiveInfinity));

        var pedacos = new Dictionary<(int faixa, int lado), Pedaco>();
        var poli = new List<Vert>(); var tmp = new List<Vert>();
        for (int t = 0; t < tris.Length; t += 3)
            for (int f = 0; f < faixas.Count; f++)
            {
                poli.Clear(); poli.Add(verts[tris[t]]); poli.Add(verts[tris[t + 1]]); poli.Add(verts[tris[t + 2]]);
                if (!float.IsInfinity(faixas[f].de)) { Recortar(poli, faixas[f].de, false, tmp); poli.Clear(); poli.AddRange(tmp); }
                if (poli.Count >= 3 && !float.IsInfinity(faixas[f].ate)) { Recortar(poli, faixas[f].ate, true, tmp); poli.Clear(); poli.AddRange(tmp); }
                if (poli.Count < 3) continue;
                Vector3 c = Vector3.zero; foreach (var v in poli) c += v.p; c /= poli.Count;
                int lado = Lado(c, caixa);
                if (!pedacos.TryGetValue((f, lado), out var pd)) pedacos[(f, lado)] = pd = new Pedaco();
                int baseI = pd.p.Count;
                foreach (var v in poli) { pd.p.Add(v.p); pd.n.Add(v.n); pd.uv.Add(v.uv); }
                for (int k = 1; k < poli.Count - 1; k++) { pd.tri.Add(baseI); pd.tri.Add(baseI + k); pd.tri.Add(baseI + k + 1); }
            }

        string[] nomesLado = { "Leste", "Oeste", "Frente", "Fundos" };
        string[] nomesFaixa = { "terreo", "andar de cima", "empena" };
        foreach (var kv in pedacos)
        {
            var pd = kv.Value;
            var malha = new Mesh { name = $"{r.name} {nomesFaixa[Mathf.Min(kv.Key.faixa, 2)]} {nomesLado[kv.Key.lado]}" };
            if (pd.p.Count > 65000) malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            malha.SetVertices(pd.p); malha.SetNormals(pd.n); malha.SetUVs(0, pd.uv); malha.SetTriangles(pd.tri, 0);
            malha.RecalculateBounds(); malha.RecalculateTangents();
            var go = new GameObject(malha.name);
            go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = r.sharedMaterials;
            go.AddComponent<MeshCollider>().sharedMesh = malha;
        }
        return pedacos.Count;
    }

    // 0 leste (+x), 1 oeste (-x), 2 frente (+z), 3 fundos (-z)
    private static int Lado(Vector3 c, Bounds caixa)
    {
        float dx = (c.x - caixa.center.x) / Mathf.Max(0.01f, caixa.extents.x);
        float dz = (c.z - caixa.center.z) / Mathf.Max(0.01f, caixa.extents.z);
        if (Mathf.Abs(dx) >= Mathf.Abs(dz)) return dx >= 0f ? 0 : 1;
        return dz >= 0f ? 2 : 3;
    }

    // Sutherland–Hodgman contra o plano y = corte (fica embaixo ou em cima)
    private static void Recortar(List<Vert> entra, float corte, bool ficaEmbaixo, List<Vert> sai)
    {
        sai.Clear();
        int n = entra.Count;
        for (int i = 0; i < n; i++)
        {
            var atual = entra[i]; var antes = entra[(i + n - 1) % n];
            bool aDentro = ficaEmbaixo ? atual.p.y <= corte : atual.p.y >= corte;
            bool bDentro = ficaEmbaixo ? antes.p.y <= corte : antes.p.y >= corte;
            if (aDentro)
            {
                if (!bDentro) sai.Add(Cruzar(antes, atual, corte));
                sai.Add(atual);
            }
            else if (bDentro) sai.Add(Cruzar(antes, atual, corte));
        }
    }

    private static Vert Cruzar(Vert a, Vert b, float y)
    {
        float t = Mathf.Abs(b.p.y - a.p.y) < 1e-6f ? 0.5f : (y - a.p.y) / (b.p.y - a.p.y);
        return new Vert { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t) };
    }

    // ================================================================== cômodos trancados

    /// <summary>
    /// Acha os cômodos da casa (pedaços de piso separados por paredes e portas) e tranca um que só
    /// tem uma porta. O cômodo ganha uma cobertura escura: na isométrica não dá para ver dentro.
    /// </summary>
    private static void TrancarComodo(Transform casa, string nome, Random rnd)
    {
        var est = casa.GetComponent<Estrutura>();
        if (est == null || est.pisos == null || est.pisos.Length == 0) return;
        var piso = est.pisos[0];
        float pisoY = piso.bounds.max.y;
        const float lado = 0.3f;
        var pb = piso.bounds;
        int nx = Mathf.CeilToInt(pb.size.x / lado), nz = Mathf.CeilToInt(pb.size.z / lado);
        var dentro = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
                dentro[i, k] = DentroDe(casa, est, Celula(pb, i, k, lado, pisoY));

        // regiões ligadas sem atravessar nada sólido (paredes e portas fechadas) na altura do peito
        var regiao = new int[nx, nz];
        for (int i = 0; i < nx; i++) for (int k = 0; k < nz; k++) regiao[i, k] = -1;
        var tamanhos = new List<int>();
        int[] di = { 1, -1, 0, 0 }, dk = { 0, 0, 1, -1 };
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                if (!dentro[i, k] || regiao[i, k] >= 0) continue;
                int id = tamanhos.Count; int conta = 0;
                var fila = new Queue<(int, int)>(); fila.Enqueue((i, k)); regiao[i, k] = id;
                while (fila.Count > 0)
                {
                    var (a, b) = fila.Dequeue(); conta++;
                    var pa = Celula(pb, a, b, lado, pisoY) + Vector3.up * 1.2f;
                    for (int d = 0; d < 4; d++)
                    {
                        int a2 = a + di[d], b2 = b + dk[d];
                        if (a2 < 0 || b2 < 0 || a2 >= nx || b2 >= nz || !dentro[a2, b2] || regiao[a2, b2] >= 0) continue;
                        var pb2 = Celula(pb, a2, b2, lado, pisoY) + Vector3.up * 1.2f;
                        if (Physics.Linecast(pa, pb2, out _, ~0, QueryTriggerInteraction.Ignore)) continue;
                        regiao[a2, b2] = id; fila.Enqueue((a2, b2));
                    }
                }
                tamanhos.Add(conta);
            }

        int RegiaoEm(Vector3 p)
        {
            int i = Mathf.FloorToInt((p.x - pb.min.x) / lado), k = Mathf.FloorToInt((p.z - pb.min.z) / lado);
            for (int raio = 0; raio <= 1; raio++)
                for (int a = i - raio; a <= i + raio; a++)
                    for (int b = k - raio; b <= k + raio; b++)
                        if (a >= 0 && b >= 0 && a < nx && b < nz && regiao[a, b] >= 0) return regiao[a, b];
            return -1;
        }

        // quais regiões cada porta liga
        var portasDe = new Dictionary<int, List<PortaAutomatica>>();
        var ligacao = new Dictionary<PortaAutomatica, (int, int)>();
        foreach (var porta in casa.GetComponentsInChildren<PortaAutomatica>())
        {
            var b = porta.GetComponent<Renderer>().bounds;
            // a casa pode estar girada: a espessura da folha é medida no espaço da casa
            var bl = NoLocal(casa, b);
            Vector3 normal = casa.TransformDirection(bl.size.x < bl.size.z ? Vector3.right : Vector3.forward); // atravessa a folha
            int r1 = RegiaoEm(b.center + normal * 0.55f), r2 = RegiaoEm(b.center - normal * 0.55f);
            ligacao[porta] = (r1, r2);
            foreach (var rg in new[] { r1, r2 })
            {
                if (rg < 0) continue;
                if (!portasDe.TryGetValue(rg, out var l)) portasDe[rg] = l = new List<PortaAutomatica>();
                if (!l.Contains(porta)) l.Add(porta);
            }
        }

        var den = casa.Find("Dentro");
        int entrada = den != null ? RegiaoEm(den.position) : -1;
        var candidatos = new List<int>();
        for (int rg = 0; rg < tamanhos.Count; rg++)
        {
            float area = tamanhos[rg] * lado * lado;
            if (rg == entrada || area < 3f || area > 30f) continue;
            if (!portasDe.TryGetValue(rg, out var ps) || ps.Count != 1) continue;
            var (a, b) = ligacao[ps[0]];
            if (a < 0 || b < 0 || a == b) continue; // porta para fora ou que não separa nada
            candidatos.Add(rg);
        }
        if (candidatos.Count == 0) { log.AppendLine($"{nome}: nenhum cômodo com uma porta só para trancar ({tamanhos.Count} regiões)"); return; }

        int escolhido = candidatos[rnd.Next(candidatos.Count)];
        var portaTrancada = portasDe[escolhido][0];

        // cobertura: um quadrado por célula, logo abaixo do teto, um pouco maior para tampar a beira das paredes
        var ps2 = new List<Vector3>(); var tri = new List<int>();
        float yCob = pisoY + 2.15f, meio = lado * 0.5f + 0.12f;
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                if (regiao[i, k] != escolhido) continue;
                var c = casa.InverseTransformPoint(Celula(pb, i, k, lado, yCob));
                int b0 = ps2.Count;
                ps2.Add(c + new Vector3(-meio, 0f, -meio)); ps2.Add(c + new Vector3(-meio, 0f, meio));
                ps2.Add(c + new Vector3(meio, 0f, meio)); ps2.Add(c + new Vector3(meio, 0f, -meio));
                tri.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
            }
        var malha = new Mesh { name = $"Breu {nome}" };
        malha.SetVertices(ps2); malha.SetTriangles(tri, 0); malha.RecalculateNormals(); malha.RecalculateBounds();
        var cob = new GameObject("Breu do Comodo Trancado");
        cob.transform.SetParent(casa, false);
        cob.AddComponent<MeshFilter>().sharedMesh = malha;
        var mr = cob.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MatBreu();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var tr = portaTrancada.gameObject.AddComponent<PortaTrancada>();
        tr.porta = portaTrancada;
        tr.cobertura = cob;
        string chave = new string(nome.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        tr.flag = $"estrutura.{chave}.porta_trancada";
        var so = new SerializedObject(tr);
        so.FindProperty("acao").stringValue = "FORÇAR A PORTA";
        so.ApplyModifiedPropertiesWithoutUndo();
        portaTrancada.trancada = true;
        log.AppendLine($"{nome}: cômodo trancado ({tamanhos[escolhido] * lado * lado:0.0} m², porta '{portaTrancada.name}'), {tamanhos.Count} regiões");
    }

    private static Vector3 Celula(Bounds pb, int i, int k, float lado, float y) => new Vector3(pb.min.x + (i + 0.5f) * lado, y, pb.min.z + (k + 0.5f) * lado);

    private static Material breu;
    private static Material MatBreu()
    {
        if (breu != null) return breu;
        const string caminho = PastaMat + "/Breu do Comodo.mat";
        breu = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (breu != null) return breu;
        breu = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        breu.SetColor("_BaseColor", new Color(0.015f, 0.014f, 0.013f));
        AssetDatabase.CreateAsset(breu, caminho);
        return breu;
    }

    // ================================================================== vasculhar

    private static int recipientes;

    private static void FazerRecipiente(GameObject go, Recipiente.Tipo tipo, string chaveEstrutura, string chaveMovel, int minimo, int maximo)
    {
        if (go.GetComponent<Collider>() == null && go.GetComponentInChildren<Collider>() == null) return;
        var rc = go.GetComponent<Recipiente>();
        if (rc == null) rc = go.AddComponent<Recipiente>();
        rc.tipo = tipo;
        rc.flag = $"estrutura.{chaveEstrutura}.{chaveMovel}";
        rc.semente = rc.flag.Aggregate(17, (h, c) => h * 31 + c);
        rc.minimo = minimo; rc.maximo = maximo;
        var so = new SerializedObject(rc);
        so.FindProperty("acao").stringValue = "VASCULHAR";
        so.ApplyModifiedPropertiesWithoutUndo();
        recipientes++;
    }

    /// <summary>Prateleiras, estoque, geladeiras e armários do mercadinho.</summary>
    private static void RecipientesDoMercadinho(Transform loja)
    {
        int antes = recipientes;
        foreach (var r in loja.GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.name;
            if (n.StartsWith("estanteCaja")) FazerRecipiente(r.gameObject, Recipiente.Tipo.Estoque, "mercadinho", n, 1, 3);
            else if (n.StartsWith("estante") || n.StartsWith("exhibidor")) FazerRecipiente(r.gameObject, Recipiente.Tipo.PrateleiraLoja, "mercadinho", n, 0, 2);
            else if (n.StartsWith("heladera") || n.StartsWith("refresquera") || n == "Plane" || n.StartsWith("freezerPizza")) FazerRecipiente(r.gameObject, Recipiente.Tipo.Geladeira, "mercadinho", n, 0, 2);
            else if (n.StartsWith("alacena")) FazerRecipiente(r.gameObject, Recipiente.Tipo.Armario, "mercadinho", n, 0, 2);
        }
        log.AppendLine($"{loja.name}: {recipientes - antes} móveis para vasculhar");
    }

    /// <summary>Móveis das casas que dá para vasculhar (pelo nome do móvel).</summary>
    private static void RecipienteDoMovel(GameObject movel, string nomeMovel, string nomeEstrutura, int indice, Random rnd)
    {
        string chave = new string(nomeEstrutura.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        switch (nomeMovel)
        {
            case "Geladeira":
            case "Freezer": FazerRecipiente(movel, Recipiente.Tipo.Geladeira, chave, $"movel{indice}", 0, 2); break;
            case "Armario":
            case "Estante": FazerRecipiente(movel, Recipiente.Tipo.Armario, chave, $"movel{indice}", 0, 2); break;
            case "Caixa": if (rnd.Next(2) == 0) FazerRecipiente(movel, Recipiente.Tipo.Armario, chave, $"movel{indice}", 0, 1); break;
        }
    }
}
