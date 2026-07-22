using System.Collections.Generic;
using UnityEngine;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Constrói o(s) caminho(s) de contorno (PathPoint loops) a partir do
    /// formato do tabuleiro (grid + células ativas). Substitui a extração
    /// de contorno que antes vivia dentro do BoardFrameMesh — agora isolada,
    /// testável, e sem nenhuma responsabilidade de mesh/UV/textura.
    ///
    /// Suporta múltiplos loops (por exemplo, se o tabuleiro tiver mais de
    /// uma "ilha" desconectada de células ativas — caso raro, mas suportado).
    /// </summary>
    public static class PathBuilder
    {
        /// <summary>
        /// Extrai o(s) loop(s) de contorno do formato do tabuleiro.
        /// </summary>
        /// <param name="gridWidth">Largura do grid (colunas).</param>
        /// <param name="gridHeight">Altura do grid (linhas).</param>
        /// <param name="activePositions">Conjunto de posições (row*gridWidth+col) que existem no tabuleiro.</param>
        /// <param name="cellCenterFunc">Função que converte uma posição de grid no centro visual daquela célula.</param>
        /// <param name="cellSize">Tamanho de cada célula (assumido quadrado, largura = altura).</param>
        public static List<List<PathPoint>> BuildBoundaryPaths(
            int gridWidth, int gridHeight, HashSet<int> activePositions,
            System.Func<int, Vector2> cellCenterFunc, float cellSize)
        {
            float halfCell = cellSize * 0.5f;

            Vector2 GetVertexPos(int vr, int vc)
            {
                if (IsActive(activePositions, gridWidth, gridHeight, vr, vc))
                {
                    Vector2 c = cellCenterFunc(vr * gridWidth + vc);
                    return new Vector2(c.x - halfCell, c.y + halfCell);
                }
                if (IsActive(activePositions, gridWidth, gridHeight, vr - 1, vc - 1))
                {
                    Vector2 c = cellCenterFunc((vr - 1) * gridWidth + (vc - 1));
                    return new Vector2(c.x + halfCell, c.y - halfCell);
                }
                if (IsActive(activePositions, gridWidth, gridHeight, vr - 1, vc))
                {
                    Vector2 c = cellCenterFunc((vr - 1) * gridWidth + vc);
                    return new Vector2(c.x - halfCell, c.y - halfCell);
                }
                if (IsActive(activePositions, gridWidth, gridHeight, vr, vc - 1))
                {
                    Vector2 c = cellCenterFunc(vr * gridWidth + (vc - 1));
                    return new Vector2(c.x + halfCell, c.y + halfCell);
                }
                return Vector2.zero;
            }

            // Coleta todas as arestas expostas (onde uma célula ativa encosta
            // num buraco ou na borda do grid), já orientadas de forma consistente
            // (sentido horário visual ao redor da célula ativa).
            var edges = new List<(VertexKey a, VertexKey b)>();

            for (int r = 0; r < gridHeight; r++)
            {
                for (int c = 0; c < gridWidth; c++)
                {
                    if (!activePositions.Contains(r * gridWidth + c)) continue;

                    if (!IsActive(activePositions, gridWidth, gridHeight, r - 1, c))
                        edges.Add((new VertexKey(r, c), new VertexKey(r, c + 1)));
                    if (!IsActive(activePositions, gridWidth, gridHeight, r + 1, c))
                        edges.Add((new VertexKey(r + 1, c + 1), new VertexKey(r + 1, c)));
                    if (!IsActive(activePositions, gridWidth, gridHeight, r, c - 1))
                        edges.Add((new VertexKey(r + 1, c), new VertexKey(r, c)));
                    if (!IsActive(activePositions, gridWidth, gridHeight, r, c + 1))
                        edges.Add((new VertexKey(r, c + 1), new VertexKey(r + 1, c + 1)));
                }
            }

            var adjacency = new Dictionary<VertexKey, List<VertexKey>>();
            foreach (var e in edges)
            {
                if (!adjacency.TryGetValue(e.a, out var list))
                {
                    list = new List<VertexKey>();
                    adjacency[e.a] = list;
                }
                list.Add(e.b);
            }

            var visited = new HashSet<(VertexKey, VertexKey)>();
            var loops = new List<List<PathPoint>>();

            foreach (var startVertex in new List<VertexKey>(adjacency.Keys))
            {
                foreach (var firstNext in adjacency[startVertex])
                {
                    if (visited.Contains((startVertex, firstNext))) continue;

                    var loopVerts = new List<VertexKey> { startVertex };
                    var current = startVertex;
                    var next = firstNext;

                    while (true)
                    {
                        visited.Add((current, next));
                        loopVerts.Add(next);

                        if (next.Equals(startVertex)) break;

                        if (!adjacency.TryGetValue(next, out var candidates) || candidates.Count == 0)
                            break;

                        VertexKey? chosen = null;
                        foreach (var cand in candidates)
                        {
                            if (!visited.Contains((next, cand))) { chosen = cand; break; }
                        }
                        if (chosen == null) break;

                        current = next;
                        next = chosen.Value;
                    }

                    bool closedProperly = loopVerts.Count >= 4 && loopVerts[0].Equals(loopVerts[loopVerts.Count - 1]);
                    if (closedProperly)
                    {
                        loopVerts.RemoveAt(loopVerts.Count - 1);

                        var pathPoints = new List<PathPoint>(loopVerts.Count);
                        for (int i = 0; i < loopVerts.Count; i++)
                        {
                            Vector2 pos = GetVertexPos(loopVerts[i].row, loopVerts[i].col);
                            pathPoints.Add(new PathPoint(pos, i));
                        }
                        loops.Add(pathPoints);
                    }
                }
            }

            return loops;
        }

        private static bool IsActive(HashSet<int> activePositions, int gridWidth, int gridHeight, int r, int c)
        {
            if (r < 0 || r >= gridHeight || c < 0 || c >= gridWidth) return false;
            return activePositions.Contains(r * gridWidth + c);
        }

        /// <summary>Chave de vértice de grid (linha, coluna) — usada só internamente para montar a adjacência.</summary>
        private readonly struct VertexKey : System.IEquatable<VertexKey>
        {
            public readonly int row;
            public readonly int col;

            public VertexKey(int row, int col)
            {
                this.row = row;
                this.col = col;
            }

            public bool Equals(VertexKey other) => row == other.row && col == other.col;
            public override bool Equals(object obj) => obj is VertexKey other && Equals(other);
            public override int GetHashCode() => row * 73856093 ^ col * 19349663;
        }
    }
}