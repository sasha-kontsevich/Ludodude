using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отображение подсказки: подписывается на <see cref="TooltipManager"/> и обновляет uGUI.
/// Замените на свой скрипт, если нужен другой вид — контракт тот же (реакция на <see cref="TooltipManager.StateChanged"/>).
/// </summary>
public class TooltipHudView : MonoBehaviour
{
    [SerializeField] private TooltipManager tooltipManager;

    [SerializeField] private GameObject panelRoot;

    [SerializeField] private Text label;

    private void Awake()
    {
        if (tooltipManager == null)
            tooltipManager = TooltipManager.Instance;
    }

    private void OnEnable()
    {
        if (tooltipManager == null)
            tooltipManager = TooltipManager.Instance;
        if (tooltipManager != null)
            tooltipManager.StateChanged += OnTooltipStateChanged;

        if (tooltipManager != null)
            Apply(tooltipManager.IsVisible, tooltipManager.CurrentText);
        else
            Apply(false, string.Empty);
    }

    private void OnDisable()
    {
        if (tooltipManager != null)
            tooltipManager.StateChanged -= OnTooltipStateChanged;
    }

    private void OnTooltipStateChanged(bool visible, string text)
    {
        Apply(visible, text);
    }

    private void Apply(bool visible, string text)
    {
        if (label != null)
            label.text = text ?? string.Empty;

        if (panelRoot != null)
            panelRoot.SetActive(visible);
        else if (label != null)
            label.gameObject.SetActive(visible);
    }
}
