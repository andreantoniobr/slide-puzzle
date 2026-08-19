using System.Collections.Generic;
using UnityEngine;

public class NumberPuzzleBackgroundController : MonoBehaviour
{
    [SerializeField] private GameObject backgroundTilePrefab;
    [SerializeField] private RectTransform backgroundPanel;
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.2f);

    private NumberBackgroundTile[] backgroundTiles;

    public void Build(int numberedTileCount, List<int> activeCells, System.Func<int, Vector2> cellPositionFunc,
                       float cellSize, int fontSize, HashSet<int> skipTileIds = null)
    {
        Clear();
        if (backgroundTilePrefab == null || backgroundPanel == null) return;

        backgroundTiles = new NumberBackgroundTile[numberedTileCount];

        for (int tileId = 0; tileId < numberedTileCount; tileId++)
        {
            if (skipTileIds != null && skipTileIds.Contains(tileId))
                continue; // NOVO — pula Hole, sem número-fantasma naquela posição

            int gridPos = activeCells[tileId];

            GameObject go = Instantiate(backgroundTilePrefab, backgroundPanel);
            go.name = $"BgTile_{tileId + 1}";

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = cellPositionFunc(gridPos);

            NumberBackgroundTile bgTile = go.GetComponent<NumberBackgroundTile>();
            bgTile.Init(tileId + 1, textColor);
            bgTile.SetFontSize(fontSize);

            backgroundTiles[tileId] = bgTile;
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