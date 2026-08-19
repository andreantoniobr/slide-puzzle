using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpecialInfoDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Image background;

    [Header("Proporção da Fonte")]
    [SerializeField, Range(0.1f, 1f)] private float fontSizeRatio = 0.6f; // 60% do tamanho do badge, por padrão

    public void SetNumber(int value)
    {
        if (infoText != null)
            infoText.text = value.ToString();

        SetVisible(true);
    }

    /// <summary>Ajusta o tamanho da fonte proporcionalmente ao tamanho do badge (chamado logo após instanciar).</summary>
    public void SetBadgeSize(float badgeSize)
    {
        if (infoText != null)
            infoText.fontSize = badgeSize * fontSizeRatio;
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}