using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    // positionToTile[pos] = id da peça (0-based) que está na posição pos.
    // IDs de (gridSize*gridSize - emptyTileCount) até (gridSize*gridSize - 1) representam vazios.
    private int[] positionToTile;
    private int lastGridSize = -1;
    private int lastEmptyCount = -1;

    public override void OnInspectorGUI()
    {
        LevelData data = (LevelData)target;

        DrawDefaultInspector();

        if (data.mode != LevelData.LevelMode.ArranjoPersonalizado)
        {
            positionToTile = null;
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Visual de Arranjo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Clique numa peça adjacente a um espaço vazio (cinza) para movê-la, igual ao jogo real.\n" +
            "Isso garante que o arranjo final seja sempre resolvível.\n" +
            $"Espaços vazios configurados: {data.emptyTileCount}.\n" +
            "Mudar o tamanho do grid ou a quantidade de vazios reseta o arranjo visual.",
            MessageType.Info);

        int n = data.gridSize * data.gridSize;
        int emptyCount = Mathf.Clamp(data.emptyTileCount, 1, Mathf.Max(1, n / 2));

        if (positionToTile == null || lastGridSize != data.gridSize || lastEmptyCount != emptyCount)
        {
            LoadWorkingStateFrom(data, emptyCount);
            lastGridSize = data.gridSize;
            lastEmptyCount = emptyCount;
        }

        DrawGrid(data, emptyCount);

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Resetar (resolvido)"))
            {
                ResetSolved(data.gridSize, emptyCount);
                SaveWorkingStateTo(data);
            }
            if (GUILayout.Button("Embaralhar aleatoriamente (válido)"))
            {
                RandomShuffleValid(data.gridSize, emptyCount, 120);
                SaveWorkingStateTo(data);
            }
        }
    }

    // ── Estado ↔ LevelData ───────────────────────────────────────────

    private void LoadWorkingStateFrom(LevelData data, int emptyCount)
    {
        int n = data.gridSize * data.gridSize;
        positionToTile = new int[n];

        bool hasValidSavedArrangement =
            data.customArrangement != null && data.customArrangement.Count == n;

        if (hasValidSavedArrangement)
        {
            for (int tileId = 0; tileId < n; tileId++)
            {
                int pos = data.customArrangement[tileId];
                if (pos >= 0 && pos < n) positionToTile[pos] = tileId;
            }
        }
        else
        {
            ResetSolved(data.gridSize, emptyCount);
        }
    }

    private void SaveWorkingStateTo(LevelData data)
    {
        int n = data.gridSize * data.gridSize;
        var arrangement = new List<int>(new int[n]);

        for (int pos = 0; pos < n; pos++)
        {
            int tileId = positionToTile[pos];
            arrangement[tileId] = pos;
        }

        Undo.RecordObject(data, "Editar arranjo do nível");
        data.customArrangement = arrangement;
        EditorUtility.SetDirty(data);
    }

    // ── Manipulação do estado local ──────────────────────────────────

    private void ResetSolved(int gridSize, int emptyCount)
    {
        int n = gridSize * gridSize;
        positionToTile = new int[n];
        for (int i = 0; i < n; i++) positionToTile[i] = i;
    }

    private bool IsEmptyTileId(int tileId, int totalTiles, int emptyCount)
    {
        return tileId >= totalTiles - emptyCount;
    }

    private void RandomShuffleValid(int gridSize, int emptyCount, int moves)
    {
        int n = gridSize * gridSize;
        if (positionToTile == null || positionToTile.Length != n) ResetSolved(gridSize, emptyCount);

        for (int i = 0; i < moves; i++)
        {
            // Escolhe um dos vazios ao acaso e tenta mover um vizinho pra dentro dele
            List<int> emptyPositions = FindEmptyPositions(n, emptyCount);
            int emptyPos = emptyPositions[Random.Range(0, emptyPositions.Count)];

            List<int> neighbors = GetNeighbors(emptyPos, gridSize);
            neighbors.RemoveAll(pos => IsEmptyTileId(positionToTile[pos], n, emptyCount));

            if (neighbors.Count == 0) continue;

            int pick = neighbors[Random.Range(0, neighbors.Count)];

            (positionToTile[emptyPos], positionToTile[pick]) =
                (positionToTile[pick], positionToTile[emptyPos]);
        }
    }

    private List<int> FindEmptyPositions(int n, int emptyCount)
    {
        var result = new List<int>();
        for (int pos = 0; pos < n; pos++)
            if (IsEmptyTileId(positionToTile[pos], n, emptyCount))
                result.Add(pos);
        return result;
    }

    // ── Desenho da grade ──────────────────────────────────────────────

    private void DrawGrid(LevelData data, int emptyCount)
    {
        int gridSize = data.gridSize;
        int n        = gridSize * gridSize;
        float cellSize = Mathf.Min(40f, (EditorGUIUtility.currentViewWidth - 40f) / gridSize);

        for (int r = 0; r < gridSize; r++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int c = 0; c < gridSize; c++)
                {
                    int pos    = r * gridSize + c;
                    int tileId = positionToTile[pos];
                    bool isEmpty = IsEmptyTileId(tileId, n, emptyCount);

                    // Peça só pode ser clicada se houver AO MENOS UM vazio adjacente
                    int adjacentEmpty = isEmpty ? -1 : FindAdjacentEmptyPosition(pos, gridSize, n, emptyCount);
                    GUI.enabled = !isEmpty && adjacentEmpty != -1;

                    string label = isEmpty ? "" : (tileId + 1).ToString();

                    Color previousColor = GUI.backgroundColor;
                    if (isEmpty) GUI.backgroundColor = Color.gray;

                    if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                    {
                        (positionToTile[pos], positionToTile[adjacentEmpty]) =
                            (positionToTile[adjacentEmpty], positionToTile[pos]);

                        SaveWorkingStateTo(data);
                    }

                    GUI.backgroundColor = previousColor;
                    GUI.enabled = true;
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

    /// <summary>
    /// Retorna a posição do primeiro vazio adjacente encontrado, ou -1 se não houver nenhum.
    /// No editor (ferramenta manual), não há necessidade de desambiguar como no jogo real —
    /// qualquer vazio adjacente serve para montar o arranjo.
    /// </summary>
    private int FindAdjacentEmptyPosition(int pos, int gridSize, int n, int emptyCount)
    {
        foreach (int neighborPos in GetNeighbors(pos, gridSize))
        {
            if (IsEmptyTileId(positionToTile[neighborPos], n, emptyCount))
                return neighborPos;
        }
        return -1;
    }

    private List<int> GetNeighbors(int pos, int gridSize)
    {
        var list = new List<int>();
        int r = pos / gridSize, c = pos % gridSize;
        if (r > 0)              list.Add(pos - gridSize);
        if (r < gridSize - 1)   list.Add(pos + gridSize);
        if (c > 0)              list.Add(pos - 1);
        if (c < gridSize - 1)   list.Add(pos + 1);
        return list;
    }
}