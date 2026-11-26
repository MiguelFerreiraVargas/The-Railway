using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mede uma arma (no espaço do objeto dela): o eixo comprido (do cabo para a cabeça/ponta/cano),
/// o lado "de cima" (gume do machado e da faca; para o rifle, o lado do cano, oposto à coronha que desce)
/// e pontos do cabo, para as mãos agarrarem no lugar certo.
/// </summary>
public class GeometriaDaArma
{
    public Vector3 Eixo { get; private set; }  // cabo → ponta
    public Vector3 Cima { get; private set; }  // gume (machado/faca) ou cano (rifle)
    public Vector3 Lado { get; private set; }
    private float uMin, uMax;
    private readonly List<Vector3> vs = new List<Vector3>();

    /// <param name="eixo">eixo comprido já com o sentido certo (do cabo para a ponta), no espaço do objeto.</param>
    /// <param name="gumeAoLado">true: o "cima" é o lado em que a cabeça sai mais (machado, faca);
    /// false: o "cima" é o lado do cano, oposto ao que a coronha desce (rifle).</param>
    public static GeometriaDaArma Medir(Transform arma, Vector3 eixo, bool gumeAoLado)
    {
        var g = new GeometriaDaArma { Eixo = eixo.normalized };
        foreach (var mf in arma.GetComponentsInChildren<MeshFilter>())
        {
            Vector3[] v;
            try { v = mf.sharedMesh.vertices; } catch { continue; }
            int passo = Mathf.Max(1, v.Length / 4000);
            for (int i = 0; i < v.Length; i += passo) g.vs.Add(arma.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
        }
        if (g.vs.Count == 0) { g.Cima = Vector3.Cross(g.Eixo, Vector3.right).sqrMagnitude > 0.1f ? Vector3.ProjectOnPlane(Vector3.up, g.Eixo).normalized : Vector3.forward; g.Lado = Vector3.Cross(g.Cima, g.Eixo); return g; }

        g.uMin = float.MaxValue; g.uMax = float.MinValue;
        foreach (var p in g.vs) { float u = Vector3.Dot(p, g.Eixo); g.uMin = Mathf.Min(g.uMin, u); g.uMax = Mathf.Max(g.uMax, u); }

        // dos dois eixos que sobram (alinhados ao objeto), o "cima" fica no que é mais largo
        Vector3[] cands = { Vector3.right, Vector3.up, Vector3.forward };
        Vector3 s1 = Vector3.zero; float maior = -1f;
        foreach (var c in cands)
        {
            if (Mathf.Abs(Vector3.Dot(c, g.Eixo)) > 0.7f) continue;
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var p in g.vs) { float s = Vector3.Dot(p, c); mn = Mathf.Min(mn, s); mx = Mathf.Max(mx, s); }
            if (mx - mn > maior) { maior = mx - mn; s1 = c; }
        }
        s1 = Vector3.ProjectOnPlane(s1, g.Eixo).normalized;

        float linha, mais, menos;
        if (gumeAoLado)
        {
            // linha do cabo; a cabeça sai mais para o lado do gume
            linha = g.Media(s1, 0.08f, 0.45f);
            g.Faixa(s1, 0.55f, 0.97f, out menos, out mais);
        }
        else
        {
            // linha do cano (na boca); a coronha desce para o outro lado
            linha = g.Media(s1, 0.88f, 1f);
            g.Faixa(s1, 0f, 0.3f, out menos, out mais);
        }
        bool positivo = (mais - linha) >= (linha - menos);
        if (!gumeAoLado) positivo = !positivo;
        g.Cima = positivo ? s1 : -s1;
        g.Lado = Vector3.Cross(g.Cima, g.Eixo).normalized;
        return g;
    }

    private float U(Vector3 p) => (Vector3.Dot(p, Eixo) - uMin) / Mathf.Max(1e-5f, uMax - uMin);

    private float Media(Vector3 dir, float u0, float u1)
    {
        double soma = 0; int n = 0;
        foreach (var p in vs) { float u = U(p); if (u >= u0 && u <= u1) { soma += Vector3.Dot(p, dir); n++; } }
        return n > 0 ? (float)(soma / n) : 0f;
    }

    private void Faixa(Vector3 dir, float u0, float u1, out float mn, out float mx)
    {
        mn = float.MaxValue; mx = float.MinValue;
        foreach (var p in vs) { float u = U(p); if (u < u0 || u > u1) continue; float s = Vector3.Dot(p, dir); mn = Mathf.Min(mn, s); mx = Mathf.Max(mx, s); }
        if (mn > mx) { mn = mx = 0f; }
    }

    /// <summary>Raio do cabo na altura <paramref name="u"/> (média da altura e da largura da fatia), no espaço do objeto.</summary>
    public float Raio(float u, float largura = 0.03f)
    {
        float mnC = float.MaxValue, mxC = float.MinValue, mnL = float.MaxValue, mxL = float.MinValue;
        foreach (var p in vs)
        {
            if (Mathf.Abs(U(p) - u) > largura) continue;
            float c = Vector3.Dot(p, Cima), l = Vector3.Dot(p, Lado);
            mnC = Mathf.Min(mnC, c); mxC = Mathf.Max(mxC, c); mnL = Mathf.Min(mnL, l); mxL = Mathf.Max(mxL, l);
        }
        if (mnC > mxC) return 0f;
        return ((mxC - mnC) + (mxL - mnL)) * 0.25f;
    }

    /// <summary>
    /// Ponto do cabo na altura <paramref name="u"/> (0 = fim do cabo, 1 = ponta), no espaço do objeto.
    /// <paramref name="k"/>: altura na fatia (0 = embaixo, 1 = em cima, -1 = no meio da massa).
    /// </summary>
    public Vector3 Ponto(float u, float k = -1f, float largura = 0.04f)
    {
        float mnC = float.MaxValue, mxC = float.MinValue; double somaC = 0, somaL = 0; int n = 0;
        foreach (var p in vs)
        {
            if (Mathf.Abs(U(p) - u) > largura) continue;
            float c = Vector3.Dot(p, Cima);
            mnC = Mathf.Min(mnC, c); mxC = Mathf.Max(mxC, c);
            somaC += c; somaL += Vector3.Dot(p, Lado); n++;
        }
        float ao = uMin + u * (uMax - uMin);
        if (n == 0) return Eixo * ao;
        float alt = k < 0f ? (float)(somaC / n) : Mathf.Lerp(mnC, mxC, k);
        return Eixo * ao + Cima * alt + Lado * (float)(somaL / n);
    }
}
