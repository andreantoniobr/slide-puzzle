using System;
using UnityEngine;

public class SFXController : AudioMain
{
    [SerializeField] private AudioClip slideTileSound;
    [SerializeField] private AudioClip correctTileSound;
    [SerializeField] private AudioClip highlightShownSound;
    [SerializeField] private AudioClip victorySound;
    
    private void Awake()
    {        
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
        NumberPuzzleManager.SolvedPuzzleEvent += OnSolvedPuzzle;
    }    

    private void UnsubscribeInEvents()
    {
        NumberPuzzleManager.SlidedTileEvent -= OnSlidedTile;
        NumberPuzzleManager.HighlightShownEvent -= OnHighlightShown;
        NumberTile.TileCorrectPositionEvent -= OnTileCorrectPosition;
        NumberPuzzleManager.SolvedPuzzleEvent -= OnSolvedPuzzle;
    }

    private void OnSlidedTile()
    {
        PlayAudio(slideTileSound);
    }

    private void OnTileCorrectPosition()
    {
        PlayAudio(correctTileSound);
    }

    private void OnHighlightShown()
    {
        PlayAudio(highlightShownSound);
    }

    private void OnSolvedPuzzle(int time, int movements)
    {
        PlayAudio(victorySound);
    }
}
