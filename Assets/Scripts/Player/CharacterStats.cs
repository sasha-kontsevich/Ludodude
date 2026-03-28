using UnityEngine;

/// <summary>
/// Базовые статы персонажа, которые используются другими компонентами (перенос, скорость, продажа).
/// </summary>
[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour
{
    [Header("Перенос предметов")]
    [Tooltip("Максимальная суммарная стоимость переносимых предметов. 0 или меньше — без лимита.")]
    [SerializeField] private int maxCarryCost = 10;

    [Header("Передвижение")]
    [Tooltip("Мультипликатор итоговой скорости персонажа.")]
    [SerializeField] [Min(0f)] private float speedMultiplier = 1f;

    [Header("Продажа")]
    [Tooltip("Мультипликатор стоимости сдачи предметов в депозит.")]
    [SerializeField] [Min(0f)] private float sellPriceMultiplier = 1f;

    public int MaxCarryCost => maxCarryCost;
    public float SpeedMultiplier => speedMultiplier;
    public float SellPriceMultiplier => sellPriceMultiplier;
}
