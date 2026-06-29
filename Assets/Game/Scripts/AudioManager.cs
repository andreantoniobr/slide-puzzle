using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip slideTileSound;
    [SerializeField] private AudioClip correctTileSound;


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
        NumberTile.TileCorrectPositionEvent += OnTileCorrectPosition;
    }

    private void UnsubscribeInEvents()
    {
        NumberPuzzleManager.SlidedTileEvent += OnSlidedTile;
        NumberTile.TileCorrectPositionEvent += OnTileCorrectPosition;
    }

    private void OnSlidedTile()
    {
        audioSource.PlayOneShot(slideTileSound);
    }

    private void OnTileCorrectPosition()
    {
        audioSource.PlayOneShot(correctTileSound);
    }
}
