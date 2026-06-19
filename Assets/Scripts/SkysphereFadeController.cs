using UnityEngine;

/// <summary>
/// Fades the skysphere based on camera proximity between two transforms.
/// At _start the sphere is fully transparent; at _end it is fully opaque.
/// Works in edit mode: move the camera object to preview.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class SkysphereFadeController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera to track. Leave empty to use Camera.main.")]
    [SerializeField] Transform _camera;

    [Tooltip("Position where the sphere is fully transparent (passthrough).")]
    [SerializeField] Transform _start;

    [Tooltip("Position where the sphere is fully opaque (immersed).")]
    [SerializeField] Transform _end;

    [Header("Settings")]
    [Tooltip("Smooth-damp speed. Set to 0 for instant response.")]
    [SerializeField] float _smoothing = 6f;

    // ── state ─────────────────────────────────────────────────────────────────
    float _currentProgress;
    float _velocity;

    static readonly int ProgressID = Shader.PropertyToID("_Progress");

    Material Mat => Application.isPlaying
        ? GetComponent<Renderer>().material
        : GetComponent<Renderer>().sharedMaterial;

    void OnEnable() => ResolveCamera();

    void Update()
    {
        ResolveCamera();
        if (_camera == null || _start == null || _end == null) return;

        Vector3 startPos = _start.position;
        Vector3 axis     = _end.position - startPos;
        float   sqLen    = axis.sqrMagnitude;
        if (sqLen < 0.0001f) return;

        // Project camera onto the start→end axis, clamped to [0,1]
        float t = Vector3.Dot(_camera.position - startPos, axis) / sqLen;
        float target = Mathf.Clamp01(t);

        if (_smoothing > 0f)
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            _currentProgress = Mathf.SmoothDamp(
                _currentProgress, target, ref _velocity, 1f / _smoothing, Mathf.Infinity, dt);
        }
        else
        {
            _currentProgress = target;
            _velocity = 0f;
        }

        var mat = Mat;
        if (mat != null) mat.SetFloat(ProgressID, _currentProgress);
    }

    void ResolveCamera()
    {
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;
    }

    public void Reset()
    {
        _currentProgress = 0f;
        _velocity        = 0f;
        var mat = Mat;
        if (mat != null) mat.SetFloat(ProgressID, 0f);
    }
}
