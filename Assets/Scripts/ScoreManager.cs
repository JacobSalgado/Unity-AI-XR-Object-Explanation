using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Tracks score and a countdown timer for a play session, and persists the 
/// best score across sessions via PlayerPrefs. 
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private int swishBonus = 1; // extra points awarded for a clean swish over a bank shot

    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool IsGameActive { get; private set; }

    private const string HighScoreKey = "HighScore";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsGameActive) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            EndGame();
        }
    }

    public void StartGame()
    {
        CurrentScore = 0;
        TimeRemaining = gameDuration;
        IsGameActive = true;
    }

    public void RegisterScore(bool wasSwish)
    {
        if (!IsGameActive) return;

        int points = 1 + (wasSwish ? swishBonus : 0);
        CurrentScore += points;
    }

    private void EndGame()
    {
        IsGameActive = false;
        TimeRemaining = 0f;

        if (CurrentScore > HighScore)
        {
            HighScore = CurrentScore;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
        }
    }
}
