using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using BoardFrame.UI;

public class NumberPuzzleManager : MonoBehaviour
{
    [Header("Tabuleiro")]
    public GameObject    tilePrefab;
    public RectTransform boardPanel;

    [Header("Tamanho do Tabuleiro")]
    [SerializeField] private BoardSizeController boardSizeController;

    [Range(1, 8)]
    public int gridWidth = 4;
    [Range(1, 8)]
    public int gridHeight = 4;

    [Header("Formato (buracos)")]
    [Tooltip("Posições do grid (0-based, row-major: row*gridWidth+col) que NÃO existem no tabuleiro.")]
    [SerializeField] private List<int> disabledCells = new List<int>();

    [Header("Espaços Vazios")]
    [Min(1)]
    public int emptyTileCount = 1;

    [Header("Aparência")]
    public float gapSize      = 6f;
    public float moveDuration = 0.10f;

    [Header("Fonts")]
    [SerializeField] private float fontSizePercent = 0.45f;
    [SerializeField] private int   minFontSize     = 16;
    [SerializeField] private int   maxFontSize     = 128;

    [Header("Embaralhamento")]
    [Range(30, 600)]
    public int shuffleMoves = 120;

    [Header("Highlight — Shake")]
    [SerializeField] private float shakeAmplitude = 5f;
    [SerializeField] private float shakeDuration  = 0.35f;
    [SerializeField] private float shakeFrequency = 28f;

    [Header("Números de Fundo (guia visual)")]
    [SerializeField] private bool showBackgroundNumbers = true;
    [SerializeField] private NumberPuzzleBackgroundController backgroundController;

    [Header("Moldura (mesh único)")]
    [SerializeField] private BoardFrameMesh boardFrameMesh;

    [Header("Fundo do Tabuleiro (caixa)")]
    [SerializeField] private BoardBackgroundController boardBackgroundController;

    [Header("UI (opcional)")]
    public Text   movesText;
    public Text   statusText;
    public Button shuffleButton;
    public Button solveButton;

    // ── Eventos públicos ─────────────────────────────────────────────
    public static event Action PuzzleStartedEvent;
    public static event Action<int, int>   SolvedPuzzleEvent;  // (movimentos, segundos)

    public static event Action             SlidedTileEvent;

    public static event Action             HighlightShownEvent;

    // ── Estado privado ───────────────────────────────────────────────
    private NumberTile[] tiles;              // indexado por tileId (identidade lógica da peça)
    private int[]        board;              // indexado por posição do grid; -1 = buraco (nunca usado)
    private List<int>    activeCells;        // posições do grid que existem, em ordem — activeCells[tileId] = posição correta daquele tileId
    private Dictionary<int, int> gridPosToCorrectTileId; // posição do grid -> tileId que pertence ali quando resolvido
    private List<int>    emptyIndexes = new List<int>(); // posições do grid (não tileIds) que estão vazias agora
    private int          totalActiveCells;
    private int          moveCount;
    private bool         puzzleSolved;
    private bool         isAnimating;

    private NumberTile pendingSelectionTile;

    // Layout unificado — evita distorção em grids não-quadrados
    private float cellSize;
    private Vector2 boardOrigin;

    // controla o tempo de jogo
    private float puzzleStartTime;

    // controla se o jogador já interagiu nesta fase (usado pro gameplayStart da Poki)
    private bool hasFiredFirstInput;

    // guarda a posição inicial de cada tile para permitir restart exato
    private int[] initialTileIndexes;

    private Dictionary<NumberTile, Coroutine> activeShakes =
        new Dictionary<NumberTile, Coroutine>();

    // ────────────────────────────────────────────────────────────────
    //  Unity
    // ────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (shuffleButton != null) shuffleButton.onClick.AddListener(Shuffle);
        if (solveButton   != null) solveButton.onClick.AddListener(SolveInstant);

        // Se não houver LevelManager na cena, mantém o comportamento antigo (útil pra testar isolado)
        if (FindAnyObjectByType<LevelManager>() == null)
        {
            BuildBoard();
            Shuffle();
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Layout (tamanho de célula unificado, sem distorção)
    // ────────────────────────────────────────────────────────────────

    private void ComputeLayout()
    {
        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;

        float cellFromWidth  = (panelW - gapSize * (gridWidth + 1)) / gridWidth;
        float cellFromHeight = (panelH - gapSize * (gridHeight + 1)) / gridHeight;
        cellSize = Mathf.Min(cellFromWidth, cellFromHeight); // nunca distorce — sempre quadrado

        float boardWidth  = gridWidth  * cellSize + gapSize * (gridWidth + 1);
        float boardHeight = gridHeight * cellSize + gapSize * (gridHeight + 1);

        boardOrigin = new Vector2(
            (panelW - boardWidth) * 0.5f,
            (panelH - boardHeight) * 0.5f
        );
    }

    // ────────────────────────────────────────────────────────────────
    //  Construção
    // ────────────────────────────────────────────────────────────────

    private void ComputeActiveCells()
    {
        activeCells = new List<int>();
        int totalGridCells = gridWidth * gridHeight;

        for (int pos = 0; pos < totalGridCells; pos++)
        {
            if (!disabledCells.Contains(pos))
                activeCells.Add(pos);
        }

        totalActiveCells = activeCells.Count;

        gridPosToCorrectTileId = new Dictionary<int, int>();
        for (int tileId = 0; tileId < activeCells.Count; tileId++)
            gridPosToCorrectTileId[activeCells[tileId]] = tileId;
    }

    public void BuildBoard()
    {
        ClearBoard();
        ComputeActiveCells();
        ComputeLayout();

        int totalGridCells = gridWidth * gridHeight;
        board = new int[totalGridCells];
        for (int i = 0; i < totalGridCells; i++) board[i] = -1; // -1 = buraco

        tiles = new NumberTile[totalActiveCells];
        emptyIndexes.Clear();

        int fontSize = Mathf.Clamp(Mathf.RoundToInt(cellSize * fontSizePercent), minFontSize, maxFontSize);
        int firstEmptyId = totalActiveCells - emptyTileCount;

        if (backgroundController != null)
        {
            backgroundController.SetVisible(showBackgroundNumbers);
            if (showBackgroundNumbers)
                backgroundController.Build(firstEmptyId, activeCells, CellPosition, cellSize, fontSize);
        }

        for (int tileId = 0; tileId < totalActiveCells; tileId++)
        {
            int gridPos = activeCells[tileId];
            board[gridPos] = tileId;

            bool isEmpty = (tileId >= firstEmptyId);

            GameObject go = Instantiate(tilePrefab, boardPanel);
            go.name = isEmpty ? $"Tile_Empty_{tileId}" : $"Tile_{tileId + 1}";

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.sizeDelta  = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = CellPosition(gridPos);

            NumberTile tile = go.GetComponent<NumberTile>();
            if (tile.numberText != null)
                tile.numberText.fontSize = fontSize;

            tile.Init(this, tileId, gridPos, isEmpty);
            tiles[tileId] = tile;

            if (isEmpty) emptyIndexes.Add(gridPos);
        }

        if (boardFrameMesh != null)
        {
            var activeSet = new HashSet<int>(activeCells);
            boardFrameMesh.Build(gridWidth, gridHeight, activeSet, CellPosition, cellSize);
        }

        if (boardBackgroundController != null)
        {
            boardBackgroundController.Build(activeCells, CellPosition, cellSize, gapSize); 
        }

        UpdateMovesUI();
    }

    private void ClearBoard()
    {
        if (boardPanel == null) return;
        activeShakes.Clear();
        foreach (Transform child in boardPanel)
            Destroy(child.gameObject);
    }

    // ────────────────────────────────────────────────────────────────
    //  Input — Clique
    // ────────────────────────────────────────────────────────────────

    public bool OnTileClicked(NumberTile tile)
    {
        if (isAnimating || puzzleSolved) return true;

        if (pendingSelectionTile != null && pendingSelectionTile != tile)
            CancelTargetSelection();

        int tileIdx = tile.currentIndex;
        List<int> adjacentEmpties = FindAllAdjacentEmpty(tileIdx);

        if (adjacentEmpties.Count == 1)
        {
            ClearHighlights();
            StartCoroutine(DoMove(tile, tileIdx, adjacentEmpties[0]));
            return true;
        }

        if (adjacentEmpties.Count > 1)
        {
            BeginTargetSelection(tile, adjacentEmpties);
            return true;
        }

        if (emptyIndexes.Count == 1)
        {
            List<NumberTile> chain = BuildMoveChain(tileIdx);
            if (chain != null && chain.Count > 0)
            {
                ClearHighlights();
                StartCoroutine(DoChainMove(chain));
                return true;
            }
        }

        ShowMovableHighlights();
        return false;
    }

    private void BeginTargetSelection(NumberTile tile, List<int> availableEmpties)
    {
        ClearHighlights();
        CancelTargetSelection();

        pendingSelectionTile = tile;
        tile.SetAwaitingSelection(true);

        foreach (int emptyPos in availableEmpties)
        {
            NumberTile emptyTile = GetTileAtIndex(emptyPos);
            emptyTile?.SetSelectableTarget(true);
        }
    }

    public void OnEmptyTileSelected(NumberTile emptyTile)
    {
        if (pendingSelectionTile == null) return;

        NumberTile selectedTile = pendingSelectionTile;
        int fromIndex = selectedTile.currentIndex;
        int targetEmptyIndex = emptyTile.currentIndex;

        CancelTargetSelection();
        StartCoroutine(DoMove(selectedTile, fromIndex, targetEmptyIndex));
    }

    private void CancelTargetSelection()
    {
        if (pendingSelectionTile == null) return;

        pendingSelectionTile.SetAwaitingSelection(false);

        foreach (int emptyPos in emptyIndexes)
            GetTileAtIndex(emptyPos)?.SetSelectableTarget(false);

        pendingSelectionTile = null;
    }

    private List<int> FindAllAdjacentEmpty(int tileIdx)
    {
        var result = new List<int>();
        foreach (int emptyPos in emptyIndexes)
            if (IsAdjacent(tileIdx, emptyPos)) result.Add(emptyPos);
        return result;
    }

    public void NotifyPlayerInput()
    {
        if (hasFiredFirstInput || puzzleSolved) return;
        hasFiredFirstInput = true;
    }

    // ────────────────────────────────────────────────────────────────
    //  Input — Swipe
    // ────────────────────────────────────────────────────────────────

    public bool TryMove(NumberTile tile, DragDirection direction)
    {
        if (isAnimating || puzzleSolved) return false;

        int tileIdx = tile.currentIndex;

        foreach (int emptyPos in emptyIndexes)
        {
            DragDirection allowed = GetDirectionToEmpty(tileIdx, emptyPos);
            if (allowed != DragDirection.None && allowed == direction)
            {
                ClearHighlights();
                StartCoroutine(DoMove(tile, tileIdx, emptyPos));
                return true;
            }
        }

        if (emptyIndexes.Count == 1)
        {
            List<NumberTile> chain = BuildMoveChain(tileIdx);
            if (chain != null && chain.Count > 0)
            {
                DragDirection chainDir = GetChainDirection(tileIdx);
                if (chainDir != DragDirection.None && chainDir == direction)
                {
                    ClearHighlights();
                    StartCoroutine(DoChainMove(chain));
                    return true;
                }
            }
        }

        return false;
    }

    private DragDirection GetDirectionToEmpty(int tileIdx, int emptyIdx)
    {
        int rTile = tileIdx / gridWidth, cTile = tileIdx % gridWidth;
        int rEmpty = emptyIdx / gridWidth, cEmpty = emptyIdx % gridWidth;

        int dr = rEmpty - rTile;
        int dc = cEmpty - cTile;
        if (Mathf.Abs(dr) + Mathf.Abs(dc) != 1) return DragDirection.None;

        if (dr == 1 && dc == 0) return DragDirection.Down;
        if (dr == -1 && dc == 0) return DragDirection.Up;
        if (dr == 0 && dc == 1) return DragDirection.Right;
        if (dr == 0 && dc == -1) return DragDirection.Left;
        return DragDirection.None;
    }

    private DragDirection GetChainDirection(int tileIndex)
    {
        if (emptyIndexes.Count != 1) return DragDirection.None;
        int emptyIdx = emptyIndexes[0];

        int rTile  = tileIndex  / gridWidth;
        int cTile  = tileIndex  % gridWidth;
        int rEmpty = emptyIdx / gridWidth;
        int cEmpty = emptyIdx % gridWidth;

        if (rTile == rEmpty && cEmpty > cTile) return DragDirection.Right;
        if (rTile == rEmpty && cEmpty < cTile) return DragDirection.Left;
        if (cTile == cEmpty && rEmpty > rTile) return DragDirection.Down;
        if (cTile == cEmpty && rEmpty < rTile) return DragDirection.Up;

        return DragDirection.None;
    }

    // ────────────────────────────────────────────────────────────────
    //  Movimento em cadeia (só existe quando há exatamente 1 espaço vazio)
    // ────────────────────────────────────────────────────────────────

    private List<NumberTile> BuildMoveChain(int targetIndex)
    {
        if (emptyIndexes.Count != 1) return null;
        int emptyIdx = emptyIndexes[0];

        int rTarget = targetIndex / gridWidth;
        int cTarget = targetIndex % gridWidth;
        int rEmpty  = emptyIdx    / gridWidth;
        int cEmpty  = emptyIdx    % gridWidth;

        if (rTarget == rEmpty && cTarget != cEmpty)
        {
            var chain = new List<NumberTile>();
            int step  = cTarget > cEmpty ? 1 : -1;
            for (int c = cEmpty + step; step > 0 ? c <= cTarget : c >= cTarget; c += step)
            {
                int idx = rEmpty * gridWidth + c;
                if (disabledCells.Contains(idx)) return null;
                NumberTile t = GetTileAtIndex(idx);
                if (t != null && !t.isEmpty) chain.Add(t);
            }
            return chain;
        }

        if (cTarget == cEmpty && rTarget != rEmpty)
        {
            var chain = new List<NumberTile>();
            int step  = rTarget > rEmpty ? 1 : -1;
            for (int r = rEmpty + step; step > 0 ? r <= rTarget : r >= rTarget; r += step)
            {
                int idx = r * gridWidth + cEmpty;
                if (disabledCells.Contains(idx)) return null;
                NumberTile t = GetTileAtIndex(idx);
                if (t != null && !t.isEmpty) chain.Add(t);
            }
            return chain;
        }

        return null;
    }

    private IEnumerator DoChainMove(List<NumberTile> chain)
    {
        isAnimating = true;

        var moves = new List<(RectTransform rt, Vector2 from, Vector2 to)>();

        foreach (NumberTile tile in chain)
        {
            int fromIndex = tile.currentIndex;
            int emptyIdx  = emptyIndexes[0];

            NumberTile    emptyTile = GetTileAtIndex(emptyIdx);
            RectTransform tileRT    = tile.GetComponent<RectTransform>();

            Vector2 startPos  = CellPosition(fromIndex);
            Vector2 targetPos = CellPosition(emptyIdx);

            moves.Add((tileRT, startPos, targetPos));

            board[emptyIdx]        = board[fromIndex];
            board[fromIndex]       = emptyTile.correctIndex;
            tile.currentIndex      = emptyIdx;
            emptyTile.currentIndex = fromIndex;

            emptyIndexes[0] = fromIndex;

            moveCount++;
            SlidedTileEvent?.Invoke();
        }

        UpdateMovesUI();

        int finalEmptyIdx = emptyIndexes[0];
        NumberTile    emptyTileFinal = GetTileAtIndex(finalEmptyIdx);
        RectTransform emptyRTFinal   = emptyTileFinal.GetComponent<RectTransform>();
        emptyRTFinal.gameObject.SetActive(false);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
            foreach (var (rt, from, to) in moves)
                rt.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        foreach (var (rt, _, to) in moves)
            rt.anchoredPosition = to;

        emptyRTFinal.anchoredPosition = CellPosition(finalEmptyIdx);
        emptyRTFinal.gameObject.SetActive(true);

        foreach (NumberTile tile in chain)
        {
            tile.Refresh();
            tile.CheckIfJustReachedCorrectPosition();
        }
        emptyTileFinal.Refresh();

        isAnimating = false;

        if (CheckWin()) OnPuzzleSolved();
    }

    // ────────────────────────────────────────────────────────────────
    //  Movimento animado (único)
    // ────────────────────────────────────────────────────────────────

    private IEnumerator DoMove(NumberTile tile, int fromIndex, int targetEmptyIndex, Action onComplete = null)
    {
        isAnimating = true;
        SlidedTileEvent?.Invoke();

        NumberTile    emptyTile = GetTileAtIndex(targetEmptyIndex);
        RectTransform tileRT    = tile.GetComponent<RectTransform>();
        RectTransform emptyRT   = emptyTile.GetComponent<RectTransform>();

        Vector2 startPos  = tileRT.anchoredPosition;
        Vector2 targetPos = emptyRT.anchoredPosition;

        emptyRT.gameObject.SetActive(false);

        board[targetEmptyIndex] = board[fromIndex];
        board[fromIndex]        = emptyTile.correctIndex;
        tile.currentIndex       = targetEmptyIndex;
        emptyTile.currentIndex  = fromIndex;

        emptyIndexes.Remove(targetEmptyIndex);
        emptyIndexes.Add(fromIndex);

        moveCount++;
        UpdateMovesUI();

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
            tileRT.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        tileRT.anchoredPosition = targetPos;

        emptyRT.anchoredPosition = startPos;
        emptyRT.gameObject.SetActive(true);

        tile.Refresh();
        emptyTile.Refresh();
        isAnimating = false;

        tile.CheckIfJustReachedCorrectPosition();
        if (CheckWin()) OnPuzzleSolved();

        onComplete?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    //  Embaralhamento
    // ────────────────────────────────────────────────────────────────

    public void Shuffle()
    {
        if (tiles == null) BuildBoard();

        moveCount    = 0;
        puzzleSolved = false;
        hasFiredFirstInput = false;
        ClearHighlights();
        if (statusText != null) statusText.text = "";

        SolveInstant();

        for (int i = 0; i < shuffleMoves; i++)
        {
            int emptyPos = emptyIndexes[UnityEngine.Random.Range(0, emptyIndexes.Count)];
            List<int> neighbors = GetValidNeighbors(emptyPos);
            neighbors.RemoveAll(n => emptyIndexes.Contains(n));

            if (neighbors.Count == 0) continue;

            int pick = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
            SwapLogical(pick, emptyPos);
        }

        RefreshVisualPositions();
        RefreshAllColors();
        UpdateMovesUI();

        SaveInitialState();
        puzzleStartTime = Time.time;
        PuzzleStartedEvent?.Invoke();
    }

    public void ShuffleDeterministic(int seed)
    {
        UnityEngine.Random.State previousState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(seed);
        Shuffle();
        UnityEngine.Random.state = previousState;
    }

    /// <summary>Carrega um nível a partir de um LevelConfig (feito à mão ou procedural).</summary>
    public void LoadLevel(LevelConfig config)
    {
        boardSizeController?.ApplySize(config.boardSizeMobile, config.boardSizePC);
        boardFrameMesh?.ApplyCustomThickness(config.customBorderThickness);
        
        gridWidth  = Mathf.Clamp(config.gridWidth,  1, 8);
        gridHeight = Mathf.Clamp(config.gridHeight, 1, 8);

        disabledCells = config.disabledCells != null
            ? new List<int>(config.disabledCells)
            : new List<int>();

        if (config.shuffleMoves > 0) shuffleMoves = config.shuffleMoves;

        int approxTotalCells = Mathf.Max(1, gridWidth * gridHeight - disabledCells.Count);
        int maxEmpty = Mathf.Max(1, approxTotalCells / 2);
        emptyTileCount = Mathf.Clamp(config.emptyTileCount > 0 ? config.emptyTileCount : 1, 1, maxEmpty);

        BuildBoard();

        if (config.customBoard != null && config.customBoard.Length == totalActiveCells)
            ApplyCustomArrangement(config.customBoard);
        else
            ShuffleDeterministic(config.seed);
    }

    private void ApplyCustomArrangement(int[] arrangement)
    {
        moveCount    = 0;
        puzzleSolved = false;
        hasFiredFirstInput = false;
        ClearHighlights();
        if (statusText != null) statusText.text = "";

        emptyIndexes.Clear();
        for (int tileId = 0; tileId < totalActiveCells; tileId++)
        {
            int gridPos = arrangement[tileId];
            tiles[tileId].currentIndex = gridPos;
            board[gridPos] = tileId;
            if (tiles[tileId].isEmpty) emptyIndexes.Add(gridPos);
        }

        RefreshVisualPositions();
        RefreshAllColors();
        UpdateMovesUI();

        SaveInitialState();
        puzzleStartTime = Time.time;
        PuzzleStartedEvent?.Invoke();
    }

    public void RestartLevel()
    {
        if (initialTileIndexes == null || initialTileIndexes.Length != totalActiveCells)
        {
            Shuffle();
            return;
        }

        moveCount    = 0;
        puzzleSolved = false;
        hasFiredFirstInput = false;
        ClearHighlights();
        if (statusText != null) statusText.text = "";

        emptyIndexes.Clear();
        for (int tileId = 0; tileId < totalActiveCells; tileId++)
        {
            int gridPos = initialTileIndexes[tileId];
            tiles[tileId].currentIndex = gridPos;
            board[gridPos] = tileId;
            if (tiles[tileId].isEmpty) emptyIndexes.Add(gridPos);
        }

        RefreshVisualPositions();
        RefreshAllColors();
        UpdateMovesUI();

        puzzleStartTime = Time.time;
        PuzzleStartedEvent?.Invoke();
    }

    private void SaveInitialState()
    {
        initialTileIndexes = new int[totalActiveCells];
        for (int tileId = 0; tileId < totalActiveCells; tileId++)
            initialTileIndexes[tileId] = tiles[tileId].currentIndex;
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

        if (tA.isEmpty) { emptyIndexes.Remove(a); emptyIndexes.Add(b); }
        if (tB.isEmpty) { emptyIndexes.Remove(b); emptyIndexes.Add(a); }
    }

    private void RefreshVisualPositions()
    {
        ComputeLayout();

        foreach (int gridPos in activeCells)
        {
            NumberTile t = GetTileAtIndex(gridPos);
            if (t != null)
                t.GetComponent<RectTransform>().anchoredPosition = CellPosition(gridPos);
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
        for (int tileId = 0; tileId < totalActiveCells; tileId++)
        {
            int gridPos = activeCells[tileId];
            board[gridPos] = tileId;
            tiles[tileId].currentIndex = gridPos;
        }

        emptyIndexes.Clear();
        int firstEmptyId = totalActiveCells - emptyTileCount;
        for (int tileId = firstEmptyId; tileId < totalActiveCells; tileId++)
            emptyIndexes.Add(activeCells[tileId]);

        RefreshVisualPositions();
        RefreshAllColors();
    }

    // ────────────────────────────────────────────────────────────────
    //  Highlights + Shake
    // ────────────────────────────────────────────────────────────────

    private void ShowMovableHighlights()
    {
        ClearHighlights();
        HighlightShownEvent?.Invoke();

        var toHighlight = new HashSet<int>();
        foreach (int emptyPos in emptyIndexes)
            foreach (int n in GetValidNeighbors(emptyPos))
                toHighlight.Add(n);

        foreach (int n in toHighlight)
        {
            NumberTile t = GetTileAtIndex(n);
            if (t == null || t.isEmpty) continue;
            t.SetHighlight(true);
            StartShake(t);
        }
    }

    private void ClearHighlights()
    {
        CancelTargetSelection();

        if (tiles == null) return;
        foreach (NumberTile t in tiles)
        {
            t.SetHighlight(false);
            StopShake(t);
        }
    }

    private void StartShake(NumberTile tile)
    {
        StopShake(tile);
        Coroutine c = StartCoroutine(ShakeTile(tile));
        activeShakes[tile] = c;
    }

    private void StopShake(NumberTile tile)
    {
        if (!activeShakes.TryGetValue(tile, out Coroutine c) || c == null) return;
        StopCoroutine(c);
        activeShakes.Remove(tile);

        RectTransform rt = tile.GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = CanonicalPosition(tile.currentIndex);
    }

    private IEnumerator ShakeTile(NumberTile tile)
    {
        RectTransform rt      = tile.GetComponent<RectTransform>();
        Vector2       origin  = CanonicalPosition(tile.currentIndex);
        float         elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float envelope = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            float offset   = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitude * envelope;
            rt.anchoredPosition = origin + new Vector2(offset, 0f);
            yield return null;
        }

        rt.anchoredPosition = origin;
        activeShakes.Remove(tile);
    }

    // ────────────────────────────────────────────────────────────────
    //  Vitória
    // ────────────────────────────────────────────────────────────────

    private bool CheckWin()
    {
        int firstEmptyId = totalActiveCells - emptyTileCount;

        foreach (int gridPos in activeCells)
        {
            int tileIdHere = board[gridPos];
            int correctTileId = gridPosToCorrectTileId[gridPos];

            if (correctTileId < firstEmptyId)
            {
                if (tileIdHere != correctTileId) return false;
            }
            else
            {
                if (tileIdHere < firstEmptyId) return false;
            }
        }

        return true;
    }

    private void OnPuzzleSolved()
    {
        puzzleSolved = true;

        int elapsedSeconds = Mathf.RoundToInt(Time.time - puzzleStartTime);
        SolvedPuzzleEvent?.Invoke(moveCount, elapsedSeconds);

        foreach (NumberTile t in tiles) t.Refresh();

        if (statusText != null)
            statusText.text = $"🎉 Resolvido em {moveCount} movimentos e {elapsedSeconds}s!";

        Debug.Log($"[NumberPuzzle] Resolvido — movimentos: {moveCount}, tempo: {elapsedSeconds}s");
    }

    // ────────────────────────────────────────────────────────────────
    //  Utilitários
    // ────────────────────────────────────────────────────────────────

    private Vector2 CellPosition(int index)
    {
        int col = index % gridWidth;
        int row = index / gridWidth;
        float x =  boardOrigin.x + gapSize + col * (cellSize + gapSize) + cellSize * 0.5f;
        float y = -(boardOrigin.y + gapSize + row * (cellSize + gapSize) + cellSize * 0.5f);
        return new Vector2(x, y);
    }

    private Vector2 CanonicalPosition(int index)
    {
        return CellPosition(index);
    }

    private bool IsAdjacent(int a, int b)
    {
        int rA = a / gridWidth, cA = a % gridWidth;
        int rB = b / gridWidth, cB = b % gridWidth;
        return Mathf.Abs(rA - rB) + Mathf.Abs(cA - cB) == 1;
    }

    private List<int> GetValidNeighbors(int index)
    {
        var list = new List<int>();
        int r = index / gridWidth, c = index % gridWidth;

        if (r > 0)              TryAddNeighbor(list, (r - 1) * gridWidth + c);
        if (r < gridHeight - 1) TryAddNeighbor(list, (r + 1) * gridWidth + c);
        if (c > 0)              TryAddNeighbor(list, r * gridWidth + (c - 1));
        if (c < gridWidth - 1)  TryAddNeighbor(list, r * gridWidth + (c + 1));

        return list;
    }

    private void TryAddNeighbor(List<int> list, int candidatePos)
    {
        if (!disabledCells.Contains(candidatePos))
            list.Add(candidatePos);
    }

    private NumberTile GetTileAtIndex(int index)
    {
        foreach (NumberTile t in tiles)
            if (t.currentIndex == index) return t;
        return null;
    }

    private void UpdateMovesUI()
    {
        if (movesText != null) movesText.text = $"Movimentos: {moveCount}";
    }

    // ────────────────────────────────────────────────────────────────
    //  Suporte ao Tutorial
    // ────────────────────────────────────────────────────────────────

    public (RectTransform tile, RectTransform target) GetFirstMovableTileAndTarget()
    {
        NumberTile bestTile = null;
        int bestEmptyPos = -1;
        int bestScore = int.MaxValue;

        foreach (int emptyPos in emptyIndexes)
        {
            foreach (int n in GetValidNeighbors(emptyPos))
            {
                NumberTile t = GetTileAtIndex(n);
                if (t == null || t.isEmpty) continue;

                int correctGridPos = activeCells[t.correctIndex];
                int distanceAfterMove = ManhattanDistance(emptyPos, correctGridPos);

                if (distanceAfterMove < bestScore)
                {
                    bestScore = distanceAfterMove;
                    bestTile = t;
                    bestEmptyPos = emptyPos;
                }
            }
        }

        if (bestTile == null) return (null, null);

        NumberTile emptyTile = GetTileAtIndex(bestEmptyPos);
        return (bestTile.GetComponent<RectTransform>(), emptyTile.GetComponent<RectTransform>());
    }

    private int ManhattanDistance(int a, int b)
    {
        int rA = a / gridWidth, cA = a % gridWidth;
        int rB = b / gridWidth, cB = b % gridWidth;
        return Mathf.Abs(rA - rB) + Mathf.Abs(cA - cB);
    }

    public (RectTransform tile, RectTransform target) GetFirstAmbiguousTileAndTarget()
    {
        foreach (NumberTile tile in tiles)
        {
            if (tile.isEmpty) continue;

            List<int> adjacentEmpties = FindAllAdjacentEmpty(tile.currentIndex);
            if (adjacentEmpties.Count >= 2)
            {
                NumberTile emptyTile = GetTileAtIndex(adjacentEmpties[0]);
                return (tile.GetComponent<RectTransform>(), emptyTile.GetComponent<RectTransform>());
            }
        }
        return (null, null);
    }
}