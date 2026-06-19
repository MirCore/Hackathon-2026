using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class GazeTriggerAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private AudioSource _audioSource;

    [Header("Gaze Detection")]
    [SerializeField] [Range(0f, 30f)] private float _startupDelay = 5f;
    [SerializeField] [Range(1f, 90f)] private float _lookAngleThreshold = 30f;
    [SerializeField] [Range(0f, 45f)] private float _maxHeadTiltAngle = 30f;
    [SerializeField] [Range(0.1f, 5f)] private float _requiredGazeDuration = 2f;

    [Header("Cooldown")]
    [SerializeField] [Range(1f, 300f)] private float _cooldownDuration = 60f;

    [Header("Gesture Animation Layer")]
    [SerializeField] [Min(0)] private int _gestureLayerIndex = 1;
    [SerializeField] [Range(0f, 10f)] private float _animationFadeDelay = 1f;
    [SerializeField] [Range(0f, 30f)] private float _animationFadeOutDelay = 3f;
    [SerializeField] [Range(0.1f, 5f)] private float _fadeInSpeed = 1f;
    [SerializeField] [Range(0.1f, 5f)] private float _fadeOutSpeed = 1f;

    private Animator _animator;
    private float _currentLayerWeight;
    private float _lastTriggerTime = float.NegativeInfinity;
    private bool _fadingIn;
    private float _gazeTimer;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_playerCamera == null)
            _playerCamera = Camera.main;

        _audioSource.enabled = false;
        _currentLayerWeight = 0f;
        _animator.SetLayerWeight(_gestureLayerIndex, 0f);
    }

    private void Update()
    {
        bool onCooldown = Time.time < _lastTriggerTime + _cooldownDuration;
        if (!onCooldown && Time.time >= _startupDelay)
        {
            if (IsValidGaze())
            {
                _gazeTimer += Time.deltaTime;
                if (_gazeTimer >= _requiredGazeDuration)
                    Trigger();
            }
            else
            {
                _gazeTimer = 0f;
            }
        }

        float targetWeight = _fadingIn ? 1f : 0f;
        float speed = _fadingIn ? _fadeInSpeed : _fadeOutSpeed;
        _currentLayerWeight = Mathf.MoveTowards(_currentLayerWeight, targetWeight, speed * Time.deltaTime);
        _animator.SetLayerWeight(_gestureLayerIndex, _currentLayerWeight);
    }

    private void Trigger()
    {
        _lastTriggerTime = Time.time;
        _gazeTimer = 0f;

        _audioSource.enabled = true;
        _audioSource.Play();

        StartCoroutine(FadeInAfterDelay());
    }

    private IEnumerator FadeInAfterDelay()
    {
        yield return new WaitForSeconds(_animationFadeDelay);
        _fadingIn = true;
        yield return new WaitForSeconds(_animationFadeOutDelay);
        _fadingIn = false;
    }

    private bool IsValidGaze()
    {
        if (_playerCamera == null) return false;

        Transform cam = _playerCamera.transform;

        float tilt = Vector3.Angle(cam.up, Vector3.up);
        if (tilt > _maxHeadTiltAngle) return false;

        Vector3 toAgent = (transform.position - cam.position).normalized;
        return Vector3.Angle(cam.forward, toAgent) < _lookAngleThreshold;
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerCamera == null) return;
        bool onCooldown = Time.time < _lastTriggerTime + _cooldownDuration;
        Gizmos.color = onCooldown ? Color.green : (_gazeTimer > 0f ? Color.cyan : Color.yellow);
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.DrawLine(_playerCamera.transform.position, transform.position);
    }
}
