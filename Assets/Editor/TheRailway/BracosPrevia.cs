using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Diagnóstico dos braços de primeira pessoa: renderiza o ArmPrincipal preso numa câmera,
/// em várias poses e escalas, numa cena de prévia (não mexe na cena aberta).
/// </summary>
public static class BracosPrevia
{
    private const string Prefab = "Assets/Prefabs/ArmPrincipal.prefab";
    private const string Pasta = "Temp/TheRailway";

    public static void Rodar(string args, StringBuilder log)
    {
        // args: "clip;escalaZ;posX,posY,posZ" (vazios = o que está no prefab)
        var partes = (args ?? "").Split(';');
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var ps = EditorSceneManager.NewPreviewScene();
        try
        {
            var camGo = new GameObject("cam");
            SceneManager.MoveGameObjectToScene(camGo, ps);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = ps; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.3f, 0.33f, 0.36f);
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.02f;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ps);
            var lp = go.transform.localPosition; var lr = go.transform.localRotation; var ls = go.transform.localScale;
            go.transform.SetParent(camGo.transform, false);
            if (partes.Length > 1 && float.TryParse(partes[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ez)) ls = new Vector3(ls.x, ls.y, ez);
            if (partes.Length > 2 && partes[2].Contains(","))
            {
                var v = partes[2].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                lp = new Vector3(v[0], v[1], v[2]);
            }
            go.transform.localPosition = lp; go.transform.localRotation = lr; go.transform.localScale = ls;
            log.AppendLine($"local pos {lp:F2} rot {lr.eulerAngles:F0} escala {ls:F3}");

            var an = go.GetComponentInChildren<Animator>();
            string nomeClip = partes.Length > 0 && partes[0] != "" ? partes[0] : "relax";
            var clips = an.runtimeAnimatorController.animationClips;
            log.AppendLine("clipes: " + string.Join(", ", clips.Select(c => c.name).Distinct()));
            float quando = 0.1f;
            if (nomeClip.Contains("@")) { quando = float.Parse(nomeClip.Substring(nomeClip.IndexOf('@') + 1), System.Globalization.CultureInfo.InvariantCulture); nomeClip = nomeClip.Substring(0, nomeClip.IndexOf('@')); }
            var clip = clips.FirstOrDefault(c => c.name.ToLowerInvariant().EndsWith(nomeClip.ToLowerInvariant()));
            if (clip != null) { clip.SampleAnimation(an.gameObject, quando * clip.length); log.AppendLine($"pose: {clip.name} @ {quando:0.00}"); }

            foreach (var t in go.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("upper_arm") || t.name.StartsWith("forearm") || t.name.StartsWith("hand") || t.name.StartsWith("shoulder") || t.parent == go.transform)
                    log.AppendLine($"  {t.name}: cam {camGo.transform.InverseTransformPoint(t.position):F2} escalaMundo {t.lossyScale:F2}");
            if (partes.Length > 3 && partes[3] == "mao")
                foreach (var t in go.GetComponentsInChildren<Transform>().First(x => x.name == "hand.R").GetComponentsInChildren<Transform>())
                    log.AppendLine($"    {t.name} (pai {t.parent.name}): cam {camGo.transform.InverseTransformPoint(t.position):F3} frente {camGo.transform.InverseTransformDirection(t.forward):F2} cima {camGo.transform.InverseTransformDirection(t.up):F2}");
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                log.AppendLine($"  malha {r.name} raiz {(r.rootBone ? r.rootBone.name : "-")} bounds {r.bounds.size:F2}");

            if (partes.Length > 3 && partes[3] == "lanterna")
            {
                var lg = NovaLanterna(camGo.transform);
                var ln = lg.AddComponent<LanternaNaMao>();
                ln.bracos = go.transform;
                ln.lenteNoPositivo = LenteNoPositivo(lg.GetComponent<MeshFilter>().sharedMesh, log);
                if (partes.Length > 4 && partes[4] != "") ln.clipePose = partes[4];
                ln.Montar();
                if (partes.Length > 5)
                {
                    var q = partes[5].Split('@');
                    var pe = PoseDoBraco.Gravar(go.transform, ".L", q[0], q.Length > 1 ? float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture) : 0.5f);
                    pe.Aplicar();
                    pe.Punho(camGo.transform.forward, out Vector3 pu, out _);
                    var pedra = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    SceneManager.MoveGameObjectToScene(pedra, ps);
                    pedra.transform.SetParent(camGo.transform, false);
                    pedra.transform.position = pu + camGo.transform.TransformVector(new Vector3(0.01f, 0.03f, 0f));
                    pedra.transform.localRotation = Quaternion.Euler(28f, -42f, -22f);
                    pedra.transform.localScale = new Vector3(0.085f, 0.065f, 0.075f) * 0.75f;
                    pedra.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.12f, 0.12f, 0.13f) };
                    log.AppendLine($"carvão: punho esquerdo em cam {camGo.transform.InverseTransformPoint(pu):F3}");
                }
                log.AppendLine($"lanterna: mão {(ln.Mao ? ln.Mao.name : "NENHUMA")}, centro cam {camGo.transform.InverseTransformPoint(lg.GetComponent<Renderer>().bounds.center):F3}, aponta {camGo.transform.InverseTransformDirection(lg.transform.right * (ln.lenteNoPositivo ? 1f : -1f)):F2}");
            }
            if (partes.Length > 3 && partes[3] == "lado") { camGo.transform.position += camGo.transform.right * 1.6f + camGo.transform.forward * 0.2f; camGo.transform.rotation = Quaternion.Euler(0f, -90f, 0f); go.transform.SetParent(null, true); camGo.transform.position = Vector3.right * 1.6f + Vector3.forward * 0.3f; }
            var luz = new GameObject("luz").AddComponent<Light>();
            luz.type = LightType.Directional; luz.transform.rotation = Quaternion.Euler(40f, -30f, 0f); luz.intensity = 1.3f;
            SceneManager.MoveGameObjectToScene(luz.gameObject, ps);
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            string nome = "bracos_" + new string((args ?? "").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()) + ".png";
            File.WriteAllBytes(Path.Combine(Pasta, nome), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null; Object.DestroyImmediate(rt);
            log.AppendLine("prévia: " + nome);
        }
        finally { EditorSceneManager.ClosePreviewScene(ps); }
    }

    // A lanterna que fica presa na câmera da CenaPrincipal (malha do pacote Survival)
    private const string SurvivalFbx = "Assets/Models/Survival/Models/Survival.fbx";
    private const long MalhaLanterna = -5420149580168298667;

    private static GameObject NovaLanterna(Transform cam)
    {
        Mesh malha = null; Material mat = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(SurvivalFbx))
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string _, out long id))
            {
                if (id == MalhaLanterna && a is Mesh m) malha = m;
                if (id == -3155152467044017229 && a is Material mt) mat = mt;
            }
        var lg = new GameObject("Lantern");
        lg.transform.SetParent(cam, false);
        lg.transform.localRotation = new Quaternion(0.43045917f, 0.56098574f, 0.5609857f, -0.43045908f);
        lg.transform.localPosition = new Vector3(0.4f, -0.215f, 0.797f);
        lg.transform.localScale = Vector3.one * 5.233308f;
        lg.AddComponent<MeshFilter>().sharedMesh = malha;
        lg.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return lg;
    }

    /// <summary>A ponta mais larga do eixo comprido é a da lente.</summary>
    public static bool LenteNoPositivo(Mesh m, StringBuilder log)
    {
        var b = m.bounds; var s = b.size;
        int ei = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
        float raioMais = 0f, raioMenos = 0f;
        foreach (var v in m.vertices)
        {
            float t = (v[ei] - b.min[ei]) / s[ei];
            var d = v - b.center; d[ei] = 0f;
            if (t > 0.85f) raioMais = Mathf.Max(raioMais, d.magnitude);
            if (t < 0.15f) raioMenos = Mathf.Max(raioMenos, d.magnitude);
        }
        log.AppendLine($"lanterna: eixo {"xyz"[ei]}, tamanho {s:F3}, raio na ponta + {raioMais:F4}, na ponta - {raioMenos:F4}");
        return raioMais >= raioMenos;
    }

    // Braços na frente da câmera: escala uniforme (a antiga era esticada 3x no Z e entortava a malha)
    // e recuados até os ombros ficarem logo atrás dos olhos (senão aparece o corte do braço).
    public static readonly Vector3 PosicaoBracos = new Vector3(0.11f, -1.76f, 0.15f);
    public const float EscalaBracos = 1.0088f;
    private const string Cena = "Assets/Scenes/CenaPrincipal.unity";

    public static void Aplicar(StringBuilder log)
    {
        var raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            log.AppendLine($"prefab antes: pos {raiz.transform.localPosition:F2} escala {raiz.transform.localScale:F3}");
            raiz.transform.localPosition = PosicaoBracos;
            raiz.transform.localScale = Vector3.one * EscalaBracos;
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }

        var cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        var cam = Camera.main;
        var bracos = cam != null ? cam.transform.Find("ArmPrincipal") : null;
        if (bracos == null) throw new System.Exception("ArmPrincipal não achado na câmera");
        log.AppendLine($"cena antes: pos {bracos.localPosition:F2} escala {bracos.localScale:F3}");
        bracos.localPosition = PosicaoBracos;
        bracos.localScale = Vector3.one * EscalaBracos;
        EditorUtility.SetDirty(bracos);

        var ln = cam.GetComponentInChildren<LanternaNaMao>(true);
        if (ln != null)
        {
            ln.bracos = bracos;
            ln.ajuste = Vector3.zero;
            var mf = ln.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) ln.lenteNoPositivo = LenteNoPositivo(mf.sharedMesh, log);
            EditorUtility.SetDirty(ln);
            log.AppendLine("lanterna: segura no punho direito");
            var luz = ln.GetComponent<LuzDaLanterna>();
            if (luz == null) luz = ln.gameObject.AddComponent<LuzDaLanterna>();
            // lanterna de verdade: clareia perto e some em poucos metros (a noite continua noite)
            luz.intensidade = 12f; luz.alcance = 16f; luz.intensidadeIso = 6f; luz.alcanceIso = 9f; luz.angulo = 50f;
            EditorUtility.SetDirty(luz);
            log.AppendLine("lanterna: luz própria (L liga e desliga)");
        }

        // câmera de fora da locomotiva: V dentro do trem, mouse mais suave
        foreach (var tc in Object.FindObjectsByType<TrainCameraView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tc.trainCamera == null) { log.AppendLine($"TrainCameraView sem câmera em '{tc.name}' (desligado)"); tc.enabled = false; EditorUtility.SetDirty(tc); continue; }
            tc.mouseSensitivity = 0.2f;
            tc.startingDistance = 22f;
            tc.startingPitch = 18f;
            tc.startingYaw = 20f;
            tc.enabled = true;
            // o objeto estava desligado na cena: a câmera do trem nunca rodava
            tc.gameObject.SetActive(true);
            tc.trainCamera.enabled = false;
            // vira a Camera.main quando liga (a do jogador fica desligada): mira, mapa e avisos continuam funcionando
            tc.trainCamera.gameObject.tag = "MainCamera";
            var ouvido = tc.trainCamera.GetComponent<AudioListener>();
            if (ouvido != null) { ouvido.enabled = false; EditorUtility.SetDirty(ouvido); }
            EditorUtility.SetDirty(tc.trainCamera);
            EditorUtility.SetDirty(tc.gameObject);
            EditorUtility.SetDirty(tc);
            log.AppendLine($"câmera do trem: {tc.name}");
        }
        DesligarCapsulaDebug(log);

        // só um AudioListener: o do minimapa sobrava (aviso "2 audio listeners" o tempo todo)
        foreach (var ouv in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ouv.GetComponent<Camera>() != null && ouv.GetComponent<Camera>() != cam && ouv.enabled && ouv.GetComponent<TrainCameraView>() == null)
            {
                ouv.enabled = false;
                EditorUtility.SetDirty(ouv);
                log.AppendLine($"AudioListener desligado em '{ouv.name}'");
            }
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        log.AppendLine("cena salva");
    }

    /// <summary>
    /// A cápsula de debug do jogador (StarterAssets) não tem material: aparece roxa quando a câmera sai
    /// de dentro dela. Fica desligada e sai da lista do que o ModoCamera liga em primeira pessoa.
    /// </summary>
    public static void DesligarCapsulaDebug(StringBuilder log)
    {
        var modo = Object.FindFirstObjectByType<ModoCamera>(FindObjectsInactive.Include);
        if (modo == null) return;
        var capsulas = modo.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Capsule").ToList();
        foreach (var r in capsulas) { r.enabled = false; EditorUtility.SetDirty(r); }
        var so = new SerializedObject(modo);
        var lista = so.FindProperty("soEmPrimeiraPessoa");
        for (int i = lista.arraySize - 1; i >= 0; i--)
        {
            var r = lista.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
            if (r != null && capsulas.Contains(r)) lista.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"cápsula de debug desligada ({capsulas.Count})");
    }
}
