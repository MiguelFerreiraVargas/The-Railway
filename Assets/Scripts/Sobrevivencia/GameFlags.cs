using System;
using System.Collections.Generic;

/// <summary>
/// Progresso da viagem em forma de "marcas" (ex.: "prologo.manivela").
/// Vai junto no save e é zerado ao começar um jogo novo.
/// </summary>
public static class GameFlags
{
    private static readonly HashSet<string> flags = new HashSet<string>();

    public static event Action<string> OnFlagSet;

    public static bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

    public static void Set(string flag)
    {
        if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
        OnFlagSet?.Invoke(flag);
    }

    public static void Clear() => flags.Clear();

    public static List<string> ToList() => new List<string>(flags);

    public static void Load(IEnumerable<string> saved)
    {
        flags.Clear();
        if (saved == null) return;
        foreach (var f in saved)
            if (!string.IsNullOrEmpty(f)) flags.Add(f);
    }
}
