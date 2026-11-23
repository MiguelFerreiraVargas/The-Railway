using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pose parada de um braço dos braços de primeira pessoa (do ombro aos dedos), tirada de uma
/// animação do rig. Aplicada no LateUpdate, por cima do que o Animator fez, com um peso
/// (0 = como o Animator deixou, 1 = a pose inteira). Usada para segurar a lanterna e o carvão.
/// </summary>
public class PoseDoBraco
{
    private Transform[] ossos;
    private Vector3[] pos;
    private Quaternion[] rot;

    /// <summary>Todos os ossos do rig (para achar dedos etc.).</summary>
    public Transform[] Rig { get; private set; }
    public Transform Mao { get; private set; }
    public string Lado { get; private set; }

    /// <summary>Grava a pose do braço do lado ".R" ou ".L". Devolve null se o rig ou a animação não existem.</summary>
    public static PoseDoBraco Gravar(Transform bracos, string lado, string clipe, float momento)
    {
        if (bracos == null) return null;
        var rig = bracos.GetComponentsInChildren<Transform>(true);
        var mao = rig.FirstOrDefault(t => t.name == "hand" + lado);
        var ombro = rig.FirstOrDefault(t => t.name == "shoulder" + lado);
        var animator = bracos.GetComponentInChildren<Animator>();
        var clip = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.EndsWith(clipe, System.StringComparison.OrdinalIgnoreCase))
            : null;
        if (mao == null || ombro == null || clip == null)
        {
            Debug.LogWarning($"[PoseDoBraco] rig sem hand{lado}/shoulder{lado} ou sem a animação {clipe}");
            return null;
        }

        // amostra a animação, guarda o braço e devolve o rig como estava (o Animator segue mandando)
        var antes = rig.Select(t => (t, t.localPosition, t.localRotation)).ToList();
        clip.SampleAnimation(animator.gameObject, momento * clip.length);
        var p = new PoseDoBraco { Rig = rig, Mao = mao, Lado = lado };
        p.ossos = ombro.GetComponentsInChildren<Transform>(true);
        p.pos = p.ossos.Select(t => t.localPosition).ToArray();
        p.rot = p.ossos.Select(t => t.localRotation).ToArray();
        foreach (var (t, lp, lr) in antes) t.SetLocalPositionAndRotation(lp, lr);
        return p;
    }

    /// <summary>Põe o braço na pose (peso 1) ou no meio do caminho entre o Animator e ela.</summary>
    public void Aplicar(float peso = 1f)
    {
        if (peso <= 0f) return;
        for (int i = 0; i < ossos.Length; i++)
        {
            var t = ossos[i];
            if (t == null) continue;
            if (peso >= 1f) t.SetLocalPositionAndRotation(pos[i], rot[i]);
            else t.SetLocalPositionAndRotation(Vector3.Lerp(t.localPosition, pos[i], peso), Quaternion.Slerp(t.localRotation, rot[i], peso));
        }
    }

    /// <summary>Guarda de novo a rotação atual de um osso (depois de ajustar a pose à mão).</summary>
    public void Regravar(Transform osso)
    {
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] == osso) rot[i] = osso.localRotation;
    }

    /// <summary>Centro do buraco do punho fechado e a direção que o atravessa (do indicador ao mínimo, virada para "frente").</summary>
    public void Punho(Vector3 frente, out Vector3 punho, out Vector3 eixo)
    {
        var centros = new List<Vector3>();
        foreach (var dedo in new[] { "f_index", "f_middle", "f_ring", "f_pinky" })
        {
            var f1 = Osso($"{dedo}.01{Lado}");
            var f2 = Osso($"{dedo}.02{Lado}");
            var f3 = Osso($"{dedo}.03{Lado}");
            if (f1 == null || f2 == null || f3 == null) continue;
            centros.Add((f2.position + f3.position + (f1.parent.position + f1.position) * 0.5f) / 3f);
        }
        punho = centros.Count > 0 ? centros.Aggregate(Vector3.zero, (a, b) => a + b) / centros.Count : Mao.position;
        var indicador = Osso("f_index.01" + Lado);
        var minimo = Osso("f_pinky.01" + Lado);
        eixo = indicador != null && minimo != null ? (minimo.position - indicador.position).normalized : frente;
        if (Vector3.Dot(eixo, frente) < 0f) eixo = -eixo;
    }

    public Transform Osso(string nome)
    {
        foreach (var t in Rig) if (t != null && t.name == nome) return t;
        return null;
    }
}
