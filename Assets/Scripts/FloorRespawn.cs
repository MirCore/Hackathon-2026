using UnityEngine;

public class FloorRespawn : MonoBehaviour
{
    [SerializeField] float floorThreshold = 0.2f;
    [SerializeField] float respawnDelay = 5f;

    Vector3 _startPos;
    Quaternion _startRot;
    Rigidbody _rb;
    float _timeOnFloor = 0f;

    void Awake()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (transform.position.y < floorThreshold)
        {
            _timeOnFloor += Time.deltaTime;
            if (_timeOnFloor >= respawnDelay)
                Respawn();
        }
        else
        {
            _timeOnFloor = 0f;
        }
    }

    void Respawn()
    {
        _timeOnFloor = 0f;
        transform.SetPositionAndRotation(_startPos, _startRot);

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
