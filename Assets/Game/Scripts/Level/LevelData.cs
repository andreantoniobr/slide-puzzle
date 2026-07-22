using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_", menuName = "Puzzle/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    public enum LevelMode { Embaralhado, ArranjoPersonalizado }

    [Header("Identificação")]
    public string levelName = "Nível";

    [Header("Tamanho")]
    public int gridWidth = 4;
    public int gridHeight = 4;

    [Header("Formato (buracos)")]
    [Tooltip("Posições do grid (0-based, row-major: row*gridWidth+col) que NÃO existem no tabuleiro.")]
    public List<int> disabledCells = new List<int>();

    [Header("Espaços Vazios")]
    [Min(1)]
    public int emptyTileCount = 1;

    [Header("Modo")]
    public LevelMode mode = LevelMode.Embaralhado;

    [Header("Embaralhado (reprodutível via seed)")]
    [Range(30, 600)] public int shuffleMoves = 120;
    public int seed = 12345;

    [Header("Arranjo Personalizado (opcional)")]
    [Tooltip("Tamanho deve ser igual ao número de células ativas (gridWidth*gridHeight - buracos). customArrangement[tileId] = posição no grid onde a peça (tileId+1) começa. Deixe vazio para usar Embaralhado.")]
    public List<int> customArrangement = new List<int>();

    [Header("Tutorial (opcional)")]
    [Tooltip("Se definido, este tutorial é exibido quando o nível carrega (e ainda não foi visto).")]
    public TutorialStageData tutorialStage;

    /// <summary>Quantas células realmente existem no tabuleiro (grid total menos buracos).</summary>
    public int TotalActiveCells => (gridWidth * gridHeight) - (disabledCells?.Count ?? 0);

    public LevelConfig ToConfig()
    {
        var config = new LevelConfig
        {
            gridWidth      = gridWidth,
            gridHeight     = gridHeight,
            disabledCells  = disabledCells != null ? new List<int>(disabledCells) : new List<int>(),
            shuffleMoves   = shuffleMoves,
            seed           = seed,
            customBoard    = null,
            emptyTileCount = emptyTileCount,
            tutorialStage  = tutorialStage
        };

        if (mode == LevelMode.ArranjoPersonalizado &&
            customArrangement != null &&
            customArrangement.Count == TotalActiveCells)
        {
            config.customBoard = customArrangement.ToArray();
        }

        return config;
    }
}