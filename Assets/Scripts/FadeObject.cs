using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// Add to any GameObject to make it fade in/out with the global _FadeProgress.
/// Works with URP Lit / Simple Lit / Unlit materials.
/// At _FadeProgress = 0 the object is invisible; at 1 it is fully opaque.
/// Use FadeProgressOverride to test, SceneFadeBroadcaster for runtime.
/// </summary>
public class FadeObject : MonoBehaviour
{
    [Tooltip("Also fade Renderers on child objects.")]
    [SerializeField] bool _includeChildren = true;

    struct Entry { public Material mat; public float originalAlpha; }
    readonly List<Entry> _entries = new();

    static readonly int BaseColorID    = Shader.PropertyToID("_BaseColor");
    static readonly int FadeProgressID = Shader.PropertyToID("_FadeProgress");

    void Start()
    {
        var renderers = _includeChildren
            ? GetComponentsInChildren<Renderer>()
            : GetComponents<Renderer>();

        foreach (var r in renderers)
        {
            // .materials creates per-instance copies — safe to modify at runtime
            foreach (var mat in r.materials)
            {
                float originalAlpha = mat.HasProperty(BaseColorID)
                    ? mat.GetColor(BaseColorID).a
                    : 1f;

                MakeTransparent(mat);
                _entries.Add(new Entry { mat = mat, originalAlpha = originalAlpha });
            }
        }
    }

    void Update()
    {
        float progress = Shader.GetGlobalFloat(FadeProgressID);

        foreach (var e in _entries)
        {
            if (e.mat == null || !e.mat.HasProperty(BaseColorID)) continue;

            Color c = e.mat.GetColor(BaseColorID);
            c.a = e.originalAlpha * progress;
            e.mat.SetColor(BaseColorID, c);
        }
    }

    static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface",        1f); // 1 = Transparent
        mat.SetFloat("_Blend",          0f); // 0 = Alpha
        mat.SetFloat("_ZWrite",         0f);
        mat.SetFloat("_ZWriteControl",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetShaderPassEnabled("DepthOnly",    false);
        mat.SetShaderPassEnabled("SHADOWCASTER", false);
        mat.renderQueue = (int)RenderQueue.Transparent;
    }
}
