using UnityEngine;

public enum GameState
{
    Playing,
    Paused,
    Victory,
    Defeat
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private bool persistBetweenScenes;

    
    public int Time = 0; //From 0 to 86400 = 24*60*60

    public float CasinoDeposit;
    public float MaxDeposit = 1e3f;
    //percentage
    public float Paranoia = 0f;

    public GameState State { get; private set; } = GameState.Playing;

    public bool IsGameOver => State != GameState.Playing;

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
