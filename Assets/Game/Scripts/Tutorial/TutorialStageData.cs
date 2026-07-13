using UnityEngine;

public enum TutorialGestureType
{
    BasicSwipe,          // usa o primeiro tile movível + seu destino
    MultiEmptySelection  // usa uma peça com 2+ vazios adjacentes + um dos destinos
}

[CreateAssetMenu(fileName = "Tutorial_", menuName = "Puzzle/Tutorial Stage", order = 2)]
public class TutorialStageData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Chave única usada para lembrar se o jogador já viu este tutorial (PlayerPrefs).")]
    public string stageId = "tutorial_stage";

    [Header("Conteúdo")]
    [TextArea(2, 4)]
    public string message = "Deslize para mover as peças para o local correto";

    [Header("Gesto")]
    public TutorialGestureType gestureType = TutorialGestureType.BasicSwipe;
}