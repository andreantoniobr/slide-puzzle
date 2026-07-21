using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardOutlineController : MonoBehaviour
{
    [Header("Prefab Base (um só, com Image + RectTransform)")]
    [SerializeField] private GameObject edgePrefab;
    [SerializeField] private GameObject cornerPrefab;

    [Header("Sprites de Aresta — Cantos Convexos (externos)")]
    [SerializeField] private Sprite edgeTopSprite;
    [SerializeField] private Sprite edgeBottomSprite;
    [SerializeField] private Sprite edgeLeftSprite;
    [SerializeField] private Sprite edgeRightSprite;

    [Header("Sprites de Canto — Convexos (externos)")]
    [SerializeField] private Sprite cornerTopLeftSprite;
    [SerializeField] private Sprite cornerTopRightSprite;
    [SerializeField] private Sprite cornerBottomLeftSprite;
    [SerializeField] private Sprite cornerBottomRightSprite;

    [Header("Sprites de Canto — Côncavos (internos, tipo 'L')")]
    [Tooltip("Usados em vértices onde 3 células estão ativas e 1 é buraco — a 'quina interna' do formato.")]
    [SerializeField] private Sprite concaveCornerTopLeftSprite;
    [SerializeField] private Sprite concaveCornerTopRightSprite;
    [SerializeField] private Sprite concaveCornerBottomLeftSprite;
    [SerializeField] private Sprite concaveCornerBottomRightSprite;

    [Header("Aparência")]
    [SerializeField] private float borderThickness = 20f;

    [Header("Ajuste Manual de Encaixe")]
    [Tooltip("Desloca edges/corners para fora (+) ou para dentro (-), relativo ao background. Use para eliminar gap ou sobreposição.")]
    [SerializeField] private float backgroundGapOffset = 0f;

    [Tooltip("Quanto cada aresta encolhe ao chegar num canto CÔNCAVO (interno), para abrir espaço para a peça de canto sem sobrepor.")]
    [SerializeField] private float concaveEdgeTrim = 10f;

    [SerializeField] private RectTransform container;

    private readonly List<GameObject> spawnedPieces = new List<GameObject>();

    public void Build(int gridWidth, int gridHeight, HashSet<int> activePositions,
                       System.Func<int, Vector2> cellCenterFunc, float cellSize, float gapSize)
    {
        Clear();
        if (container == null) return;

        _gridWidth = gridWidth;
        _gridHeight = gridHeight;
        _activePositions = activePositions;

        BuildMergedEdges(gridWidth, gridHeight, activePositions, cellCenterFunc, cellSize, gapSize);
        BuildCorners(gridWidth, gridHeight, activePositions, cellCenterFunc, cellSize);
    }

    public void Clear()
    {
        foreach (GameObject go in spawnedPieces)
            if (go != null) Destroy(go);
        spawnedPieces.Clear();
    }

    private int _gridWidth, _gridHeight;
    private HashSet<int> _activePositions;

    // ────────────────────────────────────────────────────────────────
    //  Arestas — mescladas, com encolhimento nas pontas côncavas
    // ────────────────────────────────────────────────────────────────

    private void BuildMergedEdges(int gridWidth, int gridHeight, HashSet<int> activePositions,
                                   System.Func<int, Vector2> cellCenterFunc, float cellSize, float gapSize)
    {
        float halfCell = cellSize * 0.5f;
        float halfThickness = borderThickness * 0.5f;
        float outward = halfCell + halfThickness + backgroundGapOffset;
        float step = cellSize + gapSize;

        for (int r = 0; r < gridHeight; r++)
        {
            MergeRunsInRow(r, gridWidth, activePositions, cellCenterFunc, outward, step,
                c => !IsActive(activePositions, gridWidth, gridHeight, r - 1, c),
                edgeTopSprite, new Vector2(0f, 1f), isTopEdge: true);

            MergeRunsInRow(r, gridWidth, activePositions, cellCenterFunc, outward, step,
                c => !IsActive(activePositions, gridWidth, gridHeight, r + 1, c),
                edgeBottomSprite, new Vector2(0f, -1f), isTopEdge: false);
        }

        for (int c = 0; c < gridWidth; c++)
        {
            MergeRunsInColumn(c, gridHeight, gridWidth, activePositions, cellCenterFunc, outward, step,
                r => !IsActive(activePositions, gridWidth, gridHeight, r, c - 1),
                edgeLeftSprite, new Vector2(-1f, 0f), isLeftEdge: true);

            MergeRunsInColumn(c, gridHeight, gridWidth, activePositions, cellCenterFunc, outward, step,
                r => !IsActive(activePositions, gridWidth, gridHeight, r, c + 1),
                edgeRightSprite, new Vector2(1f, 0f), isLeftEdge: false);
        }
    }

    private void MergeRunsInRow(int r, int gridWidth, HashSet<int> activePositions,
                                 System.Func<int, Vector2> cellCenterFunc, float outward, float step,
                                 System.Func<int, bool> checkNeighborInactive, Sprite sprite, Vector2 offsetDirection,
                                 bool isTopEdge)
    {
        int runStart = -1;

        for (int c = 0; c <= gridWidth; c++)
        {
            bool isActiveHere = c < gridWidth && activePositions.Contains(r * gridWidth + c);
            bool needsEdge = isActiveHere && checkNeighborInactive(c);

            if (needsEdge && runStart == -1)
                runStart = c;

            bool runEnds = !needsEdge && runStart != -1;
            if (runEnds || (needsEdge && c == gridWidth - 1))
            {
                int runEnd = runEnds ? c - 1 : c;

                // Vértice na ponta esquerda do run: (r ou r+1 dependendo do lado, coluna runStart)
                int vrStart = isTopEdge ? r : r + 1;
                bool startIsConcave = IsConcaveVertex(vrStart, runStart);

                // Vértice na ponta direita do run: coluna runEnd+1
                int vrEnd = isTopEdge ? r : r + 1;
                bool endIsConcave = IsConcaveVertex(vrEnd, runEnd + 1);

                SpawnMergedEdge(sprite, r * gridWidth + runStart, r * gridWidth + runEnd,
                    cellCenterFunc, outward, step, offsetDirection, horizontal: true,
                    trimStart: startIsConcave, trimEnd: endIsConcave);

                runStart = -1;
            }
        }
    }

    private void MergeRunsInColumn(int c, int gridHeight, int gridWidth, HashSet<int> activePositions,
                                    System.Func<int, Vector2> cellCenterFunc, float outward, float step,
                                    System.Func<int, bool> checkNeighborInactive, Sprite sprite, Vector2 offsetDirection,
                                    bool isLeftEdge)
    {
        int runStart = -1;

        for (int r = 0; r <= gridHeight; r++)
        {
            bool isActiveHere = r < gridHeight && activePositions.Contains(r * gridWidth + c);
            bool needsEdge = isActiveHere && checkNeighborInactive(r);

            if (needsEdge && runStart == -1)
                runStart = r;

            bool runEnds = !needsEdge && runStart != -1;
            if (runEnds || (needsEdge && r == gridHeight - 1))
            {
                int runEnd = runEnds ? r - 1 : r;

                int vcStart = isLeftEdge ? c : c + 1;
                bool startIsConcave = IsConcaveVertex(runStart, vcStart);

                int vcEnd = isLeftEdge ? c : c + 1;
                bool endIsConcave = IsConcaveVertex(runEnd + 1, vcEnd);

                SpawnMergedEdge(sprite, runStart * gridWidth + c, runEnd * gridWidth + c,
                    cellCenterFunc, outward, step, offsetDirection, horizontal: false,
                    trimStart: startIsConcave, trimEnd: endIsConcave);

                runStart = -1;
            }
        }
    }

    private bool IsConcaveVertex(int vr, int vc)
    {
        bool topLeft     = IsActive(_activePositions, _gridWidth, _gridHeight, vr - 1, vc - 1);
        bool topRight    = IsActive(_activePositions, _gridWidth, _gridHeight, vr - 1, vc);
        bool bottomLeft  = IsActive(_activePositions, _gridWidth, _gridHeight, vr, vc - 1);
        bool bottomRight = IsActive(_activePositions, _gridWidth, _gridHeight, vr, vc);

        int activeCount = (topLeft?1:0)+(topRight?1:0)+(bottomLeft?1:0)+(bottomRight?1:0);
        return activeCount == 3; // côncavo/reflexo
    }

    private void SpawnMergedEdge(Sprite sprite, int startPos, int endPos,
                                  System.Func<int, Vector2> cellCenterFunc, float outward, float step,
                                  Vector2 offsetDirection, bool horizontal, bool trimStart, bool trimEnd)
    {
        if (edgePrefab == null || sprite == null) return;

        Vector2 startCenter = cellCenterFunc(startPos);
        Vector2 endCenter = cellCenterFunc(endPos);

        int cellCount = 1 + Mathf.RoundToInt(horizontal
            ? Mathf.Abs(endCenter.x - startCenter.x) / step
            : Mathf.Abs(endCenter.y - startCenter.y) / step);

        float length = cellCount * step;

        // Encolhe as pontas que terminam em vértice côncavo, abrindo espaço pro canto interno
        float trimAtStart = trimStart ? concaveEdgeTrim : 0f;
        float trimAtEnd = trimEnd ? concaveEdgeTrim : 0f;
        length -= (trimAtStart + trimAtEnd);

        Vector2 midPoint = (startCenter + endCenter) * 0.5f + offsetDirection * outward;

        // Desloca o centro do segmento na direção do eixo para compensar o encolhimento assimétrico
        float axisShift = (trimAtEnd - trimAtStart) * 0.5f;
        Vector2 axisDir = horizontal ? Vector2.right : Vector2.up;
        // Sinal: se endCenter está "antes" de startCenter no eixo (grid crescente), inverte
        float sign = horizontal
            ? Mathf.Sign(endCenter.x - startCenter.x == 0 ? 1 : endCenter.x - startCenter.x)
            : Mathf.Sign(endCenter.y - startCenter.y == 0 ? 1 : endCenter.y - startCenter.y);
        midPoint += axisDir * axisShift * sign;

        GameObject go = Instantiate(edgePrefab, container);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = horizontal
            ? new Vector2(length, borderThickness)
            : new Vector2(borderThickness, length);
        rt.anchoredPosition = midPoint;

        Image img = go.GetComponent<Image>();
        if (img != null) img.sprite = sprite;

        spawnedPieces.Add(go);
    }

    // ────────────────────────────────────────────────────────────────
    //  Cantos — convexos e côncavos tratados separadamente
    // ────────────────────────────────────────────────────────────────

    private void BuildCorners(int gridWidth, int gridHeight, HashSet<int> activePositions,
                               System.Func<int, Vector2> cellCenterFunc, float cellSize)
    {
        for (int vr = 0; vr <= gridHeight; vr++)
        {
            for (int vc = 0; vc <= gridWidth; vc++)
            {
                bool topLeft     = IsActive(activePositions, gridWidth, gridHeight, vr - 1, vc - 1);
                bool topRight    = IsActive(activePositions, gridWidth, gridHeight, vr - 1, vc);
                bool bottomLeft  = IsActive(activePositions, gridWidth, gridHeight, vr, vc - 1);
                bool bottomRight = IsActive(activePositions, gridWidth, gridHeight, vr, vc);

                int activeCount = (topLeft?1:0)+(topRight?1:0)+(bottomLeft?1:0)+(bottomRight?1:0);
                if (activeCount == 0 || activeCount == 4) continue;

                bool isStraightHorizontal = (topLeft == topRight) && (bottomLeft == bottomRight) && (topLeft != bottomLeft);
                bool isStraightVertical   = (topLeft == bottomLeft) && (topRight == bottomRight) && (topLeft != topRight);
                if (isStraightHorizontal || isStraightVertical) continue;

                if (activeCount == 1)
                    SpawnConvexCorner(vr, vc, gridWidth, gridHeight, activePositions, cellCenterFunc, cellSize);
                else // activeCount == 3 → côncavo
                    SpawnConcaveCorner(vr, vc, gridWidth, gridHeight, topLeft, topRight, bottomLeft, bottomRight, cellCenterFunc, cellSize);
            }
        }
    }

    private void SpawnConvexCorner(int vr, int vc, int gridWidth, int gridHeight,
                                    HashSet<int> activePositions,
                                    System.Func<int, Vector2> cellCenterFunc, float cellSize)
    {
        float outward = cellSize * 0.5f + borderThickness * 0.5f + backgroundGapOffset;

        if (IsActive(activePositions, gridWidth, gridHeight, vr, vc))
        {
            Vector2 c = cellCenterFunc(vr * gridWidth + vc);
            SpawnCorner(cornerTopLeftSprite, new Vector2(c.x - outward, c.y + outward));
            return;
        }
        if (IsActive(activePositions, gridWidth, gridHeight, vr - 1, vc - 1))
        {
            Vector2 c = cellCenterFunc((vr - 1) * gridWidth + (vc - 1));
            SpawnCorner(cornerBottomRightSprite, new Vector2(c.x + outward, c.y - outward));
            return;
        }
        if (IsActive(activePositions, gridWidth, gridHeight, vr - 1, vc))
        {
            Vector2 c = cellCenterFunc((vr - 1) * gridWidth + vc);
            SpawnCorner(cornerBottomLeftSprite, new Vector2(c.x - outward, c.y - outward));
            return;
        }
        if (IsActive(activePositions, gridWidth, gridHeight, vr, vc - 1))
        {
            Vector2 c = cellCenterFunc(vr * gridWidth + (vc - 1));
            SpawnCorner(cornerTopRightSprite, new Vector2(c.x + outward, c.y + outward));
        }
    }

    /// <summary>
    /// Canto côncavo (interno): a peça é escolhida pelo quadrante que É BURACO
    /// (o único inativo), pois é ele que define para qual lado a "reentrância" aponta.
    /// A posição do canto é exatamente o próprio vértice (sem deslocamento outward),
    /// já ajustado pelo backgroundGapOffset.
    /// </summary>
    private void SpawnConcaveCorner(int vr, int vc, int gridWidth, int gridHeight,
                                     bool topLeft, bool topRight, bool bottomLeft, bool bottomRight,
                                     System.Func<int, Vector2> cellCenterFunc, float cellSize)
    {
        // Posição do vértice = interpola a partir de qualquer célula ativa adjacente, sem deslocamento outward
        Vector2 vertexPos = GetVertexRawPosition(vr, vc, gridWidth, gridHeight, topLeft, topRight, bottomLeft, bottomRight, cellCenterFunc, cellSize);
        vertexPos += new Vector2(backgroundGapOffset, -backgroundGapOffset) * 0f; // reservado, sem deslocamento direcional aqui

        // O quadrante QUE É BURACO define o sprite (aponta para onde a reentrância "olha")
        if (!topLeft)     { SpawnCorner(concaveCornerTopLeftSprite, vertexPos); return; }
        if (!topRight)     { SpawnCorner(concaveCornerTopRightSprite, vertexPos); return; }
        if (!bottomLeft)  { SpawnCorner(concaveCornerBottomLeftSprite, vertexPos); return; }
        if (!bottomRight) { SpawnCorner(concaveCornerBottomRightSprite, vertexPos); return; }
    }

    private Vector2 GetVertexRawPosition(int vr, int vc, int gridWidth, int gridHeight,
                                          bool topLeft, bool topRight, bool bottomLeft, bool bottomRight,
                                          System.Func<int, Vector2> cellCenterFunc, float cellSize)
    {
        float halfCell = cellSize * 0.5f;

        if (bottomRight)
        {
            Vector2 c = cellCenterFunc(vr * gridWidth + vc);
            return new Vector2(c.x - halfCell, c.y + halfCell);
        }
        if (topLeft)
        {
            Vector2 c = cellCenterFunc((vr - 1) * gridWidth + (vc - 1));
            return new Vector2(c.x + halfCell, c.y - halfCell);
        }
        if (topRight)
        {
            Vector2 c = cellCenterFunc((vr - 1) * gridWidth + vc);
            return new Vector2(c.x - halfCell, c.y - halfCell);
        }
        // bottomLeft
        Vector2 c2 = cellCenterFunc(vr * gridWidth + (vc - 1));
        return new Vector2(c2.x + halfCell, c2.y + halfCell);
    }

    private void SpawnCorner(Sprite sprite, Vector2 position)
    {
        if (cornerPrefab == null || sprite == null) return;

        GameObject go = Instantiate(cornerPrefab, container);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(borderThickness, borderThickness);
        rt.anchoredPosition = position;

        Image img = go.GetComponent<Image>();
        if (img != null) img.sprite = sprite;

        spawnedPieces.Add(go);
    }

    private bool IsActive(HashSet<int> activePositions, int gridWidth, int gridHeight, int r, int c)
    {
        if (r < 0 || r >= gridHeight || c < 0 || c >= gridWidth) return false;
        return activePositions.Contains(r * gridWidth + c);
    }
}