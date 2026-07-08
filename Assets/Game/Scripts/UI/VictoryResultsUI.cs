using UnityEngine;
using TMPro;

/// <summary>
/// Escuta o evento de conclusão do puzzle e atualiza os textos de tempo/movimentos
/// na Victory UI. Este script deve ficar num GameObject SEMPRE ATIVO (ex.: o mesmo
/// do GameMode) — NÃO dentro da VictoryOverlay, que começa desativada.
/// As referências de texto podem apontar para objetos inativos sem problema.
/// </summary>
public class VictoryResultsUI : MonoBehaviour
{
    [Header("Textos (dentro da VictoryOverlay, mesmo desativada)")]
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private TMP_Text timeText;

    [Header("Formatação")]
    [SerializeField] private string movesFormat = "{0} movimentos";
    [SerializeField] private bool formatTimeAsMinutesSeconds = true;

    private void Awake()
    {
        NumberPuzzleManager.SolvedPuzzleEvent += OnPuzzleSolved;
    }

    private void OnDestroy()
    {
        NumberPuzzleManager.SolvedPuzzleEvent -= OnPuzzleSolved;
    }

    private void OnPuzzleSolved(int movements, int seconds)
    {
        UpdateMovesText(movements);
        UpdateTimeText(seconds);
    }

    private void UpdateMovesText(int movements)
    {
        if (movesText == null) return;
        movesText.text = string.Format(movesFormat, movements);
    }

    private void UpdateTimeText(int seconds)
    {
        if (timeText == null) return;

        timeText.text = formatTimeAsMinutesSeconds
            ? FormatAsMinutesSeconds(seconds)
            : $"{seconds}s";
    }

    private string FormatAsMinutesSeconds(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int secs    = totalSeconds % 60;
        return $"{minutes:00}:{secs:00}";
    }
}