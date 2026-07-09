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

#if !UNITY_EDITOR && UNITY_WEBGL
        PokiUnitySDK.Instance.init();
#endif
    }

    public void NotifyLoadingFinished()
    {
        if (hasFiredLoadingFinished) return;
        hasFiredLoadingFinished = true;

#if !UNITY_EDITOR && UNITY_WEBGL
        PokiUnitySDK.Instance.gameLoadingFinished();
#endif
        Debug.Log("[Poki] gameLoadingFinished disparado");
    }

    public void NotifyGameplayStart()
    {
        if (isGameplayActive || isAdPlaying) return;
        isGameplayActive = true;

#if !UNITY_EDITOR && UNITY_WEBGL
        PokiUnitySDK.Instance.gameplayStart();
#endif
        Debug.Log("[Poki] gameplayStart disparado");
    }

    public void NotifyGameplayStop()
    {
        if (!isGameplayActive) return;
        isGameplayActive = false;

#if !UNITY_EDITOR && UNITY_WEBGL
        PokiUnitySDK.Instance.gameplayStop();
#endif
        Debug.Log("[Poki] gameplayStop disparado");
    }

    public void RequestCommercialBreak(Action onComplete)
    {
        NotifyGameplayStop();

        isAdPlaying = true;
        SetGameMuted(true);
        SetInputEnabled(false);

#if !UNITY_EDITOR && UNITY_WEBGL
        // Só existe ponte com o SDK real dentro de um build WebGL publicado
        if (PokiUnitySDK.Instance.isInitialized())
        {
            PokiUnitySDK.Instance.commercialBreakCallBack = () => FinishCommercialBreak(onComplete);
            PokiUnitySDK.Instance.commercialBreak();
            return;
        }
#endif
        // Editor, ou WebGL rodando fora da Poki (SDK não inicializado): segue direto, sem anúncio
        FinishCommercialBreak(onComplete);
    }

    private void FinishCommercialBreak(Action onComplete)
    {
        isAdPlaying = false;
        SetGameMuted(false);
        SetInputEnabled(true);

        Debug.Log("[Poki] commercialBreak finalizado (ou pulado fora do ambiente Poki)");
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