using UnityEngine;

/// <summary>
/// Computes a 0-1 fade progress from camera position between two transforms
/// and broadcasts it as the global shader property _FadeProgress each frame.
/// Assign the same _start / _end as your SkysphereFadeController.
/// The SceneFadeFeature renderer feature reads _FadeProgress to fade the whole frame.
/// </summary>
[ExecuteAlways]
public class SceneFadeBroadcaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera to track. Leave empty to use Camera.main.")]
    [SerializeField] Transform _camera;

    [Tooltip("Position where the scene is fully transparent (passthrough).")]
    [SerializeField] Transform _start;

    [Tooltip("Position where the scene is fully opaque (immersed).")]
    [SerializeField] Transform _end;

    [Header("Settings")]
    [Tooltip("Smooth-damp speed. Set to 0 for instant.")]
    [SerializeField] float _smoothing = 6f;

#if UNITY_EDITOR
    [Header("Editor Testing")]
    [Tooltip("When enabled, the slider below overrides the camera-based progress.")]
    [SerializeField] bool _useManualOverride = false;
    [Range(0f, 1f)]
    [SerializeField] float _manualProgress = 0f;
#endif

    float _currentProgress;
    float _velocity;

    static readonly int FadeProgressID = Shader.PropertyToID("_FadeProgress");

    void OnEnable() => ResolveCamera();

    void Update()
    {
#if UNITY_EDITOR
        if (_useManualOverride)
        {
            Shader.SetGlobalFloat(FadeProgressID, _manualProgress);
            return;
        }
#endif

        ResolveCamera();
        if (!_camera || !_start || !_end) return;

        Vector3 axis  = _end.position - _start.position;
        float   sqLen = axis.sqrMagnitude;
        if (sqLen < 0.0001f) return;

        float t      = Vector3.Dot(_camera.position - _start.position, axis) / sqLen;
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

        Shader.SetGlobalFloat(FadeProgressID, _currentProgress);
    }

    void ResolveCamera()
    {
        if (!_camera && Camera.main)
            _camera = Camera.main.transform;
    }
}
