using UnityEngine;

/// <summary>
/// Ativa as partículas de confete sempre que este GameObject (a overlay de Victory)
/// for habilitado. Coloque este script no mesmo GameObject da tela de Victory.
/// </summary>
public class VictoryConfetti : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] confettiSystems;
    [SerializeField] private bool restartOnEnable = true;

    private void OnEnable()
    {
        PlayConfetti();
    }

    private void OnDisable()
    {
        StopConfetti();
    }

    private void PlayConfetti()
    {
        if (confettiSystems == null) return;

        foreach (ParticleSystem ps in confettiSystems)
        {
            if (ps == null) continue;

            if (restartOnEnable)
                ps.Clear(); // garante que não sobrem partículas de uma vitória anterior

            ps.Play();
        }
    }

    private void StopConfetti()
    {
        if (confettiSystems == null) return;

        foreach (ParticleSystem ps in confettiSystems)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}