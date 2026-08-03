using System;
using UnityEngine;

public class SFXController : AudioMain
{
    [SerializeField] private AudioClip slideTileSound;
    [SerializeField] private AudioClip correctTileSound;
    [SerializeField] private AudioClip highlightShownSound;
    [SerializeField] private AudioClip victorySound;

    [Header("Tiles Especiais")]
    [SerializeField] private AudioClip rockCrackSound;
    [SerializeField] private AudioClip rockBreakSound;
    [SerializeField] private AudioClip lockOpenSound;
    [SerializeField] private AudioClip questionRevealedSound;

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

        NumberTile.RockCrackEvent += OnRockCrack;
        NumberTile.RockBreakEvent += OnRockBreak;
        NumberTile.LockOpenEvent += OnLockOpen;
        NumberTile.QuestionRevealedEvent += OnQuestionRevealed;
    }

    private void UnsubscribeInEvents()
    {
        NumberPuzzleManager.SlidedTileEvent -= OnSlidedTile;
        NumberPuzzleManager.HighlightShownEvent -= OnHighlightShown;
        NumberTile.TileCorrectPositionEvent -= OnTileCorrectPosition;
        NumberPuzzleManager.SolvedPuzzleEvent -= OnSolvedPuzzle;

        NumberTile.RockCrackEvent -= OnRockCrack;
        NumberTile.RockBreakEvent -= OnRockBreak;
        NumberTile.LockOpenEvent -= OnLockOpen;
        NumberTile.QuestionRevealedEvent -= OnQuestionRevealed;
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

    private void OnRockCrack()
    {
        PlayAudio(rockCrackSound);
    }

    private void OnRockBreak()
    {
        PlayAudio(rockBreakSound);
    }

    private void OnLockOpen()
    {
        PlayAudio(lockOpenSound);
    }
    
    private void OnQuestionRevealed()
    {
        PlayAudio(questionRevealedSound);
    }
}