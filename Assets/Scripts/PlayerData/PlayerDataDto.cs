using UnityEngine;

[CreateAssetMenu(fileName="PlayerDataDto", menuName="Dungeon/PlayerDataDto")]

public class PlayerDataDto : ScriptableObject
{
    public float StartingHealth;
    public float Health;

    void OnEnable()
    {
        Health = StartingHealth;
    }

}
