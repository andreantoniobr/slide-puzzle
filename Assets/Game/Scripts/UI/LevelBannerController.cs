using System.Collections;
using TMPro;
using UnityEngine;

public class LevelBannerController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private CanvasGroup bannerCanvasGroup;
    [SerializeField] private TMP_Text levelText;

    [Header("Posição Visível — Portrait")]
    [SerializeField] private Vector2 visiblePositionPortrait = Vector2.zero;

    [Header("Posição Visível — Landscape")]
    [SerializeField] private Vector2 visiblePositionLandscape = Vector2.zero;

    [Header("Posição Escondida (offset a partir da posição visível)")]
    [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, 200f);

    [Header("Timing")]
    [SerializeField] private float initialDelay = 0f;
    [SerializeField] private float slideInDuration = 0.35f;
    [SerializeField] private float visibleDuration = 1.5f;
    [SerializeField] private float slideOutDuration = 0.3f;

    [Header("Formatação")]
    [SerializeField] private string textFormat = "Nível {0}";

    private Coroutine activeRoutine;

    private Vector2 CurrentVisiblePosition =>
        IsPortrait() ? visiblePositionPortrait : visiblePositionLandscape;

    private Vector2 CurrentHiddenPosition =>
        CurrentVisiblePosition + hiddenOffset;

    private bool IsPortrait() => Screen.height > Screen.width;

    private void Awake()
    {
        if (bannerRoot != null)
            bannerRoot.anchoredPosition = CurrentHiddenPosition;

        if (bannerCanvasGroup != null)
            bannerCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        LevelManager.LevelLoadedEvent += Show;
    }

    private void OnDisable()
    {
        LevelManager.LevelLoadedEvent -= Show;
    }

    public void Show(int levelNumber)
    {
        if (bannerRoot == null) return;

        if (levelText != null)
            levelText.text = string.Format(textFormat, levelNumber);

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        Vector2 visible = CurrentVisiblePosition; // trava a orientação no início da animação
        Vector2 hidden  = visible + hiddenOffset;

        bannerRoot.anchoredPosition = hidden;

        yield return AnimateInOut(hidden, visible, 0f, 1f, slideInDuration);
        yield return new WaitForSeconds(visibleDuration);
        yield return AnimateInOut(visible, hidden, 1f, 0f, slideOutDuration);

        activeRoutine = null;
    }

    private IEnumerator AnimateInOut(Vector2 fromPos, Vector2 toPos, float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            bannerRoot.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);

            if (bannerCanvasGroup != null)
                bannerCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);

            yield return null;
        }

        bannerRoot.anchoredPosition = toPos;
        if (bannerCanvasGroup != null)
            bannerCanvasGroup.alpha = toAlpha;
    }
}