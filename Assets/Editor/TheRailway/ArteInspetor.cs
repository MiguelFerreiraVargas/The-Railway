using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Ferramenta de leitura: recorta a área desenhada das artes em pixel
/// (que vêm num canvas grande e transparente) e salva ampliada em Temp,
/// para dar para ver os detalhes. Não altera os arquivos originais.
/// </summary>
public static class ArteInspetor
{
    public static void Gerar(StringBuilder log)
    {
        Directory.CreateDirectory("Temp/TheRailway/arte");
        Folha("Assets/Arte/pixilart-frames", "frames", log);
        Folha("Assets/Arte/pixilart-frames (1)", "frames1", log);
        foreach (var f in new[] { "Assets/Arte/pixilart-drawing (1).png", "Assets/Arte/pixilart-drawing (2).png" })
            Folha(null, Path.GetFileNameWithoutExtension(f).Replace(" ", "").Replace("(", "").Replace(")", ""), log, f);
        foreach (var b in Buracos("Assets/Arte/pixilart-drawing (1).png"))
            log.AppendLine($"  buraco: x {b.xMin}..{b.xMax - 1}  y {b.yMin}..{b.yMax - 1}  ({b.width}x{b.height}, área {b.width * b.height})");
        log.AppendLine("  conteúdo: " + Conteudo("Assets/Arte/pixilart-drawing (1).png"));
        log.AppendLine("  orbe: " + Conteudo("Assets/Arte/pixilart-frames (1)/pixil-frame-0.png"));
        log.AppendLine("  barra: " + Conteudo("Assets/Arte/pixilart-frames/pixil-frame-0.png"));
    }

    private static Texture2D Ler(string path)
    {
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(path));
        return t;
    }

    /// <summary>Retângulo com pixels visíveis (coordenadas da textura, origem embaixo à esquerda).</summary>
    public static RectInt Conteudo(string path)
    {
        var t = Ler(path);
        var px = t.GetPixels32();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                if (px[y * t.width + x].a > 10) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        Object.DestroyImmediate(t);
        return maxX < 0 ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// Áreas transparentes fechadas dentro do desenho (as "janelas" da moldura),
    /// em coordenadas da textura. Ordenadas da maior para a menor.
    /// </summary>
    public static List<RectInt> Buracos(string path)
    {
        var t = Ler(path);
        int w = t.width, h = t.height;
        var px = t.GetPixels32();
        Object.DestroyImmediate(t);
        bool Vazio(int i) => px[i].a <= 10;

        var marca = new int[w * h]; // 0 = não visitado, -1 = fora, >0 = buraco n
        var fila = new Queue<int>();
        void Encher(int ini, int valor, List<int> lista)
        {
            fila.Enqueue(ini); marca[ini] = valor;
            while (fila.Count > 0)
            {
                int i = fila.Dequeue(); lista?.Add(i);
                int x = i % w, y = i / w;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (marca[j] != 0 || !Vazio(j)) continue;
                    marca[j] = valor; fila.Enqueue(j);
                }
            }
        }
        for (int x = 0; x < w; x++) { foreach (int y in new[] { 0, h - 1 }) { int i = y * w + x; if (marca[i] == 0 && Vazio(i)) Encher(i, -1, null); } }
        for (int y = 0; y < h; y++) { foreach (int x in new[] { 0, w - 1 }) { int i = y * w + x; if (marca[i] == 0 && Vazio(i)) Encher(i, -1, null); } }

        var result = new List<RectInt>();
        int n = 0;
        for (int i = 0; i < w * h; i++)
        {
            if (marca[i] != 0 || !Vazio(i)) continue;
            var lista = new List<int>();
            Encher(i, ++n, lista);
            if (lista.Count < 4) continue;
            int minX = lista.Min(k => k % w), maxX = lista.Max(k => k % w), minY = lista.Min(k => k / w), maxY = lista.Max(k => k / w);
            result.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }
        return result.OrderByDescending(r => r.width * r.height).ToList();
    }

    private static void Folha(string pasta, string nome, StringBuilder log, string arquivo = null)
    {
        var arquivos = arquivo != null ? new List<string> { arquivo }
            : Directory.GetFiles(pasta, "*.png").OrderBy(p => int.TryParse(new string(Path.GetFileNameWithoutExtension(p).Where(char.IsDigit).ToArray()), out int n) ? n : 0).ToList();
        var texs = arquivos.Select(p => { var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(p)); return t; }).ToList();

        // retângulo comum com conteúdo
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        foreach (var t in texs)
        {
            var px = t.GetPixels32();
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                    if (px[y * t.width + x].a > 10) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        }
        if (maxX < 0) { log.AppendLine(nome + ": vazio"); return; }
        int w = maxX - minX + 1, h = maxY - minY + 1;
        log.AppendLine($"{nome}: {texs.Count} quadro(s), canvas {texs[0].width}x{texs[0].height}, desenho {w}x{h} em ({minX},{minY})");

        int esc = Mathf.Max(1, Mathf.Min(12, 900 / Mathf.Max(w, h)));
        int cols = Mathf.Min(5, texs.Count), rows = (texs.Count + cols - 1) / cols, pad = 6;
        var outTex = new Texture2D(cols * (w * esc + pad), rows * (h * esc + pad), TextureFormat.RGBA32, false);
        var fundo = new Color32(200, 60, 200, 255); // fundo magenta para ver o transparente
        outTex.SetPixels32(Enumerable.Repeat(fundo, outTex.width * outTex.height).ToArray());
        for (int i = 0; i < texs.Count; i++)
        {
            int ox = (i % cols) * (w * esc + pad), oy = (rows - 1 - i / cols) * (h * esc + pad);
            var t = texs[i];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = t.GetPixel(minX + x, minY + y);
                    if (c.a < 0.04f) continue;
                    for (int a = 0; a < esc; a++)
                        for (int b = 0; b < esc; b++)
                            outTex.SetPixel(ox + x * esc + a, oy + y * esc + b, c);
                }
        }
        outTex.Apply();
        File.WriteAllBytes($"Temp/TheRailway/arte/{nome}.png", outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        foreach (var t in texs) Object.DestroyImmediate(t);
    }
}
