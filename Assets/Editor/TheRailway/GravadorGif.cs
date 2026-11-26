using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Junta quadros (PNG) num GIF animado, para mandar os vídeos dos testes. Paleta fixa de 252 cores
/// (mais níveis no escuro, o jogo é de noite) com pontilhado, compressão LZW do próprio GIF.
/// </summary>
public static class GravadorGif
{
    private const int NR = 6, NG = 7, NB = 6;
    private static readonly float[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>Lê os PNG (na ordem dada), reduz para <paramref name="largura"/> e grava o GIF. Atrasos em segundos.</summary>
    public static void Salvar(string caminho, IList<string> pngs, IList<float> atrasos, int largura = 560)
    {
        if (pngs.Count == 0) return;
        int w = 0, h = 0;
        var quadros = new List<byte[]>();
        foreach (var arq in pngs)
        {
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(File.ReadAllBytes(arq))) { Object.DestroyImmediate(tex); continue; }
            if (w == 0) { w = Mathf.Min(largura, tex.width); h = Mathf.RoundToInt(tex.height * (w / (float)tex.width)); }
            quadros.Add(Indices(tex, w, h));
            Object.DestroyImmediate(tex);
        }
        if (quadros.Count == 0) return;

        using var f = new FileStream(caminho, FileMode.Create);
        using var o = new BinaryWriter(f);
        o.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));
        o.Write((ushort)w); o.Write((ushort)h);
        o.Write((byte)0xF7); o.Write((byte)0); o.Write((byte)0); // paleta global de 256
        for (int i = 0; i < 256; i++)
        {
            Color c = Cor(i);
            o.Write((byte)(c.r * 255f)); o.Write((byte)(c.g * 255f)); o.Write((byte)(c.b * 255f));
        }
        // repete sempre
        o.Write(new byte[] { 0x21, 0xFF, 0x0B }); o.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        o.Write(new byte[] { 3, 1, 0, 0, 0 });
        for (int q = 0; q < quadros.Count; q++)
        {
            int cent = Mathf.Clamp(Mathf.RoundToInt((q < atrasos.Count ? atrasos[q] : 0.06f) * 100f), 2, 500);
            o.Write(new byte[] { 0x21, 0xF9, 4, 0 }); o.Write((ushort)cent); o.Write((byte)0); o.Write((byte)0);
            o.Write((byte)0x2C); o.Write((ushort)0); o.Write((ushort)0); o.Write((ushort)w); o.Write((ushort)h); o.Write((byte)0);
            Lzw(o, quadros[q]);
        }
        o.Write((byte)0x3B);
    }

    private static Color Cor(int i)
    {
        if (i >= NR * NG * NB) { float g = (i - NR * NG * NB + 1) / 5f; return new Color(g, g, g); }
        int r = i / (NG * NB), gg = (i / NB) % NG, b = i % NB;
        return new Color(Nivel(r, NR), Nivel(gg, NG), Nivel(b, NB));
    }

    private static float Nivel(int k, int n) { float t = k / (float)(n - 1); return t * t; }
    private static int Indice(float v, int n, float pont) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(Mathf.Clamp01(v)) * (n - 1) + 0.5f + pont), 0, n - 1);

    private static byte[] Indices(Texture2D tex, int w, int h)
    {
        var px = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // o GIF começa em cima; a textura, embaixo
                Color c = tex.GetPixelBilinear((x + 0.5f) / w, 1f - (y + 0.5f) / h);
                float p = Bayer[y & 3, x & 3] / 16f - 0.47f;
                px[y * w + x] = (byte)(Indice(c.r, NR, p) * NG * NB + Indice(c.g, NG, p) * NB + Indice(c.b, NB, p));
            }
        return px;
    }

    // LZW do GIF: códigos de 9 a 12 bits, em blocos de até 255 bytes
    private static void Lzw(BinaryWriter o, byte[] px)
    {
        const int minimo = 8, limpar = 256, fimCod = 257;
        o.Write((byte)minimo);
        var blocos = new List<byte>();
        int acumulado = 0, bits = 0, tamCod = minimo + 1, proximo = fimCod + 1;
        var dic = new Dictionary<int, int>();
        void Emitir(int cod)
        {
            acumulado |= cod << bits; bits += tamCod;
            while (bits >= 8) { blocos.Add((byte)(acumulado & 0xFF)); acumulado >>= 8; bits -= 8; }
        }
        Emitir(limpar);
        int atual = px[0];
        for (int i = 1; i < px.Length; i++)
        {
            int k = px[i];
            int chave = (atual << 8) | k;
            if (dic.TryGetValue(chave, out int cod)) { atual = cod; continue; }
            Emitir(atual);
            if (proximo < 4096)
            {
                dic[chave] = proximo++;
                // o leitor monta o dicionário um código atrás: cresce só depois do próximo
                if (proximo > (1 << tamCod) && tamCod < 12) tamCod++;
            }
            else { Emitir(limpar); dic.Clear(); tamCod = minimo + 1; proximo = fimCod + 1; }
            atual = k;
        }
        Emitir(atual);
        Emitir(fimCod);
        if (bits > 0) blocos.Add((byte)(acumulado & 0xFF));
        for (int i = 0; i < blocos.Count; i += 255)
        {
            int n = Mathf.Min(255, blocos.Count - i);
            o.Write((byte)n);
            for (int j = 0; j < n; j++) o.Write(blocos[i + j]);
        }
        o.Write((byte)0);
    }
}
