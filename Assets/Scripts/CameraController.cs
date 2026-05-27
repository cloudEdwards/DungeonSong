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
    void LateUpdate()
    {
        float time = lerpTime * Time.deltaTime;
        Vector3 lookAheadPos = cameraLookAhead.transform.position;
        transform.position = new Vector3(
            Mathf.Lerp(transform.position.x, lookAheadPos.x, time),
            Mathf.Lerp(transform.position.y, lookAheadPos.y, time),
            cameraZPos);


        // Vector3 vel = Vector3.zero;
        // Vector3 lookAheadPos = new Vector3(cameraLookAhead.transform.position.x, cameraLookAhead.transform.position.y, cameraZPos);
        // transform.position = Vector3.SmoothDamp(transform.position, lookAheadPos, ref vel, lerpTime);
    }

    public void SnapToCameraLookAhead()
    {
        transform.position = cameraLookAhead.transform.position;
    }
}
