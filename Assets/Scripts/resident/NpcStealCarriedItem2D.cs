using UnityEngine;

/// <summary>
/// Вешается на дочерний объект с <see cref="Collider2D"/> (Is Trigger).
/// При касании игрока снимает все предметы со стопки <see cref="ItemCarrier"/> (рождаются рядом с игроком, как при дропе).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NpcStealCarriedItem2D : MonoBehaviour
{
    // Снимает всю стопку с носителя (как дроп рядом с игроком).
    public static void StripAll(ItemCarrier carrier)
    {
        if (carrier == null)
            return;
        while (carrier.CarriedItems.Count > 0)
            carrier.DropLastCarriedItem();
    }

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        StripAll(other.GetComponentInParent<ItemCarrier>());
    }
}
