using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Animator))]
public class EyeGazeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinnedMeshRenderer CharacterSkinnedMeshRenderer;

    [Header("Behaviors")]
    [SerializeField] private bool EnableEyeGaze = true;
    [SerializeField] private bool EnableHeadGaze = true;
    [SerializeField] private bool EnableBlink = true;
    [SerializeField] private bool DrawGazeDebugRays = false;

    [Header("Gaze Target")]
    public Transform PlayerTarget;

    [Header("Eye Gaze Settings")]
    [Range(0, 100)] public float MaxEyeAngleH = 45f;
    [Range(0, 100)] public float MaxEyeAngleV = 25f;
    [Range(0, 100)] public float EyeBlendshapeStrength = 100f;
    public float EyeGazeSpeed = 2f;

    [Header("Head Gaze Settings (IK)")]
    [Range(0, 1)] public float HeadLookWeight = 0.5f;
    public float HeadGazeSpeed = 4f;
    public Vector3 HeadLookOffset = Vector3.zero;

    [Header("Blink Settings")]
    public float MinBlinkInterval = 0.3f;
    public float MaxBlinkInterval = 3f;
    public float BlinkDuration = 0.1f;

    public enum GazeMode { Idle, LookAtPlayer }
    public GazeMode CurrentGazeMode = GazeMode.Idle;

    // Eye blendshape name candidates (supports multiple character rigs)
    private readonly List<string> _eyeLLookLOptions = new() { "Eye_L_Look_L" };
    private readonly List<string> _eyeRLookLOptions = new() { "Eye_R_Look_L" };
    private readonly List<string> _eyeLLookROptions = new() { "Eye_L_Look_R" };
    private readonly List<string> _eyeRLookROptions = new() { "Eye_R_Look_R"};
    private readonly List<string> _eyeLLookUpOptions = new() { "Eye_L_Look_Up" };
    private readonly List<string> _eyeRLookUpOptions = new() { "Eye_R_Look_Up" };
    private readonly List<string> _eyeLLookDownOptions = new() { "Eye_L_Look_Down" };
    private readonly List<string> _eyeRLookDownOptions = new() { "Eye_R_Look_Down" };
    private readonly List<string> _eyeLBlinkOptions = new() { "Eye_Blink_L" };
    private readonly List<string> _eyeRBlinkOptions = new() { "Eye_Blink_R" };
    
    private Transform _headBone;
    private Transform _eyeBoneL;
    private Transform _eyeBoneR;

    private Animator _animator;
    private Vector3 _currentGazeDirection = Vector3.forward;
    private Vector3 _targetGazeDirection = Vector3.forward;
    private Vector3 _currentHeadLookAtPosition;

    private int _eyeLLookLIndex, _eyeRLookLIndex, _eyeLLookRIndex, _eyeRLookRIndex;
    private int _eyeLLookUpIndex, _eyeRLookUpIndex, _eyeLLookDownIndex, _eyeRLookDownIndex;
    private int _eyeLBlinkIndex, _eyeRBlinkIndex;

    private float _nextBlinkTime;
    private bool _isBlinking;
    private int _blendShapeCount;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (_animator == null)
        {
            Debug.LogError("EyeGazeController: Animator not found. Head IK disabled.", this);
            EnableHeadGaze = false;
            return;
        }

        if (_animator.avatar == null || !_animator.avatar.isHuman)
        {
            Debug.LogError("EyeGazeController: Animator requires a Humanoid Avatar for Head IK.", this);
            EnableHeadGaze = false;
        }
        else
        {
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);                                                                             
            _eyeBoneL = _animator.GetBoneTransform(HumanBodyBones.LeftEye);
            _eyeBoneR = _animator.GetBoneTransform(HumanBodyBones.RightEye);
        }
    }

    private void Start()
    {
        if (CharacterSkinnedMeshRenderer == null)
        {
            CharacterSkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (CharacterSkinnedMeshRenderer == null)
            {
                Debug.LogError("EyeGazeController: No SkinnedMeshRenderer found. Assign it in the Inspector.", this);
                EnableEyeGaze = false;
                EnableBlink = false;
            }
        }

        if (EnableEyeGaze || EnableBlink) InitBlendshapes();
        if (EnableHeadGaze) InitHeadGaze();
        if (EnableBlink) ScheduleNextBlink();
    }

    private void Update()
    {
        if (EnableEyeGaze || EnableHeadGaze)
            UpdateGazeDirection();

        if (EnableHeadGaze)
            UpdateHeadLookTarget();

        if (EnableBlink && !_isBlinking && Time.time >= _nextBlinkTime)
            StartCoroutine(Blink());
    }

    private void LateUpdate()
    {
        if (!EnableEyeGaze) return;

        ApplyGazeToBlendshapes(_currentGazeDirection);
    }
    
    private void OnDrawGizmos()
    {
        if (!DrawGazeDebugRays) return;
        if (_eyeBoneL) Debug.DrawRay(_eyeBoneL.position, -_eyeBoneL.up * 0.5f, Color.blue);
        if (_eyeBoneR) Debug.DrawRay(_eyeBoneR.position, -_eyeBoneR.up * 0.5f, Color.blue);
        //Debug.DrawRay(GetEyeCenterPosition(), _currentGazeDirection * 0.5f, Color.green);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!EnableHeadGaze) return;

        _animator.SetLookAtWeight(HeadLookWeight, 0.3f, 1f, 0f, 0.5f);
        _animator.SetLookAtPosition(_currentHeadLookAtPosition);
    }

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------

    private void InitBlendshapes()
    {
        if (CharacterSkinnedMeshRenderer == null) return;

        _eyeLLookLIndex   = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeLLookLOptions);
        _eyeRLookLIndex   = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeRLookLOptions);
        _eyeLLookRIndex   = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeLLookROptions);
        _eyeRLookRIndex   = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeRLookROptions);
        _eyeLLookUpIndex  = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeLLookUpOptions);
        _eyeRLookUpIndex  = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeRLookUpOptions);
        _eyeLLookDownIndex = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeLLookDownOptions);
        _eyeRLookDownIndex = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeRLookDownOptions);
        _eyeLBlinkIndex    = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeLBlinkOptions);
        _eyeRBlinkIndex    = GetFirstValidBlendshapeIndex(CharacterSkinnedMeshRenderer, _eyeRBlinkOptions);

        CheckGazeBlendshapeIndices();
        
        _blendShapeCount = CharacterSkinnedMeshRenderer.sharedMesh.blendShapeCount;
    }

    private void InitHeadGaze()
    {
        if (_headBone != null)
            _currentHeadLookAtPosition = _headBone.position + _headBone.forward * 10f;
        else
        {
            Debug.LogWarning("EyeGazeController: Head bone not found. Head IK disabled.", this);
            EnableHeadGaze = false;
        }
    }

    // -------------------------------------------------------------------------
    // Gaze update
    // -------------------------------------------------------------------------

    private void UpdateGazeDirection()
    {
        switch (CurrentGazeMode)
        {
            case GazeMode.Idle:
                float noiseX = Mathf.PerlinNoise(Time.time * 0.2f, 0f) * 2f - 1f;
                float noiseY = Mathf.PerlinNoise(0f, Time.time * 0.2f) * 2f - 1f;
                _targetGazeDirection = transform.TransformDirection(new Vector3(noiseX, noiseY, 1f).normalized);
                break;

            case GazeMode.LookAtPlayer:
                if (PlayerTarget)
                    _targetGazeDirection = (PlayerTarget.position - GetEyeCenterPosition()).normalized;
                else
                {
                    Debug.LogWarning("EyeGazeController: playerTarget is null, falling back to Idle.", this);
                    CurrentGazeMode = GazeMode.Idle;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _currentGazeDirection = Vector3.Lerp(_currentGazeDirection, _targetGazeDirection, Time.deltaTime * EyeGazeSpeed).normalized;
    }

    private void UpdateHeadLookTarget()
    {
        Vector3 target = _headBone.position + _currentGazeDirection * 100f + HeadLookOffset;
        _currentHeadLookAtPosition = Vector3.Lerp(_currentHeadLookAtPosition, target, Time.deltaTime * HeadGazeSpeed);
    }

    // -------------------------------------------------------------------------
    // Eye gaze blendshapes
    // -------------------------------------------------------------------------

    private void ApplyGazeToBlendshapes(Vector3 gazeDirection)
    {
        Vector3 local = _headBone.InverseTransformDirection(gazeDirection).normalized;
        float h = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -MaxEyeAngleH, MaxEyeAngleH);
        float v = Mathf.Clamp(Mathf.Asin(local.y) * Mathf.Rad2Deg, -MaxEyeAngleV, MaxEyeAngleV);

        ResetGazeBlendshapes();

        if (h < -0.1f)
        {
            float w = Mathf.Abs(h) / MaxEyeAngleH * EyeBlendshapeStrength;
            SetBlendshape(_eyeLLookLIndex, w);
            SetBlendshape(_eyeRLookLIndex, w);
        }
        else if (h > 0.1f)
        {
            float w = h / MaxEyeAngleH * EyeBlendshapeStrength;
            SetBlendshape(_eyeLLookRIndex, w);
            SetBlendshape(_eyeRLookRIndex, w);
        }

        if (v > 0.1f)
        {
            float w = v / MaxEyeAngleV * EyeBlendshapeStrength;
            SetBlendshape(_eyeLLookUpIndex, w);
            SetBlendshape(_eyeRLookUpIndex, w);
        }
        else if (v < -0.1f)
        {
            float w = Mathf.Abs(v) / MaxEyeAngleV * EyeBlendshapeStrength;
            SetBlendshape(_eyeLLookDownIndex, w);
            SetBlendshape(_eyeRLookDownIndex, w);
        }
    }

    private void ResetGazeBlendshapes()
    {
        SetBlendshape(_eyeLLookLIndex, 0);
        SetBlendshape(_eyeRLookLIndex, 0);
        SetBlendshape(_eyeLLookRIndex, 0);
        SetBlendshape(_eyeRLookRIndex, 0);
        SetBlendshape(_eyeLLookUpIndex, 0);
        SetBlendshape(_eyeRLookUpIndex, 0);
        SetBlendshape(_eyeLLookDownIndex, 0);
        SetBlendshape(_eyeRLookDownIndex, 0);
    }

    private void SetBlendshape(int index, float weight)
    {
        if (index < 0 || index >= _blendShapeCount) return;
        CharacterSkinnedMeshRenderer.SetBlendShapeWeight(index, weight);
    }

    // -------------------------------------------------------------------------
    // Blink
    // -------------------------------------------------------------------------

    private void ScheduleNextBlink()
    {
        _nextBlinkTime = Time.time + Random.Range(MinBlinkInterval, MaxBlinkInterval);
    }

    private IEnumerator Blink()
    {
        _isBlinking = true;
        yield return AnimateBlinkBlendshape(80f);
        yield return new WaitForSeconds(BlinkDuration);
        yield return AnimateBlinkBlendshape(0f);
        ScheduleNextBlink();
        _isBlinking = false;
    }

    private IEnumerator AnimateBlinkBlendshape(float targetValue)
    {
        float startValue = CharacterSkinnedMeshRenderer.GetBlendShapeWeight(_eyeLBlinkIndex);
        float elapsed = 0f;
        float duration = BlinkDuration / 2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float value = Mathf.Lerp(startValue, targetValue, elapsed / duration);
            CharacterSkinnedMeshRenderer.SetBlendShapeWeight(_eyeLBlinkIndex, value);
            CharacterSkinnedMeshRenderer.SetBlendShapeWeight(_eyeRBlinkIndex, value);
            yield return null;
        }

        CharacterSkinnedMeshRenderer.SetBlendShapeWeight(_eyeLBlinkIndex, targetValue);
        CharacterSkinnedMeshRenderer.SetBlendShapeWeight(_eyeRBlinkIndex, targetValue);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Vector3 GetEyeCenterPosition()
    {
        return _headBone ? _headBone.position + _headBone.up * 0.1f : transform.position + transform.up * 1.6f;
    }

    private void CheckGazeBlendshapeIndices()
    {
        bool allFound = true;

        Check(_eyeLLookLIndex,    _eyeLLookLOptions);
        Check(_eyeRLookLIndex,    _eyeRLookLOptions);
        Check(_eyeLLookRIndex,    _eyeLLookROptions);
        Check(_eyeRLookRIndex,    _eyeRLookROptions);
        Check(_eyeLLookUpIndex,   _eyeLLookUpOptions);
        Check(_eyeRLookUpIndex,   _eyeRLookUpOptions);
        Check(_eyeLLookDownIndex, _eyeLLookDownOptions);
        Check(_eyeRLookDownIndex, _eyeRLookDownOptions);
        Check(_eyeLBlinkIndex,    _eyeLBlinkOptions);
        Check(_eyeRBlinkIndex,    _eyeRBlinkOptions);

        if (!allFound)
            Debug.LogError("EyeGazeController: Some gaze blendshapes missing. Eye gaze may be impaired.", this);
        return;

        void Check(int idx, List<string> names)
        {
            if (idx != -1) return;
            Debug.LogError($"EyeGazeController: Blendshape '{names}' not found.", this); 
            allFound = false;
        }
    }

    private static int GetFirstValidBlendshapeIndex(SkinnedMeshRenderer meshRenderer, List<string> candidates)
    {
        foreach (int index in candidates.Select(shapeName => meshRenderer.sharedMesh.GetBlendShapeIndex(shapeName)).Where(index => index != -1))
        {
            return index;
        }

        return -1;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void SetGazeModeIdle() => CurrentGazeMode = GazeMode.Idle;
    public void SetGazeModeLookAtPlayer() => CurrentGazeMode = GazeMode.LookAtPlayer;
}