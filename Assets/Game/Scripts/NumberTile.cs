using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;
using System;

/// <summary>
/// Peça do puzzle numérico.
/// Exibe um número centralizado em vez de sprite fatiado.
/// </summary>
public class NumberTile : MonoBehaviour, IPointerClickHandler
{
    [Header("Referências")]
    public Image background;
    public Image highlightOverlay;
    public TMP_Text  numberText;

    [HideInInspector] public int correctIndex;
    [HideInInspector] public int currentIndex;
    [HideInInspector] public bool isEmpty;

    public static event Action TileCorrectPositionEvent;

    private NumberPuzzleManager manager;
    private RectTransform rect;
    private bool isAnimating;

    // Paleta
    // new Color(0.20f, 0.55f, 0.95f, 1f)
    [SerializeField] private static readonly Color TileColor      = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color TileCorrectColor = new Color(0.64f, 1f, 0.35f, 1f);
    
    [SerializeField] private static readonly Color HighlightColor  = new Color(1f, 0.85f, 0.20f, 0.55f);

    [Header("Text Color")]
    [SerializeField] private Color TextTileColor = new Color(0.34f, 0.20f, 0.125f, 1f);
    [SerializeField] private Color TextTileCorrectColor = new Color(0.64f, 1f, 0.35f, 1f);

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Init(NumberPuzzleManager mgr, int correct, int current, bool empty)
    {
        manager      = mgr;
        correctIndex = correct;
        currentIndex = current;
        isEmpty      = empty;

        if (highlightOverlay != null) highlightOverlay.color = Color.clear;

        Refresh();
    }

    /// <summary>Atualiza texto e cor conforme estado atual.</summary>
    public void Refresh()
    {
        if (isEmpty)
        {
            if (background  != null) background.color  = new Color(0f, 0f, 0f, 0.0f);
            if (numberText  != null) numberText.text   = "";
            if (highlightOverlay != null) highlightOverlay.color = Color.clear;
            return;
        }

        int number = correctIndex + 1; // 1-based para o jogador
        if (numberText != null) numberText.text = number.ToString();

        bool inPlace = IsInCorrectPosition();        

        if (background != null)            
            background.color = inPlace ? TileCorrectColor : TileColor;
            numberText.color = inPlace ? TextTileCorrectColor : TextTileColor;

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isAnimating || isEmpty) return;
        manager.OnTileClicked(this);
    }

    public void MoveTo(Vector2 targetPos, float duration, System.Action onComplete = null)
    {
        if (isAnimating) return;
        StartCoroutine(AnimateMove(targetPos, duration, onComplete));
    }

    private IEnumerator AnimateMove(Vector2 target, float duration, System.Action onComplete)
    {
        isAnimating = true;
        Vector2 start   = rect.anchoredPosition;
        float   elapsed = 0f;

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

    public bool IsInCorrectPosition() => currentIndex == correctIndex;

    public void CheckIfJustReachedCorrectPosition()
    {
        if (!isEmpty && IsInCorrectPosition())
        {
            TileCorrectPositionEvent?.Invoke();
        }
    }
}