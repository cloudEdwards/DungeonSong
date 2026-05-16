using Unity.VisualScripting;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    protected GameObject player;
    [SerializeField]
    protected GameObject cameraLookAhead;
    [SerializeField]
    protected float lerpTime = 0.5f;

    protected float cameraZPos = -10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float time = lerpTime * Time.deltaTime;
        Vector3 lookAheadPos = cameraLookAhead.transform.position;
        transform.position = new Vector3(
            Mathf.Lerp(transform.position.x, lookAheadPos.x, time),
            Mathf.Lerp(transform.position.y, lookAheadPos.y, time),
            cameraZPos);
    }
}
