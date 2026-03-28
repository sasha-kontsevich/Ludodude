using UnityEngine;

/// <summary>
/// Отключает контакты Physics2D между коллайдерами этого NPC и игрока (тег Player),
/// чтобы kinematic NPC не раздвигали dynamic Rigidbody2D при преследовании.
/// </summary>
[DisallowMultipleComponent]
public class NpcIgnorePlayerCollision2D : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return;

        var npcColliders = GetComponentsInChildren<Collider2D>(true);
        var playerColliders = player.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < npcColliders.Length; i++)
        {
            Collider2D a = npcColliders[i];
            if (a == null || a.isTrigger)
                continue;
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D b = playerColliders[j];
                if (b == null)
                    continue;
                Physics2D.IgnoreCollision(a, b, true);
            }
        }
    }
}
