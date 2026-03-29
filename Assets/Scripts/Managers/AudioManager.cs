using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Глобальный менеджер звуков и музыки с доступом по строковому ключу.
///
/// Быстрый гайд:
/// 1) Добавьте AudioManager на объект сцены (обычно GameManager).
/// 2) В инспекторе заполните:
///    - sounds: SFX по ключам (one-shot и loop).
///    - music: треки музыки по ключам.
/// 3) Используйте API из других компонентов:
///    - One-shot: AudioManager.Instance.PlaySound("ui_click");
///    - Loop состояние (идемпотентно): AudioManager.Instance.SetLoopSoundPlaying("footsteps_loop", isMoving);
///    - Музыка: AudioManager.Instance.PlayMusic("map_theme_1");
///
/// Рекомендации по конфигу:
/// - key должен быть уникальным в пределах каталога.
/// - pitch > 0 (если <= 0, будет использован fallback 1 с warning).
/// - для длительных SFX (шаги/двигатель) ставьте loop = true.
/// </summary>
public class AudioManager : MonoBehaviour
{
    /*
     * API guide (в коде)
     *
     * Проверка ключей:
     *   AudioManager.Instance.HasSound("ui_click");
     *   AudioManager.Instance.HasMusic("map_theme_1");
     *
     * One-shot SFX:
     *   AudioManager.Instance.PlaySound("ui_click");
     *
     * Loop SFX (рекомендуемый паттерн для состояний):
     *   AudioManager.Instance.SetLoopSoundPlaying("footsteps_loop", isMoving);
     *
     * Loop SFX (ручное управление):
     *   AudioManager.Instance.PlayLoopSound("engine_loop");
     *   AudioManager.Instance.IsSoundPlaying("engine_loop");
     *   AudioManager.Instance.StopSound("engine_loop");
     *
     * Music:
     *   AudioManager.Instance.PlayMusic("map_theme_1");
     *   AudioManager.Instance.PlayMusic("map_theme_1", restartIfSame: true);
     *   AudioManager.Instance.StopMusic();
     *
     * Global:
     *   AudioManager.Instance.SetMute(true);
     *   AudioManager.Instance.SetMasterVolume(0.8f);
     *   AudioManager.Instance.StopAllSounds();
     */

    [Serializable]
    private class AudioEntry
    {
        [SerializeField] private string key;
        [SerializeField] private AudioClip clip;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;
        [SerializeField] private float pitch = 1f;
        [SerializeField] private bool loop;

        public string Key => key;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public float Pitch => pitch;
        public bool Loop => loop;
    }

    private sealed class LoopSoundState
    {
        public AudioSource Source;
        public float EntryVolume;
    }

    public static AudioManager Instance { get; private set; }

    [Header("Lifecycle")]
    [SerializeField] private bool persistBetweenScenes = true;

    [Header("Global")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private bool mute;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;

    [Header("Catalog: Sounds")]
    [SerializeField] private List<AudioEntry> sounds = new List<AudioEntry>();

    [Header("Catalog: Music")]
    [SerializeField] private List<AudioEntry> music = new List<AudioEntry>();

    private readonly Dictionary<string, AudioEntry> _soundByKey = new Dictionary<string, AudioEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AudioEntry> _musicByKey = new Dictionary<string, AudioEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoopSoundState> _activeLoopSounds = new Dictionary<string, LoopSoundState>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warnedInvalidPitchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private float _currentMusicEntryVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureAudioSources();
        RebuildCatalogs();
        ApplyGlobalSettings();

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void Reset()
    {
        EnsureAudioSources();
    }

    private void OnValidate()
    {
        EnsureAudioSources();
    }

    private void OnDestroy()
    {
        ClearLoopSoundState();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>Проверка существования SFX ключа в каталоге sounds.</summary>
    public bool HasSound(string key)
    {
        return TryGetEntry(_soundByKey, key, out _);
    }

    /// <summary>Проверка существования музыкального ключа в каталоге music.</summary>
    public bool HasMusic(string key)
    {
        return TryGetEntry(_musicByKey, key, out _);
    }

    /// <summary>
    /// Проигрывает SFX по ключу.
    /// Если у записи loop=true, запускает/поддерживает loop-источник по ключу.
    /// </summary>
    public bool PlaySound(string key)
    {
        if (!TryGetEntry(_soundByKey, key, out var entry))
        {
            WarnUnknownKey("sound", key);
            return false;
        }

        if (soundSource == null)
        {
            Debug.LogWarning("[AudioManager] Sound source is missing.", this);
            return false;
        }

        if (entry.Loop)
            return PlayLoopEntry(key.Trim(), entry);

        var effectiveVolume = GetEffectiveVolume(entry.Volume);
        soundSource.loop = false;
        soundSource.pitch = GetEffectivePitch(entry.Pitch, $"sound:{key.Trim()}");
        soundSource.PlayOneShot(entry.Clip, effectiveVolume);

        return true;
    }

    /// <summary>
    /// Идемпотентное управление loop-звуком: удобно вызывать в Update/FixedUpdate.
    /// shouldPlay=true эквивалентно <see cref="PlayLoopSound"/>, false — <see cref="StopSound"/>.
    /// Возвращает true, если целевое состояние успешно применено.
    /// </summary>
    public bool SetLoopSoundPlaying(string key, bool shouldPlay)
    {
        if (shouldPlay)
            return PlayLoopSound(key);

        return StopSound(key);
    }

    /// <summary>
    /// Явный запуск loop-звука по ключу.
    /// Возвращает false, если ключ не найден или запись сконфигурирована как one-shot (loop=false).
    /// </summary>
    public bool PlayLoopSound(string key)
    {
        if (!TryGetEntry(_soundByKey, key, out var entry))
        {
            WarnUnknownKey("sound", key);
            return false;
        }

        if (!entry.Loop)
        {
            Debug.LogWarning($"[AudioManager] Sound key '{key.Trim()}' is configured as one-shot (loop=false). Set loop=true for long running sounds.", this);
            return false;
        }

        return PlayLoopEntry(key.Trim(), entry);
    }

    /// <summary>
    /// Проигрывает музыку по ключу.
    /// restartIfSame=false не перезапускает трек, если уже играет тот же clip.
    /// </summary>
    public bool PlayMusic(string key, bool restartIfSame = false)
    {
        if (!TryGetEntry(_musicByKey, key, out var entry))
        {
            WarnUnknownKey("music", key);
            return false;
        }

        if (musicSource == null)
        {
            Debug.LogWarning("[AudioManager] Music source is missing.", this);
            return false;
        }

        if (!restartIfSame && musicSource.isPlaying && musicSource.clip == entry.Clip)
            return true;

        musicSource.loop = entry.Loop;
        musicSource.clip = entry.Clip;
        musicSource.pitch = GetEffectivePitch(entry.Pitch, $"music:{key.Trim()}");
        _currentMusicEntryVolume = Mathf.Clamp01(entry.Volume);
        musicSource.volume = GetEffectiveVolume(entry.Volume);
        musicSource.Play();
        return true;
    }

    /// <summary>Останавливает текущую музыку (musicSource).</summary>
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
        _currentMusicEntryVolume = 1f;
    }

    /// <summary>Останавливает музыку, one-shot канал и все активные loop-SFX.</summary>
    public void StopAllSounds()
    {
        if (soundSource != null)
            soundSource.Stop();
        if (musicSource != null)
            musicSource.Stop();
        ClearLoopSoundState();
        _currentMusicEntryVolume = 1f;
    }

    /// <summary>
    /// Останавливает loop-SFX по ключу.
    /// Для one-shot звуков (PlayOneShot) остановка по ключу не применяется.
    /// Возвращает true, если loop-звук с таким ключом был активен и остановлен.
    /// </summary>
    public bool StopSound(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalizedKey = key.Trim();
        if (_activeLoopSounds.TryGetValue(normalizedKey, out var loopState))
        {
            ReleaseLoopSound(normalizedKey, loopState);
            return true;
        }

        if (!_soundByKey.ContainsKey(normalizedKey))
            WarnUnknownKey("sound", normalizedKey);

        return false;
    }

    /// <summary>
    /// Проверка, играет ли loop-SFX по указанному ключу.
    /// Для one-shot звуков всегда будет false.
    /// </summary>
    public bool IsSoundPlaying(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalizedKey = key.Trim();
        if (!_activeLoopSounds.TryGetValue(normalizedKey, out var loopState))
            return false;

        return loopState.Source != null && loopState.Source.isPlaying;
    }

    public void SetMute(bool isMuted)
    {
        mute = isMuted;
        ApplyGlobalSettings();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyGlobalSettings();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
            musicSource = FindFirstAvailableSource(soundSource);
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        if (soundSource == null)
            soundSource = FindFirstAvailableSource(musicSource);
        if (soundSource == null)
            soundSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        soundSource.playOnAwake = false;
    }

    private AudioSource FindFirstAvailableSource(AudioSource excludedSource)
    {
        var sources = GetComponents<AudioSource>();
        foreach (var source in sources)
        {
            if (source == null || source == excludedSource)
                continue;

            return source;
        }

        return null;
    }

    private void RebuildCatalogs()
    {
        _soundByKey.Clear();
        _musicByKey.Clear();

        BuildCatalog(sounds, _soundByKey, "sound");
        BuildCatalog(music, _musicByKey, "music");
    }

    private void BuildCatalog(List<AudioEntry> sourceCatalog, Dictionary<string, AudioEntry> targetMap, string catalogName)
    {
        foreach (var entry in sourceCatalog)
        {
            if (!IsValidEntry(entry, catalogName))
                continue;

            var normalizedKey = entry.Key.Trim();
            if (targetMap.ContainsKey(normalizedKey))
            {
                Debug.LogWarning($"[AudioManager] Duplicate {catalogName} key '{normalizedKey}'. The first entry is used.", this);
                continue;
            }

            targetMap.Add(normalizedKey, entry);
        }
    }

    private bool IsValidEntry(AudioEntry entry, string catalogName)
    {
        if (entry == null)
        {
            Debug.LogWarning($"[AudioManager] Null entry found in {catalogName} catalog.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Key))
        {
            Debug.LogWarning($"[AudioManager] Entry with empty key found in {catalogName} catalog.", this);
            return false;
        }

        if (entry.Clip == null)
        {
            Debug.LogWarning($"[AudioManager] Entry '{entry.Key}' in {catalogName} catalog has no clip.", this);
            return false;
        }

        return true;
    }

    private bool TryGetEntry(Dictionary<string, AudioEntry> catalog, string key, out AudioEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return catalog.TryGetValue(key.Trim(), out entry);
    }

    private float GetEffectiveVolume(float entryVolume)
    {
        if (mute)
            return 0f;

        return Mathf.Clamp01(masterVolume * Mathf.Clamp01(entryVolume));
    }

    private void ApplyGlobalSettings()
    {
        if (musicSource != null)
            musicSource.mute = mute;

        if (soundSource != null)
            soundSource.mute = mute;

        if (musicSource != null && musicSource.clip != null)
            musicSource.volume = mute ? 0f : Mathf.Clamp01(masterVolume * _currentMusicEntryVolume);

        foreach (var loopState in _activeLoopSounds.Values)
        {
            if (loopState.Source == null)
                continue;

            loopState.Source.mute = mute;
            loopState.Source.volume = GetEffectiveVolume(loopState.EntryVolume);
        }
    }

    private void WarnUnknownKey(string catalogName, string key)
    {
        var sanitizedKey = string.IsNullOrWhiteSpace(key) ? "<empty>" : key.Trim();
        Debug.LogWarning($"[AudioManager] Unknown {catalogName} key '{sanitizedKey}'.", this);
    }

    private bool PlayLoopEntry(string normalizedKey, AudioEntry entry)
    {
        if (_activeLoopSounds.TryGetValue(normalizedKey, out var existingState))
        {
            if (existingState.Source == null)
            {
                _activeLoopSounds.Remove(normalizedKey);
            }
            else
            {
                existingState.Source.pitch = GetEffectivePitch(entry.Pitch, $"sound:{normalizedKey}");
                existingState.EntryVolume = Mathf.Clamp01(entry.Volume);
                existingState.Source.volume = GetEffectiveVolume(existingState.EntryVolume);
                if (!existingState.Source.isPlaying || existingState.Source.clip != entry.Clip)
                {
                    existingState.Source.clip = entry.Clip;
                    existingState.Source.loop = true;
                    existingState.Source.Play();
                }
                return true;
            }
        }

        var sourceObject = new GameObject($"LoopSound_{normalizedKey}");
        sourceObject.transform.SetParent(transform, false);
        var loopSource = sourceObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.clip = entry.Clip;
        loopSource.pitch = GetEffectivePitch(entry.Pitch, $"sound:{normalizedKey}");
        loopSource.mute = mute;

        var state = new LoopSoundState
        {
            Source = loopSource,
            EntryVolume = Mathf.Clamp01(entry.Volume)
        };

        loopSource.volume = GetEffectiveVolume(state.EntryVolume);
        loopSource.Play();
        _activeLoopSounds[normalizedKey] = state;
        return true;
    }

    private void ClearLoopSoundState()
    {
        foreach (var kv in _activeLoopSounds)
        {
            ReleaseLoopSound(kv.Key, kv.Value);
        }

        _activeLoopSounds.Clear();
    }

    private void ReleaseLoopSound(string key, LoopSoundState state)
    {
        if (state.Source != null)
        {
            state.Source.Stop();
            Destroy(state.Source.gameObject);
        }

        _activeLoopSounds.Remove(key);
    }

    private float GetEffectivePitch(float configuredPitch, string warnKey)
    {
        if (configuredPitch > 0f)
            return configuredPitch;

        if (_warnedInvalidPitchKeys.Add(warnKey))
            Debug.LogWarning($"[AudioManager] Invalid pitch '{configuredPitch}' for '{warnKey}'. Using fallback pitch 1.", this);

        return 1f;
    }
}
