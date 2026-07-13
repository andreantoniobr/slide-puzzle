using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class TileCorrectGlowEffect : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem correctParticles;

    private static readonly int IsCorrectProperty = Shader.PropertyToID("_IsCorrect");
    private static readonly int TimestampProperty = Shader.PropertyToID("_CorrectTimestamp");
    private static readonly int IsSelectedProperty = Shader.PropertyToID("_IsSelected"); // NOVO

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
            hasInitialized = true;
            wasCorrect = isCorrect;
            materialInstance.SetFloat(IsCorrectProperty, isCorrect ? 1f : 0f);
            return;
        }

        if (isCorrect && !wasCorrect)
        {
            materialInstance.SetFloat(TimestampProperty, Time.time);

            if (correctParticles != null)
            {
                correctParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                correctParticles.Play();
            }
        }

        materialInstance.SetFloat(IsCorrectProperty, isCorrect ? 1f : 0f);
        wasCorrect = isCorrect;
    }

    /// <summary>
    /// Liga/desliga o glow de borda rotativo — usado quando a peça
    /// tem mais de um vazio adjacente e está aguardando o jogador escolher.
    /// </summary>
    public void SetSelected(bool selected)
    {
        materialInstance.SetFloat(IsSelectedProperty, selected ? 1f : 0f);
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
            Destroy(materialInstance);
    }
}