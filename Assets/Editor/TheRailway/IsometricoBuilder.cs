using System.Linq;
using System.Text;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta a câmera isométrica na CenaPrincipal: corpo animado do Cândido (male_casual) dentro do
/// PlayerCapsule, a "Camera Isometrica" (CinemachineCamera) e o ModoCamera que alterna com a
/// primeira pessoa. Pode rodar de novo: reaproveita o que já existe e não mexe no resto da cena.
/// </summary>
public static class IsometricoBuilder
{
    public const string ScenePath = "Assets/Scenes/CenaPrincipal.unity";
    private const string CorpoFbx = "Assets/Packages/male_casual/male_casual.fbx";

    public static void Instalar(StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        if (fpc == null) { log.AppendLine("ERRO: FirstPersonController não encontrado"); return; }
        var player = fpc.transform;
        log.AppendLine($"jogador: {player.name} em {player.position}");

        // Corpo
        var corpo = player.Find("Corpo");
        if (corpo == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorpoFbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "Corpo";
            go.transform.SetParent(player, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            corpo = go.transform;
            log.AppendLine("corpo (male_casual) colocado no jogador");
        }
        var anim = Pegar<CorpoAnimado>(corpo.gameObject);
        anim.raiz = player;
        anim.velocidadeAndar = fpc.MoveSpeed;
        anim.velocidadeCorrer = fpc.SprintSpeed;
        var animator = corpo.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        EditorUtility.SetDirty(anim);

        // Câmera isométrica
        var camIsoGo = GameObject.Find("Camera Isometrica");
        if (camIsoGo == null)
        {
            camIsoGo = new GameObject("Camera Isometrica");
            SceneManagerMove(camIsoGo, scene);
            camIsoGo.AddComponent<CinemachineCamera>();
            log.AppendLine("Camera Isometrica criada");
        }
        var vc = Pegar<CinemachineCamera>(camIsoGo);
        vc.Priority = 30;
        var iso = Pegar<CameraIsometrica>(camIsoGo);
        iso.alvo = player;
        camIsoGo.transform.SetPositionAndRotation(player.position + Vector3.up * 1.1f - iso.Rotacao * Vector3.forward * iso.distancia, iso.Rotacao);
        EditorUtility.SetDirty(iso);
        var ocultar = Pegar<OcultarObstaculos>(camIsoGo);
        ocultar.alvo = player;
        EditorUtility.SetDirty(ocultar);

        // Câmera de primeira pessoa: a que segue o PlayerCameraRoot
        CinemachineVirtualCameraBase fp = null;
        foreach (var c in Object.FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.gameObject != camIsoGo && (c.name.Contains("PlayerFollowCamera") || (c.Follow != null && c.Follow.name.Contains("PlayerCameraRoot"))))
                fp = c;
        log.AppendLine("câmera de primeira pessoa: " + (fp != null ? fp.name : "NÃO ACHADA"));

        // Cápsula mais fina e baixa: passa nas portas (0,76 m) e não flutua
        var cc = player.GetComponent<CharacterController>();
        cc.radius = 0.3f; cc.height = 1.8f; cc.center = new Vector3(cc.center.x, 0.92f, cc.center.z);
        fpc.GroundedRadius = 0.3f; fpc.GroundedOffset = -0.22f;
        Pegar<AderirAoChao>(player.gameObject);
        EditorUtility.SetDirty(cc); EditorUtility.SetDirty(fpc);
        log.AppendLine("cápsula do jogador: raio 0,3, altura 1,8 (cabe nas portas)");

        // Mão de primeira pessoa (ArmPrincipal) presa na câmera
        var camMain = Camera.main;
        if (camMain != null)
        {
            var mao = camMain.transform.Find("ArmPrincipal");
            if (mao == null)
            {
                var prefabMao = AssetDatabase.LoadAssetAtPath<GameObject>(MaoPrefab);
                var mg = (GameObject)PrefabUtility.InstantiatePrefab(prefabMao, scene);
                var lp = mg.transform.localPosition; var lr = mg.transform.localRotation; var ls = mg.transform.localScale;
                mg.transform.SetParent(camMain.transform, false);
                mg.transform.localPosition = lp; mg.transform.localRotation = lr; mg.transform.localScale = ls;
                mao = mg.transform;
                log.AppendLine("mão (ArmPrincipal) presa na câmera");
            }
            Pegar<MaoPrimeiraPessoa>(mao.gameObject);
            var lanterna = camMain.transform.Find("Lantern");
            if (lanterna != null)
            {
                var ln = Pegar<LanternaNaMao>(lanterna.gameObject);
                ln.bracos = mao;
                EditorUtility.SetDirty(ln);
                log.AppendLine("lanterna vai para a mão direita ao começar o jogo");
            }
            foreach (var r in mao.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Casas: colisores, portas automáticas e interior em primeira pessoa
        foreach (var raiz in scene.GetRootGameObjects())
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(raiz);
            if (raiz.name.StartsWith("Dom") || (src != null && AssetDatabase.GetAssetPath(src).EndsWith("/Dom.fbx")))
                ConfigurarCasa(raiz.transform, log);
        }

        // Trem: interação pelo sistema novo (E na isométrica e na mira); o TrainClick antigo sai de cena
        foreach (var trem in Object.FindObjectsByType<TrainInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var ti = Pegar<TremInteragivel>(trem.gameObject);
            var tso = new SerializedObject(ti);
            tso.FindProperty("trem").objectReferenceValue = trem;
            tso.FindProperty("acao").stringValue = "SUBIR NO TREM";
            tso.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("trem interagível: " + trem.name);
        }
        foreach (var tc in player.GetComponents<TrainClick>())
            if (tc.enabled) { tc.enabled = false; EditorUtility.SetDirty(tc); log.AppendLine("TrainClick antigo desligado (o E agora passa pelo PlayerInteractor)"); }

        // O que só aparece em primeira pessoa: cápsula de debug e objetos presos na câmera (lanterna)
        var soFp = player.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(corpo) && r.name != "Capsule").ToList();
        var main = Camera.main;
        if (main != null) soFp.AddRange(main.GetComponentsInChildren<Renderer>(true));
        log.AppendLine("só em primeira pessoa: " + string.Join(", ", soFp.Select(r => r.name)));

        var modo = Pegar<ModoCamera>(player.gameObject);
        var so = new SerializedObject(modo);
        so.FindProperty("cameraIsometrica").objectReferenceValue = iso;
        so.FindProperty("cameraPrimeiraPessoa").objectReferenceValue = fp;
        so.FindProperty("corpo").objectReferenceValue = corpo;
        so.FindProperty("modoInicial").enumValueIndex = (int)ModoCamera.Modo.PrimeiraPessoa;
        so.FindProperty("modoDentro").enumValueIndex = (int)ModoCamera.Modo.Isometrico;
        var arr = so.FindProperty("soEmPrimeiraPessoa");
        arr.arraySize = soFp.Count;
        for (int i = 0; i < soFp.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = soFp[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        BracosPrevia.DesligarCapsulaDebug(log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("cena salva: " + ScenePath);
    }

    private const string MaoPrefab = "Assets/Prefabs/ArmPrincipal.prefab";

    private static void ConfigurarCasa(Transform casa, StringBuilder log)
    {
        int colisores = 0, portas = 0;
        Collider piso = null;
        foreach (var r in casa.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.GetComponentInParent<Interagivel>() != null) continue; // documentos e itens dentro da casa
            var moveis = casa.Find("Moveis"); var sup = casa.Find("Suprimentos");
            if ((moveis != null && r.transform.IsChildOf(moveis)) || (sup != null && r.transform.IsChildOf(sup))) continue; // móveis têm colisor próprio
            var mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
            var go = r.gameObject;
            if (mat.StartsWith("Door"))
            {
                if (go.GetComponent<BoxCollider>() == null) go.AddComponent<BoxCollider>();
                var porta = Pegar<PortaAutomatica>(go);
                // dobradiça na ponta do lado mais comprido da folha, no meio da espessura
                var b = r.bounds;
                bool aoLongoDeX = b.size.x >= b.size.z;
                Vector3 dobMundo = aoLongoDeX ? new Vector3(b.min.x + 0.03f, b.min.y, b.center.z) : new Vector3(b.center.x, b.min.y, b.min.z + 0.03f);
                porta.dobradicaLocal = go.transform.parent.InverseTransformPoint(dobMundo);
                EditorUtility.SetDirty(porta);
                portas++;
                continue;
            }
            if (mat == "Shifer") continue; // telhado: ninguém anda nele
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) { mc = go.AddComponent<MeshCollider>(); colisores++; }
            // piso: peça baixa, fina e de maior área
            var bp = r.bounds;
            if (bp.size.y < 0.6f && bp.center.y - casa.position.y < 0.6f && (piso == null || bp.size.x * bp.size.z > piso.bounds.size.x * piso.bounds.size.z))
                piso = mc;
        }

        var est = Pegar<Estrutura>(casa.gameObject);
        est.pisos = piso != null ? new Collider[] { piso } : new Collider[0];
        EditorUtility.SetDirty(est);

        // Zona sem árvores em volta da casa (o gerador de vegetação respeita)
        var zona = casa.Find("Zona sem Arvores");
        if (zona == null)
        {
            var rs = casa.GetComponentsInChildren<Renderer>();
            var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            var zg = new GameObject("Zona sem Arvores");
            zg.transform.SetParent(casa, false);
            zg.transform.position = bb.center;
            var bc = zg.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = bb.size + new Vector3(5f, 2f, 5f);
            zona = zg.transform;
        }
        var spawner = Object.FindFirstObjectByType<ProceduralVegetationSpawner>();
        if (spawner != null)
        {
            var zc = zona.GetComponent<Collider>();
            var lista = (spawner.exclusionZones ?? new Collider[0]).Where(c => c != null).ToList();
            if (!lista.Contains(zc)) { lista.Add(zc); spawner.exclusionZones = lista.ToArray(); EditorUtility.SetDirty(spawner); }
        }

        log.AppendLine($"casa \"{casa.name}\": {colisores} colisores novos, {portas} portas automáticas, piso: {(piso != null ? piso.name : "NÃO ACHADO")}");
    }

    // GetComponent no editor devolve um "nulo falso" que o ?? não enxerga
    private static T Pegar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
