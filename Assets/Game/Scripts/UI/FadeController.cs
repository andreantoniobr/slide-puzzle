using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla um fade to black full-screen, reutilizável para
/// transição de nível, restart, ou qualquer troca de tela.
/// Usa Time.unscaledDeltaTime para funcionar mesmo com o jogo pausado (timeScale = 0).
/// </summary>
public class FadeController : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration  = 0.35f;

    private void Awake()
    {
        if (fadeImage == null) return;

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;
        fadeImage.raycastTarget = false;
    }

    public IEnumerator FadeOut()
    {
        yield return Fade(0f, 1f, fadeOutDuration);
        if (fadeImage != null) fadeImage.raycastTarget = true; // bloqueia cliques atrás do preto
    }

    public IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f, fadeInDuration);
        if (fadeImage != null) fadeImage.raycastTarget = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color c = fadeImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(from, to, t);
            fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        fadeImage.color = c;
    }
}