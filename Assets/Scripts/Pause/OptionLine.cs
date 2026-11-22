using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Linha de opção no painel: "VOLUME          < 80% >".
/// Setas esquerda/direita (ou clique) mudam o valor, que é salvo na hora.
/// </summary>
public class OptionLine : MonoBehaviour, IMoveHandler, IPointerClickHandler, ISubmitHandler
{
    public enum Kind { Volume, Sensitivity, Fullscreen, FieldOfView }

    [SerializeField] private Kind kind;
    [SerializeField] private string title = "VOLUME";
    [SerializeField] private TMP_Text label;
    [SerializeField] private int titleWidth = 14;

    public event System.Action Changed;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (label != null)
            label.text = $"{title.PadRight(titleWidth)}< {ValueText()} >";
    }

    private string ValueText()
    {
        switch (kind)
        {
            case Kind.Volume: return $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%".PadLeft(4);
            case Kind.Sensitivity: return $"{GameSettings.Sensitivity:0.0}X".PadLeft(4);
            case Kind.FieldOfView: return $"{Mathf.RoundToInt(GameSettings.FieldOfView)}".PadLeft(4);
            default: return GameSettings.Fullscreen ? " SIM" : " NÃO";
        }
    }

    private void Step(int direction)
    {
        switch (kind)
        {
            case Kind.Volume:
                GameSettings.Volume = Mathf.Round(Mathf.Clamp01(GameSettings.Volume + 0.1f * direction) * 10f) / 10f;
                break;
            case Kind.Sensitivity:
                GameSettings.Sensitivity = Mathf.Round((GameSettings.Sensitivity + 0.1f * direction) * 10f) / 10f;
                break;
            case Kind.FieldOfView:
                GameSettings.FieldOfView = Mathf.Round(GameSettings.FieldOfView / 5f) * 5f + 5f * direction;
                break;
            default:
                GameSettings.Fullscreen = !GameSettings.Fullscreen;
                break;
        }

        Refresh();
        Changed?.Invoke();
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left) Step(-1);
        else if (eventData.moveDir == MoveDirection.Right) Step(+1);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Clique esquerdo aumenta, clique direito diminui
        Step(eventData.button == PointerEventData.InputButton.Right ? -1 : +1);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Step(+1);
    }
}
