using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CharacterStatsPresenter : MonoBehaviour
{
    [SerializeField] private TopDownPlayerController playerController;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private ItemCarrier itemCarrier;
    [SerializeField] private TMP_Text statsLabelTmp;
    [SerializeField] private Text statsLabelLegacy;
    [TextArea]
    [SerializeField] private string statsFormat =
        "Грузоподъемность: {0}\n" +
        "Скорость: x{1:0.##}\n" +
        "Множитель продажи: x{2:0.##}\n" +
        "Занято: {3}";

    private void Awake()
    {
        ResolveReferencesIfNeeded();
    }

    private void Update()
    {
        ResolveReferencesIfNeeded();
        if (characterStats == null)
            return;

        int currentCarry = itemCarrier != null ? itemCarrier.CurrentCarriedCost : 0;
        string text = string.Format(
            statsFormat,
            characterStats.MaxCarryCost,
            characterStats.SpeedMultiplier,
            characterStats.SellPriceMultiplier,
            currentCarry);

        if (statsLabelTmp != null)
            statsLabelTmp.text = text;
        if (statsLabelLegacy != null)
            statsLabelLegacy.text = text;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Exclude);

        if (playerController != null)
        {
            if (characterStats == null)
                characterStats = playerController.GetComponent<CharacterStats>();
            if (itemCarrier == null)
                itemCarrier = playerController.GetComponent<ItemCarrier>();
        }

        if (characterStats == null)
            characterStats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Exclude);
        if (itemCarrier == null)
            itemCarrier = FindFirstObjectByType<ItemCarrier>(FindObjectsInactive.Exclude);
    }
}
