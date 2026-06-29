using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Componente anexado a cada peça do puzzle.
/// Guarda a posição correta e a posição atual no grid.
/// </summary>
public class TilePiece : MonoBehaviour, IPointerClickHandler
{
    [Header("Dados da Peça")]
    public int correctIndex;   // Índice correto (0 = topo-esquerda)
    public int currentIndex;   // Índice atual no tabuleiro
    public bool isEmpty;       // É o espaço vazio?

    [Header("Visual")]
    public Image tileImage;
    public Image highlightOverlay;

    private SlidePuzzleManager manager;
    private RectTransform rect;
    private bool isAnimating;

    private static readonly Color HighlightColor = new Color(1f, 1f, 1f, 0.25f);

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        if (tileImage == null) tileImage = GetComponent<Image>();
    }

    public void Init(SlidePuzzleManager mgr, int correct, int current, bool empty)
    {
        manager = mgr;
        correctIndex = correct;
        currentIndex = current;
        isEmpty = empty;

        if (highlightOverlay != null)
            highlightOverlay.color = Color.clear;

        UpdateVisibility();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isAnimating || isEmpty) return;
        manager.OnTileClicked(this);
    }

    /// <summary>Move a peça para a posição do espaço vazio com animação.</summary>
    public void MoveTo(Vector2 targetPosition, float duration, System.Action onComplete = null)
    {
        if (isAnimating) return;
        StartCoroutine(AnimateMove(targetPosition, duration, onComplete));
    }

    private IEnumerator AnimateMove(Vector2 target, float duration, System.Action onComplete)
    {
        isAnimating = true;
        Vector2 start = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        rect.anchoredPosition = target;
        isAnimating = false;
        onComplete?.Invoke();
    }

    public void SetHighlight(bool on)
    {
        if (highlightOverlay == null) return;
        highlightOverlay.color = on ? HighlightColor : Color.clear;
    }

    private void UpdateVisibility()
    {
        if (tileImage != null)
            tileImage.enabled = !isEmpty;
    }

    public bool IsInCorrectPosition() => currentIndex == correctIndex;
}