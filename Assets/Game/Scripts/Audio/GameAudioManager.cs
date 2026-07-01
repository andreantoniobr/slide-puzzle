using UnityEngine;
using UnityEngine.Audio;
using System.IO;
using System;



public class GameAudioManager : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    private readonly float dBMin = -60f;
    private readonly float dBMax = 0f;

    private string audioDataPath;

    private GameAudioData _audioData;
    public GameAudioData AudioData => audioData; // mantém para uso externo

    private GameAudioData audioData
    {
        get
        {
            if (_audioData == null)
            {
                audioDataPath ??= $"{Application.persistentDataPath}/AudioDataSave.json";
                _audioData = LoadAudioData() ?? new GameAudioData();
            }
            return _audioData;
        }
        set => _audioData = value;
    }

    public bool IsMusicEnabled => audioData.MusicEnabled;
    public bool IsSFXEnabled => audioData.SFXEnabled;

    public static event Action<bool> OnMusicToggled;
    public static event Action<bool> OnSFXToggled;

    
    private void Start()
    {
        ApplyAudioData(audioData);
        OnMusicToggled?.Invoke(audioData.MusicEnabled);
        OnSFXToggled?.Invoke(audioData.SFXEnabled);
    }

    public void SetVolumes(float main, float music, float sfx)
    {
        audioData.MainVolume = Mathf.Clamp01(main);
        audioData.MusicVolume = Mathf.Clamp01(music);
        audioData.SFXVolume = Mathf.Clamp01(sfx);

        ApplyAudioData(audioData);
        SaveAudioData(audioData);
    }

    public void ToggleMusic()
    {
        audioData.MusicEnabled = !audioData.MusicEnabled;
        ApplyMusicToggle();
        SaveAudioData(audioData);
        OnMusicToggled?.Invoke(audioData.MusicEnabled);
    }

    public void ToggleSFX()
    {
        audioData.SFXEnabled = !audioData.SFXEnabled;
        ApplySFXToggle();
        SaveAudioData(audioData);
        OnSFXToggled?.Invoke(audioData.SFXEnabled);
    }

    private void ApplyAudioData(GameAudioData data)
    {
        if (!audioMixer) return;
        audioMixer.SetFloat("MainVolume", Mathf.Lerp(dBMin, dBMax, data.MainVolume));
        ApplyMusicToggle();
        ApplySFXToggle();
    }

    private void ApplyMusicToggle()
    {
        if (!audioMixer) return;
        float volume = audioData.MusicEnabled ? Mathf.Lerp(dBMin, dBMax, audioData.MusicVolume) : dBMin;
        audioMixer.SetFloat("MusicVolume", volume);
    }

    private void ApplySFXToggle()
    {
        if (!audioMixer) return;
        float volume = audioData.SFXEnabled ? Mathf.Lerp(dBMin, dBMax, audioData.SFXVolume) : dBMin;
        audioMixer.SetFloat("SFXVolume", volume);
    }

    private void SaveAudioData(GameAudioData data)
    {
        File.WriteAllText(audioDataPath, JsonUtility.ToJson(data, true));
    }

    private GameAudioData LoadAudioData()
    {
        if (!File.Exists(audioDataPath)) return null;
        string json = File.ReadAllText(audioDataPath);
        return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<GameAudioData>(json);
    }

    public void ResetAudioData()
    {
        audioData = new GameAudioData();
        ApplyAudioData(audioData);
        SaveAudioData(audioData);
        OnMusicToggled?.Invoke(audioData.MusicEnabled);
        OnSFXToggled?.Invoke(audioData.SFXEnabled);
    }
}