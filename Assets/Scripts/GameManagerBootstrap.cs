using UnityEngine;

public class GameManagerBootstrap : MonoBehaviour {

    [SerializeField]
    private GameObject sceneTransitionManagerPrefab;

    void Awake()
    {
        SceneTransitionManager sceneTransitionManager = FindAnyObjectByType<SceneTransitionManager>(); 

        if (! sceneTransitionManager)
        {
            Instantiate(sceneTransitionManagerPrefab);
        }
    }
}
