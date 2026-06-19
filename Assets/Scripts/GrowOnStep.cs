using UnityEngine;

public class GrowOnStep : MonoBehaviour
{
    [SerializeField] float _growDuration = 30f;
    [SerializeField] float TargetHeight = 1f;
    [SerializeField] string _playerTag = "Player";

    float _progress;
    bool _playerOnTop;
    MeshRenderer[] _renderers;

    void Start()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>();
        SetScaleY(0f);
        foreach (var r in _renderers) r.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag))
            _playerOnTop = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_playerTag))
            _playerOnTop = false;
    }

    void Update()
    {
        if (!_playerOnTop || _progress >= TargetHeight) return;

        if (_progress == 0f) foreach (var r in _renderers) r.enabled = true;
        _progress = Mathf.MoveTowards(_progress, TargetHeight, Time.deltaTime / _growDuration);
        SetScaleY(_progress);
    }

    void SetScaleY(float y)
    {
        Vector3 s = transform.localScale;
        s.y = y;
        transform.localScale = s;
    }
}
