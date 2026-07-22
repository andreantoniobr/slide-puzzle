using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera níveis quando acabam os níveis feitos à mão na LevelDatabase.
/// Dificuldade escala com o índice: tabuleiro maior e mais embaralhamento.
/// Sempre gera grids retangulares completos (sem buracos) — formatos
/// irregulares ficam reservados para níveis feitos à mão.
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

        // Suaviza a transição: primeiros níveis procedurais ainda têm 2 vazios,
        // depois cai pra 1 (padrão clássico) conforme o jogador avança
        int emptyTileCount = proceduralIndex < 6 ? 2 : 1;

        int shuffleMoves = gridSize <= 3
            ? Mathf.Clamp(20 + proceduralIndex * 5, 20, 60)
            : Mathf.Clamp(BaseShuffleMoves + proceduralIndex * ShuffleMovesPerLevel, BaseShuffleMoves, MaxShuffleMoves);

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        return new LevelConfig
        {
            gridWidth      = gridSize,
            gridHeight     = gridSize,
            disabledCells  = new List<int>(), // procedural nunca tem buracos
            shuffleMoves   = shuffleMoves,
            seed           = seed,
            emptyTileCount = emptyTileCount,
            customBoard    = null,
            tutorialStage  = null
        };
    }
}