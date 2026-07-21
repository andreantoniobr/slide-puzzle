using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private enum EditMode { Formato, Arranjo }
    private EditMode currentEditMode = EditMode.Formato;

    // positionToTile[gridPos] = tileId (0-based) que está naquela posição do grid.
    // Só é usado/válido para posições que NÃO são buraco.
    private int[] positionToTile;
    private int lastGridWidth = -1;
    private int lastGridHeight = -1;
    private int lastEmptyCount = -1;
    private int lastDisabledCellsHash = -1;

    public override void OnInspectorGUI()
    {
        LevelData data = (LevelData)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        currentEditMode = (EditMode)GUILayout.Toolbar((int)currentEditMode,
            new[] { "1. Formato (buracos)", "2. Arranjo de Peças" });

        int gridWidth = Mathf.Max(1, data.gridWidth);
        int gridHeight = Mathf.Max(1, data.gridHeight);

        if (currentEditMode == EditMode.Formato)
        {
            DrawShapeEditor(data, gridWidth, gridHeight);
            return;
        }

        // ── Modo Arranjo ──
        if (data.mode != LevelData.LevelMode.ArranjoPersonalizado)
        {
            positionToTile = null;
            EditorGUILayout.HelpBox("Mude o Mode para 'ArranjoPersonalizado' para editar o arranjo manualmente.", MessageType.Info);
            return;
        }

        int totalActiveCells = data.TotalActiveCells;
        int emptyCount = Mathf.Clamp(data.emptyTileCount, 1, Mathf.Max(1, totalActiveCells / 2));
        int disabledHash = ComputeDisabledCellsHash(data.disabledCells);

        bool needsReload = positionToTile == null
            || lastGridWidth != gridWidth
            || lastGridHeight != gridHeight
            || lastEmptyCount != emptyCount
            || lastDisabledCellsHash != disabledHash;

        if (needsReload)
        {
            LoadWorkingStateFrom(data, gridWidth, gridHeight, emptyCount);
            lastGridWidth = gridWidth;
            lastGridHeight = gridHeight;
            lastEmptyCount = emptyCount;
            lastDisabledCellsHash = disabledHash;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Visual de Arranjo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Clique numa peça adjacente a um espaço vazio (cinza) para movê-la, igual ao jogo real.\n" +
            "Isso garante que o arranjo final seja sempre resolvível.\n" +
            $"Células ativas: {totalActiveCells}. Espaços vazios: {emptyCount}.\n" +
            "Mudar o grid, os buracos, ou a quantidade de vazios reseta o arranjo visual.",
            MessageType.Info);

        DrawArrangementGrid(data, gridWidth, gridHeight, totalActiveCells, emptyCount);

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Resetar (resolvido)"))
            {
                ResetSolved(gridWidth, gridHeight, data.disabledCells);
                SaveWorkingStateTo(data, gridWidth, gridHeight);
            }
            if (GUILayout.Button("Embaralhar aleatoriamente (válido)"))
            {
                RandomShuffleValid(gridWidth, gridHeight, data.disabledCells, emptyCount, 120);
                SaveWorkingStateTo(data, gridWidth, gridHeight);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Modo 1: Editor de Formato (buracos)
    // ────────────────────────────────────────────────────────────────

    private void DrawShapeEditor(LevelData data, int gridWidth, int gridHeight)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor de Formato", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Clique numa célula para alternar entre existir (clara) ou ser um buraco (escura).\n" +
            "Buracos formam o contorno do nível — útil para tabuleiros não-quadrados, formas em L, pirâmides, etc.",
            MessageType.Info);

        float cellSize = Mathf.Min(36f, (EditorGUIUtility.currentViewWidth - 40f) / gridWidth);

        for (int r = 0; r < gridHeight; r++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int c = 0; c < gridWidth; c++)
                {
                    int pos = r * gridWidth + c;
                    bool isHole = data.disabledCells.Contains(pos);

                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = isHole ? Color.black : Color.white;

                    if (GUILayout.Button("", GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                    {
                        Undo.RecordObject(data, "Editar formato do nível");
                        if (isHole) data.disabledCells.Remove(pos);
                        else data.disabledCells.Add(pos);
                        EditorUtility.SetDirty(data);
                    }

                    GUI.backgroundColor = prev;
                }
                GUILayout.FlexibleSpace();
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Células ativas: {data.TotalActiveCells} de {gridWidth * gridHeight}");
    }

    private int ComputeDisabledCellsHash(List<int> disabledCells)
    {
        if (disabledCells == null || disabledCells.Count == 0) return 0;
        int hash = 17;
        foreach (int cell in disabledCells) hash = hash * 31 + cell;
        return hash;
    }

    // ────────────────────────────────────────────────────────────────
    //  Modo 2: Editor de Arranjo — Estado ↔ LevelData
    // ────────────────────────────────────────────────────────────────

    private void LoadWorkingStateFrom(LevelData data, int gridWidth, int gridHeight, int emptyCount)
    {
        int totalGridCells = gridWidth * gridHeight;
        positionToTile = new int[totalGridCells];

        List<int> activeCells = ComputeActiveCells(gridWidth, gridHeight, data.disabledCells);
        int totalActiveCells = activeCells.Count;

        bool hasValidSavedArrangement =
            data.customArrangement != null && data.customArrangement.Count == totalActiveCells;

        if (hasValidSavedArrangement)
        {
            for (int tileId = 0; tileId < totalActiveCells; tileId++)
            {
                int pos = data.customArrangement[tileId];
                if (pos >= 0 && pos < totalGridCells) positionToTile[pos] = tileId;
            }
        }
        else
        {
            ResetSolved(gridWidth, gridHeight, data.disabledCells);
        }
    }

    private void SaveWorkingStateTo(LevelData data, int gridWidth, int gridHeight)
    {
        List<int> activeCells = ComputeActiveCells(gridWidth, gridHeight, data.disabledCells);
        int totalActiveCells = activeCells.Count;

        var arrangement = new List<int>(new int[totalActiveCells]);

        foreach (int pos in activeCells)
        {
            int tileId = positionToTile[pos];
            if (tileId >= 0 && tileId < totalActiveCells)
                arrangement[tileId] = pos;
        }

        Undo.RecordObject(data, "Editar arranjo do nível");
        data.customArrangement = arrangement;
        EditorUtility.SetDirty(data);
    }

    // ── Manipulação do estado local ──────────────────────────────────

    private List<int> ComputeActiveCells(int gridWidth, int gridHeight, List<int> disabledCells)
    {
        var result = new List<int>();
        int totalGridCells = gridWidth * gridHeight;
        for (int pos = 0; pos < totalGridCells; pos++)
            if (disabledCells == null || !disabledCells.Contains(pos))
                result.Add(pos);
        return result;
    }

    private void ResetSolved(int gridWidth, int gridHeight, List<int> disabledCells)
    {
        int totalGridCells = gridWidth * gridHeight;
        positionToTile = new int[totalGridCells];

        List<int> activeCells = ComputeActiveCells(gridWidth, gridHeight, disabledCells);
        for (int tileId = 0; tileId < activeCells.Count; tileId++)
            positionToTile[activeCells[tileId]] = tileId;
    }

    private bool IsEmptyTileId(int tileId, int totalActiveCells, int emptyCount)
    {
        return tileId >= totalActiveCells - emptyCount;
    }

    private void RandomShuffleValid(int gridWidth, int gridHeight, List<int> disabledCells, int emptyCount, int moves)
    {
        int totalGridCells = gridWidth * gridHeight;
        List<int> activeCells = ComputeActiveCells(gridWidth, gridHeight, disabledCells);
        int totalActiveCells = activeCells.Count;

        if (positionToTile == null || positionToTile.Length != totalGridCells)
            ResetSolved(gridWidth, gridHeight, disabledCells);

        for (int i = 0; i < moves; i++)
        {
            List<int> emptyPositions = FindEmptyPositions(activeCells, totalActiveCells, emptyCount);
            if (emptyPositions.Count == 0) break;

            int emptyPos = emptyPositions[Random.Range(0, emptyPositions.Count)];

            List<int> neighbors = GetNeighbors(emptyPos, gridWidth, gridHeight, disabledCells);
            neighbors.RemoveAll(pos => IsEmptyTileId(positionToTile[pos], totalActiveCells, emptyCount));

            if (neighbors.Count == 0) continue;

            int pick = neighbors[Random.Range(0, neighbors.Count)];

            (positionToTile[emptyPos], positionToTile[pick]) =
                (positionToTile[pick], positionToTile[emptyPos]);
        }
    }

    private List<int> FindEmptyPositions(List<int> activeCells, int totalActiveCells, int emptyCount)
    {
        var result = new List<int>();
        foreach (int pos in activeCells)
            if (IsEmptyTileId(positionToTile[pos], totalActiveCells, emptyCount))
                result.Add(pos);
        return result;
    }

    // ── Desenho da grade de arranjo ────────────────────────────────────

    private void DrawArrangementGrid(LevelData data, int gridWidth, int gridHeight, int totalActiveCells, int emptyCount)
    {
        float cellSize = Mathf.Min(40f, (EditorGUIUtility.currentViewWidth - 40f) / gridWidth);
        List<int> disabledCells = data.disabledCells;

        for (int r = 0; r < gridHeight; r++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int c = 0; c < gridWidth; c++)
                {
                    int pos = r * gridWidth + c;

                    if (disabledCells != null && disabledCells.Contains(pos))
                    {
                        // Buraco: espaço reservado, sem botão, pra manter alinhamento visual da grade
                        GUILayout.Space(cellSize + 2f);
                        continue;
                    }

                    int tileId = positionToTile[pos];
                    bool isEmpty = IsEmptyTileId(tileId, totalActiveCells, emptyCount);

                    int adjacentEmpty = isEmpty ? -1 : FindAdjacentEmptyPosition(pos, gridWidth, gridHeight, disabledCells, totalActiveCells, emptyCount);
                    GUI.enabled = !isEmpty && adjacentEmpty != -1;

                    string label = isEmpty ? "" : (tileId + 1).ToString();

                    Color previousColor = GUI.backgroundColor;
                    if (isEmpty) GUI.backgroundColor = Color.gray;

                    if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                    {
                        (positionToTile[pos], positionToTile[adjacentEmpty]) =
                            (positionToTile[adjacentEmpty], positionToTile[pos]);

                        SaveWorkingStateTo(data, gridWidth, gridHeight);
                    }

                    GUI.backgroundColor = previousColor;
                    GUI.enabled = true;
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

    private int FindAdjacentEmptyPosition(int pos, int gridWidth, int gridHeight, List<int> disabledCells, int totalActiveCells, int emptyCount)
    {
        foreach (int neighborPos in GetNeighbors(pos, gridWidth, gridHeight, disabledCells))
        {
            if (IsEmptyTileId(positionToTile[neighborPos], totalActiveCells, emptyCount))
                return neighborPos;
        }
        return -1;
    }

    private List<int> GetNeighbors(int pos, int gridWidth, int gridHeight, List<int> disabledCells)
    {
        var list = new List<int>();
        int r = pos / gridWidth, c = pos % gridWidth;

        if (r > 0)              TryAdd(list, pos - gridWidth, disabledCells);
        if (r < gridHeight - 1)  TryAdd(list, pos + gridWidth, disabledCells);
        if (c > 0)              TryAdd(list, pos - 1, disabledCells);
        if (c < gridWidth - 1)  TryAdd(list, pos + 1, disabledCells);

        return list;
    }

    private void TryAdd(List<int> list, int candidatePos, List<int> disabledCells)
    {
        if (disabledCells == null || !disabledCells.Contains(candidatePos))
            list.Add(candidatePos);
    }
}