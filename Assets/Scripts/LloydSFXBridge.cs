using System.Collections;
using UnityEngine;

public class LloydSFXBridge : MonoBehaviour
{
    [Header("References")]
    public LloydLinesOverlay lloydOverlay;
    public AudioSource       proximityAudio;

    [Header("Fade")]
    public float fadeInTime  = 0.5f;
    public float fadeOutTime = 1.0f;

    Coroutine _fadeCoroutine;
    float     _maxAlpha;
    bool      _wasPlaying;

    void Awake()
    {
        if (lloydOverlay == null)   { Debug.LogError("[LloydSFXBridge] lloydOverlay not assigned!");  enabled = false; return; }
        if (proximityAudio == null) { Debug.LogError("[LloydSFXBridge] proximityAudio not assigned!"); enabled = false; return; }
        lloydOverlay.externalControl = true;
        _maxAlpha = lloydOverlay.lineAlpha;
    }

    IEnumerator Start()
    {
        yield return null; // wait for LloydLinesOverlay.Start() to finish RT init
        lloydOverlay.lineAlpha = 0f;
        lloydOverlay.StartAnim();
        Debug.Log("[LloydSFXBridge] Ready");
    }

    void Update()
    {
        bool playing = proximityAudio != null && proximityAudio.isPlaying;
        if (playing == _wasPlaying) return;
        _wasPlaying = playing;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAlpha(playing ? _maxAlpha : 0f, playing ? fadeInTime : fadeOutTime));
    }

    IEnumerator FadeAlpha(float target, float duration)
    {
        float start   = lloydOverlay.lineAlpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lloydOverlay.lineAlpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        lloydOverlay.lineAlpha = target;
        _fadeCoroutine = null;
    }
}
