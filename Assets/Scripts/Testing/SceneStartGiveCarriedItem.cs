using UnityEngine;

/// <summary>
/// Один раз при старте сцены создаёт предмет и передаёт его в <see cref="ItemCarrier"/> игрока
/// (для тестов, например погони полиции при краже).
/// </summary>
public class SceneStartGiveCarriedItem : MonoBehaviour
{
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        if (itemPrefab == null)
            return;

        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return;

        var carrier = player.GetComponent<ItemCarrier>();
        if (carrier == null)
            return;

        var instance = Instantiate(itemPrefab, player.transform.position, Quaternion.identity);
        var item = instance.GetComponent<Item>();
        if (item == null)
        {
            Destroy(instance);
            return;
        }

        carrier.PickUp(item);
    }
}
