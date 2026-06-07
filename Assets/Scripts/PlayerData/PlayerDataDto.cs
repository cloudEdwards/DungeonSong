using UnityEngine;

[CreateAssetMenu(fileName="PlayerDataDto", menuName="Dungeon/PlayerDataDto")]

public class PlayerDataDto : ScriptableObject
{
    public float MaxHealth = 100f;
    public float HealRate = 25f;
    public float StartingHealth;
    public float Health;

    void OnEnable()
    {
        Health = StartingHealth;
    }

}
