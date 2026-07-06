using UnityEngine;

public class MusicController : AudioMain
{
    [SerializeField] private AudioClip backgroundMusic;
    private AudioSource musicSource;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();
        musicSource.clip = backgroundMusic;
        musicSource.loop = true;

        SubscribeInEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeInEvents();
    }

    private void Start()
    {
        PlayMusic();
    }

    private void SubscribeInEvents()
    {
        NumberPuzzleManager.SolvedPuzzleEvent += OnSolvedPuzzle;
        NumberPuzzleManager.PuzzleStartedEvent += OnPuzzleStarted; // ver abaixo
    }

    private void UnsubscribeInEvents()
    {
        NumberPuzzleManager.SolvedPuzzleEvent -= OnSolvedPuzzle;
        NumberPuzzleManager.PuzzleStartedEvent -= OnPuzzleStarted;
    }

    private void OnSolvedPuzzle(int time, int movements)
    {
        musicSource.Stop();
    }

    private void OnPuzzleStarted()
    {
        PlayMusic();
    }

    private void PlayMusic()
    {
        if (!musicSource.isPlaying)
            musicSource.Play();
    }
}