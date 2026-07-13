using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_", menuName = "Puzzle/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    public enum LevelMode { Embaralhado, ArranjoPersonalizado }

    [Header("Identificação")]
    public string levelName = "Nível";

    [Header("Tamanho")]
    [Range(2, 8)] public int gridSize = 4;

    [Header("Espaços Vazios")]
    [Range(1, 4)]
    public int emptyTileCount = 1;

    [Header("Modo")]
    public LevelMode mode = LevelMode.Embaralhado;

    [Header("Embaralhado (reprodutível via seed)")]
    [Range(30, 600)] public int shuffleMoves = 120;
    public int seed = 12345;

    [Header("Arranjo Personalizado (opcional)")]
    [Tooltip("Tamanho deve ser gridSize*gridSize. customArrangement[i] = posição inicial da peça número (i+1). Deixe vazio para usar Embaralhado.")]
    public List<int> customArrangement = new List<int>();

    [Header("Tutorial (opcional)")]
    [Tooltip("Se definido, este tutorial é exibido quando o nível carrega (e ainda não foi visto).")]
    public TutorialStageData tutorialStage;

    public LevelConfig ToConfig()
    {
        var config = new LevelConfig
        {
            gridSize = gridSize,
            shuffleMoves = shuffleMoves,
            seed = seed,
            customBoard = null,
            emptyTileCount = emptyTileCount,
            tutorialStage  = tutorialStage
        };

        if (mode == LevelMode.ArranjoPersonalizado &&
            customArrangement != null &&
            customArrangement.Count == gridSize * gridSize)
        {
            config.customBoard = customArrangement.ToArray();
        }

        return config;
    }
}