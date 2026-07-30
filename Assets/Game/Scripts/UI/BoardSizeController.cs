using UnityEngine;

public class BoardSizeController : MonoBehaviour
{
    [Header("Referência (fonte de verdade)")]
    [SerializeField] private RectTransform boardPanel;

    [Header("Painéis a sincronizar com o boardPanel")]
    [SerializeField] private RectTransform boardBackground;
    [SerializeField] private RectTransform backgroundBoardPanel;
    [SerializeField] private RectTransform boardFrame;

    [Header("Critério de Orientação")]
    [SerializeField] private bool usePlatformCheck = false;

    public void ApplySize(Vector2 sizeMobile, Vector2 sizePC)
    {
        if (boardPanel == null) return;

        Vector2 targetSize = IsMobileContext() ? sizeMobile : sizePC;

        // Recalcula a posição para manter o painel centralizado no ponto de
        // ancoragem, independente do pivot configurado — a fórmula garante
        // que o CENTRO VISUAL do retângulo sempre coincide com o anchor,
        // mesmo quando o tamanho muda.
        Vector2 centeredPosition = new Vector2(
            targetSize.x * (boardPanel.pivot.x - 0.5f),
            targetSize.y * (boardPanel.pivot.y - 0.5f));

        boardPanel.sizeDelta = targetSize;
        boardPanel.anchoredPosition = centeredPosition;

        MirrorToboardPanel(boardBackground);
        MirrorToboardPanel(backgroundBoardPanel);
        MirrorToboardPanel(boardFrame);
    }

    private bool IsMobileContext()
    {
        if (usePlatformCheck)
            return Application.isMobilePlatform;

        return Screen.height > Screen.width;
    }

    private void MirrorToboardPanel(RectTransform rt)
    {
        if (rt == null || boardPanel == null) return;

        rt.anchorMin = boardPanel.anchorMin;
        rt.anchorMax = boardPanel.anchorMax;
        rt.pivot = boardPanel.pivot;
        rt.anchoredPosition = boardPanel.anchoredPosition;
        rt.sizeDelta = boardPanel.sizeDelta;
    }
}