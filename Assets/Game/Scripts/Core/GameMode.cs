using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMode : MonoBehaviour
{       
    [SerializeField] private HUDManager hudManager;
   

    [Header("Music")]
    
    [SerializeField] private HUDAudioController HUDAudioController;   

    

    private bool isGameStarted = false;
    private bool isGamePaused = false;



    public bool IsGameStarted => isGameStarted;
    public bool IsGamePaused => isGamePaused;


    private void Awake()
    {  
        hudManager.SetActiveOverlay(OverlayName.MainHud);
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

    

    public void PauseAndResumeGame()
    {
        HUDAudioController.PlayButtonPressSound();
        if (isGameStarted)
        {
            isGamePaused = !isGamePaused;
            if (isGamePaused)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }        
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
