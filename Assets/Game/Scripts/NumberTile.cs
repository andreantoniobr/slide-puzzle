using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;
using System;

/// <summary>
/// Peça do puzzle numérico.
/// Exibe um número centralizado em vez de sprite fatiado.
///
/// Input via EventSystem — funciona em Windows, WebGL, Android e iOS:
///
///   CLIQUE/TAP
///     IPointerDownHandler + IPointerUpHandler
///     Se o dedo soltou sem arrastar (delta < SWIPE_THRESHOLD), trata como clique.
///
///   SWIPE
///     IPointerDownHandler + IPointerUpHandler
///     Se o delta >= SWIPE_THRESHOLD, determina a direção dominante e chama TryMove.
///     A peça NÃO acompanha o dedo em momento algum — o swipe é apenas um gatilho.
///
/// IDragHandler é implementado unicamente para suprimir o scroll do ScrollRect pai
/// e garantir que o EventSystem entregue o PointerUp corretamente após o arraste.
/// </summary>
public class NumberTile : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler
{
    // ────────────────────────────────────────────────────────────────
    //  Constantes de gesto
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Delta mínimo de tela (px) para reconhecer um swipe.
    /// Abaixo deste valor o gesto é tratado como clique/tap.
    /// </summary>
    [Header("Gestos")]
    [SerializeField] private float swipeThreshold = 5f;

    // ────────────────────────────────────────────────────────────────
    //  Inspector
    // ────────────────────────────────────────────────────────────────

    [Header("Referências")]
    public Image    background;
    public Image    highlightOverlay;
    public TMP_Text numberText;

    [Header("Glow de Posição Correta")]
    [SerializeField] private TileCorrectGlowEffect correctGlowEffect;

    [HideInInspector] public int  correctIndex;
    [HideInInspector] public int  currentIndex;
    [HideInInspector] public bool isEmpty;

    [Header("Seleção de Destino (clique em vazio)")]
    [SerializeField] private Color selectableEmptyColor = new Color(0.3f, 0.8f, 1f, 0.4f);   

    public void SetAwaitingSelection(bool awaiting)
    {
        correctGlowEffect?.SetSelected(awaiting);
    }


    // Paleta
    [SerializeField] private Color TileColor        = new Color(1f,    1f,    1f,    1f);
    [SerializeField] private Color TileCorrectColor = new Color(0.64f, 1f,    0.35f, 1f);

    
    [Header("Text Color")]
    [SerializeField] private Color TextTileColor        = new Color(0.34f, 0.20f, 0.125f, 1f);
    [SerializeField] private Color TextTileCorrectColor = new Color(0.64f, 1f,    0.35f,  1f); 
    private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.20f, 0.55f);


    [Header("Feedback de Movimento Inválido")]
    [SerializeField] private float invalidMoveShrinkFactor = 0.85f;
    [SerializeField] private float invalidMoveDuration = 0.15f;

    private bool isPlayingInvalidFeedback;

    // ────────────────────────────────────────────────────────────────
    //  Eventos públicos
    // ────────────────────────────────────────────────────────────────

    public static event Action TileCorrectPositionEvent;

    // ────────────────────────────────────────────────────────────────
    //  Estado privado
    // ────────────────────────────────────────────────────────────────

    private NumberPuzzleManager manager;
    private RectTransform       rect;
    private bool                isAnimating;

    /// <summary>Posição de tela no momento do PointerDown.</summary>
    private Vector2 pointerDownScreenPos;

    private Color originalBackgroundColor;
    private bool isSelectableTarget;

    private Coroutine scaleCoroutine;
    private Vector3 baseScale = Vector3.one;

    // ────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    // ────────────────────────────────────────────────────────────────
    //  Inicialização
    // ────────────────────────────────────────────────────────────────

    public void Init(NumberPuzzleManager mgr, int correct, int current, bool empty)
    {
        manager      = mgr;
        correctIndex = correct;
        currentIndex = current;
        isEmpty      = empty;

        if (highlightOverlay != null) highlightOverlay.color = Color.clear;

        Refresh();
    }

    // ────────────────────────────────────────────────────────────────
    //  Aparência
    // ────────────────────────────────────────────────────────────────

    /// <summary>Atualiza texto e cor conforme estado atual.</summary>
    public void Refresh()
    {
        if (isEmpty)
        {
            if (background       != null) background.color       = new Color(0f, 0f, 0f, 0f);
            if (numberText       != null) numberText.text        = "";
            if (highlightOverlay != null) highlightOverlay.color = Color.clear;
            correctGlowEffect?.SetCorrect(false);
            return;
        }

        int number = correctIndex + 1;
        if (numberText != null) numberText.text = number.ToString();

        bool inPlace = IsInCorrectPosition();

        if (background != null)
            background.color = inPlace ? TileCorrectColor : TileColor;

        if (numberText != null)
            numberText.color = inPlace ? TextTileCorrectColor : TextTileColor;

        correctGlowEffect?.SetCorrect(inPlace); // NOVO
    }

    // ────────────────────────────────────────────────────────────────
    //  EventSystem — PointerDown
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registra a posição inicial do ponteiro.
    /// Não move a peça — apenas guarda o ponto de referência para o gesto.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isAnimating) return;
        if (isEmpty && !isSelectableTarget) return;

        manager.NotifyPlayerInput();
        pointerDownScreenPos = eventData.position;

        
    }

   

    // ────────────────────────────────────────────────────────────────
    //  EventSystem — Drag (supressão de scroll pai, sem mover a peça)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implementado apenas para capturar o evento de arraste no EventSystem,
    /// garantindo que o PointerUp seja entregue a esta peça após o gesto.
    /// A peça NÃO se move durante o drag.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        // Intencional: corpo vazio.
        // A presença do IDragHandler impede que ScrollRects pais consumam o evento
        // e assegura que OnPointerUp seja chamado nesta peça ao soltar.
    }

    // ────────────────────────────────────────────────────────────────
    //  EventSystem — PointerUp (decisão: clique ou swipe)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ao soltar, calcula o delta desde o PointerDown e decide:
    ///   - delta menor que SWIPE_THRESHOLD → clique.
    ///   - delta maior ou igual              → swipe na direção dominante.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isAnimating) return;
        if (isEmpty && !isSelectableTarget) return;

        

        Vector2 delta       = eventData.position - pointerDownScreenPos;
        float   deltaLength = delta.magnitude;

        if (isEmpty && isSelectableTarget)
        {
            manager.OnEmptyTileSelected(this);
            return;
        }

        if (deltaLength < swipeThreshold)
        {
            bool moved = manager.OnTileClicked(this);
            if (!moved) PlayInvalidMoveFeedback();
        }
        else
        {
            DragDirection swipeDir = GetSwipeDirection(delta);
            bool moved = manager.TryMove(this, swipeDir);
            if (!moved) PlayInvalidMoveFeedback();
        }
    }

    public void PlayInvalidMoveFeedback()
    {
        if (isAnimating) return;
        StartCoroutine(InvalidMoveBounce());
    }

    private IEnumerator InvalidMoveBounce()
    {
        Vector3 shrunkScale = baseScale * invalidMoveShrinkFactor;
        float   half        = invalidMoveDuration * 0.5f;

        StartScaleAnimation(shrunkScale, half);
        yield return new WaitForSeconds(half);

        StartScaleAnimation(baseScale, half);
    }

    private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        transform.localScale = to;
        scaleCoroutine = null;
    }

    private void StartScaleAnimation(Vector3 targetScale, float duration)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleOverTime(transform.localScale, targetScale, duration));
    }



    // ────────────────────────────────────────────────────────────────
    //  Suporte ao gesto (privados)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determina a direção dominante do swipe comparando os eixos X e Y do delta.
    /// Retorna sempre uma das quatro direções cardeais.
    /// </summary>
    private DragDirection GetSwipeDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x > 0f ? DragDirection.Right : DragDirection.Left;
        else
            return delta.y > 0f ? DragDirection.Up : DragDirection.Down;
    }

    // ────────────────────────────────────────────────────────────────
    //  Movimento programático (usado pelo NumberPuzzleManager)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Anima a peça até <paramref name="targetPos"/>.
    /// Chamado pelo manager para executar o slide confirmado.
    /// </summary>
    public void MoveTo(Vector2 targetPos, float duration, Action onComplete = null)
    {
        if (isAnimating) return;
        StartCoroutine(AnimateMove(targetPos, duration, onComplete));
    }

    private IEnumerator AnimateMove(Vector2 target, float duration, Action onComplete)
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

    // ────────────────────────────────────────────────────────────────
    //  Highlight
    // ────────────────────────────────────────────────────────────────

    public void SetHighlight(bool on)
    {
        if (highlightOverlay == null) return;
        highlightOverlay.color = on ? HighlightColor : Color.clear;
    }

    // ────────────────────────────────────────────────────────────────
    //  Utilitários públicos
    // ────────────────────────────────────────────────────────────────

    public bool IsInCorrectPosition() => currentIndex == correctIndex;

    public void CheckIfJustReachedCorrectPosition()
    {
        if (!isEmpty && IsInCorrectPosition())
            TileCorrectPositionEvent?.Invoke();
    }

    public void SetSelectableTarget(bool selectable)
    {
        isSelectableTarget = selectable;

        if (background == null) return;

        if (selectable)
        {
            originalBackgroundColor = background.color;
            background.color = selectableEmptyColor;
        }
        else if (isEmpty)
        {
            background.color = originalBackgroundColor;
        }
    }
}