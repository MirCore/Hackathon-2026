using System.Collections;
using System.Collections.Generic;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.XR;

// Prevents the XR camera from rendering before the XR display subsystem
// has any render passes — avoids IndexOutOfRangeException at startup with PDC.
public class XRReadyGuard : MonoBehaviour
{
    [Tooltip("Camera to disable until XR is ready. Defaults to Camera on this GameObject.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("How long to wait between readiness checks (seconds).")]
    [SerializeField] private float pollInterval = 0.05f;

    [SerializeField] private PXR_Manager PXR_Manager;
    
    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
        {
            targetCamera.enabled = false;
            StartCoroutine(WaitForXR());
        }
    }

    private IEnumerator WaitForXR()
    {
        var displays = new List<XRDisplaySubsystem>();

        while (true)
        {
            SubsystemManager.GetSubsystems(displays);

            foreach (var display in displays)
            {
                if (display.running && display.GetRenderPassCount() > 0)
                {
                    targetCamera.enabled = true;
                    
                    PXR_Manager.EnableVideoSeeThrough = true;
                    yield break;
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }
}
