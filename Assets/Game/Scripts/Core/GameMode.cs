using System;
using System.Collections;
using UnityEngine;

public class GameMode : MonoBehaviour
{       
    [SerializeField] private HUDManager hudManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private FadeController fadeController;

    [Header("Victory")]
    [SerializeField] private float victoryDelaySeconds = 0.6f;

    [Header("Transição de Nível")]
    [SerializeField] private AudioSource transitionAudioSource;
    [SerializeField] private AudioClip   levelTransitionSound;
    [SerializeField] private AudioClip   restartSound;

    private bool isGameStarted = true;
    private bool isGamePaused = false;

    public bool IsGameStarted => isGameStarted;
    public bool IsGamePaused => isGamePaused;

    private void Awake()
    {  
        hudManager.SetActiveOverlay(OverlayName.MainHud);
        NumberPuzzleManager.SolvedPuzzleEvent += OnSolvedPuzzed;
    }

    private void OnDestroy()
    {
        NumberPuzzleManager.SolvedPuzzleEvent -= OnSolvedPuzzed;
    }

    private void Pause()
    {
        Time.timeScale = 0f;
        hudManager.SetActiveOverlay(OverlayName.Pause);
    }

    private void Resume()
    {
        Time.timeScale = 1f;
        hudManager.SetActiveOverlay(OverlayName.MainHud);
    }

    private void OnSolvedPuzzed(int movements, int time)
    {
        StartCoroutine(ShowVictoryDelayed());
    }

    private IEnumerator ShowVictoryDelayed()
    {
        yield return new WaitForSeconds(victoryDelaySeconds);
        hudManager.SetActiveOverlay(OverlayName.Victory);
    }

    public void PauseAndResumeGame()
    {
        if (isGameStarted)
        {
            isGamePaused = !isGamePaused;
            if (isGamePaused) Pause();
            else Resume();
        }        
    }

    public void RestartGame()
    {
        StartCoroutine(TransitionAndExecute(restartSound, () => levelManager.RestartLevel()));
    }

    public void NextLevel()
    {
        StartCoroutine(TransitionAndExecute(levelTransitionSound, () => levelManager.GoToNextLevel()));
    }

    private IEnumerator TransitionAndExecute(AudioClip sound, Action action)
    {
        if (sound != null && transitionAudioSource != null)
            transitionAudioSource.PlayOneShot(sound);

        if (fadeController != null)
            yield return StartCoroutine(fadeController.FadeOut());

        Time.timeScale = 1f;
        isGamePaused = false;
        isGameStarted = true;

        hudManager.SetActiveOverlay(OverlayName.MainHud);
        action?.Invoke();

        if (fadeController != null)
            yield return StartCoroutine(fadeController.FadeIn());
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public void Settings()
    {
        hudManager.SetActiveOverlay(OverlayName.Settings);
    }

    public void CloseSettings()
    {
        hudManager.SetActiveOverlay(OverlayName.MainHud);
    }
}