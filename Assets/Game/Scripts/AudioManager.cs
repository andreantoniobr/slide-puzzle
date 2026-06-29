using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip slideTileSound;
    [SerializeField] private AudioClip correctTileSound;
    [SerializeField] private AudioClip highlightShownSound;


    private AudioSource audioSource;

    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        SubscribeInEvents();
    } 

    private void OnDestroy()
    {
        UnsubscribeInEvents();
    }

    private void SubscribeInEvents()
    {
        NumberPuzzleManager.SlidedTileEvent += OnSlidedTile;
        NumberPuzzleManager.HighlightShownEvent += OnHighlightShown;
        NumberTile.TileCorrectPositionEvent += OnTileCorrectPosition;
    }

    

    private void UnsubscribeInEvents()
    {
        NumberPuzzleManager.SlidedTileEvent -= OnSlidedTile;
        NumberPuzzleManager.HighlightShownEvent -= OnHighlightShown;
        NumberTile.TileCorrectPositionEvent -= OnTileCorrectPosition;
    }

    private void OnSlidedTile()
    {
        audioSource.PlayOneShot(slideTileSound);
    }

    private void OnTileCorrectPosition()
    {
        audioSource.PlayOneShot(correctTileSound);
    }

    private void OnHighlightShown()
    {
        audioSource.PlayOneShot(highlightShownSound);
    }
}
