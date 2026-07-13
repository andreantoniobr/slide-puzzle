using UnityEngine;

/// <summary>
/// Controla os números de fundo (guia visual) mostrando onde cada peça deve terminar.
/// Puramente visual — o NumberPuzzleManager comanda quando construir/limpar,
/// passando as dimensões da célula já calculadas.
/// </summary>
public class NumberPuzzleBackgroundController : MonoBehaviour
{
    [SerializeField] private GameObject backgroundTilePrefab;
    [SerializeField] private RectTransform backgroundPanel;
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0f);

    private NumberBackgroundTile[] backgroundTiles;

    public void Build(int numberedTileCount, System.Func<int, float, float, Vector2> cellPositionFunc, float cellW, float cellH, int fontSize)
    {
        Clear();
        if (backgroundTilePrefab == null || backgroundPanel == null) return;

        backgroundTiles = new NumberBackgroundTile[numberedTileCount];

        for (int i = 0; i < numberedTileCount; i++) 
        {
            GameObject go = Instantiate(backgroundTilePrefab, backgroundPanel);
            go.name = $"BgTile_{i + 1}";

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellW, cellH);
            rt.anchoredPosition = cellPositionFunc(i, cellW, cellH);

            NumberBackgroundTile bgTile = go.GetComponent<NumberBackgroundTile>();
            bgTile.Init(i + 1, textColor, backgroundColor);
            bgTile.SetFontSize(fontSize);

            backgroundTiles[i] = bgTile;
        }
    }

    public void Clear()
    {
        if (backgroundPanel == null) return;

        foreach (Transform child in backgroundPanel)
            Destroy(child.gameObject);

        backgroundTiles = null;
    }

    public void SetVisible(bool visible)
    {
        if (backgroundPanel != null)
            backgroundPanel.gameObject.SetActive(visible);
    }
}