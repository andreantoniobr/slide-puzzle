using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    // positionToTile[pos] = id da peça (0-based) que está na posição pos.
    // O id (gridSize*gridSize - 1) representa o espaço vazio.
    private int[] positionToTile;
    private int lastGridSize = -1;

    public override void OnInspectorGUI()
    {
        LevelData data = (LevelData)target;

        DrawDefaultInspector();

        if (data.mode != LevelData.LevelMode.ArranjoPersonalizado)
        {
            positionToTile = null; // limpa estado ao sair do modo personalizado
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Visual de Arranjo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Clique numa peça adjacente ao espaço vazio para movê-la, igual ao jogo real.\n" +
            "Isso garante que o arranjo final seja sempre resolvível.\n" +
            "Mudar o tamanho do grid acima reseta o arranjo visual.",
            MessageType.Info);

        int n = data.gridSize * data.gridSize;

        if (positionToTile == null || lastGridSize != data.gridSize)
        {
            LoadWorkingStateFrom(data);
            lastGridSize = data.gridSize;
        }

        DrawGrid(data);

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Resetar (resolvido)"))
            {
                ResetSolved(data.gridSize);
                SaveWorkingStateTo(data);
            }
            if (GUILayout.Button("Embaralhar aleatoriamente (válido)"))
            {
                RandomShuffleValid(data.gridSize, 120);
                SaveWorkingStateTo(data);
            }
        }
    }

    // ── Estado ↔ LevelData ───────────────────────────────────────────

    private void LoadWorkingStateFrom(LevelData data)
    {
        int n = data.gridSize * data.gridSize;
        positionToTile = new int[n];

        bool hasValidSavedArrangement =
            data.customArrangement != null && data.customArrangement.Count == n;

        if (hasValidSavedArrangement)
        {
            // customArrangement[tileId] = posição -> inverte para positionToTile
            for (int tileId = 0; tileId < n; tileId++)
            {
                int pos = data.customArrangement[tileId];
                if (pos >= 0 && pos < n) positionToTile[pos] = tileId;
            }
        }
        else
        {
            ResetSolved(data.gridSize);
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

    private void ResetSolved(int gridSize)
    {
        int n = gridSize * gridSize;
        positionToTile = new int[n];
        for (int i = 0; i < n; i++) positionToTile[i] = i;
    }

    private void RandomShuffleValid(int gridSize, int moves)
    {
        int n = gridSize * gridSize;
        if (positionToTile == null || positionToTile.Length != n) ResetSolved(gridSize);

        int emptyPos  = System.Array.IndexOf(positionToTile, n - 1);
        int lastEmpty = -1;

        for (int i = 0; i < moves; i++)
        {
            List<int> neighbors = GetNeighbors(emptyPos, gridSize);
            neighbors.Remove(lastEmpty);

            int pick = neighbors[Random.Range(0, neighbors.Count)];

            (positionToTile[emptyPos], positionToTile[pick]) =
                (positionToTile[pick], positionToTile[emptyPos]);

            lastEmpty = emptyPos;
            emptyPos  = pick;
        }
    }

    // ── Desenho da grade ──────────────────────────────────────────────

    private void DrawGrid(LevelData data)
    {
        int gridSize = data.gridSize;
        int n        = gridSize * gridSize;
        float cellSize = Mathf.Min(40f, (EditorGUIUtility.currentViewWidth - 40f) / gridSize);

        int emptyPos = System.Array.IndexOf(positionToTile, n - 1);

        for (int r = 0; r < gridSize; r++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int c = 0; c < gridSize; c++)
                {
                    int pos    = r * gridSize + c;
                    int tileId = positionToTile[pos];
                    bool isEmpty = tileId == n - 1;

                    GUI.enabled = !isEmpty && IsAdjacent(pos, emptyPos, gridSize);

                    string label = isEmpty ? "" : (tileId + 1).ToString();
                    if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                    {
                        (positionToTile[pos], positionToTile[emptyPos]) =
                            (positionToTile[emptyPos], positionToTile[pos]);

                        emptyPos = pos;
                        SaveWorkingStateTo(data); // salva a cada clique
                    }

                    GUI.enabled = true;
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

    private bool IsAdjacent(int a, int b, int gridSize)
    {
        int rA = a / gridSize, cA = a % gridSize;
        int rB = b / gridSize, cB = b % gridSize;
        return Mathf.Abs(rA - rB) + Mathf.Abs(cA - cB) == 1;
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