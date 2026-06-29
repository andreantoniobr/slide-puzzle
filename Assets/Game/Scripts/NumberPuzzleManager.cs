using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;


/// <summary>
/// Slide Puzzle numérico — sem sprites, sem corte de imagem.
/// Cada peça exibe seu número (1, 2, 3 … N²-1) e fica verde quando está na posição correta.
///
/// SETUP RÁPIDO (Auto-Setup):
///   1. Crie um GameObject vazio → adicione NumberPuzzleManager + NumberPuzzleAutoSetup.
///   2. Press Play. A cena inteira é montada por código.
///
/// SETUP MANUAL:
///   - boardPanel  → RectTransform do painel container.
///   - tilePrefab  → Prefab com NumberTile + Image (bg) + Text + Image (highlight).
///   - Campos de UI opcionais: movesText, statusText, shuffleButton.
/// </summary>
public class NumberPuzzleManager : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────
    //  Inspector
    // ────────────────────────────────────────────────────────────────
    [Header("Tabuleiro")]
    public GameObject     tilePrefab;
    public RectTransform  boardPanel;

    [Range(2, 8)]
    public int gridSize = 4;

    [Header("Aparência")]
    public float gapSize     = 6f;
    public float moveDuration = 0.10f;

    [Header("Fonts")]
    [SerializeField] private float fontSizePercent = 0.45f;
    [SerializeField] private int minFontSize = 16;
    [SerializeField] private int maxFontSize = 128;

    [Header("Embaralhamento")]
    [Range(30, 600)]
    public int shuffleMoves = 120;

    [Header("UI (opcional)")]
    public Text   movesText;
    public Text   statusText;
    public Button shuffleButton;
    public Button solveButton;

    public static event Action SlidedTileEvent;
    

    // ────────────────────────────────────────────────────────────────
    //  Estado
    // ────────────────────────────────────────────────────────────────
    private NumberTile[] tiles;
    private int[]        board;          // board[posição] = índice da peça
    private int          emptyIndex;
    private int          totalTiles;
    private int          moveCount;
    private bool         puzzleSolved;
    private bool         isAnimating;

    // ────────────────────────────────────────────────────────────────
    //  Unity
    // ────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (shuffleButton != null) shuffleButton.onClick.AddListener(Shuffle);
        if (solveButton   != null) solveButton.onClick.AddListener(SolveInstant);

        BuildBoard();
        Shuffle();
    }

    // ────────────────────────────────────────────────────────────────
    //  Construção
    // ────────────────────────────────────────────────────────────────
    public void BuildBoard()
    {
        ClearBoard();

        totalTiles = gridSize * gridSize;
        board      = new int[totalTiles];
        tiles      = new NumberTile[totalTiles];

        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW  = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH  = (panelH - gapSize * (gridSize + 1)) / gridSize;

        // Tamanho de fonte proporcional à célula
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(cellW * fontSizePercent), minFontSize, maxFontSize);

        for (int i = 0; i < totalTiles; i++)
        {
            board[i] = i;

            bool isEmpty = (i == totalTiles - 1);

            GameObject go = Instantiate(tilePrefab, boardPanel);
            go.name = isEmpty ? "Tile_Empty" : $"Tile_{i + 1}";

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.sizeDelta  = new Vector2(cellW, cellH);
            rt.anchoredPosition = CellPosition(i, cellW, cellH);

            // Ajusta tamanho de fonte
            NumberTile tile = go.GetComponent<NumberTile>();
            if (tile.numberText != null)
            {
                tile.numberText.fontSize  = fontSize;
                //tile.numberText.fontStyle = FontStyle.Bold;
            }

            tile.Init(this, i, i, isEmpty);
            tiles[i] = tile;

            if (isEmpty) emptyIndex = i;
        }

        UpdateMovesUI();
    }

    private void ClearBoard()
    {
        if (boardPanel == null) return;
        foreach (Transform child in boardPanel)
            Destroy(child.gameObject);
    }

    // ────────────────────────────────────────────────────────────────
    //  Clique
    // ────────────────────────────────────────────────────────────────
    public void OnTileClicked(NumberTile tile)
    {
        if (isAnimating || puzzleSolved) return;

        int idx = tile.currentIndex;

        if (!IsAdjacent(idx, emptyIndex))
        {
            ShowMovableHighlights();
            return;
        }

        ClearHighlights();
        StartCoroutine(DoMove(tile, idx));
    }

    private IEnumerator DoMove(NumberTile tile, int fromIndex)
    {
        isAnimating = true;

        SlidedTileEvent?.Invoke();

        NumberTile    emptyTile = GetTileAtIndex(emptyIndex);
        RectTransform tileRT   = tile.GetComponent<RectTransform>();
        RectTransform emptyRT  = emptyTile.GetComponent<RectTransform>();

        // Posição atual da peça e destino (onde está o buraco)
        Vector2 startPos  = tileRT.anchoredPosition;
        Vector2 targetPos = emptyRT.anchoredPosition;

        // Esconde o empty durante a animação (a peça passa por cima)
        emptyRT.gameObject.SetActive(false);

        // Atualiza lógica ANTES de animar
        board[emptyIndex]      = board[fromIndex];
        board[fromIndex]       = totalTiles - 1;
        tile.currentIndex      = emptyIndex;
        emptyTile.currentIndex = fromIndex;
        emptyIndex             = fromIndex;

        moveCount++;
        UpdateMovesUI();

        // ── Slide animado ────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
            tileRT.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        tileRT.anchoredPosition = targetPos;

        // Reativa o empty na posição original da peça (agora o "buraco")
        emptyRT.anchoredPosition = startPos;
        emptyRT.gameObject.SetActive(true);

        // Atualiza cores
        tile.Refresh();
        emptyTile.Refresh();

        isAnimating = false;

        tile.CheckIfJustReachedCorrectPosition();

        if (CheckWin()) OnPuzzleSolved();
    }
    

    // ────────────────────────────────────────────────────────────────
    //  Embaralhamento
    // ────────────────────────────────────────────────────────────────
    public void Shuffle()
    {
        if (tiles == null) BuildBoard();

        moveCount    = 0;
        puzzleSolved = false;
        ClearHighlights();
        if (statusText != null) statusText.text = "";

        SolveInstant();

        int lastEmpty = -1;
        for (int i = 0; i < shuffleMoves; i++)
        {
            List<int> neighbors = GetValidNeighbors(emptyIndex);
            neighbors.RemoveAll(n => n == lastEmpty);

            int pick = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
            lastEmpty = emptyIndex;
            SwapLogical(pick, emptyIndex);
        }

        RefreshVisualPositions();
        RefreshAllColors();
        UpdateMovesUI();
    }

    private void SwapLogical(int a, int b)
    {
        NumberTile tA = GetTileAtIndex(a);
        NumberTile tB = GetTileAtIndex(b);

        int temp = board[a];
        board[a] = board[b];
        board[b] = temp;

        tA.currentIndex = b;
        tB.currentIndex = a;

        if (tA.isEmpty) emptyIndex = b;
        if (tB.isEmpty) emptyIndex = a;
    }

    private void RefreshVisualPositions()
    {
        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW  = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH  = (panelH - gapSize * (gridSize + 1)) / gridSize;

        for (int i = 0; i < totalTiles; i++)
        {
            NumberTile t = GetTileAtIndex(i);
            if (t != null)
                t.GetComponent<RectTransform>().anchoredPosition = CellPosition(i, cellW, cellH);
        }
    }

    private void RefreshAllColors()
    {
        foreach (NumberTile t in tiles) t.Refresh();
    }

    // ────────────────────────────────────────────────────────────────
    //  Resolver (debug / reset visual)
    // ────────────────────────────────────────────────────────────────
    public void SolveInstant()
    {
        for (int i = 0; i < totalTiles; i++)
        {
            board[i]           = i;
            tiles[i].currentIndex = i;
        }
        emptyIndex = totalTiles - 1;
        RefreshVisualPositions();
        RefreshAllColors();
    }

    // ────────────────────────────────────────────────────────────────
    //  Utilitários
    // ────────────────────────────────────────────────────────────────
    private Vector2 CellPosition(int index, float cellW, float cellH)
    {
        float x = gapSize + (index % gridSize) * (cellW + gapSize) + cellW * 0.5f;
        float y = -(gapSize + (index / gridSize) * (cellH + gapSize) + cellH * 0.5f);
        return new Vector2(x, y);
    }

    private bool IsAdjacent(int a, int b)
    {
        int rA = a / gridSize, cA = a % gridSize;
        int rB = b / gridSize, cB = b % gridSize;
        return Mathf.Abs(rA - rB) + Mathf.Abs(cA - cB) == 1;
    }

    private List<int> GetValidNeighbors(int index)
    {
        var list = new List<int>();
        int r = index / gridSize, c = index % gridSize;
        if (r > 0)            list.Add((r - 1) * gridSize + c);
        if (r < gridSize - 1) list.Add((r + 1) * gridSize + c);
        if (c > 0)            list.Add(r * gridSize + (c - 1));
        if (c < gridSize - 1) list.Add(r * gridSize + (c + 1));
        return list;
    }

    private NumberTile GetTileAtIndex(int index)
    {
        foreach (NumberTile t in tiles)
            if (t.currentIndex == index) return t;
        return null;
    }

    private bool CheckWin()
    {
        for (int i = 0; i < totalTiles; i++)
            if (board[i] != i) return false;
        return true;
    }

    private void OnPuzzleSolved()
    {
        puzzleSolved = true;
        // Pinta todas de verde
        foreach (NumberTile t in tiles) t.Refresh();
        if (statusText != null)
            statusText.text = $"🎉 Resolvido em {moveCount} movimentos!";
        Debug.Log($"[NumberPuzzle] Resolvido em {moveCount} movimentos!");
    }

    // ────────────────────────────────────────────────────────────────
    //  Highlights
    // ────────────────────────────────────────────────────────────────
    private void ShowMovableHighlights()
    {
        ClearHighlights();
        foreach (int n in GetValidNeighbors(emptyIndex))
            GetTileAtIndex(n)?.SetHighlight(true);
    }

    private void ClearHighlights()
    {
        if (tiles == null) return;
        foreach (NumberTile t in tiles) t.SetHighlight(false);
    }

    // ────────────────────────────────────────────────────────────────
    //  UI
    // ────────────────────────────────────────────────────────────────
    private void UpdateMovesUI()
    {
        if (movesText != null) movesText.text = $"Movimentos: {moveCount}";
    }
}