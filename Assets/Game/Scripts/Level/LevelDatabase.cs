using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Puzzle/Level Database", order = 0)]
public class LevelDatabase : ScriptableObject
{
    [Tooltip("Índice 0 = Nível 1, índice 1 = Nível 2, etc.")]
    public List<LevelData> levels = new List<LevelData>();
}