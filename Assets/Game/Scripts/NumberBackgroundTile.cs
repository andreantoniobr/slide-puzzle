using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Peça de fundo (guia visual) mostrando onde cada número deve ser posicionado.
/// Puramente visual — sem input, sem animação, sem lógica de jogo.
/// </summary>
public class NumberBackgroundTile : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text numberText;

    public void Init(int number, Color textColor, Color backgroundColor)
    {
        if (numberText != null)
            numberText.text = number.ToString();

        if (numberText != null)
            numberText.color = textColor;

        if (background != null)
            background.color = backgroundColor;
    }

    public void SetFontSize(int fontSize)
    {
        if (numberText != null)
            numberText.fontSize = fontSize;
    }
}