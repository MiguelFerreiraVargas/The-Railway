using System;
using TMPro;
using UnityEngine;

/// <summary>Relógio do quadro de partidas, com os dois pontos piscando.</summary>
public class BoardClock : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Update()
    {
        if (label == null)
            return;

        var now = DateTime.Now;
        bool colon = now.Millisecond < 500;
        label.text = colon ? now.ToString("HH:mm") : now.ToString("HH mm");
    }
}
