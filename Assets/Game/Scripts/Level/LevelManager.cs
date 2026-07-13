using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private NumberPuzzleManager puzzleManager;
    [SerializeField] private TutorialOverlayController tutorialOverlay; // NOVO

    private const string CurrentLevelKey = "CurrentLevelIndex";

    private bool isFirstLoad = true;

    private int currentLevelIndex;

    public static event System.Action<int> LevelLoadedEvent; // caso ainda não tenha adicionado antes

    public int CurrentLevelNumber => currentLevelIndex + 1;

    private void Awake()
    {
        currentLevelIndex = PlayerPrefs.GetInt(CurrentLevelKey, 0);
    }

    private void Start()
    {
        LoadCurrentLevel();
    }

    public void LoadCurrentLevel()
    {
        LevelConfig config = GetConfigForLevel(currentLevelIndex);
        puzzleManager.LoadLevel(config);

        LevelLoadedEvent?.Invoke(CurrentLevelNumber);

        if (isFirstLoad)
        {
            isFirstLoad = false;
            PokiManager.Instance.NotifyLoadingFinished();
        }

        TryShowTutorialForThisLevel(config); // NOVO

    }

    public void RestartLevel()
    {
        puzzleManager.RestartLevel();
    }

    public void GoToNextLevel()
    {
        currentLevelIndex++;
        PlayerPrefs.SetInt(CurrentLevelKey, currentLevelIndex);
        PlayerPrefs.Save();
        LoadCurrentLevel();
    }

    public void GoToLevel(int levelNumber)
    {
        int index = Mathf.Max(0, levelNumber - 1);
        currentLevelIndex = index;
        PlayerPrefs.SetInt(CurrentLevelKey, currentLevelIndex);
        PlayerPrefs.Save();
        LoadCurrentLevel();
    }

    public void ResetProgress()
    {
        currentLevelIndex = 0;
        PlayerPrefs.SetInt(CurrentLevelKey, 0);

        TutorialOverlayController.ResetAllTutorials(); // NOVO

        PlayerPrefs.Save();
        LoadCurrentLevel();
    }

    private LevelConfig GetConfigForLevel(int index)
    {
        int handMadeCount = levelDatabase != null ? levelDatabase.levels.Count : 0;

        if (index < handMadeCount)
            return levelDatabase.levels[index].ToConfig();

        int proceduralIndex = index - handMadeCount;
        return ProceduralLevelGenerator.Generate(proceduralIndex);
    }

    // ────────────────────────────────────────────────────────────────
    //  Tutorial (NOVO)
    // ────────────────────────────────────────────────────────────────

private void TryShowTutorialForThisLevel(LevelConfig config)
{
    if (tutorialOverlay == null) return;
    if (config.tutorialStage == null) return;
    if (TutorialOverlayController.HasSeenStage(config.tutorialStage.stageId)) return;

    RectTransform sourceTile = null;
    RectTransform targetCell = null;

    if (config.tutorialStage.gestureType == TutorialGestureType.BasicSwipe)
    {
        (sourceTile, targetCell) = puzzleManager.GetFirstMovableTileAndTarget();
    }
    else if (config.tutorialStage.gestureType == TutorialGestureType.MultiEmptySelection)
    {
        (sourceTile, targetCell) = puzzleManager.GetFirstAmbiguousTileAndTarget();
    }

    if (sourceTile == null || targetCell == null) return;

    tutorialOverlay.Show(config.tutorialStage, sourceTile, targetCell);

    void OnFirstSlide()
    {
        tutorialOverlay.CompleteCurrentStage();
        NumberPuzzleManager.SlidedTileEvent -= OnFirstSlide;
    }
    NumberPuzzleManager.SlidedTileEvent += OnFirstSlide;
}
}