using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla os números de fundo (guia visual) mostrando onde cada peça deve terminar.
/// Puramente visual — o NumberPuzzleManager comanda quando construir/limpar,
/// passando a lista de posições ativas do grid e a função de cálculo de posição
/// (que agora recebe só o índice da célula, já que cellSize é único e uniforme).
/// </summary>
public class NumberPuzzleBackgroundController : MonoBehaviour
{
    [SerializeField] private GameObject backgroundTilePrefab;
    [SerializeField] private RectTransform backgroundPanel;
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.2f);

    private NumberBackgroundTile[] backgroundTiles;

    /// <param name="numberedTileCount">Quantas peças numeradas existem (exclui os vazios).</param>
    /// <param name="activeCells">Posições do grid que realmente existem (activeCells[tileId] = posição correta daquele tileId).</param>
    /// <param name="cellPositionFunc">Função que converte uma posição de grid em anchoredPosition.</param>
    /// <param name="cellSize">Tamanho uniforme da célula (largura = altura, sempre quadrado).</param>
    /// <param name="fontSize">Tamanho de fonte já calculado pelo manager.</param>
    public void Build(int numberedTileCount, List<int> activeCells, System.Func<int, Vector2> cellPositionFunc, float cellSize, int fontSize)
    {
        Clear();
        if (backgroundTilePrefab == null || backgroundPanel == null) return;

        backgroundTiles = new NumberBackgroundTile[numberedTileCount];

        for (int tileId = 0; tileId < numberedTileCount; tileId++)
        {
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