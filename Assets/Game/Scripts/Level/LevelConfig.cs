using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Contrato comum entre níveis feitos à mão e níveis procedurais.
/// O NumberPuzzleManager só entende isso — não sabe de onde veio.
/// </summary>
[Serializable]
public class LevelConfig
{
    public int gridWidth;
    public int gridHeight;
    public List<int> disabledCells;
    public int shuffleMoves;
    public int seed;

    public Vector2 boardSizeMobile;
    public Vector2 boardSizePC;
    public float customBorderThickness;

    public int emptyTileCount = 1;

    /// <summary>
    /// Se preenchido (tamanho == gridSize*gridSize), define exatamente onde
    /// cada peça começa. Se null, o nível é embaralhado usando o seed.
    /// customBoard[i] = posição inicial da peça de número (i+1).
    /// </summary>
    public int[] customBoard;

    public TutorialStageData tutorialStage;
}