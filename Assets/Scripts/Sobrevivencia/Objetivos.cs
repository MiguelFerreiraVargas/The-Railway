using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Lista de objetivos no canto da tela. Cada objetivo tem uma "marca" (GameFlags):
/// quando a marca existe, ele aparece riscado/concluído. Ex.:
///   Objetivos.Adicionar("prologo.agua", "Encontrar algo para beber");
///   GameFlags.Set("prologo.agua");   // conclui
/// </summary>
public class Objetivos : MonoBehaviour
{
    private class Item
    {
        public string flag;
        public string texto;
        public string sub; // linha extra (ex.: "carvão 1/3")
    }

    public static Objetivos Instance { get; private set; }

    [SerializeField] private TMP_Text texto;
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somConcluido;

    private readonly List<Item> itens = new List<Item>();
    private float destaque;

    private void Awake()
    {
        Instance = this;
        GameFlags.OnFlagSet += AoMarcar;
        Atualizar();
    }

    private void OnDestroy()
    {
        GameFlags.OnFlagSet -= AoMarcar;
        if (Instance == this) Instance = null;
    }

    public static void Adicionar(string flag, string descricao)
    {
        if (Instance == null || Instance.itens.Exists(i => i.flag == flag)) return;
        Instance.itens.Add(new Item { flag = flag, texto = descricao });
        Instance.destaque = 1f;
        Instance.Atualizar();
    }

    public static void Detalhe(string flag, string sub)
    {
        if (Instance == null) return;
        var item = Instance.itens.Find(i => i.flag == flag);
        if (item == null || item.sub == sub) return;
        item.sub = sub;
        Instance.Atualizar();
    }

    public static void Remover(string flag)
    {
        if (Instance == null) return;
        Instance.itens.RemoveAll(i => i.flag == flag);
        Instance.Atualizar();
    }

    private void AoMarcar(string flag)
    {
        if (!itens.Exists(i => i.flag == flag)) return;
        if (audioSource != null && somConcluido != null) audioSource.PlayOneShot(somConcluido);
        destaque = 1f;
        Atualizar();
    }

    private void Update()
    {
        if (grupo == null) return;
        destaque = Mathf.MoveTowards(destaque, 0f, Time.unscaledDeltaTime * 0.5f);
        grupo.alpha = itens.Count == 0 ? 0f : Mathf.Lerp(0.75f, 1f, destaque);
    }

    private void Atualizar()
    {
        if (texto == null) return;
        var sb = new StringBuilder();
        foreach (var i in itens)
        {
            bool ok = GameFlags.Has(i.flag);
            if (ok)
                sb.AppendLine($"<color=#7A6A50>[X] <s>{i.texto}</s></color>");
            else
            {
                sb.AppendLine($"[ ] {i.texto}");
                if (!string.IsNullOrEmpty(i.sub)) sb.AppendLine($"<size=80%><color=#C8A060>      {i.sub}</color></size>");
            }
        }
        texto.text = sb.ToString();
    }
}
