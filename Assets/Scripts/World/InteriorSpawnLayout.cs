using UnityEngine;

/// <summary>
/// Раздаёт уникальные позиции для интерьеров зданий по сетке в дальней зоне мира, без пересечений.
/// </summary>
public static class InteriorSpawnLayout
{
    private static bool _configured;
    private static Vector2 _anchor;
    private static int _columns;
    private static float _cellSize;
    private static int _nextIndex;

    /// <summary>
    /// Задаёт регион сетки (обычно один раз из InteriorSpawnSettings или первым Building).
    /// Повторные вызовы игнорируются, чтобы сетка не ломалась.
    /// </summary>
    public static void Configure(Vector2 regionAnchor, int gridColumns, float cellSize)
    {
        if (_configured)
            return;

        _anchor = regionAnchor;
        _columns = Mathf.Max(1, gridColumns);
        _cellSize = Mathf.Max(1f, cellSize);
        _configured = true;
    }

    public static void ConfigureDefaultIfNeeded()
    {
        if (_configured)
            return;

        const float defaultDistance = 50_000f;
        Configure(new Vector2(defaultDistance, defaultDistance), 32, 2048f);
    }

    public static Vector2 AllocateNext()
    {
        ConfigureDefaultIfNeeded();

        int i = _nextIndex++;
        int col = i % _columns;
        int row = i / _columns;
        return _anchor + new Vector2(col * _cellSize, row * _cellSize);
    }
}
