using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }
    protected GateIdEnum nextGateId;
    protected int playerFacingDir = 1;

    [SerializeField]
    protected float fadeDuration = 1f;
    private CanvasGroup faderGroup;

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

        faderGroup = gameObject.GetComponentInChildren<CanvasGroup>();
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

    public void LoadNextScene(string sceneName)
    {
        StartCoroutine(FadeToNextScene(sceneName));
    }

    private IEnumerator FadeToNextScene(string sceneName)
    {
        Debug.Log("Fade Out");
        yield return StartCoroutine(Fade(1));
        AsyncOperation loadScene = SceneManager.LoadSceneAsync(sceneName);

        while (! loadScene.isDone)
        {
            Debug.Log($"Loading progress: {loadScene.progress}");

            yield return null;
        }

        Debug.Log("Fade IN");

        yield return StartCoroutine(Fade(0));
    }

    private IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = faderGroup.alpha;
        float timer = 0;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            faderGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            yield return null;
        }

        faderGroup.alpha = targetAlpha;
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
