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
///     Se o dedo soltou sem arrastar (delta < swipeThreshold), trata como clique.
///
///   SWIPE
///     IPointerDownHandler + IPointerUpHandler
///     Se o delta >= swipeThreshold, determina a direção dominante e chama TryMove.
///     A peça NÃO acompanha o dedo em momento algum — o swipe é apenas um gatilho.
///
/// IDragHandler é implementado unicamente para suprimir o scroll do ScrollRect pai
/// e garantir que o EventSystem entregue o PointerUp corretamente após o arraste.
///
/// Tiles especiais (Hole, Rock, Question, Lock, Key): controlados por
/// SpecialTileType. Por padrão todo tile é Normal — comportamento idêntico
/// ao que já existia, garantindo compatibilidade com níveis já criados.
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

    [HideInInspector] public int  correctIndex;        // tileId — identidade lógica da peça (0..totalActiveCells-1)
    [HideInInspector] public int  correctGridPosition; // posição no grid onde esta peça pertence quando resolvida
    [HideInInspector] public int  currentIndex;        // posição atual no grid (pode ter "buracos" no meio)
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

    // ────────────────────────────────────────────────────────────────
    //  Tiles Especiais
    // ────────────────────────────────────────────────────────────────

    [Header("Tipo Especial")]
    public SpecialTileType specialType = SpecialTileType.Normal;

    [Header("Sprites Especiais (opcional — só usados conforme o tipo)")]
    [SerializeField] private Image specialSpriteImage; // overlay separado, oculto por padrão
    [SerializeField] private Sprite holeSprite;
    [SerializeField] private Sprite rockCrackedSprite;     // estado após o 1º toque (mais rachada)
    [SerializeField] private Sprite rockCrackedLessSprite; // estado inicial (menos rachada)
    [SerializeField] private Sprite questionSprite;
    [SerializeField] private Sprite lockSprite;
    [SerializeField] private Sprite keySprite;

    [Header("Efeitos Especiais")]
    [SerializeField] private ParticleSystem rockBreakParticles;

    [SerializeField] private ParticleSystem lockOpenParticlesA; 
    [SerializeField] private ParticleSystem lockOpenParticlesB;

    private int rockHitsRemaining;
    private int lockKeysRemaining;

    // ────────────────────────────────────────────────────────────────
    //  Eventos públicos
    // ────────────────────────────────────────────────────────────────

    public static event Action TileCorrectPositionEvent;
    public static event Action QuestionRevealedEvent;
    public static event Action RockCrackEvent;   // NOVO — pedra rachou (ainda não quebrou de vez)
    public static event Action RockBreakEvent;   // NOVO — pedra quebrou de vez (virou Normal)
    public static event Action LockOpenEvent; 

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
    private bool wasQuestionRevealed;

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

    /// <param name="mgr">Manager dono deste tile.</param>
    /// <param name="correct">TileId — identidade lógica da peça.</param>
    /// <param name="current">Posição no grid onde a peça nasce (sempre a posição correta, no momento da construção).</param>
    /// <param name="empty">Se esta peça representa um espaço vazio.</param>
    public void Init(NumberPuzzleManager mgr, int correct, int current, bool empty)
    {
        manager             = mgr;
        correctIndex        = correct;
        correctGridPosition = current; // no Init, a peça sempre nasce na posição correta dela
        currentIndex        = current;
        isEmpty             = empty;
        specialType         = SpecialTileType.Normal; // reset — evita "vazar" tipo especial de reaproveitamento de instância

        if (highlightOverlay != null) highlightOverlay.color = Color.clear;

        Refresh();
    }

    /// <summary>
    /// Aplica a configuração de um tile especial (chamado pelo Manager logo
    /// após BuildBoard, se o nível tiver algum SpecialTileData configurado
    /// para o tileId desta peça). Se nunca for chamado, o tile permanece
    /// SpecialTileType.Normal — comportamento padrão inalterado.
    /// </summary>
    public void ApplySpecialData(SpecialTileData data)
    {
        specialType = data.type;
        rockHitsRemaining = data.rockHitsRequired;
        lockKeysRemaining = data.lockRequiredKeys;

        // Pré-registra o estado atual como baseline, para que o Refresh() abaixo
        // NÃO trate "já estar na posição correta agora" como uma transição real —
        // isso é chamado antes do embaralhamento, é só o estado de fábrica.
        wasQuestionRevealed = (specialType == SpecialTileType.Question) && IsInCorrectPosition();

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
            if (specialSpriteImage != null) specialSpriteImage.gameObject.SetActive(false);
            correctGlowEffect?.SetCorrect(false);
            return;
        }

        bool inPlace = IsInCorrectPosition();

        if (specialType == SpecialTileType.Question)
        {
            bool isRevealedNow = inPlace;
            if (isRevealedNow && !wasQuestionRevealed)
                QuestionRevealedEvent?.Invoke();
            wasQuestionRevealed = isRevealedNow;
        }

        // Tiles especiais com visual próprio assumem o lugar do número —
        // Question mostra o número real quando já está na posição correta.
        Sprite specialSprite = GetSpecialSpriteOrNull(inPlace);
        if (specialSprite != null)
        {
            if (specialSpriteImage != null)
            {
                specialSpriteImage.sprite = specialSprite;
                specialSpriteImage.gameObject.SetActive(true);
            }
            if (numberText != null) numberText.gameObject.SetActive(false);

            // Hole esconde o background completamente (nunca sai do lugar, então não
            // precisa da moldura/cor de fundo normal). Os outros tipos especiais
            // (Rock, Lock, Key, Question) continuam mostrando o background normal.
            if (background != null)
                background.gameObject.SetActive(specialType != SpecialTileType.Hole);

            correctGlowEffect?.SetCorrect(false);
            return;
        }

        if (specialSpriteImage != null) specialSpriteImage.gameObject.SetActive(false);
        if (numberText != null) numberText.gameObject.SetActive(true);

        int number = correctIndex + 1; // 1-based para o jogador
        if (numberText != null) numberText.text = number.ToString();

        if (background != null)
            background.color = inPlace ? TileCorrectColor : TileColor;

        if (numberText != null)
            numberText.color = inPlace ? TextTileCorrectColor : TextTileColor;

        correctGlowEffect?.SetCorrect(inPlace);
    }

    private Sprite GetSpecialSpriteOrNull(bool inPlace)
    {
        switch (specialType)
        {
            case SpecialTileType.Hole:  return holeSprite;
            case SpecialTileType.Rock:  return rockHitsRemaining >= 2 ? rockCrackedLessSprite : rockCrackedSprite;
            case SpecialTileType.Lock:  return lockSprite;
            case SpecialTileType.Key:   return keySprite;
            case SpecialTileType.Question: return inPlace ? null : questionSprite; // correto = revela o número real
            default: return null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Gating de movimento (tiles especiais)
    // ────────────────────────────────────────────────────────────────

    /// <summary>Se este tile pode participar de um movimento agora.</summary>
    public bool CanBeMoved
    {
        get
        {
            if (isEmpty) return false;
            return specialType == SpecialTileType.Normal
                || specialType == SpecialTileType.Question
                || specialType == SpecialTileType.Key;
        }
    }

    /// <summary>
    /// Chamado pelo Manager ANTES de decidir movimento — dá a chance de um
    /// tile especial "consumir" o toque (ex.: Rock rachando) em vez de mover.
    /// Retorna true se o toque foi tratado aqui (o fluxo normal de movimento
    /// não deve prosseguir).
    /// </summary>
    public bool HandleSpecialTouch()
    {
        if (specialType == SpecialTileType.Rock)
        {
            ApplyRockHit();
            return true;
        }
        return false;
    }

    private void ApplyRockHit()
    {
        rockHitsRemaining--;

        if (rockBreakParticles != null) rockBreakParticles.Play();

        if (rockHitsRemaining <= 0)
        {
            specialType = SpecialTileType.Normal;
            RockBreakEvent?.Invoke(); // NOVO
        }
        else
        {
            RockCrackEvent?.Invoke(); // NOVO
        }

        PlayInvalidMoveFeedback();
        Refresh();
    }

    /// <summary>Converte este tile diretamente para Normal (usado por Lock/Key ao destravar).</summary>
    public void ConvertToNormal()
    {
        specialType = SpecialTileType.Normal;
        Refresh();
    }

    public int LockRemainingKeys => lockKeysRemaining;

    /// <summary>Consome uma chave — se chegar a zero, o cadeado vira Normal.</summary>
    public void ConsumeLockKey()
    {
        lockKeysRemaining--;
        if (lockKeysRemaining <= 0)
        {
            specialType = SpecialTileType.Normal;
            if (lockOpenParticlesA != null) lockOpenParticlesA.Play(); 
            if (lockOpenParticlesB != null) lockOpenParticlesB.Play();
            LockOpenEvent?.Invoke();
        }
        Refresh();
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
    ///   - delta menor que swipeThreshold → clique.
    ///   - delta maior ou igual           → swipe na direção dominante.
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

    public bool IsInCorrectPosition() => currentIndex == correctGridPosition;

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