using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }
    protected GateIdEnum nextGateId;
    protected int playerFacingDir = 1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // must be singleton, delete the duplicate
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (nextGateId != GateIdEnum.None)
        {
            PlayerToGateSpawn();
        }
    }

    private void PlayerToGateSpawn()
    {
        GameObject player = GameObject.FindGameObjectWithTag(TagEnum.Player.ToString());
        GameObject camera = GameObject.FindGameObjectWithTag(TagEnum.MainCamera.ToString());

        if (! player)
        {
            return;
        }

        RoomGateScript[] gates = FindObjectsByType<RoomGateScript>();

        foreach (RoomGateScript gate in gates)
        {
            if (gate.GetGateId() == nextGateId)
            {
                Debug.Log("gate found "+nextGateId);
                player.transform.position = gate.GetGateSpawn().position;
                player.GetComponent<PlayerController>().SetFacingDir(playerFacingDir);
                camera.GetComponent<CameraController>().SnapToCameraLookAhead();
                
                // reset gate ID after teleport
                nextGateId = GateIdEnum.None;

                return;
            }
        }
    }

    public void SetNextGateId(GateIdEnum gateId)
    {
        nextGateId = gateId;
    }

    public void SetPlayerFacingDir(int facingDir)
    {
        playerFacingDir = facingDir;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
