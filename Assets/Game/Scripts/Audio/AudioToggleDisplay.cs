using UnityEngine;
using TMPro;

public class AudioToggleDisplay : MonoBehaviour
{
    [SerializeField] private GameAudioManager gameAudioManager;

    [Header("Music Toggle")]
    [SerializeField] private TextMeshProUGUI musicToggleText;
    [SerializeField] private string musicOnText = "Music: ON";
    [SerializeField] private string musicOffText = "Music: OFF";

    [Header("SFX Toggle")]
    [SerializeField] private TextMeshProUGUI sfxToggleText;
    [SerializeField] private string sfxOnText = "SFX: ON";
    [SerializeField] private string sfxOffText = "SFX: OFF";

    private void OnEnable()
    {
        GameAudioManager.OnMusicToggled += HandleMusicToggle;
        GameAudioManager.OnSFXToggled += HandleSFXToggle;

        RefreshFromCurrentState();
    }

    private void OnDisable()
    {
        GameAudioManager.OnMusicToggled -= HandleMusicToggle;
        GameAudioManager.OnSFXToggled -= HandleSFXToggle;
    }

    private void RefreshFromCurrentState()
    {
        if (!gameAudioManager) return;

        HandleMusicToggle(gameAudioManager.IsMusicEnabled);
        HandleSFXToggle(gameAudioManager.IsSFXEnabled);
    }

    private void HandleMusicToggle(bool isOn)
    {
        if (musicToggleText) musicToggleText.text = isOn ? musicOnText : musicOffText;
    }

    private void HandleSFXToggle(bool isOn)
    {
        if (sfxToggleText) sfxToggleText.text = isOn ? sfxOnText : sfxOffText;
    }
}