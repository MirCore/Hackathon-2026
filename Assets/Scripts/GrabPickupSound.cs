using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class GrabPickupSound : MonoBehaviour
{
    [SerializeField] AudioClip _clip;
    [SerializeField] AudioSource _audioSource;

    const float Cooldown = 30f;
    float _nextAllowedTime = 0f;

    XRGrabInteractable _grab;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (Time.time < _nextAllowedTime) return;
        _nextAllowedTime = Time.time + Cooldown;
        _audioSource.Stop();
        _audioSource.PlayOneShot(_clip);
    }
}
