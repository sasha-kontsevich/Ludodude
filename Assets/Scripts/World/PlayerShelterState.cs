/// <summary>
/// Состояние «игрок в укрытии» (дом ГГ). Вызовите Notify при входе/выходе из интерьера дома.
/// </summary>
public static class PlayerShelterState
{
    public static bool IsInsidePlayerHome { get; private set; }

    public static void NotifyEnteredPlayerHome()
    {
        IsInsidePlayerHome = true;
    }

    public static void NotifyExitedPlayerHome()
    {
        IsInsidePlayerHome = false;
    }
}
