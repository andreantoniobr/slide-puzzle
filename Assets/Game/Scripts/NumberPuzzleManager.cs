using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

// ────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Slide Puzzle numérico — sem sprites, sem corte de imagem.
/// Cada peça exibe seu número (1, 2, 3 … N²-1) e fica verde quando está na posição correta.
///
/// Funcionalidades:
///   - Clique em peça adjacente ao espaço vazio → move diretamente.
///   - Clique em peça na mesma linha/coluna do espaço vazio → encadeia todos os
///     movimentos intermediários automaticamente, um por um.
///   - Swipe/drag → mesmo comportamento do clique via TryMove.
///   - Highlight das peças movíveis com tremidinha e evento de áudio.
///
/// SETUP RÁPIDO (Auto-Setup):
///   1. Crie um GameObject vazio → adicione NumberPuzzleManager + NumberPuzzleAutoSetup.
///   2. Press Play. A cena inteira é montada por código.
///
/// SETUP MANUAL:
///   - boardPanel  → RectTransform do painel container.
///   - tilePrefab  → Prefab com NumberTile + Image (bg) + Text + Image (highlight).
///   - Campos de UI opcionais: movesText, statusText, shuffleButton, solveButton.
/// </summary>
public class NumberPuzzleManager : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────
    //  Inspector
    // ────────────────────────────────────────────────────────────────

    [Header("Tabuleiro")]
    public GameObject    tilePrefab;
    public RectTransform boardPanel;

    [Range(2, 8)]
    public int gridSize = 4;

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
    [Tooltip("Amplitude do shake nas peças destacadas (px).")]
    [SerializeField] private float shakeAmplitude = 5f;
    [Tooltip("Duração total do shake (segundos).")]
    [SerializeField] private float shakeDuration  = 0.35f;
    [Tooltip("Frequência de oscilações por segundo.")]
    [SerializeField] private float shakeFrequency = 28f;

    [Header("UI (opcional)")]
    public Text   movesText;
    public Text   statusText;
    public Button shuffleButton;
    public Button solveButton;

    // ────────────────────────────────────────────────────────────────
    //  Eventos públicos
    // ────────────────────────────────────────────────────────────────

    /// <summary>Disparado sempre que uma peça desliza com sucesso.</summary>
    public static event Action SlidedTileEvent;

    /// <summary>
    /// Disparado quando o highlight de peças movíveis é exibido.
    /// Use para reproduzir um som de feedback (clique inválido / "wobble").
    /// </summary>
    public static event Action HighlightShownEvent;

    // ────────────────────────────────────────────────────────────────
    //  Estado privado
    // ────────────────────────────────────────────────────────────────

    private NumberTile[] tiles;
    private int[]        board;          // board[posição] = índice da peça
    private int          emptyIndex;
    private int          totalTiles;
    private int          moveCount;
    private bool         puzzleSolved;
    private bool         isAnimating;

    // Coroutines de shake ativos, indexados por peça, para cancelamento limpo
    private Dictionary<NumberTile, Coroutine> activeShakes =
        new Dictionary<NumberTile, Coroutine>();

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

            NumberTile tile = go.GetComponent<NumberTile>();
            if (tile.numberText != null)
                tile.numberText.fontSize = fontSize;

            tile.Init(this, i, i, isEmpty);
            tiles[i] = tile;

            if (isEmpty) emptyIndex = i;
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

    /// <summary>
    /// Trata o clique em uma peça.
    ///
    /// Caso 1 — peça adjacente ao espaço vazio:
    ///   Move diretamente (comportamento clássico).
    ///
    /// Caso 2 — peça na mesma linha ou coluna do espaço vazio, mas não adjacente:
    ///   Encadeia automaticamente todos os movimentos intermediários, um por um,
    ///   deslizando cada peça até o espaço chegar à posição da peça clicada.
    ///
    /// Caso 3 — peça fora da linha e coluna do espaço vazio:
    ///   Exibe highlight com shake nas peças movíveis e dispara HighlightShownEvent.
    /// </summary>
    public void OnTileClicked(NumberTile tile)
    {
        if (isAnimating || puzzleSolved) return;

        int tileIdx = tile.currentIndex;

        // ── Caso 1: adjacente ────────────────────────────────────────
        if (IsAdjacent(tileIdx, emptyIndex))
        {
            ClearHighlights();
            StartCoroutine(DoMove(tile, tileIdx));
            return;
        }

        // ── Caso 2: mesma linha ou coluna → movimento em cadeia ──────
        List<NumberTile> chain = BuildMoveChain(tileIdx);
        if (chain != null && chain.Count > 0)
        {
            ClearHighlights();
            StartCoroutine(DoChainMove(chain));
            return;
        }

        // ── Caso 3: peça inacessível → highlight + shake + evento ────
        ShowMovableHighlights();
    }

    // ────────────────────────────────────────────────────────────────
    //  Input — Swipe
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna a direção em que a peça pode deslizar (espaço vazio adjacente).
    /// Retorna <see cref="DragDirection.None"/> se não houver espaço vazio adjacente.
    /// </summary>
    public DragDirection GetAllowedDirection(NumberTile tile)
    {
        int tileIdx  = tile.currentIndex;
        int emptyIdx = emptyIndex;

        int rTile  = tileIdx  / gridSize;
        int cTile  = tileIdx  % gridSize;
        int rEmpty = emptyIdx / gridSize;
        int cEmpty = emptyIdx % gridSize;

        int dr = rEmpty - rTile;
        int dc = cEmpty - cTile;

        if (Mathf.Abs(dr) + Mathf.Abs(dc) != 1) return DragDirection.None;

        if (dr ==  1 && dc ==  0) return DragDirection.Down;
        if (dr == -1 && dc ==  0) return DragDirection.Up;
        if (dr ==  0 && dc ==  1) return DragDirection.Right;
        if (dr ==  0 && dc == -1) return DragDirection.Left;

        return DragDirection.None;
    }

    /// <summary>
    /// Tenta mover a peça na direção indicada pelo swipe.
    /// Retorna <c>true</c> se o movimento foi executado com sucesso.
    /// </summary>
    public bool TryMove(NumberTile tile, DragDirection direction)
    {
        if (isAnimating || puzzleSolved) return false;

        int tileIdx = tile.currentIndex;

        // ── Caso 1: adjacente ao espaço vazio ────────────────────────────
        DragDirection allowed = GetAllowedDirection(tile);
        if (allowed != DragDirection.None && allowed == direction)
        {
            ClearHighlights();
            StartCoroutine(DoMove(tile, tileIdx));
            return true;
        }

        // ── Caso 2: mesma linha/coluna → verifica se o swipe aponta para o vazio
        List<NumberTile> chain = BuildMoveChain(tileIdx);
        if (chain == null || chain.Count == 0) return false;

        // Confirma que a direção do swipe é compatível com a direção da cadeia
        DragDirection chainDir = GetChainDirection(tileIdx);
        if (chainDir == DragDirection.None || chainDir != direction) return false;

        ClearHighlights();
        StartCoroutine(DoChainMove(chain));
        return true;
    }

    /// <summary>
    /// Retorna a direção em que a peça precisa se mover para chegar ao espaço vazio
    /// (mesma linha ou coluna). Usado para validar o swipe em movimentos em cadeia.
    /// </summary>
    private DragDirection GetChainDirection(int tileIndex)
    {
        int rTile  = tileIndex  / gridSize;
        int cTile  = tileIndex  % gridSize;
        int rEmpty = emptyIndex / gridSize;
        int cEmpty = emptyIndex % gridSize;

        if (rTile == rEmpty && cEmpty > cTile) return DragDirection.Right;
        if (rTile == rEmpty && cEmpty < cTile) return DragDirection.Left;
        if (cTile == cEmpty && rEmpty > rTile) return DragDirection.Down;
        if (cTile == cEmpty && rEmpty < rTile) return DragDirection.Up;

        return DragDirection.None;
    }

    // ────────────────────────────────────────────────────────────────
    //  Movimento em cadeia (mesma linha/coluna)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Constrói a lista ordenada de peças que precisam ser movidas para que o
    /// espaço vazio chegue até <paramref name="targetIndex"/>.
    ///
    /// Percorre a linha ou coluna entre o espaço vazio e a peça clicada,
    /// listando cada peça intermediária + a peça clicada, na ordem correta
    /// de execução (a mais próxima do espaço vazio primeiro).
    ///
    /// Retorna null se a peça não estiver na mesma linha nem coluna.
    /// </summary>
    private List<NumberTile> BuildMoveChain(int targetIndex)
    {
        int rTarget = targetIndex / gridSize;
        int cTarget = targetIndex % gridSize;
        int rEmpty  = emptyIndex  / gridSize;
        int cEmpty  = emptyIndex  % gridSize;

        // Mesma linha
        if (rTarget == rEmpty && cTarget != cEmpty)
        {
            var chain = new List<NumberTile>();
            int step  = cTarget > cEmpty ? 1 : -1;

            for (int c = cEmpty + step; step > 0 ? c <= cTarget : c >= cTarget; c += step)
            {
                int   idx  = rEmpty * gridSize + c;
                NumberTile t = GetTileAtIndex(idx);
                if (t != null && !t.isEmpty) chain.Add(t);
            }
            return chain;
        }

        // Mesma coluna
        if (cTarget == cEmpty && rTarget != rEmpty)
        {
            var chain = new List<NumberTile>();
            int step  = rTarget > rEmpty ? 1 : -1;

            for (int r = rEmpty + step; step > 0 ? r <= rTarget : r >= rTarget; r += step)
            {
                int   idx  = r * gridSize + cEmpty;
                NumberTile t = GetTileAtIndex(idx);
                if (t != null && !t.isEmpty) chain.Add(t);
            }
            return chain;
        }

        return null;
    }

    /// <summary>
    /// Executa todos os movimentos da cadeia em paralelo — as peças da linha/coluna
    /// deslizam simultaneamente, dando a impressão de que a linha inteira se move.
    ///
    /// Para isso não podemos usar DoMove diretamente (ele controla isAnimating e
    /// altera o estado do board peça por peça), então a lógica de estado é aplicada
    /// antecipadamente para todas as peças e a animação corre em paralelo.
    /// </summary>
    private IEnumerator DoChainMove(List<NumberTile> chain)
    {
        isAnimating = true;

        // ── 1. Pré-calcula as posições de origem e destino de cada peça ──
        //       e atualiza toda a lógica do board de uma vez, antes de animar.

        float panelW = boardPanel.rect.width;
        float panelH = boardPanel.rect.height;
        float cellW  = (panelW - gapSize * (gridSize + 1)) / gridSize;
        float cellH  = (panelH - gapSize * (gridSize + 1)) / gridSize;

        // Snapshot: para cada peça da cadeia, guarda (from, to) em anchoredPosition
        var moves = new List<(RectTransform rt, Vector2 from, Vector2 to)>();

        // Aplica os swaps lógicos em ordem — o emptyIndex avança a cada swap
        foreach (NumberTile tile in chain)
        {
            int fromIndex = tile.currentIndex;

            NumberTile    emptyTile = GetTileAtIndex(emptyIndex);
            RectTransform tileRT    = tile.GetComponent<RectTransform>();
            RectTransform emptyRT   = emptyTile.GetComponent<RectTransform>();

            Vector2 startPos  = CellPosition(fromIndex, cellW, cellH);
            Vector2 targetPos = CellPosition(emptyIndex, cellW, cellH);

            moves.Add((tileRT, startPos, targetPos));

            // Atualiza estado lógico
            board[emptyIndex]      = board[fromIndex];
            board[fromIndex]       = totalTiles - 1;
            tile.currentIndex      = emptyIndex;
            emptyTile.currentIndex = fromIndex;
            emptyIndex             = fromIndex;

            moveCount++;

            SlidedTileEvent?.Invoke();
        }

        UpdateMovesUI();

        // ── 2. Esconde o espaço vazio durante a animação ─────────────
        NumberTile emptyTileFinal = GetTileAtIndex(emptyIndex);
        RectTransform emptyRTFinal = emptyTileFinal.GetComponent<RectTransform>();
        emptyRTFinal.gameObject.SetActive(false);

        // ── 3. Anima todas as peças em paralelo ──────────────────────
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));

            foreach (var (rt, from, to) in moves)
                rt.anchoredPosition = Vector2.Lerp(from, to, t);

            yield return null;
        }

        // Garante posição final exata
        foreach (var (rt, _, to) in moves)
            rt.anchoredPosition = to;

        // ── 4. Reativa o espaço vazio na posição correta ─────────────
        emptyRTFinal.anchoredPosition = CellPosition(emptyIndex, cellW, cellH);
        emptyRTFinal.gameObject.SetActive(true);

        // ── 5. Atualiza cores e verifica vitória ─────────────────────
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

    private IEnumerator DoMove(NumberTile tile, int fromIndex, Action onComplete = null)
    {
        isAnimating = true;

        SlidedTileEvent?.Invoke();

        NumberTile    emptyTile = GetTileAtIndex(emptyIndex);
        RectTransform tileRT    = tile.GetComponent<RectTransform>();
        RectTransform emptyRT   = emptyTile.GetComponent<RectTransform>();

        Vector2 startPos  = tileRT.anchoredPosition;
        Vector2 targetPos = emptyRT.anchoredPosition;

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
            board[i]              = i;
            tiles[i].currentIndex = i;
        }
        emptyIndex = totalTiles - 1;
        RefreshVisualPositions();
        RefreshAllColors();
    }

    // ────────────────────────────────────────────────────────────────
    //  Highlights + Shake
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exibe o highlight nas peças movíveis, inicia a tremidinha em cada uma
    /// e dispara <see cref="HighlightShownEvent"/> para feedback de áudio.
    /// </summary>
    private void ShowMovableHighlights()
    {
        ClearHighlights();

        HighlightShownEvent?.Invoke();

        foreach (int n in GetValidNeighbors(emptyIndex))
        {
            NumberTile t = GetTileAtIndex(n);
            if (t == null || t.isEmpty) continue;

            t.SetHighlight(true);
            StartShake(t);
        }
    }

    private void ClearHighlights()
    {
        if (tiles == null) return;

        foreach (NumberTile t in tiles)
        {
            t.SetHighlight(false);
            StopShake(t);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Shake
    // ────────────────────────────────────────────────────────────────

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

        // Garante que a peça volta à posição canônica após o shake ser interrompido
        RectTransform rt = tile.GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = CanonicalPosition(tile.currentIndex);
    }

    /// <summary>
    /// Oscila a peça horizontalmente com amplitude decrescente (envelope linear),
    /// sem nunca deslocar permanentemente a anchoredPosition canônica.
    /// </summary>
    private IEnumerator ShakeTile(NumberTile tile)
    {
        RectTransform rt     = tile.GetComponent<RectTransform>();
        Vector2       origin = CanonicalPosition(tile.currentIndex);
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
        for (int i = 0; i < totalTiles; i++)
            if (board[i] != i) return false;
        return true;
    }

    private void OnPuzzleSolved()
    {
        puzzleSolved = true;
        foreach (NumberTile t in tiles) t.Refresh();
        if (statusText != null)
            statusText.text = $"🎉 Resolvido em {moveCount} movimentos!";
        Debug.Log($"[NumberPuzzle] Resolvido em {moveCount} movimentos!");
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

    /// <summary>
    /// Calcula a anchoredPosition canônica de um índice usando o tamanho atual do painel.
    /// Usado pelo shake para restaurar a posição correta sem depender de estado externo.
    /// </summary>
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

    // ────────────────────────────────────────────────────────────────
    //  UI
    // ────────────────────────────────────────────────────────────────

    private void UpdateMovesUI()
    {
        if (movesText != null) movesText.text = $"Movimentos: {moveCount}";
    }
}