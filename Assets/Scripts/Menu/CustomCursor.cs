using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    [SerializeField] private Texture2D cursorTexture;

    [Header("Cursor Hotspot")]
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    private void Start()
    {
        Cursor.SetCursor(
            cursorTexture,
            hotspot,
            CursorMode.Auto
        );
    }

    private void OnDestroy()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}