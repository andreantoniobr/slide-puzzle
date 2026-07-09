using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PokiManager : MonoBehaviour
{
    public static PokiManager Instance { get; private set; }

    private bool isGameplayActive;
    private bool hasFiredLoadingFinished;
    private bool isAdPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PokiUnitySDK.Instance.init(); // funciona no Editor também (mock com 0.5s de delay) e em WebGL real
    }

    public void NotifyLoadingFinished()
    {
        if (hasFiredLoadingFinished) return;
        hasFiredLoadingFinished = true;

        PokiUnitySDK.Instance.gameLoadingFinished();
        Debug.Log("[Poki] gameLoadingFinished disparado");
    }

    public void NotifyGameplayStart()
    {
        if (isGameplayActive || isAdPlaying) return;
        isGameplayActive = true;

        PokiUnitySDK.Instance.gameplayStart();
        Debug.Log("[Poki] gameplayStart disparado");
    }

    public void NotifyGameplayStop()
    {
        if (!isGameplayActive) return;
        isGameplayActive = false;

        PokiUnitySDK.Instance.gameplayStop();
        Debug.Log("[Poki] gameplayStop disparado");
    }

    public void RequestCommercialBreak(Action onComplete)
    {
        NotifyGameplayStop();

        isAdPlaying = true;
        SetGameMuted(true);
        SetInputEnabled(false);

#if UNITY_EDITOR
        // No Editor, o próprio PokiUnitySDK NÃO dispara commercialBreakCallBack (ver commercialBreakCompleted()).
        // Então chamamos o fallback diretamente aqui, sem depender do callback do SDK.
        PokiUnitySDK.Instance.commercialBreak(); // só pra logar/simular no Console
        FinishCommercialBreak(onComplete);
#else
        PokiUnitySDK.Instance.commercialBreakCallBack = () => FinishCommercialBreak(onComplete);
        PokiUnitySDK.Instance.commercialBreak();
#endif
    }

    private void FinishCommercialBreak(Action onComplete)
    {
        isAdPlaying = false;
        SetGameMuted(false);
        SetInputEnabled(true);

        Debug.Log("[Poki] commercialBreak finalizado");
        onComplete?.Invoke();
    }

    private void SetGameMuted(bool muted)
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.SetTemporaryMute(muted);
    }

    private void SetInputEnabled(bool enabled)
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es != null) es.enabled = enabled;
    }
}