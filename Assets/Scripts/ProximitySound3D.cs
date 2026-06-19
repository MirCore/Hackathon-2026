using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(SphereCollider))]
public class ProximitySound3D : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)]
    public float maxVolume = 0.75f;
    [Range(0.1f, 5f)]
    public float fadeSpeed = 2f;

    [Header("Trigger Radius")]
    public float triggerRadius = 2f;

    [Header("Lloyd Lines (optional)")]
    public LloydLinesOverlay lloydOverlay;

    [Header("Grow On Step (optional — 0 disables)")]
    public GameObject growTarget;
    public float growDuration     = 0f;
    public float growTargetHeight = 1f;

    AudioSource    audioSource;
    Coroutine      fadeCoroutine;
    float          lloydMaxAlpha;

    MeshRenderer[] _renderers;
    float          _growProgress;
    bool           _playerInRange;

    void Awake()
    {
        audioSource             = GetComponent<AudioSource>();
        audioSource.volume      = 0f;
        audioSource.loop        = true;
        audioSource.playOnAwake = false;

        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = triggerRadius;

        if (lloydOverlay != null)
        {
            lloydOverlay.externalControl = true;
            lloydMaxAlpha = lloydOverlay.lineAlpha;
        }

        if (growDuration > 0f && growTarget != null)
        {
            _renderers = growTarget.GetComponentsInChildren<MeshRenderer>();
            SetScaleY(0f);
            foreach (var r in _renderers) r.enabled = false;
        }
    }

    IEnumerator Start()
    {
        yield return null;

        if (lloydOverlay != null)
        {
            lloydOverlay.lineAlpha = 0f;
            lloydOverlay.StartAnim();
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, triggerRadius);
        foreach (Collider c in hits)
        {
            if (c.CompareTag("Player"))
            {
                if (!audioSource.isPlaying) audioSource.Play();
                StartFade(maxVolume);
                _playerInRange = true;
                break;
            }
        }
    }

    void Update()
    {
        if (growDuration <= 0f || growTarget == null || !_playerInRange || _growProgress >= growTargetHeight) return;

        if (_growProgress == 0f)
            foreach (var r in _renderers) r.enabled = true;

        _growProgress = Mathf.MoveTowards(_growProgress, growTargetHeight, Time.deltaTime / growDuration);
        SetScaleY(_growProgress);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!audioSource.isPlaying) audioSource.Play();
        StartFade(maxVolume);
        _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        StartFade(0f, stopOnSilent: true);
        _playerInRange = false;
    }

    void StartFade(float targetVolume, bool stopOnSilent = false)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeVolume(targetVolume, stopOnSilent));
    }

    IEnumerator FadeVolume(float target, bool stopOnSilent)
    {
        float alphaTarget = target > 0f ? lloydMaxAlpha : 0f;

        while (!Mathf.Approximately(audioSource.volume, target))
        {
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, target, fadeSpeed * Time.deltaTime);

            if (lloydOverlay != null)
                lloydOverlay.lineAlpha = Mathf.MoveTowards(lloydOverlay.lineAlpha, alphaTarget, fadeSpeed * Time.deltaTime);

            yield return null;
        }

        audioSource.volume = target;
        if (lloydOverlay != null) lloydOverlay.lineAlpha = alphaTarget;
        if (stopOnSilent && target == 0f) audioSource.Stop();
        fadeCoroutine = null;
    }

    void SetScaleY(float y)
    {
        Vector3 s = growTarget.transform.localScale;
        s.y = y;
        growTarget.transform.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
        Gizmos.DrawSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
