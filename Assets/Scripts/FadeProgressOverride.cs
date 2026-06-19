using UnityEngine;

/// <summary>
/// Manually overrides _FadeProgress for testing.
/// Drag the slider in the Inspector to test the FadeOverlay effect.
/// Disable or remove this component when using SceneFadeBroadcaster at runtime.
/// </summary>
[ExecuteAlways]
public class FadeProgressOverride : MonoBehaviour
{
    [Range(0f, 1f)]
    public float fadeProgress = 0f;

    static readonly int FadeProgressID = Shader.PropertyToID("_FadeProgress");

    void Update()
    {
        Shader.SetGlobalFloat(FadeProgressID, fadeProgress);
    }
}
