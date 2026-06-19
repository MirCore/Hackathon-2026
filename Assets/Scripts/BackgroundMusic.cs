using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    [Header("BGM Settings")]
    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float volume     = 0.4f;
    [Range(0f, 5f)]
    public float fadeInTime = 2f;

    [Header("Singleton")]
    public bool dontDestroyOnLoad = true;

    static BackgroundMusic instance;
    AudioSource audioSource;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        audioSource              = GetComponent<AudioSource>();
        audioSource.loop         = true;
        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f;   // 2D，非空间音
        audioSource.volume       = 0f;
        if (bgmClip != null) audioSource.clip = bgmClip;
    }

    void Start()
    {
        if (audioSource.clip == null) return;
        audioSource.Play();
        StartCoroutine(FadeIn());
    }

    System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, volume, elapsed / fadeInTime);
            yield return null;
        }
        audioSource.volume = volume;
    }

    public void SetVolume(float v) { volume = Mathf.Clamp01(v); audioSource.volume = volume; }
    public void Pause()  => audioSource.Pause();
    public void Resume() => audioSource.UnPause();
    public void Stop()   => audioSource.Stop();
}