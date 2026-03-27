using UnityEngine;

/// <summary>
/// Один объект в сцене: задаёт дальнюю зону и шаг сетки для InteriorSpawnLayout.
/// Если отсутствует, Building использует значения по умолчанию (далеко от (0,0)).
/// </summary>
public class InteriorSpawnSettings : MonoBehaviour
{
    [Tooltip("Нижний левый угол сетки слотов (далеко от центра карты, обычно большие положительные координаты).")]
    [SerializeField] private Vector2 regionAnchor = new Vector2(50_000f, 50_000f);

    [Tooltip("Число колонок сетки; следующая строка начинается после заполнения ряда.")]
    [SerializeField] private int gridColumns = 32;

    [Tooltip("Расстояние между соседними слотами (должно быть ≥ размера префаба интерьера).")]
    [SerializeField] private float cellSize = 2048f;

    private void Awake()
    {
        InteriorSpawnLayout.Configure(regionAnchor, gridColumns, cellSize);
    }
}
