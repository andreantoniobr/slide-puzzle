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
    private const float SWIPE_THRESHOLD = 15f;

    // ────────────────────────────────────────────────────────────────
    //  Inspector
    // ────────────────────────────────────────────────────────────────

    [Header("Referências")]
    public Image    background;
    public Image    highlightOverlay;
    public TMP_Text numberText;

    [HideInInspector] public int  correctIndex;
    [HideInInspector] public int  currentIndex;
    [HideInInspector] public bool isEmpty;

    // Paleta
    [SerializeField] private Color TileColor        = new Color(1f,    1f,    1f,    1f);
    [SerializeField] private Color TileCorrectColor = new Color(0.64f, 1f,    0.35f, 1f);

    [Header("Text Color")]
    [SerializeField] private Color TextTileColor        = new Color(0.34f, 0.20f, 0.125f, 1f);
    [SerializeField] private Color TextTileCorrectColor = new Color(0.64f, 1f,    0.35f,  1f);

    private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.20f, 0.55f);

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
            return;
        }

        int number = correctIndex + 1; // 1-based para o jogador
        if (numberText != null) numberText.text = number.ToString();

        bool inPlace = IsInCorrectPosition();

        if (background != null)
            background.color = inPlace ? TileCorrectColor : TileColor;

        if (numberText != null)
            numberText.color = inPlace ? TextTileCorrectColor : TextTileColor;
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
        if (isAnimating || isEmpty) return;
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
        if (isAnimating || isEmpty) return;

        Vector2 delta       = eventData.position - pointerDownScreenPos;
        float   deltaLength = delta.magnitude;

        if (deltaLength < SWIPE_THRESHOLD)
        {
            // ── Clique/Tap ───────────────────────────────────────────
            manager.OnTileClicked(this);
        }
        else
        {
            // ── Swipe ────────────────────────────────────────────────
            DragDirection swipeDir = GetSwipeDirection(delta);
            manager.TryMove(this, swipeDir);
            // TryMove retornar false não requer ação: a peça nunca se moveu.
        }
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
}