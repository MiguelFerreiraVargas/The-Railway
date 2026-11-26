using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Um braço dos braços de primeira pessoa agarrando um cabo de verdade:
/// 1) a mão é virada para o cabo atravessar a palma, logo abaixo dos nós dos dedos;
/// 2) ombro e cotovelo dobram (IK de dois ossos) até o pulso chegar lá, e o giro do pulso passa
///    para o antebraço (como um braço de verdade, sem torcer a pele do pulso);
/// 3) cada dedo se enrola em volta do cabo, junta por junta, e o polegar fecha por cima.
/// Usado pelas armas (machado e rifle com as duas mãos, faca com uma).
/// </summary>
public class MaoNoCabo
{
    private class Dedo { public Transform[] ossos; public float ponta; public Vector3 eixoPonta; public bool polegar; }

    private PoseDoBraco basePose;
    private Transform braco, antebraco, mao;
    private readonly List<Dedo> dedos = new List<Dedo>();
    // no giro da mão (metros, sem escala)
    private Vector3 nosLocal, eixoLocal, palmaLocal, frenteLocal;
    public string Lado { get; private set; }
    public Transform Ombro => braco;
    public Transform Mao => mao;

    public static MaoNoCabo Criar(Transform bracos, string lado)
    {
        var pose = PoseDoBraco.Gravar(bracos, lado, "guard_idle", 0.1f);
        if (pose == null) return null;
        var m = new MaoNoCabo { basePose = pose, Lado = lado };
        m.braco = pose.Osso("upper_arm" + lado);
        m.antebraco = pose.Osso("forearm" + lado);
        m.mao = pose.Mao;
        if (m.braco == null || m.antebraco == null || m.mao == null) return null;

        var guardar = pose.Rig.Select(t => (t, t.localPosition, t.localRotation)).ToList();
        pose.Aplicar();
        var inv = Quaternion.Inverse(m.mao.rotation);
        Vector3 L(Vector3 mundo) => inv * (mundo - m.mao.position);

        var nos = new List<Vector3>(); var pontas = new List<Vector3>();
        foreach (var nome in new[] { "f_index", "f_middle", "f_ring", "f_pinky" })
        {
            var o = new[] { pose.Osso($"{nome}.01{lado}"), pose.Osso($"{nome}.02{lado}"), pose.Osso($"{nome}.03{lado}") };
            if (o.Any(x => x == null)) continue;
            var d = new Dedo { ossos = o, ponta = 0.85f * Vector3.Distance(o[1].position, o[2].position) };
            d.eixoPonta = o[2].InverseTransformDirection((o[2].position - o[1].position).normalized);
            m.dedos.Add(d);
            nos.Add(L(o[0].position)); pontas.Add(L(o[2].position + (o[2].position - o[1].position)));
        }
        var p = new[] { pose.Osso("thumb.01" + lado), pose.Osso("thumb.02" + lado), pose.Osso("thumb.03" + lado) };
        if (p.All(x => x != null))
            m.dedos.Add(new Dedo { ossos = p, polegar = true, ponta = 0.85f * Vector3.Distance(p[1].position, p[2].position), eixoPonta = p[2].InverseTransformDirection((p[2].position - p[1].position).normalized) });

        if (nos.Count >= 2)
        {
            m.nosLocal = nos.Aggregate(Vector3.zero, (a, b) => a + b) / nos.Count;
            m.eixoLocal = (nos[nos.Count - 1] - nos[0]).normalized;                     // do indicador ao mínimo
            m.frenteLocal = Vector3.ProjectOnPlane(m.nosLocal, m.eixoLocal).normalized;  // do pulso para os dedos
            // a palma é o lado para onde os dedos dobram (mesmo meio fechados)
            Vector3 dobra = pontas.Aggregate(Vector3.zero, (a, b) => a + b) / pontas.Count - m.nosLocal;
            m.palmaLocal = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(dobra, m.eixoLocal), m.frenteLocal).normalized;
            if (m.palmaLocal.sqrMagnitude < 0.5f) m.palmaLocal = Vector3.Cross(m.frenteLocal, m.eixoLocal).normalized;
        }
        foreach (var (t, lp, lr) in guardar) t.SetLocalPositionAndRotation(lp, lr);
        return m;
    }

    /// <summary>
    /// Agarra um cabo de <paramref name="raio"/> metros em <paramref name="pega"/> (centro do cabo).
    /// <paramref name="dedos"/>: ao longo do cabo, do lado do indicador para o do mínimo.
    /// <paramref name="pulso"/>: de que lado do cabo fica o pulso. <paramref name="cotovelo"/>: para onde o cotovelo aponta.
    /// </summary>
    public void Segurar(Vector3 pega, Vector3 dedos, Vector3 pulso, Vector3 cotovelo, float raio = 0.017f)
    {
        basePose.Aplicar();
        dedos = dedos.normalized;
        // o cabo passa na palma, logo abaixo dos nós, encostado
        Vector3 caboLocal = nosLocal + palmaLocal * (raio + 0.011f) - frenteLocal * 0.012f;
        Vector3 pulsoLocal = Vector3.ProjectOnPlane(-caboLocal, eixoLocal).normalized;
        pulso = Vector3.ProjectOnPlane(pulso, dedos);
        if (pulso.sqrMagnitude < 1e-6f) pulso = Vector3.ProjectOnPlane(Vector3.down, dedos);
        var rot = Quaternion.LookRotation(dedos, pulso.normalized) * Quaternion.Inverse(Quaternion.LookRotation(eixoLocal, pulsoLocal));

        var maoPosada = mao.localRotation;
        DoisOssos(braco, antebraco, mao, pega - rot * caboLocal, cotovelo);
        // o giro que sobraria no pulso vai para o antebraço (ele gira em volta do próprio eixo)
        Vector3 eixoAnte = (mao.position - antebraco.position).normalized;
        var falta = rot * Quaternion.Inverse(antebraco.rotation * maoPosada);
        antebraco.rotation = Quaternion.Slerp(Quaternion.identity, Torcao(falta, eixoAnte), 0.85f) * antebraco.rotation;
        mao.rotation = rot;

        foreach (var d in this.dedos) Enrolar(d, pega, dedos);
    }

    // põe as juntas do dedo num círculo em volta do eixo do cabo, saindo do nó para o lado da palma
    private void Enrolar(Dedo d, Vector3 pega, Vector3 eixo)
    {
        var o = d.ossos;
        int ini = d.polegar ? 1 : 0; // o polegar dobra a partir da segunda junta
        Vector3 k = o[ini].position;
        Vector3 c = pega + eixo * Vector3.Dot(k - pega, eixo);
        Vector3 raioV = k - c;
        float rho = raioV.magnitude;
        if (rho < 0.004f) return;
        // sentido: o que leva o dedo para a frente (dos nós para fora), em volta do cabo
        Vector3 frente = mao.rotation * frenteLocal;
        Vector3 tangente = Vector3.Cross(eixo, raioV).normalized;
        float s = Vector3.Dot(tangente, d.polegar ? (o[ini + 1].position - o[ini].position) : frente) >= 0f ? 1f : -1f;
        float ang = 0f;
        for (int i = ini; i < 3; i++)
        {
            float comp = i < 2 ? Vector3.Distance(o[i].position, o[i + 1].position) : d.ponta;
            float passo = 2f * Mathf.Asin(Mathf.Clamp(comp / (2f * rho), -1f, 1f)) * Mathf.Rad2Deg;
            ang = Mathf.Min(ang + passo, d.polegar ? 150f : 250f);
            Vector3 alvo = c + Quaternion.AngleAxis(ang * s, eixo) * raioV;
            Vector3 atual = i < 2 ? o[i + 1].position - o[i].position : o[i].TransformDirection(d.eixoPonta) * d.ponta;
            o[i].rotation = Quaternion.FromToRotation(atual, alvo - o[i].position) * o[i].rotation;
        }
    }

    // parte de q que gira em volta de eixo
    private static Quaternion Torcao(Quaternion q, Vector3 eixo)
    {
        Vector3 v = new Vector3(q.x, q.y, q.z);
        Vector3 p = Vector3.Project(v, eixo);
        var t = new Quaternion(p.x, p.y, p.z, q.w);
        float n = Mathf.Sqrt(t.x * t.x + t.y * t.y + t.z * t.z + t.w * t.w);
        if (n < 1e-6f) return Quaternion.identity;
        return new Quaternion(t.x / n, t.y / n, t.z / n, t.w / n);
    }

    private static void DoisOssos(Transform a, Transform b, Transform c, Vector3 alvo, Vector3 dica)
    {
        Vector3 pa = a.position, pb = b.position, pc = c.position;
        float l1 = (pb - pa).magnitude, l2 = (pc - pb).magnitude;
        Vector3 d = alvo - pa;
        float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
        Vector3 dir = d.sqrMagnitude > 1e-8f ? d.normalized : (pc - pa).normalized;
        float x = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
        float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 lado = Vector3.ProjectOnPlane(dica - pa, dir);
        if (lado.sqrMagnitude < 1e-8f) lado = Vector3.ProjectOnPlane(pb - pa, dir);
        Vector3 cot = pa + dir * x + lado.normalized * h;
        a.rotation = Quaternion.FromToRotation(pb - pa, cot - pa) * a.rotation;
        pb = b.position; pc = c.position;
        b.rotation = Quaternion.FromToRotation(pc - pb, pa + dir * dist - pb) * b.rotation;
    }
}
