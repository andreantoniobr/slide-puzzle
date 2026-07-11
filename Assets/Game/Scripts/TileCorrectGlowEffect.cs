using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aplica um brilho diagonal (shine) que varre a peça UMA ÚNICA VEZ,
/// no momento exato em que ela chega na posição correta.
/// </summary>
[RequireComponent(typeof(Image))]
public class TileCorrectGlowEffect : MonoBehaviour
{
    private static readonly int IsCorrectProperty = Shader.PropertyToID("_IsCorrect");
    private static readonly int TimestampProperty = Shader.PropertyToID("_CorrectTimestamp");

    private Image image;
    private Material materialInstance;
    private bool wasCorrect;
    private bool hasInitialized;

    private void Awake()
    {
        image = GetComponent<Image>();
        materialInstance = new Material(image.material);
        image.material = materialInstance;
    }

    public void SetCorrect(bool isCorrect)
    {
        if (!hasInitialized)
        {
            // Primeira chamada (montagem do tabuleiro): só registra o estado, sem disparar shine
            hasInitialized = true;
            wasCorrect = isCorrect;
            materialInstance.SetFloat(IsCorrectProperty, isCorrect ? 1f : 0f);
            return;
        }

        // Só dispara o shine quando a peça PASSA a ficar correta agora (transição real)
        if (isCorrect && !wasCorrect)
        {
            materialInstance.SetFloat(TimestampProperty, Time.time);
        }

        materialInstance.SetFloat(IsCorrectProperty, isCorrect ? 1f : 0f);
        wasCorrect = isCorrect;
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
            Destroy(materialInstance);
    }
}