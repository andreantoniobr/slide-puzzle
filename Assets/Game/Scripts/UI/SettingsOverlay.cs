using UnityEngine;
using TMPro;
using UnityEngine.UI;

public static class ButtonTexts
{
    public const string deleteDataButtonText = "DELETE DATA";
    public const string deletetButtonText = "DELETED!";
}

public class SettingsOverlay : MonoBehaviour
{
    [SerializeField] private GameAudioManager gameAudioManager;

    [SerializeField] private TextMeshProUGUI deleteDataText;

    [Header("Sliders")]
    [SerializeField] private Slider mainVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider SFXVolumeSlider;

    private void OnEnable()
    {
        UpdateSliders();
        SetDeleteDataButtonText();
    }

    private void OnDisable()
    {
        SaveCurrentSliderValues();
    }

    private void UpdateSliders()
    {
        if (!gameAudioManager) return;

        GameAudioData data = gameAudioManager.AudioData;
        if (data == null) return;

        if (mainVolumeSlider) mainVolumeSlider.value = data.MainVolume;
        if (musicVolumeSlider) musicVolumeSlider.value = data.MusicVolume;
        if (SFXVolumeSlider) SFXVolumeSlider.value = data.SFXVolume;
    }

    private void SaveCurrentSliderValues()
    {
        if (!gameAudioManager) return;
        if (!mainVolumeSlider || !musicVolumeSlider || !SFXVolumeSlider) return;

        gameAudioManager.SetVolumes(
            mainVolumeSlider.value,
            musicVolumeSlider.value,
            SFXVolumeSlider.value
        );
    }

    private void SetDeleteDataButtonText()
    {
        if (deleteDataText) deleteDataText.text = ButtonTexts.deleteDataButtonText;
    }

    public void OnMainVolumeChange(float value)
    {
        if (gameAudioManager) gameAudioManager.SetVolumes(value, musicVolumeSlider.value, SFXVolumeSlider.value);
    }

    public void OnMusicVolumeChange(float value)
    {
        if (gameAudioManager) gameAudioManager.SetVolumes(mainVolumeSlider.value, value, SFXVolumeSlider.value);
    }

    public void OnSFXVolumeChange(float value)
    {
        if (gameAudioManager) gameAudioManager.SetVolumes(mainVolumeSlider.value, musicVolumeSlider.value, value);
    }

    public void DeleteGameData()
    {
        if (!gameAudioManager) return;

        gameAudioManager.ResetAudioData();
        UpdateSliders();

        if (deleteDataText) deleteDataText.text = ButtonTexts.deletetButtonText;
    }
}