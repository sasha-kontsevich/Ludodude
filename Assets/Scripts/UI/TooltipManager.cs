using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Логика подсказки на экране без ссылок на UI. Игровой код вызывает <see cref="Show"/> / <see cref="Hide"/>.
/// Отображение подключается отдельным скриптом (например <see cref="TooltipHudView"/>): подписка на
/// <see cref="StateChanged"/> и чтение <see cref="IsVisible"/> / <see cref="CurrentText"/>.
/// </summary>
public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private bool persistBetweenScenes;

    [Header("Отладка")]
    [SerializeField] private bool logTooltipStateToConsole;

    public bool IsVisible { get; private set; }

    public string CurrentText { get; private set; } = string.Empty;

    /// <summary>Вызывается после любого изменения видимости или текста.</summary>
    public event Action<bool, string> StateChanged;

    private Coroutine _autoHideCoroutine;

    private void Awake()
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(string text)
    {
        StopAutoHide();
        CurrentText = text ?? string.Empty;
        IsVisible = true;
        RaiseStateChanged();
        LogStateIfEnabled(nameof(Show));
    }

    public void Show(string text, float durationSeconds)
    {
        Show(text);
        if (durationSeconds <= 0f)
            return;

        StopAutoHide();
        _autoHideCoroutine = StartCoroutine(AutoHideAfter(durationSeconds));
    }

    public void Hide()
    {
        StopAutoHide();
        if (!IsVisible && string.IsNullOrEmpty(CurrentText))
            return;

        IsVisible = false;
        CurrentText = string.Empty;
        RaiseStateChanged();
        LogStateIfEnabled(nameof(Hide));
    }

    private IEnumerator AutoHideAfter(float durationSeconds)
    {
        yield return new WaitForSeconds(durationSeconds);
        _autoHideCoroutine = null;
        Hide();
    }

    private void StopAutoHide()
    {
        if (_autoHideCoroutine == null)
            return;
        StopCoroutine(_autoHideCoroutine);
        _autoHideCoroutine = null;
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(IsVisible, CurrentText);
    }

    private void LogStateIfEnabled(string source)
    {
        if (!logTooltipStateToConsole)
            return;
        Debug.Log($"[TooltipManager] {source}: visible={IsVisible}, text=\"{CurrentText}\"", this);
    }
}
