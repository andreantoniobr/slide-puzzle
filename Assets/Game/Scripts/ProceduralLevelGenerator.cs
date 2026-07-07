using UnityEngine;

/// <summary>
/// Gera níveis quando acabam os níveis feitos à mão na LevelDatabase.
/// Dificuldade escala com o índice: tabuleiro maior e mais embaralhamento.
/// </summary>
public static class ProceduralLevelGenerator
{
    private const int MinGridSize = 3;
    private const int MaxGridSize = 8;

    private const int BaseShuffleMoves = 60;
    private const int ShuffleMovesPerLevel = 15;
    private const int MaxShuffleMoves = 600;

    public static LevelConfig Generate(int proceduralIndex)
    {
        int gridSize = Mathf.Clamp(MinGridSize + proceduralIndex / 3, MinGridSize, MaxGridSize);

        int shuffleMoves = Mathf.Clamp(
            BaseShuffleMoves + proceduralIndex * ShuffleMovesPerLevel,
            BaseShuffleMoves,
            MaxShuffleMoves);

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        return new LevelConfig
        {
            gridSize     = gridSize,
            shuffleMoves = shuffleMoves,
            seed         = seed,
            customBoard  = null
        };
    }
}