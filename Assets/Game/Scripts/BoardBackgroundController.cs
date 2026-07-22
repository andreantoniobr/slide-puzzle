using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preenche o fundo do tabuleiro (como se fosse a "caixa" por trás das peças),
/// cobrindo toda célula ativa (com tile numerado OU vazia), incluindo o espaço
/// do gap entre células adjacentes — sem isso, ficaria uma lacuna visível.
/// Puramente visual — sem qualquer componente de input, não é clicável.
/// </summary>
public class BoardBackgroundController : MonoBehaviour
{
    [SerializeField] private GameObject backgroundCellPrefab;
    [SerializeField] private RectTransform container;

    private readonly List<GameObject> spawnedCells = new List<GameObject>();

    public void Build(List<int> activeCells, System.Func<int, Vector2> cellCenterFunc, float cellSize, float gapSize)
    {
        Clear();
        if (backgroundCellPrefab == null || container == null) return;

        // Cobre a célula inteira + o gap (metade pra cada lado), preenchendo a lacuna entre vizinhas
        float coveredSize = cellSize + gapSize;

        foreach (int gridPos in activeCells)
        {
            GameObject go = Instantiate(backgroundCellPrefab, container);
            go.name = $"BgCell_{gridPos}";

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(coveredSize, coveredSize);
            rt.anchoredPosition = cellCenterFunc(gridPos);

            spawnedCells.Add(go);
        }
    }

    public void Clear()
    {
        foreach (GameObject go in spawnedCells)
            if (go != null) Destroy(go);
        spawnedCells.Clear();
    }

    public void SetVisible(bool visible)
    {
        if (container != null)
            container.gameObject.SetActive(visible);
    }
}