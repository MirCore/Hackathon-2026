using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class LloydLinesOverlay : MonoBehaviour
{
    [Header("Compute Shader (Required)")]
    public ComputeShader linesCompute;

    [Header("Voronoi")]
    [Range(1, 200)]    public int   maxSeeds    = 50;
    [Range(128, 1024)] public int   textureSize = 512;

    [Header("Line Style")]
    [Range(0.001f, 0.05f)] public float edgeThreshold = 0.012f;
    [Range(0f, 1f)]        public float lineAlpha     = 1f;
    [Range(0f, 8f)]        public float seedRadiusPx  = 3f;
    [Range(0f, 1f)]        public float lineStrength  = 0.9f;

    [Header("Growth Animation")]
    [Range(1, 10)]       public int   batchSize    = 5;
    [Range(1, 30)]       public int   lloydSteps   = 15;
    [Range(0f, 0.1f)]    public float stepInterval = 0.03f;
    [Range(0.1f, 2f)]    public float holdPerBatch = 0.3f;
    [Range(0f, 3f)]      public float loopDelay    = 2f;
    [Range(0.01f, 0.4f)] public float spawnRadius  = 0.05f;

    RenderTexture rt;
    ComputeBuffer seedBuf;
    Vector2[]     seeds;
    Material      mat;
    int           kernelClear;
    int           kernelDraw;

    [HideInInspector] public bool externalControl = false;

    static readonly int LinesTexID     = Shader.PropertyToID("_LinesTex");
    static readonly int LineStrengthID = Shader.PropertyToID("_LineStrength");
    
    void Start()
    {
        if (linesCompute == null) { Debug.LogError("[LloydLines] Assign Compute Shader!"); enabled = false; return; }
        mat = GetComponent<Renderer>().material;
        if (!mat.HasProperty(LinesTexID)) { Debug.LogError("[LloydLines] Shader missing _LinesTex! " + mat.shader.name); enabled = false; return; }

        kernelClear = linesCompute.FindKernel("ClearRT");
        kernelDraw  = linesCompute.FindKernel("DrawLines");

        CreateRenderTexture();
        mat.SetTexture(LinesTexID,     rt);
        mat.SetFloat  (LineStrengthID, lineStrength);
        Debug.Log("[LloydLines] OK — " + mat.shader.name + " RT=" + rt.width);

        DispatchClear();

        if (!externalControl)
            StartCoroutine(GrowLoop());
    }

    public void StopAnim()
    {
        StopAllCoroutines();
        DispatchClear();
    }

    public void StartAnim()
    {
        StopAllCoroutines();
        StartCoroutine(GrowLoop());
    }

    void OnValidate() { if (Application.isPlaying && mat != null) mat.SetFloat(LineStrengthID, lineStrength); }
    void OnDestroy()  { seedBuf?.Release(); if (rt != null && rt.IsCreated()) rt.Release(); }

    void CreateRenderTexture()
    {
        rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32)
            { enableRandomWrite = true, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Mirror };
        rt.Create();
        seeds   = new Vector2[maxSeeds];
        seedBuf = new ComputeBuffer(maxSeeds, sizeof(float) * 2);
        seedBuf.SetData(seeds);
    }

    void DispatchClear()
    {
        linesCompute.SetTexture(kernelClear, "Result",  rt);
        linesCompute.SetInt    ("TextureSize",          textureSize);
        int g = Mathf.CeilToInt(textureSize / 8f);
        linesCompute.Dispatch(kernelClear, g, g, 1);
    }

    void DrawFrame(int count)
    {
        DispatchClear();
        if (count <= 0) return;
        linesCompute.SetTexture(kernelDraw, "Result",      rt);
        linesCompute.SetBuffer (kernelDraw, "Seeds",       seedBuf);
        linesCompute.SetInt    ("NumSeeds",                count);
        linesCompute.SetInt    ("TextureSize",             textureSize);
        linesCompute.SetFloat  ("EdgeThreshold",           edgeThreshold);
        linesCompute.SetFloat  ("LineAlpha",               lineAlpha);
        linesCompute.SetFloat  ("SeedRadius",              seedRadiusPx);
        int g = Mathf.CeilToInt(textureSize / 8f);
        linesCompute.Dispatch(kernelDraw, g, g, 1);
    }

    void LloydPass(int count)
    {
        int res = Mathf.Min(textureSize, 256);
        Vector2[] centSum   = new Vector2[count];
        int[]     cellCount = new int[count];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                Vector2 uv      = new Vector2((float)x / res, (float)y / res);
                int     nearest = NearestInFirst(uv, count);
                centSum[nearest]   += uv;
                cellCount[nearest]++;
            }
        for (int i = 0; i < count; i++)
            if (cellCount[i] > 0)
                seeds[i] = centSum[i] / cellCount[i];
    }

    int NearestInFirst(Vector2 uv, int count)
    {
        int best = 0; float minD = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            float d = Vector2.SqrMagnitude(uv - seeds[i]);
            if (d < minD) { minD = d; best = i; }
        }
        return best;
    }

    void SpawnBatchAtCenter(int existingCount, int batch, int batchIndex)
    {
        float angleOffset = batchIndex * (Mathf.PI / batchSize);
        for (int i = 0; i < batch; i++)
        {
            float angle = angleOffset + i * (Mathf.PI * 2f / batch);
            seeds[existingCount + i] = new Vector2(
                0.5f + Mathf.Cos(angle) * spawnRadius,
                0.5f + Mathf.Sin(angle) * spawnRadius);
        }
    }

    IEnumerator GrowLoop()
    {
        while (true)
        {
            int totalCount = 0;
            int batchIndex = 0;

            while (totalCount < maxSeeds)
            {
                int batch = Mathf.Min(batchSize, maxSeeds - totalCount);
                SpawnBatchAtCenter(totalCount, batch, batchIndex);
                totalCount += batch;
                batchIndex++;

                seedBuf.SetData(seeds);
                DrawFrame(totalCount);
                yield return null;

                for (int step = 0; step < lloydSteps; step++)
                {
                    float t      = (step + 1f) / lloydSteps;
                    float easedT = 1f - Mathf.Pow(1f - t, 3f);

                    LloydPass(totalCount);

                    if (step < lloydSteps / 2)
                    {
                        float rotAngle = easedT * Mathf.PI * 0.25f * (batchIndex % 2 == 0 ? 1f : -1f);
                        for (int i = totalCount - batch; i < totalCount; i++)
                        {
                            Vector2 dir = seeds[i] - Vector2.one * 0.5f;
                            float   c   = Mathf.Cos(rotAngle);
                            float   s   = Mathf.Sin(rotAngle);
                            seeds[i]    = Vector2.one * 0.5f + new Vector2(
                                dir.x * c - dir.y * s,
                                dir.x * s + dir.y * c);
                        }
                    }

                    seedBuf.SetData(seeds);
                    DrawFrame(totalCount);
                    yield return new WaitForSeconds(stepInterval);
                }

                yield return new WaitForSeconds(holdPerBatch);
            }

            yield return new WaitForSeconds(loopDelay);

            System.Array.Clear(seeds, 0, seeds.Length);
            seedBuf.SetData(seeds);
            DispatchClear();
            yield return null;
        }
    }
}