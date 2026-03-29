using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Зона глобальной музыки: пока игрок находится внутри триггера,
/// гарантируется проигрывание музыки по заданному ключу.
/// </summary>
[DisallowMultipleComponent]
public class GlobalMusicTrigger2D : MonoBehaviour
{
    [SerializeField] private Collider2D zoneTrigger;
    [SerializeField] private string musicKey = "main_theme";
    [SerializeField] private List<string> musicKeys = new List<string>();
    [SerializeField] private bool stopMusicOnExit;
    [SerializeField] private bool restartIfSameOnEnsure;
    [SerializeField] private bool avoidImmediateRepeat = true;
    [SerializeField] private bool recoverPlayerWhenTeleportedInside = true;

    private TopDownPlayerController _playerInside;
    private string _selectedMusicKey;
    private readonly List<Collider2D> _overlapResults = new List<Collider2D>(8);
    private ContactFilter2D _overlapFilter;
    private bool _warnedNoPlayableMusic;

    private void Reset()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<Collider2D>();

        if (zoneTrigger != null)
            zoneTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<Collider2D>();

        if (zoneTrigger != null)
            zoneTrigger.isTrigger = true;

        if (zoneTrigger != null && zoneTrigger.gameObject != gameObject)
        {
            var relay = zoneTrigger.GetComponent<GlobalMusicTriggerRelay2D>();
            if (relay == null)
                relay = zoneTrigger.gameObject.AddComponent<GlobalMusicTriggerRelay2D>();
            relay.Init(this);
        }

        _overlapFilter = new ContactFilter2D
        {
            useTriggers = true
        };
    }

    private void Start()
    {
        TryRecoverPlayerInsideZone();
    }

    private void FixedUpdate()
    {
        if (_playerInside == null)
        {
            TryRecoverPlayerInsideZone();
            return;
        }

        // Дополнительный ensure на случай внешних переключений музыки.
        EnsureMusicPlaying();
    }

    private void OnDisable()
    {
        _playerInside = null;
        _selectedMusicKey = null;
        _warnedNoPlayableMusic = false;
    }

    private void OnValidate()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<Collider2D>();

        if (zoneTrigger != null)
            zoneTrigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleTriggerEnter(other);

    private void OnTriggerStay2D(Collider2D other) => HandleTriggerStay(other);

    private void OnTriggerExit2D(Collider2D other) => HandleTriggerExit(other);

    internal void HandleTriggerEnter(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null)
            return;

        _playerInside = player;
        _selectedMusicKey = SelectRandomMusicKey();
        _warnedNoPlayableMusic = false;
        EnsureMusicPlaying();
    }

    internal void HandleTriggerStay(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null || (_playerInside != null && player != _playerInside))
            return;

        _playerInside = player;
        EnsureMusicPlaying();
    }

    internal void HandleTriggerExit(Collider2D other)
    {
        var player = other.GetComponentInParent<TopDownPlayerController>();
        if (player == null || player != _playerInside)
            return;

        _playerInside = null;
        _selectedMusicKey = null;
        _warnedNoPlayableMusic = false;
        if (stopMusicOnExit)
            AudioManager.Instance?.StopMusic();
    }

    private void TryRecoverPlayerInsideZone()
    {
        if (!recoverPlayerWhenTeleportedInside || zoneTrigger == null)
            return;

        _overlapResults.Clear();
        int count = zoneTrigger.Overlap(_overlapFilter, _overlapResults);
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var other = _overlapResults[i];
            if (other == null)
                continue;

            var player = other.GetComponentInParent<TopDownPlayerController>();
            if (player == null)
                continue;

            _playerInside = player;
            if (string.IsNullOrWhiteSpace(_selectedMusicKey))
                _selectedMusicKey = SelectRandomMusicKey();
            EnsureMusicPlaying();
            return;
        }
    }

    private void EnsureMusicPlaying()
    {
        if (_playerInside == null)
            return;

        var audioManager = AudioManager.Instance;
        if (audioManager == null)
            return;

        if (string.IsNullOrWhiteSpace(_selectedMusicKey))
            _selectedMusicKey = SelectRandomMusicKey();

        if (!string.IsNullOrWhiteSpace(_selectedMusicKey) && audioManager.HasMusic(_selectedMusicKey))
        {
            if (audioManager.PlayMusic(_selectedMusicKey, restartIfSameOnEnsure))
            {
                _warnedNoPlayableMusic = false;
                return;
            }
        }

        _selectedMusicKey = SelectRandomPlayableMusicKey(audioManager, _selectedMusicKey);
        if (string.IsNullOrWhiteSpace(_selectedMusicKey))
        {
            if (!_warnedNoPlayableMusic)
            {
                Debug.LogWarning("[GlobalMusicTrigger2D] No playable music keys found in AudioManager for this trigger.", this);
                _warnedNoPlayableMusic = true;
            }
            return;
        }

        audioManager.PlayMusic(_selectedMusicKey, restartIfSameOnEnsure);
        _warnedNoPlayableMusic = false;
    }

    private string SelectRandomMusicKey()
    {
        var validKeys = new List<string>();
        for (int i = 0; i < musicKeys.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(musicKeys[i]))
                continue;

            validKeys.Add(musicKeys[i].Trim());
        }

        if (validKeys.Count == 0)
            return string.IsNullOrWhiteSpace(musicKey) ? string.Empty : musicKey.Trim();

        if (!avoidImmediateRepeat || validKeys.Count == 1 || string.IsNullOrWhiteSpace(_selectedMusicKey))
            return validKeys[Random.Range(0, validKeys.Count)];

        // Избегаем немедленного повтора последнего выбора, если есть альтернатива.
        string nextKey = _selectedMusicKey;
        int maxAttempts = validKeys.Count * 2;
        for (int i = 0; i < maxAttempts && string.Equals(nextKey, _selectedMusicKey); i++)
            nextKey = validKeys[Random.Range(0, validKeys.Count)];

        return nextKey;
    }

    private string SelectRandomPlayableMusicKey(AudioManager audioManager, string excludedKey)
    {
        if (audioManager == null)
            return string.Empty;

        var playableKeys = new List<string>();
        CollectPlayableKey(playableKeys, audioManager, musicKey);

        for (int i = 0; i < musicKeys.Count; i++)
            CollectPlayableKey(playableKeys, audioManager, musicKeys[i]);

        if (playableKeys.Count == 0)
            return string.Empty;

        if (!avoidImmediateRepeat || playableKeys.Count == 1)
            return playableKeys[Random.Range(0, playableKeys.Count)];

        string excluded = string.IsNullOrWhiteSpace(excludedKey) ? string.Empty : excludedKey.Trim();
        string nextKey = playableKeys[Random.Range(0, playableKeys.Count)];
        int maxAttempts = playableKeys.Count * 2;
        for (int i = 0; i < maxAttempts && string.Equals(nextKey, excluded); i++)
            nextKey = playableKeys[Random.Range(0, playableKeys.Count)];

        return nextKey;
    }

    private static void CollectPlayableKey(List<string> target, AudioManager audioManager, string rawKey)
    {
        if (audioManager == null || string.IsNullOrWhiteSpace(rawKey))
            return;

        string key = rawKey.Trim();
        if (!audioManager.HasMusic(key))
            return;
        if (target.Contains(key))
            return;

        target.Add(key);
    }
}

public class GlobalMusicTriggerRelay2D : MonoBehaviour
{
    private GlobalMusicTrigger2D _trigger;

    public void Init(GlobalMusicTrigger2D trigger) => _trigger = trigger;

    private void OnTriggerEnter2D(Collider2D other) => _trigger?.HandleTriggerEnter(other);

    private void OnTriggerStay2D(Collider2D other) => _trigger?.HandleTriggerStay(other);

    private void OnTriggerExit2D(Collider2D other) => _trigger?.HandleTriggerExit(other);
}
