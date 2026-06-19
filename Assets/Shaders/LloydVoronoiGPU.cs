using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class LloydVoronoiGPU : MonoBehaviour
{
    [Header("Compute Shader (Required)")]
    public ComputeShader voronoiCompute;

    [Header("Voronoi Settings")]
    [Range(5, 500)]    public int   numSeeds    = 50;
    [Range(1, 100)]    public int   iterations  = 20;
    [Range(128, 2048)] public int   textureSize = 512;

    [Header("Visualization")]
    public bool  showEdges     = true;
    public bool  showSeeds     = true;
    [Range(0f, 0.02f)]
    public float edgeThreshold = 0.001f;

    [Header("Animation")]
    [Range(0.05f, 2f)]
    public float stepDelay = 0.05f;

    [Header("Brownian Motion")]
    [Range(0f, 0.02f)]
    public float brownianStrength = 0.004f;
    [Range(0f, 1f)]
    public float brownianSmooth   = 0.15f;

    static readonly Color[] Palette = new Color[]
    {
        // ── 暖土色（提亮，高对比）
        new Color(232f/255f, 103f/255f,  53f/255f),  // #E86735 vivid burnt orange
        new Color(253f/255f, 213f/255f, 165f/255f),  // #FDD5A5 bright warm sand
        new Color(156f/255f,  84f/255f,  50f/255f),  // #9C5432 warm mid-brown
        new Color(255f/255f, 121f/255f,  72f/255f),  // #FF7948 bright orange-red
        new Color( 98f/255f,  48f/255f,  27f/255f),  // #62301B rich clay brown
        // ── 冷色点缀（与暖土形成对比）
        new Color( 89f/255f, 135f/255f, 166f/255f),  // #5987A6 steel blue
        new Color(132f/255f, 191f/255f, 211f/255f),  // #84BFD3 light dusty teal
        // ── 深色锚点（阴影 / 深度）
        new Color(181f/255f,  61f/255f,  47f/255f),  // #B53D2F deep red
        new Color( 37f/255f,  23f/255f,  16f/255f),  // #251710 near-black brown
        new Color( 25f/255f,  37f/255f,  63f/255f),  // #19253F near-black navy
    };

    RenderTexture rt;
    ComputeBuffer seedBuf;
    ComputeBuffer colorBuf;
    Vector2[] seeds;
    Vector4[] colors;
    int       kernelDraw;
    int       currentIter = 0;
    Vector2[] velocities;

    void Start()
    {
        if (voronoiCompute == null)
        {
            Debug.LogError("[LloydVoronoiGPU] Assign the Compute Shader in the Inspector!");
            enabled = false;
            return;
        }
        CreateRenderTexture();
        InitSeedsAndBuffers();
        BindTextureToMaterial();
        DrawOnGPU();
        StartCoroutine(AnimateRelaxation());
    }

    void OnDestroy()
    {
        seedBuf?.Release();
        colorBuf?.Release();
        if (rt != null && rt.IsCreated()) rt.Release();
    }

    void CreateRenderTexture()
    {
        rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true,
            filterMode        = FilterMode.Bilinear,
            wrapMode          = TextureWrapMode.Clamp
        };
        rt.Create();
    }

    void InitSeedsAndBuffers()
    {
        seeds      = new Vector2[numSeeds];
        colors     = new Vector4[numSeeds];
        velocities = new Vector2[numSeeds];

        for (int i = 0; i < numSeeds; i++)
        {
            seeds[i] = new Vector2(Random.value, Random.value);
            AssignColor(i);
        }

        seedBuf  = new ComputeBuffer(numSeeds, sizeof(float) * 2);
        colorBuf = new ComputeBuffer(numSeeds, sizeof(float) * 4);
        seedBuf.SetData(seeds);
        colorBuf.SetData(colors);
        kernelDraw = voronoiCompute.FindKernel("DrawVoronoi");
    }

    void AssignColor(int i)
    {
        Color baseColor = Palette[i % Palette.Length];
        float jitter    = Random.Range(-0.04f, 0.04f);
        colors[i] = new Vector4(
            Mathf.Clamp01(baseColor.r + jitter),
            Mathf.Clamp01(baseColor.g + jitter),
            Mathf.Clamp01(baseColor.b + jitter),
            1f
        );
    }

    void BindTextureToMaterial()
    {
        GetComponent<Renderer>().material.mainTexture = rt;
    }

    void DrawOnGPU()
    {
        voronoiCompute.SetTexture(kernelDraw, "Result",    rt);
        voronoiCompute.SetBuffer (kernelDraw, "Seeds",     seedBuf);
        voronoiCompute.SetBuffer (kernelDraw, "Colors",    colorBuf);
        voronoiCompute.SetInt    ("NumSeeds",              numSeeds);
        voronoiCompute.SetInt    ("TextureSize",           textureSize);
        voronoiCompute.SetFloat  ("EdgeThreshold",         edgeThreshold);
        voronoiCompute.SetBool   ("ShowEdges",             showEdges);
        voronoiCompute.SetBool   ("ShowSeeds",             showSeeds);
        int groups = Mathf.CeilToInt(textureSize / 8f);
        voronoiCompute.Dispatch(kernelDraw, groups, groups, 1);
    }

    void BrownianStep()
    {
        for (int i = 0; i < numSeeds; i++)
        {
            Vector2 impulse = new Vector2(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized * brownianStrength;

            velocities[i] = Vector2.Lerp(impulse, velocities[i], brownianSmooth);
            seeds[i]     += velocities[i];

            if (seeds[i].x < 0f) { seeds[i].x =  0f; velocities[i].x *= -0.5f; }
            if (seeds[i].x > 1f) { seeds[i].x =  1f; velocities[i].x *= -0.5f; }
            if (seeds[i].y < 0f) { seeds[i].y =  0f; velocities[i].y *= -0.5f; }
            if (seeds[i].y > 1f) { seeds[i].y =  1f; velocities[i].y *= -0.5f; }
        }
        seedBuf.SetData(seeds);
    }

    void LloydStepCPU()
    {
        Vector2[] centroidSum = new Vector2[numSeeds];
        int[]     cellCount   = new int[numSeeds];

        for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 uv      = new Vector2((float)x / textureSize, (float)y / textureSize);
                int     nearest = GetNearestSeed(uv);
                centroidSum[nearest] += uv;
                cellCount  [nearest]++;
            }

        for (int i = 0; i < numSeeds; i++)
            if (cellCount[i] > 0)
                seeds[i] = centroidSum[i] / cellCount[i];

        seedBuf.SetData(seeds);
    }

    int GetNearestSeed(Vector2 uv)
    {
        int   best    = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < numSeeds; i++)
        {
            float d = Vector2.SqrMagnitude(uv - seeds[i]);
            if (d < minDist) { minDist = d; best = i; }
        }
        return best;
    }

    IEnumerator AnimateRelaxation()
    {
        int stepCount = 0;
        while (true)
        {
            BrownianStep();
            stepCount++;
            if (stepCount % Mathf.Max(1, iterations / 5) == 0)
                LloydStepCPU();
            DrawOnGPU();
            yield return new WaitForSeconds(stepDelay);
        }
    }

    public void ResetAndRerun()
    {
        StopAllCoroutines();
        currentIter = 0;
        for (int i = 0; i < numSeeds; i++)
        {
            seeds[i]      = new Vector2(Random.value, Random.value);
            velocities[i] = Vector2.zero;
            AssignColor(i);
        }
        seedBuf.SetData(seeds);
        colorBuf.SetData(colors);
        DrawOnGPU();
        StartCoroutine(AnimateRelaxation());
    }

    public void ManualStep()
    {
        if (currentIter >= iterations) return;
        currentIter++;
        LloydStepCPU();
        DrawOnGPU();
    }
}