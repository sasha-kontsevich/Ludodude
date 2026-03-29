using UnityEngine;
using UnityEngine.Serialization;

public enum GameState
{
    Playing,
    Paused,
    Victory,
    Defeat
}

public class GameManager : MonoBehaviour
{
    public const float WinUnlockBet = 1_000_000f;

    public int Level = 1;
    public static GameManager Instance { get; private set; }

    [SerializeField] private bool persistBetweenScenes;

    
    public int Time = 0; //From 0 to 86400 = 24*60*60

    public float CasinoDeposit;

    public float GoalDeposit => 67f * Mathf.Pow(2f, Level);
    public float FakeWinChance01 => CalculateFakeWinChance01(GoalDeposit);
    public float FakeWinChancePercent => FakeWinChance01 * 100f;

    //percentage
    public float Paranoia = 0f;

    public GameState State { get; private set; } = GameState.Playing;

    public bool IsGameOver => State != GameState.Playing;

    public static float CalculateFakeWinChance01(float stakeAmount)
    {
        float threshold = Mathf.Max(2f, WinUnlockBet);
        float safeStake = Mathf.Max(1f, stakeAmount);

        float numerator = Mathf.Log(safeStake, 2f);
        float denominator = Mathf.Log(threshold, 2f);
        if (denominator <= 0f)
            return 0f;

        return Mathf.Clamp01(numerator / denominator);
    }

    public void Pause()
    {
        State = GameState.Paused;
        //To Do
    }
    public void Resume()
    {
        State = GameState.Playing;
        //To Do
    }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetVictory()
    {
        if (State != GameState.Playing) return;
        State = GameState.Victory;
    }

    public void SetDefeat()
    {
        if (State != GameState.Playing) return;
        State = GameState.Defeat;
    }

    public void ResetGameState()
    {
        State = GameState.Playing;
    }
}
