using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Item))]
public class WinGoalItem : MonoBehaviour
{
    private Item _item;
    private bool _victoryTriggered;

    private void Awake()
    {
        _item = GetComponent<Item>();
    }

    private void Update()
    {
        if (_victoryTriggered || _item == null)
            return;
        if (!_item.IsCarried)
            return;

        _victoryTriggered = true;
        GameManager.Instance?.SetVictory();
    }
}
