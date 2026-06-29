using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Gerencia o tabuleiro de Slide Puzzle.
/// 
/// SETUP:
///  - Crie um GameObject com Canvas (Screen Space - Overlay).
///  - Adicione este script a um GameObject vazio chamado "PuzzleManager".
///  - Configure o prefab da peça (TilePrefab) e o painel do tabuleiro (BoardPanel).
///  - O TilePrefab precisa ter: Image, TilePiece, e um filho com Image para o highlight.
/// 
/// COMO FUNCIONA:
///  1. A imagem completa é cortada em (gridSize x gridSize) sprites via código.
///  2. Cada sprite é atribuído a uma peça.
///  3. A última peça fica invisível (espaço vazio).
///  4. O tabuleiro é embaralhado garantindo solução válida.
///  5. O jogador clica em peças adjacentes ao espaço para movê-las.
/// </summary>
public class SlidePuzzleManager : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────
    //  Inspetor
    // ────────────────────────────────────────────────────────────────
    [Header("Configuração do Tabuleiro")]
    [Tooltip("Prefab de cada peça. Deve conter TilePiece + Image.")]
    public GameObject tilePrefab;

    [Tooltip("RectTransform que serve de container das peças.")]
    public RectTransform boardPanel;

    [Tooltip("Tamanho do grid (3 = 3x3, 4 = 4x4, etc.)")]
    [Range(2, 6)]
    public int gridSize = 3;

    [Header("Imagem do Puzzle")]
    [Tooltip("Sprite com a imagem completa que será fatiada.")]
    public Sprite puzzleSprite;

    [Header("Aparência")]
    [Tooltip("Espessura da borda entre peças (pixels).")]
    public float gapSize = 4f;

    [Tooltip("Duração da animação de movimento (segundos).")]
    [Range(0.05f, 0.5f)]
    public float moveDuration = 0.12f;

    [Header("Embaralhamento")]
    [Tooltip("Número de movimentos aleatórios para embaralhar.")]
    [Range(20, 500)]
    public int shuffleMoves = 100;

    [Header("UI (opcional)")]
    public Text movesText;
    public Text statusText;
    public Button shuffleButton;
    public Button solveButton;   // Mostra a solução (debug)

    // ────────────────────────────────────────────────────────────────
    //  Estado Interno
    // ────────────────────────────────────────────────────────────────
    private TilePiece[] tiles;          // Todas as peças instanciadas
    private int[] board;                // board[currentIndex] = tileIndex
    private int emptyIndex;             // Posição atual do espaço vazio
    private int totalTiles;

    private int moveCount;
    private bool puzzleSolved;
    private bool isAnimating;
    private TilePiece lastHighlighted;

    // ────────────────────────────────────────────────────────────────
    //  Unity
    // ────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (shuffleButton != null) shuffleButton.onClick.AddListener(Shuffle);
        if (solveButton != null)   solveButton.onClick.AddListener(SolveInstant);

        BuildBoard();
        Shuffle();
    }

    // ────────────────────────────────────────────────────────────────
    //  Construção do Tabuleiro
    // ────────────────────────────────────────────────────────────────

    /// <summary>Cria todas as peças e as posiciona no estado resolvido.</summary>
    public void BuildBoard()
    {
        ClearBoard();

        totalTiles = gridSize * gridSize;
        board = new int[totalTiles];
        tiles = new TilePiece[totalTiles];

        // Fatia a imagem em sprites individuais
        Sprite[] sprites = SliceSprite(puzzleSprite, gridSize);

        // Tamanho de cada célula no painel
        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH = (panelH - gapSize * (gridSize + 1)) / gridSize;

        for (int i = 0; i < totalTiles; i++)
        {
            board[i] = i;

            // Posição âncora (top-left da célula)
            float x = gapSize + (i % gridSize) * (cellW + gapSize) + cellW * 0.5f;
            float y = -gapSize - (i / gridSize) * (cellH + gapSize) - cellH * 0.5f;

            GameObject go = Instantiate(tilePrefab, boardPanel);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellW, cellH);
            rt.anchoredPosition = new Vector2(x, y);

            TilePiece tile = go.GetComponent<TilePiece>();
            bool isEmpty = (i == totalTiles - 1);

            // Aplica sprite à Image principal da peça
            Image img = tile.tileImage != null ? tile.tileImage : go.GetComponent<Image>();
            if (!isEmpty && sprites != null && i < sprites.Length)
                img.sprite = sprites[i];

            img.enabled = !isEmpty;
            go.name = isEmpty ? "Tile_Empty" : $"Tile_{i}";

            tile.Init(this, i, i, isEmpty);
            tiles[i] = tile;

            if (isEmpty) emptyIndex = i;
        }

        UpdateMovesUI();
    }

    private void ClearBoard()
    {
        if (tiles == null) return;
        foreach (Transform child in boardPanel)
            Destroy(child.gameObject);
    }

    // ────────────────────────────────────────────────────────────────
    //  Fatiamento de Sprite
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Divide um sprite em (size x size) sub-sprites.
    /// Requer que o sprite seja Read/Write enabled nas Import Settings.
    /// </summary>
    private Sprite[] SliceSprite(Sprite source, int size)
    {
        if (source == null)
        {
            Debug.LogWarning("[SlidePuzzle] puzzleSprite não definido! Peças ficarão sem imagem.");
            return null;
        }

        Texture2D tex = source.texture;

        // Verifica se é legível
        if (!tex.isReadable)
        {
            Debug.LogError("[SlidePuzzle] A textura precisa ter 'Read/Write Enabled' nas Import Settings!");
            return null;
        }

        int totalPieces = size * size;
        Sprite[] result = new Sprite[totalPieces - 1]; // Última peça = vazio

        int pieceW = tex.width / size;
        int pieceH = tex.height / size;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                int idx = row * size + col;
                if (idx >= totalPieces - 1) break; // Pula a última (espaço vazio)

                // Flip vertical: row 0 = topo da imagem
                int texRow = (size - 1 - row);
                int pixelX = col * pieceW;
                int pixelY = texRow * pieceH;

                Rect rect = new Rect(pixelX, pixelY, pieceW, pieceH);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                result[idx] = Sprite.Create(tex, rect, pivot, source.pixelsPerUnit);
            }
        }

        return result;
    }

    // ────────────────────────────────────────────────────────────────
    //  Clique do Jogador
    // ────────────────────────────────────────────────────────────────

    public void OnTileClicked(TilePiece tile)
    {
        if (isAnimating || puzzleSolved) return;

        int idx = tile.currentIndex;

        if (!IsAdjacent(idx, emptyIndex))
        {
            // Feedback: pisca highlight nas peças que PODEM se mover
            ShowMovableHighlights();
            return;
        }

        ClearHighlights();
        StartCoroutine(DoMove(tile, idx));
    }

    private IEnumerator DoMove(TilePiece tile, int fromIndex)
    {
        isAnimating = true;

        // Calcula posição destino (onde está o espaço vazio)
        TilePiece emptyTile = GetTileAtIndex(emptyIndex);
        Vector2 targetPos = emptyTile.GetComponent<RectTransform>().anchoredPosition;

        // Atualiza board lógico
        board[emptyIndex] = board[fromIndex];
        board[fromIndex] = totalTiles - 1; // vazio

        tile.currentIndex = emptyIndex;
        emptyTile.currentIndex = fromIndex;
        emptyIndex = fromIndex;

        moveCount++;
        UpdateMovesUI();

        bool done = false;
        tile.MoveTo(targetPos, moveDuration, () => done = true);

        // Troca posições visuais simultaneamente
        RectTransform tileRT = tile.GetComponent<RectTransform>();
        RectTransform emptyRT = emptyTile.GetComponent<RectTransform>();
        Vector2 emptyOriginalPos = emptyRT.anchoredPosition;
        Vector2 tileOriginalPos = tileRT.anchoredPosition;
        emptyRT.anchoredPosition = tileOriginalPos;

        yield return new WaitUntil(() => done);

        isAnimating = false;

        if (CheckWin())
            OnPuzzleSolved();
    }

    // ────────────────────────────────────────────────────────────────
    //  Embaralhamento
    // ────────────────────────────────────────────────────────────────

    public void Shuffle()
    {
        if (tiles == null) BuildBoard();

        moveCount = 0;
        puzzleSolved = false;
        ClearHighlights();

        if (statusText != null) statusText.text = "";

        // Resolve primeiro para garantir estado inicial limpo
        SolveInstant();

        // Embaralha via movimentos válidos (garante solução)
        int lastEmpty = -1;
        for (int i = 0; i < shuffleMoves; i++)
        {
            List<int> neighbors = GetValidNeighbors(emptyIndex);
            neighbors.RemoveAll(n => n == lastEmpty);

            int pick = neighbors[Random.Range(0, neighbors.Count)];
            lastEmpty = emptyIndex;

            // Swap lógico instantâneo
            SwapTilesLogical(pick, emptyIndex);
        }

        // Aplica posições visuais sem animação
        RefreshVisualPositions();
        UpdateMovesUI();
    }

    private void SwapTilesLogical(int a, int b)
    {
        TilePiece tileA = GetTileAtIndex(a);
        TilePiece tileB = GetTileAtIndex(b);

        int temp = board[a];
        board[a] = board[b];
        board[b] = temp;

        tileA.currentIndex = b;
        tileB.currentIndex = a;

        if (tileA.isEmpty) emptyIndex = b;
        if (tileB.isEmpty) emptyIndex = a;
    }

    /// <summary>Reposiciona todas as peças conforme o estado lógico atual.</summary>
    private void RefreshVisualPositions()
    {
        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH = (panelH - gapSize * (gridSize + 1)) / gridSize;

        for (int i = 0; i < totalTiles; i++)
        {
            TilePiece tile = GetTileAtIndex(i);
            if (tile == null) continue;

            float x = gapSize + (i % gridSize) * (cellW + gapSize) + cellW * 0.5f;
            float y = -gapSize - (i / gridSize) * (cellH + gapSize) - cellH * 0.5f;

            tile.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Resolver (debug / reset)
    // ────────────────────────────────────────────────────────────────

    public void SolveInstant()
    {
        for (int i = 0; i < totalTiles; i++)
        {
            board[i] = i;
            tiles[i].currentIndex = i;
        }

        emptyIndex = totalTiles - 1;
        RefreshVisualPositions();
    }

    // ────────────────────────────────────────────────────────────────
    //  Utilitários
    // ────────────────────────────────────────────────────────────────

    private bool IsAdjacent(int a, int b)
    {
        int rowA = a / gridSize, colA = a % gridSize;
        int rowB = b / gridSize, colB = b % gridSize;
        return Mathf.Abs(rowA - rowB) + Mathf.Abs(colA - colB) == 1;
    }

    private List<int> GetValidNeighbors(int index)
    {
        List<int> list = new List<int>();
        int row = index / gridSize, col = index % gridSize;

        if (row > 0)             list.Add((row - 1) * gridSize + col);
        if (row < gridSize - 1)  list.Add((row + 1) * gridSize + col);
        if (col > 0)             list.Add(row * gridSize + (col - 1));
        if (col < gridSize - 1)  list.Add(row * gridSize + (col + 1));

        return list;
    }

    private TilePiece GetTileAtIndex(int index)
    {
        foreach (TilePiece t in tiles)
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
        if (statusText != null) statusText.text = $"🎉 Parabéns! Resolvido em {moveCount} movimentos!";
        Debug.Log($"[SlidePuzzle] Puzzle resolvido em {moveCount} movimentos!");
    }

    // ────────────────────────────────────────────────────────────────
    //  Highlights
    // ────────────────────────────────────────────────────────────────

    private void ShowMovableHighlights()
    {
        ClearHighlights();
        foreach (int n in GetValidNeighbors(emptyIndex))
        {
            TilePiece t = GetTileAtIndex(n);
            if (t != null) t.SetHighlight(true);
        }
    }

    private void ClearHighlights()
    {
        if (tiles == null) return;
        foreach (TilePiece t in tiles) t.SetHighlight(false);
    }

    // ────────────────────────────────────────────────────────────────
    //  UI
    // ────────────────────────────────────────────────────────────────

    private void UpdateMovesUI()
    {
        if (movesText != null) movesText.text = $"Movimentos: {moveCount}";
    }
}