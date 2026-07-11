using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

public class NumberPuzzleManager : MonoBehaviour
{
    [Header("Tabuleiro")]
    public GameObject    tilePrefab;
    public RectTransform boardPanel;

    [Range(2, 8)]
    public int gridSize = 4;

    [Header("Espaços Vazios")]
    [Range(1, 4)]
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
    private NumberTile[] tiles;
    private int[]        board;
    private List<int>    emptyIndexes = new List<int>();
    private int          totalTiles;
    private int          moveCount;
    private bool         puzzleSolved;
    private bool         isAnimating;

    private NumberTile pendingSelectionTile;

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
    //  Construção
    // ────────────────────────────────────────────────────────────────

    public void BuildBoard()
    {
        ClearBoard();

        totalTiles = gridSize * gridSize;
        board      = new int[totalTiles];
        tiles      = new NumberTile[totalTiles];
        emptyIndexes.Clear();

        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW  = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH  = (panelH - gapSize * (gridSize + 1)) / gridSize;

        int fontSize = Mathf.Clamp(Mathf.RoundToInt(cellW * fontSizePercent), minFontSize, maxFontSize);
        int firstEmptyId = totalTiles - emptyTileCount;

        if (backgroundController != null)
        {
            backgroundController.SetVisible(showBackgroundNumbers);
            if (showBackgroundNumbers)
                backgroundController.Build(firstEmptyId, CellPosition, cellW, cellH, fontSize);
        }

        for (int i = 0; i < totalTiles; i++)
        {
            board[i] = i;
            bool isEmpty = (i >= firstEmptyId);

            GameObject go = Instantiate(tilePrefab, boardPanel);
            go.name = isEmpty ? $"Tile_Empty_{i}" : $"Tile_{i + 1}";

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.sizeDelta  = new Vector2(cellW, cellH);
            rt.anchoredPosition = CellPosition(i, cellW, cellH);

            NumberTile tile = go.GetComponent<NumberTile>();
            if (tile.numberText != null)
                tile.numberText.fontSize = fontSize;

            tile.Init(this, i, i, isEmpty);
            tiles[i] = tile;

            if (isEmpty) emptyIndexes.Add(i);
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
            CancelTargetSelection(); // clicou em outra peça — cancela a seleção anterior

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
            return true; // não é "inválido" — está aguardando escolha
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
        CancelTargetSelection(); // por segurança, limpa qualquer seleção anterior pendente

        pendingSelectionTile = tile;

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

    /// <summary>
    /// Chamado pelo NumberTile assim que o jogador toca em qualquer peça (PointerDown),
    /// independente de o toque resultar em movimento. Marca o início real de gameplay.
    /// </summary>
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
        int rTile = tileIdx / gridSize, cTile = tileIdx % gridSize;
        int rEmpty = emptyIdx / gridSize, cEmpty = emptyIdx % gridSize;

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

        int rTile  = tileIndex  / gridSize;
        int cTile  = tileIndex  % gridSize;
        int rEmpty = emptyIdx / gridSize;
        int cEmpty = emptyIdx % gridSize;

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

        int rTarget = targetIndex / gridSize;
        int cTarget = targetIndex % gridSize;
        int rEmpty  = emptyIdx    / gridSize;
        int cEmpty  = emptyIdx    % gridSize;

        if (rTarget == rEmpty && cTarget != cEmpty)
        {
            var chain = new List<NumberTile>();
            int step  = cTarget > cEmpty ? 1 : -1;
            for (int c = cEmpty + step; step > 0 ? c <= cTarget : c >= cTarget; c += step)
            {
                int        idx = rEmpty * gridSize + c;
                NumberTile t   = GetTileAtIndex(idx);
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
                int        idx = r * gridSize + cEmpty;
                NumberTile t   = GetTileAtIndex(idx);
                if (t != null && !t.isEmpty) chain.Add(t);
            }
            return chain;
        }

        return null;
    }

    private IEnumerator DoChainMove(List<NumberTile> chain)
    {
        isAnimating = true;

        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW  = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH  = (panelH - gapSize * (gridSize + 1)) / gridSize;

        var moves = new List<(RectTransform rt, Vector2 from, Vector2 to)>();

        foreach (NumberTile tile in chain)
        {
            int fromIndex   = tile.currentIndex;
            int emptyIdx    = emptyIndexes[0];

            NumberTile    emptyTile = GetTileAtIndex(emptyIdx);
            RectTransform tileRT    = tile.GetComponent<RectTransform>();

            Vector2 startPos  = CellPosition(fromIndex, cellW, cellH);
            Vector2 targetPos = CellPosition(emptyIdx, cellW, cellH);

            moves.Add((tileRT, startPos, targetPos));

            board[emptyIdx]         = board[fromIndex];
            board[fromIndex]        = emptyTile.correctIndex;
            tile.currentIndex       = emptyIdx;
            emptyTile.currentIndex  = fromIndex;

            emptyIndexes[0] = fromIndex; // move o vazio pra posição que a peça deixou

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

        emptyRTFinal.anchoredPosition = CellPosition(finalEmptyIdx, cellW, cellH);
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
            neighbors.RemoveAll(n => emptyIndexes.Contains(n)); // não troca vazio com vazio

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

    // embaralha de forma determinística (mesmo seed = mesmo resultado)
    public void ShuffleDeterministic(int seed)
    {
        UnityEngine.Random.State previousState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(seed);
        Shuffle();
        UnityEngine.Random.state = previousState; // não polui o RNG global
    }

    // carrega um nível a partir de um LevelConfig (feito à mão ou procedural)
    public void LoadLevel(LevelConfig config)
    {
        gridSize = Mathf.Clamp(config.gridSize, 2, 8);
        if (config.shuffleMoves > 0) shuffleMoves = config.shuffleMoves;

        int maxEmpty = Mathf.Max(1, (gridSize * gridSize) / 2);
        emptyTileCount = Mathf.Clamp(config.emptyTileCount > 0 ? config.emptyTileCount : 1, 1, maxEmpty);

        BuildBoard();

        if (config.customBoard != null && config.customBoard.Length == gridSize * gridSize)
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
        for (int i = 0; i < totalTiles; i++)
        {
            int idx = arrangement[i];
            tiles[i].currentIndex = idx;
            board[idx] = i;
            if (tiles[i].isEmpty) emptyIndexes.Add(idx);
        }

        RefreshVisualPositions();
        RefreshAllColors();
        UpdateMovesUI();

        SaveInitialState();
        puzzleStartTime = Time.time;
        PuzzleStartedEvent?.Invoke();
    }

    // reinicia o nível atual EXATAMENTE como estava no começo (sem re-embaralhar)
    public void RestartLevel()
    {
        if (initialTileIndexes == null || initialTileIndexes.Length != totalTiles)
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
        for (int i = 0; i < totalTiles; i++)
        {
            int idx = initialTileIndexes[i];
            tiles[i].currentIndex = idx;
            board[idx] = i;
            if (tiles[i].isEmpty) emptyIndexes.Add(idx);
        }

        RefreshVisualPositions();
        RefreshAllColors();
        UpdateMovesUI();

        puzzleStartTime = Time.time;
        PuzzleStartedEvent?.Invoke();
    }

    private void SaveInitialState()
    {
        initialTileIndexes = new int[totalTiles];
        for (int i = 0; i < totalTiles; i++)
            initialTileIndexes[i] = tiles[i].currentIndex;
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
            board[i]              = i;
            tiles[i].currentIndex = i;
        }

        emptyIndexes.Clear();
        int firstEmptyId = totalTiles - emptyTileCount;
        for (int id = firstEmptyId; id < totalTiles; id++)
            emptyIndexes.Add(id);

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
        int firstEmptyId = totalTiles - emptyTileCount;

        for (int i = 0; i < totalTiles; i++)
        {
            if (i < firstEmptyId)
            {
                // célula numerada: precisa ser exatamente a peça correta
                if (board[i] != i) return false;
            }
            else
            {
                // célula de espaço vazio: qualquer peça vazia serve aqui, não precisa ser a MESMA identidade
                if (board[i] < firstEmptyId) return false;
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

    private Vector2 CellPosition(int index, float cellW, float cellH)
    {
        float x =  gapSize + (index % gridSize) * (cellW + gapSize) + cellW * 0.5f;
        float y = -(gapSize + (index / gridSize) * (cellH + gapSize) + cellH * 0.5f);
        return new Vector2(x, y);
    }

    private Vector2 CanonicalPosition(int index)
    {
        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW  = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH  = (panelH - gapSize * (gridSize + 1)) / gridSize;
        return CellPosition(index, cellW, cellH);
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

    private void UpdateMovesUI()
    {
        if (movesText != null) movesText.text = $"Movimentos: {moveCount}";
    }
}