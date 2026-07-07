using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private NumberPuzzleManager puzzleManager;

    private const string CurrentLevelKey = "CurrentLevelIndex";

    private int currentLevelIndex;

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
        int index = Mathf.Max(0, levelNumber - 1); // levelNumber é 1-based pra quem usa (nível 1, 2, 3...)
        currentLevelIndex = index;
        PlayerPrefs.SetInt(CurrentLevelKey, currentLevelIndex);
        PlayerPrefs.Save();
        LoadCurrentLevel();
    }

    public void ResetProgress()
    {
        currentLevelIndex = 0;
        PlayerPrefs.SetInt(CurrentLevelKey, 0);
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
}