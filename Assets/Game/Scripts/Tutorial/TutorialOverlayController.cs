using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay de tutorial: mostra um ícone de mão fazendo o gesto de swipe
/// entre a posição de uma peça e seu destino, em loop, com uma seta fixa
/// apontando na direção do movimento, texto de instrução e botão de pular.
/// Cada estágio (definido por um TutorialStageData) fica marcado como "visto"
/// via PlayerPrefs após ser fechado, e não aparece novamente.
/// </summary>
public class TutorialOverlayController : MonoBehaviour
{
    private const string TutorialSeenKeyPrefix = "TutorialSeen_";

    [Header("Overlay")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Texto")]
    [SerializeField] private TMP_Text instructionText;

    [Header("Botão de Skip")]
    [SerializeField] private Button skipButton;

    [Header("Mão (se move) + Seta (fixa, só rotaciona)")]
    [SerializeField] private RectTransform handIcon;
    [SerializeField] private RectTransform arrowIcon;
    [SerializeField] private CanvasGroup gestureCanvasGroup;
    [SerializeField] private float gestureMoveDuration = 0.8f;
    [SerializeField] private float gesturePauseDuration = 0.3f;
    [SerializeField] private float gestureFadeDuration = 0.2f;

    private Coroutine gestureRoutine;
    private RectTransform sourcePoint;
    private RectTransform targetPoint;
    private string currentStageId;

    private void Awake()
    {
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.gameObject.SetActive(false);
        }

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);
    }

    // ────────────────────────────────────────────────────────────────
    //  Controle de "já visto"
    // ────────────────────────────────────────────────────────────────

    public static bool HasSeenStage(string stageId)
    {
        return PlayerPrefs.GetInt(TutorialSeenKeyPrefix + stageId, 0) == 1;
    }

    private static void MarkStageSeen(string stageId)
    {
        PlayerPrefs.SetInt(TutorialSeenKeyPrefix + stageId, 1);
        PlayerPrefs.Save();
    }

    // ────────────────────────────────────────────────────────────────
    //  API pública
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mostra um estágio específico do tutorial. Não mostra novamente se o
    /// jogador já tiver visto/pulado esse stageId antes.
    /// </summary>
    public void Show(TutorialStageData stageData, RectTransform sourceTilePosition, RectTransform targetCellPosition)
    {
        if (stageData == null) return;
        if (HasSeenStage(stageData.stageId)) return;

        currentStageId = stageData.stageId;
        sourcePoint = sourceTilePosition;
        targetPoint = targetCellPosition;

        if (instructionText != null)
            instructionText.text = stageData.message;

        // NOVO: posiciona e rotaciona a seta ANTES de qualquer fade — já nasce correta
        PositionArrow(sourcePoint.position, targetPoint.position);

        gameObject.SetActive(true);
        overlayCanvasGroup.gameObject.SetActive(true);

        StartCoroutine(FadeOverlay(0f, 1f, fadeInDuration, () =>
        {
            gestureRoutine = StartCoroutine(GestureLoop());
        }));
    }

    public void Hide()
    {
        if (gestureRoutine != null)
        {
            StopCoroutine(gestureRoutine);
            gestureRoutine = null;
        }

        StartCoroutine(FadeOverlay(overlayCanvasGroup.alpha, 0f, fadeOutDuration, () =>
        {
            overlayCanvasGroup.gameObject.SetActive(false);
        }));
    }

    private void OnSkipClicked()
    {
        if (currentStageId != null)
            MarkStageSeen(currentStageId);

        Hide();
    }

    /// <summary>Chame quando o jogador completar o movimento sozinho — encerra o estágio atual automaticamente.</summary>
    public void CompleteCurrentStage()
    {
        if (currentStageId != null)
            MarkStageSeen(currentStageId);

        Hide();
    }

    // ────────────────────────────────────────────────────────────────
    //  Animação do gesto (mão se move, seta fica fixa apontando)
    // ────────────────────────────────────────────────────────────────

    private IEnumerator GestureLoop()
    {
        while (true)
        {
            SetHandPosition(sourcePoint.position);

            yield return FadeGesture(0f, 1f, gestureFadeDuration);
            yield return MoveHand(sourcePoint.position, targetPoint.position, gestureMoveDuration);
            yield return FadeGesture(1f, 0f, gestureFadeDuration);

            yield return new WaitForSeconds(gesturePauseDuration);
        }
    }

    private void PositionArrow(Vector3 sourceWorldPos, Vector3 targetWorldPos)
    {
        if (arrowIcon == null) return;

        // Posiciona a seta no meio do caminho entre origem e destino
        Vector3 midPoint = Vector3.Lerp(sourceWorldPos, targetWorldPos, 0.5f);
        arrowIcon.position = midPoint;

        // Sprite base aponta pra CIMA (0°) — Atan2(x, y) já alinha com essa orientação
        Vector3 direction = (targetWorldPos - sourceWorldPos).normalized;
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        arrowIcon.rotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void SetHandPosition(Vector3 worldPos)
    {
        if (handIcon != null) handIcon.position = worldPos;
    }

    private IEnumerator MoveHand(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (handIcon != null) handIcon.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    private IEnumerator FadeGesture(float from, float to, float duration)
    {
        if (gestureCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            gestureCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        gestureCanvasGroup.alpha = to;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration, System.Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            overlayCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        overlayCanvasGroup.alpha = to;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Remove o registro de "já visto" de todos os estágios de tutorial conhecidos.
    /// Chamado ao resetar o progresso do jogo, para o jogador ver os tutoriais de novo.
    /// </summary>
    public static void ResetAllTutorials()
    {
        var stages = Resources.LoadAll<TutorialStageData>("");
        Debug.Log($"[Tutorial] Encontrados {stages.Length} estágios para resetar.");

        foreach (TutorialStageData stage in stages)
        {
            PlayerPrefs.DeleteKey(TutorialSeenKeyPrefix + stage.stageId);
            Debug.Log($"[Tutorial] Resetado: {stage.stageId}");
        }

        PlayerPrefs.Save(); // IMPORTANTE — confirme se isso está faltando
    }
}