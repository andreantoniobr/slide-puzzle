using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMode : MonoBehaviour
{       
    [SerializeField] private HUDManager hudManager;
    [SerializeField] private LevelManager levelManager;

    [Header("Victory")]
    [SerializeField] private float victoryDelaySeconds = 0.6f;

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
        Time.timeScale = 1f;
        isGamePaused = false;
        isGameStarted = true;

        hudManager.SetActiveOverlay(OverlayName.MainHud);
        levelManager.RestartLevel();
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        isGameStarted = true;

        hudManager.SetActiveOverlay(OverlayName.MainHud);
        levelManager.GoToNextLevel();
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