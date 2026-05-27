using UnityEngine;

[CreateAssetMenu(fileName = "RoomDataDto", menuName = "Dungeon/RoomDataDto")]
public class RoomDataDto : ScriptableObject
{
    [Header("Side A")]
    [SerializeField]
    public SceneNameEnum roomA;
    [SerializeField]
    public GateIdEnum gateIdA;

    [Header("Side B")]
    [SerializeField]
    public SceneNameEnum roomB;
    [SerializeField]
    public GateIdEnum gateIdB;

    public SceneNameEnum GetRoomA()
    {
        return roomA;
    }

    public SceneNameEnum GetRoomB()
    {
        return roomB;
    }

    public GateIdEnum GetGateIdA()
    {
        return gateIdA;
    }

    public GateIdEnum GetGateIdB()
    {
        return gateIdB;
    }
}
