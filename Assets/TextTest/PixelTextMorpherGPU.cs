using TMPro;
using UnityEngine;

public class PixelTextMorpherGPU : MonoBehaviour
{
	[Header("Text source")]
	public TMP_Text hiddenText;
	public Camera textCamera;

	[Header("GPU assets")]
	public ComputeShader morphCompute;
	public Material particleMaterial;

	[Header("Render texture")]
	public int textureWidth = 512;
	public int textureHeight = 128;

	[Tooltip("Higher value = fewer pixels/particles. 4 or 6 is a good starting point.")]
	public int sampleStep = 4;

	[Header("Visual")]
	public Vector2 textWorldSize = new Vector2(8f, 2f);

	[Tooltip("If true, the square size is calculated automatically.")]
	public bool autoPixelSize = true;

	[Tooltip("Multiplier for the automatic size. 0.9 = a small gap between squares, 1 = packed tightly.")]
	[Range(0.1f, 2f)]
	public float pixelSizeMultiplier = 0.9f;

	[Tooltip("Used only when Auto Pixel Size is disabled.")]
	public float pixelSize = 0.045f;

	public Color particleColor = Color.white;

	[Header("Animation")]
	public float duration = 0.8f;
	public float chaos = 0.35f;
	public float centerSpawnRadius = 0.05f;

	[Header("Matching")]
	[Tooltip("Maximum distance an old pixel can travel to a new one. If it is farther away, the old pixel disappears and the new one spawns locally.")]
	public float maxMatchDistance = 0.6f;

	int matchedPointCount;
    int unmatchedOldPointCount;
    int unmatchedNewPointCount;
    int visibleParticleCount;

    [Header("Debug")]
    public string initialText = "HELLO";
    public bool autoInitOnStart = true;
    public bool flipY = false;

    RenderTexture oldRT;
    RenderTexture newRT;

    ComputeBuffer oldPoints;
    ComputeBuffer newPoints;
    ComputeBuffer particles;
    ComputeBuffer argsCountBuffer;

    int oldPointCount;
    int newPointCount;

    int kernelBuildOld;
    int kernelBuildNew;
    int kernelInitParticles;
    int kernelSimulateParticles;

    int gridWidth;
    int gridHeight;
    int maxParticles;

    float morphStartTime;
    bool hasInitialized;
    bool isMorphing;
    string currentText = "";

    struct PointGPU
    {
        public Vector2 pos;
    }

    struct PointCPU
    {
        public Vector2 pos;
    }

    struct ParticleGPU
    {
        public Vector2 start;
        public Vector2 target;
        public Vector2 pos;
        public float seed;
        public float active;
        public float mode;
        public float scale;
    }

    void Start()
    {
        if (autoInitOnStart)
        {
            Init(initialText);
        }
    }

    public void Init(string text)
    {
        ReleaseBuffers();
        CreateResources();

        currentText = text;
        hiddenText.text = currentText;

        RenderTextTo(oldRT, currentText);

        oldPointCount = BuildPointBuffer(oldRT, oldPoints, kernelBuildOld);

        RenderTextTo(newRT, currentText);
        newPointCount = BuildPointBuffer(newRT, newPoints, kernelBuildNew);

        matchedPointCount = 0;
        unmatchedOldPointCount = 0;
        unmatchedNewPointCount = newPointCount;
        visibleParticleCount = newPointCount;

        InitParticles(forceFromCenter: true);

        morphStartTime = Time.time;
        isMorphing = true;
        hasInitialized = true;
    }

    public void MorphTo(string newText)
    {
        if (!hasInitialized)
        {
            Init(newText);
            return;
        }

        RenderTextTo(oldRT, currentText);
        RenderTextTo(newRT, newText);

        oldPointCount = BuildPointBuffer(oldRT, oldPoints, kernelBuildOld);
        newPointCount = BuildPointBuffer(newRT, newPoints, kernelBuildNew);

        MatchNewPointsToOldNearest();

        InitParticles(forceFromCenter: false);

        currentText = newText;
        morphStartTime = Time.time;
        isMorphing = true;
    }

    void MatchNewPointsToOldNearest()
    {
        matchedPointCount = 0;
        unmatchedOldPointCount = 0;
        unmatchedNewPointCount = 0;
        visibleParticleCount = 0;

        if (oldPointCount <= 0 && newPointCount <= 0)
            return;

        if (oldPointCount <= 0)
        {
            matchedPointCount = 0;
            unmatchedOldPointCount = 0;
            unmatchedNewPointCount = newPointCount;
            visibleParticleCount = newPointCount;
            return;
        }

        if (newPointCount <= 0)
        {
            matchedPointCount = 0;
            unmatchedOldPointCount = oldPointCount;
            unmatchedNewPointCount = 0;
            visibleParticleCount = oldPointCount;
            return;
        }

        PointCPU[] oldArr = new PointCPU[oldPointCount];
        PointCPU[] newArr = new PointCPU[newPointCount];

        oldPoints.GetData(oldArr, 0, 0, oldPointCount);
        newPoints.GetData(newArr, 0, 0, newPointCount);

        bool[] usedNew = new bool[newPointCount];

        System.Collections.Generic.List<PointCPU> matchedOld = new System.Collections.Generic.List<PointCPU>();
        System.Collections.Generic.List<PointCPU> matchedNew = new System.Collections.Generic.List<PointCPU>();
        System.Collections.Generic.List<PointCPU> unmatchedOld = new System.Collections.Generic.List<PointCPU>();

        float maxDistSqr = maxMatchDistance * maxMatchDistance;

        for (int i = 0; i < oldPointCount; i++)
        {
            Vector2 oldPos = oldArr[i].pos;

            float bestDist = float.MaxValue;
            int bestIndex = -1;

            for (int j = 0; j < newPointCount; j++)
            {
                if (usedNew[j])
                    continue;

                float d = (newArr[j].pos - oldPos).sqrMagnitude;

                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = j;
                }
            }

            if (bestIndex >= 0 && bestDist <= maxDistSqr)
            {
                usedNew[bestIndex] = true;
                matchedOld.Add(oldArr[i]);
                matchedNew.Add(newArr[bestIndex]);
            }
            else
            {
                unmatchedOld.Add(oldArr[i]);
            }
        }

        System.Collections.Generic.List<PointCPU> unmatchedNew = new System.Collections.Generic.List<PointCPU>();

        for (int j = 0; j < newPointCount; j++)
        {
            if (!usedNew[j])
                unmatchedNew.Add(newArr[j]);
        }

        matchedPointCount = matchedOld.Count;
        unmatchedOldPointCount = unmatchedOld.Count;
        unmatchedNewPointCount = unmatchedNew.Count;

        visibleParticleCount = matchedPointCount + unmatchedOldPointCount + unmatchedNewPointCount;

        if (visibleParticleCount > maxParticles)
        {
            Debug.LogWarning($"Visible particle count {visibleParticleCount} is bigger than buffer size {maxParticles}. Some particles will be skipped.");
            visibleParticleCount = maxParticles;
        }

        PointCPU[] oldLayout = new PointCPU[visibleParticleCount];
        PointCPU[] newLayout = new PointCPU[visibleParticleCount];

        int write = 0;

        for (int i = 0; i < matchedPointCount && write < visibleParticleCount; i++)
        {
            oldLayout[write] = matchedOld[i];
            newLayout[write] = matchedNew[i];
            write++;
        }

        for (int i = 0; i < unmatchedOldPointCount && write < visibleParticleCount; i++)
        {
            oldLayout[write] = unmatchedOld[i];
            write++;
        }

        for (int i = 0; i < unmatchedNewPointCount && write < visibleParticleCount; i++)
        {
            newLayout[write] = unmatchedNew[i];
            write++;
        }

        oldPoints.SetData(oldLayout, 0, 0, visibleParticleCount);
        newPoints.SetData(newLayout, 0, 0, visibleParticleCount);
    }

    void CreateResources()
    {
        gridWidth = Mathf.CeilToInt(textureWidth / (float)sampleStep);
        gridHeight = Mathf.CeilToInt(textureHeight / (float)sampleStep);
        maxParticles = gridWidth * gridHeight * 2;

        RecalculatePixelSize();

        oldRT = CreateRT();
        newRT = CreateRT();

        oldPoints = new ComputeBuffer(maxParticles, sizeof(float) * 2, ComputeBufferType.Append);
        newPoints = new ComputeBuffer(maxParticles, sizeof(float) * 2, ComputeBufferType.Append);

        particles = new ComputeBuffer(maxParticles, sizeof(float) * 9);

        // 2 uint: oldCount, newCount
        argsCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

        kernelBuildOld = morphCompute.FindKernel("BuildOldPoints");
        kernelBuildNew = morphCompute.FindKernel("BuildNewPoints");
        kernelInitParticles = morphCompute.FindKernel("InitParticles");
        kernelSimulateParticles = morphCompute.FindKernel("SimulateParticles");

        particleMaterial.SetBuffer("_Particles", particles);
    }

    void RecalculatePixelSize()
    {
        if (!autoPixelSize)
            return;

        float cellWidth = textWorldSize.x / Mathf.Max(1, gridWidth);
        float cellHeight = textWorldSize.y / Mathf.Max(1, gridHeight);

        pixelSize = Mathf.Min(cellWidth, cellHeight) * pixelSizeMultiplier;
    }

    int GetAppendBufferCount(ComputeBuffer appendBuffer)
    {
        ComputeBuffer.CopyCount(appendBuffer, argsCountBuffer, 0);

        uint[] count = new uint[1];
        argsCountBuffer.GetData(count);

        return (int)count[0];
    }

    RenderTexture CreateRT()
    {
        RenderTextureDescriptor desc = new RenderTextureDescriptor(
            textureWidth,
            textureHeight,
            RenderTextureFormat.ARGB32,
            24
        );

        desc.msaaSamples = 1;
        desc.useMipMap = false;
        desc.autoGenerateMips = false;
        desc.sRGB = false;

        RenderTexture rt = new RenderTexture(desc);
        rt.name = "PixelTextMaskRT";
        rt.Create();

        return rt;
    }

    void RenderTextTo(RenderTexture target, string text)
    {
        hiddenText.text = text;
        hiddenText.ForceMeshUpdate();

        textCamera.targetTexture = target;
        textCamera.clearFlags = CameraClearFlags.SolidColor;
        textCamera.backgroundColor = new Color(0, 0, 0, 0);
        textCamera.Render();
        textCamera.targetTexture = null;
    }

    int BuildPointBuffer(RenderTexture source, ComputeBuffer targetBuffer, int kernel)
    {
        targetBuffer.SetCounterValue(0);

        morphCompute.SetTexture(kernel, "_MaskTex", source);
        morphCompute.SetBuffer(kernel, "_OldPoints", oldPoints);
        morphCompute.SetBuffer(kernel, "_NewPoints", newPoints);

        morphCompute.SetInts("_TextureSize", textureWidth, textureHeight);
        morphCompute.SetInts("_GridSize", gridWidth, gridHeight);
        morphCompute.SetInt("_SampleStep", sampleStep);
        morphCompute.SetVector("_TextWorldSize", textWorldSize);
        morphCompute.SetInt("_FlipY", flipY ? 1 : 0);

        int groupsX = Mathf.CeilToInt(gridWidth / 8f);
        int groupsY = Mathf.CeilToInt(gridHeight / 8f);

        morphCompute.Dispatch(kernel, groupsX, groupsY, 1);

        return GetAppendBufferCount(targetBuffer);
    }

    void InitParticles(bool forceFromCenter)
    {
        morphCompute.SetBuffer(kernelInitParticles, "_OldPointsRead", oldPoints);
        morphCompute.SetBuffer(kernelInitParticles, "_NewPointsRead", newPoints);
        morphCompute.SetBuffer(kernelInitParticles, "_Particles", particles);

        morphCompute.SetInt("_MatchedCount", matchedPointCount);
        morphCompute.SetInt("_UnmatchedOldCount", unmatchedOldPointCount);
        morphCompute.SetInt("_UnmatchedNewCount", unmatchedNewPointCount);
        morphCompute.SetInt("_VisibleParticleCount", visibleParticleCount);

        morphCompute.SetInt("_MaxParticles", maxParticles);
        morphCompute.SetFloat("_CenterSpawnRadius", centerSpawnRadius);
        morphCompute.SetInt("_ForceFromCenter", forceFromCenter ? 1 : 0);

        int groups = Mathf.CeilToInt(maxParticles / 128f);
        morphCompute.Dispatch(kernelInitParticles, groups, 1, 1);
    }

    void Update()
    {
        if (!hasInitialized || !isMorphing)
            return;

        float t = Mathf.Clamp01((Time.time - morphStartTime) / Mathf.Max(0.001f, duration));

        morphCompute.SetBuffer(kernelSimulateParticles, "_Particles", particles);
        morphCompute.SetInt("_MaxParticles", maxParticles);
        morphCompute.SetFloat("_T", t);
        morphCompute.SetFloat("_Chaos", chaos);

        int groups = Mathf.CeilToInt(maxParticles / 128f);
        morphCompute.Dispatch(kernelSimulateParticles, groups, 1, 1);

        if (t >= 1f)
            isMorphing = false;
    }

    void OnRenderObject()
    {
        if (!hasInitialized || particleMaterial == null)
            return;

        particleMaterial.SetBuffer("_Particles", particles);
        particleMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        particleMaterial.SetColor("_Color", particleColor);
        particleMaterial.SetFloat("_PixelSize", pixelSize);

        particleMaterial.SetPass(0);

        Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, maxParticles);
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    void ReleaseBuffers()
    {
        oldPoints?.Release();
        newPoints?.Release();
        particles?.Release();
        argsCountBuffer?.Release();

        oldPoints = null;
        newPoints = null;
        particles = null;
        argsCountBuffer = null;

        if (oldRT != null)
        {
            oldRT.Release();
            oldRT = null;
        }

        if (newRT != null)
        {
            newRT.Release();
            newRT = null;
        }
    }
}

