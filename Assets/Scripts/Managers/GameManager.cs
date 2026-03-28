using UnityEngine;

public enum GameState
{
    Playing,
    Victory,
    Defeat
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private bool persistBetweenScenes;

    public float CasinoDeposit;

    public GameState State { get; private set; } = GameState.Playing;

    public bool IsGameOver => State != GameState.Playing;

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
