using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomGateScript : MonoBehaviour
{
    [SerializeField]
    protected GateSideEnum gateSide;
    [SerializeField]
    protected RoomDataDto roomData;
    [SerializeField]
    protected Transform gateSpawn;


    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(TagEnum.Player.ToString()))
        {
            TeleportToNextGate(collision.gameObject);
        }
    }

    private void TeleportToNextGate(GameObject player)
    {
        SceneTransitionManager.Instance.SetNextGateId(GetDestinationGateId());
        SceneTransitionManager.Instance.SetPlayerFacingDir(player.GetComponent<PlayerController>().GetFacingDir());

        SceneManager.LoadScene(getDestinationRoomName());
    }

    private string getDestinationRoomName()
    {
        return gateSide == GateSideEnum.SideA ? roomData.roomB.ToString() : roomData.roomA.ToString();
    }

    public GateIdEnum GetDestinationGateId()
    {
        return gateSide == GateSideEnum.SideA ? roomData.gateIdB : roomData.gateIdA;
    }

    public GateIdEnum GetGateId()
    {
        return gateSide == GateSideEnum.SideA ? roomData.gateIdA : roomData.gateIdB;
    }

    public GateSideEnum GetGateSide()
    {
        return gateSide;
    }

    public RoomDataDto GetRoomData()
    {
        return roomData;
    }

    public Transform GetGateSpawn()
    {
        return gateSpawn;
    }
}
