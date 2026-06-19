using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class GrabGracePeriod : MonoBehaviour
{
    [SerializeField] float _gracePeriod = 0.35f;
    [SerializeField] float _reGrabDistance = 0.15f;

    XRGrabInteractable _grab;
    XRInteractionManager _manager;
    Coroutine _pendingDrop;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _manager = FindAnyObjectByType<XRInteractionManager>();
        _grab.selectExited.AddListener(OnDropped);
        _grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDestroy()
    {
        _grab.selectExited.RemoveListener(OnDropped);
        _grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (_pendingDrop == null) return;
        StopCoroutine(_pendingDrop);
        _pendingDrop = null;
    }

    void OnDropped(SelectExitEventArgs args)
    {
        if (args.isCanceled) return;
        _pendingDrop = StartCoroutine(TryReGrab(args.interactorObject));
    }

    IEnumerator TryReGrab(IXRSelectInteractor interactor)
    {
        yield return new WaitForSeconds(_gracePeriod);
        _pendingDrop = null;

        if (_grab.isSelected) yield break;

        var interactorTransform = (interactor as MonoBehaviour)?.transform;
        if (interactorTransform == null) yield break;

        if (Vector3.Distance(interactorTransform.position, transform.position) > _reGrabDistance)
            yield break;

        _manager.SelectEnter(interactor, _grab);
    }
}
