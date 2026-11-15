using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurfaceFragmentPrototype
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SurfaceFragmentRevealPrototype : MonoBehaviour
    {
        private const int MaximumBoundaries = 16;

        [Serializable]
        public sealed class Boundary
        {
            public Transform start;
            public Transform end;
        }

        [SerializeField] private MeshFilter source;
        [SerializeField, Tooltip("Any positive cutting grid size in source mesh local units. Smaller values need more time and memory. Rebuild after changing.")]
        private float cellSize = 0.5f;
        [SerializeField] private Color fallbackColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float revealProgress = 1f;
        [SerializeField, Tooltip("One to sixteen finite world-space segments. Height is ignored.")]
        private List<Boundary> boundaries = new List<Boundary>();
        [SerializeField, Min(0.001f)] private float transitionWidth = 0.4f;
        [SerializeField, Min(0f)] private float revealChaos = 0.8f;
        [SerializeField, Min(0f), Tooltip("Scatter distance in this component's local units.")]
        private float scatterDistance = 2f;
        [SerializeField, Range(0f, 180f)] private float scatterRotation = 120f;
        [SerializeField, Min(0f)] private float waveStrength = 0.2f;

        private GameObject preview;
        private Mesh generatedMesh;
        private Material[] generatedMaterials;
        private MeshRenderer previewRenderer;
        private MaterialPropertyBlock propertyBlock;
        private List<Vector3> fragmentCenters;
        private readonly Vector4[] segments = new Vector4[MaximumBoundaries];
        private readonly Vector4[] pendingSegments = new Vector4[MaximumBoundaries];
        private int boundaryCount;
        private Matrix4x4 appliedMatrix;
        private float maximumDistance;
        private float maximumRadius;
        private bool propertiesDirty = true;
        private string configurationError;
        #if UNITY_EDITOR
        private double lastEditorPreviewTick;
        #endif

        public float RevealProgress => revealProgress;
        public int FragmentCount => fragmentCenters != null ? fragmentCenters.Count : 0;
        public int PreviewVertexCount => generatedMesh != null ? generatedMesh.vertexCount : 0;

        public double EstimatedCellCount
        {
            get
            {
                if (source == null || source.sharedMesh == null || !float.IsFinite(cellSize) || cellSize <= 0f)
                {
                    return 0;
                }

                Vector3 size = source.sharedMesh.bounds.size;
                return Math.Max(1, Math.Ceiling((double)size.x / cellSize)) *
                    Math.Max(1, Math.Ceiling((double)size.y / cellSize)) *
                    Math.Max(1, Math.Ceiling((double)size.z / cellSize));
            }
        }

        [ContextMenu("Build Preview")]
        public void BuildPreview()
        {
            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("Enable the prototype component before building a preview.");
            }

            if (source == null || source.sharedMesh == null || !source.TryGetComponent(out MeshRenderer sourceRenderer))
            {
                throw new InvalidOperationException("Assign a source MeshFilter with a MeshRenderer and a mesh. Skinned meshes are not supported.");
            }

            if (!TryPackBoundaries(out string error))
            {
                throw new InvalidOperationException(error);
            }

            Shader shader = Shader.Find("Surface Fragment Prototype/URP Reveal");
            if (shader == null)
            {
                throw new InvalidOperationException("Surface Fragment Prototype/URP Reveal shader was not found. Check shader import errors.");
            }

            Matrix4x4 targetFromSource = transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            if (targetFromSource.determinant == 0f || !float.IsFinite(targetFromSource.determinant))
            {
                throw new InvalidOperationException("Source and prototype transforms must not have zero scale.");
            }

            CellFragmentBuilder.Result result;
            try
            {
                result = CellFragmentBuilder.Build(source.sharedMesh, cellSize, targetFromSource, ReportGenerationProgress);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Surface fragment generation canceled. The previous preview was retained.", this);
                return;
            }
            finally
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.ClearProgressBar();
                #endif
            }

            ClearPreview();
            generatedMesh = result.mesh;
            fragmentCenters = result.centers;
            maximumRadius = result.maximumRadius;
            Material[] sourceMaterials = sourceRenderer.sharedMaterials;
            generatedMaterials = new Material[generatedMesh.subMeshCount];
            for (int index = 0; index < generatedMaterials.Length; index++)
            {
                Material sourceMaterial = index < sourceMaterials.Length ? sourceMaterials[index] : null;
                generatedMaterials[index] = CreateMaterial(shader, sourceMaterial);
            }

            preview = new GameObject("Surface Fragments (Temporary Preview)");
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.layer = gameObject.layer;
            preview.transform.SetParent(transform, false);
            preview.AddComponent<MeshFilter>().sharedMesh = generatedMesh;
            previewRenderer = preview.AddComponent<MeshRenderer>();
            previewRenderer.sharedMaterials = generatedMaterials;
            previewRenderer.shadowCastingMode = ShadowCastingMode.On;
            previewRenderer.receiveShadows = true;
            propertyBlock = new MaterialPropertyBlock();
            boundaryCount = 0;
            propertiesDirty = true;
            Synchronize();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= AnimateEditorPreview;
            UnityEditor.EditorApplication.update += AnimateEditorPreview;
            #endif
            Debug.Log("Surface fragment preview: " + fragmentCenters.Count + " solid fragments, " +
                generatedMesh.vertexCount + " vertices. Source renderer is unchanged; disable it manually to view the preview alone.", this);
            if (result.thickenedTriangles > 0)
            {
                Debug.Log("Added a thin backing shell to " + result.thickenedTriangles + " open-surface triangles.", this);
            }

            if (result.openCuts > 0 || result.ignoredPrimitives > 0)
            {
                Debug.LogWarning("Generated with " + result.openCuts + " cuts containing inconsistent/open contours and " +
                    result.ignoredPrimitives + " point/line indices skipped. Inspect the result for cap artifacts.", this);
            }
        }

        private bool ReportGenerationProgress(float progress)
        {
            #if UNITY_EDITOR
            return UnityEditor.EditorUtility.DisplayCancelableProgressBar("Surface Fragments",
                "Cutting geometry and closing contours. Cancel keeps the previous preview.", progress);
            #else
            return false;
            #endif
        }

        private Material CreateMaterial(Shader shader, Material sourceMaterial)
        {
            Material material = new Material(shader)
            {
                name = "Surface Fragments (Temporary)",
                hideFlags = HideFlags.HideAndDontSave
            };
            Color baseColor = fallbackColor;
            if (sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty("_BaseColor"))
                {
                    baseColor = sourceMaterial.GetColor("_BaseColor");
                }
                else if (sourceMaterial.HasProperty("_Color"))
                {
                    baseColor = sourceMaterial.GetColor("_Color");
                }

                string textureProperty = sourceMaterial.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (sourceMaterial.HasProperty(textureProperty))
                {
                    material.SetTexture("_BaseMap", sourceMaterial.GetTexture(textureProperty));
                    material.SetTextureScale("_BaseMap", sourceMaterial.GetTextureScale(textureProperty));
                    material.SetTextureOffset("_BaseMap", sourceMaterial.GetTextureOffset(textureProperty));
                }

                bool alphaClip = sourceMaterial.HasProperty("_AlphaClip") && sourceMaterial.GetFloat("_AlphaClip") > 0.5f;
                alphaClip |= sourceMaterial.GetTag("RenderType", false) == "TransparentCutout";
                if (alphaClip)
                {
                    material.SetFloat("_Cutoff", sourceMaterial.HasProperty("_Cutoff") ? sourceMaterial.GetFloat("_Cutoff") : 0.5f);
                }

            }

            material.SetColor("_BaseColor", baseColor);
            return material;
        }

        [ContextMenu("Clear Preview")]
        public void ClearPreview()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= AnimateEditorPreview;
            #endif
            DestroyTemporary(preview);
            DestroyTemporary(generatedMesh);
            if (generatedMaterials != null)
            {
                foreach (Material material in generatedMaterials)
                {
                    DestroyTemporary(material);
                }
            }
            preview = null;
            generatedMesh = null;
            generatedMaterials = null;
            previewRenderer = null;
            fragmentCenters = null;
            propertyBlock = null;
        }

        public void SetProgress(float progress)
        {
            revealProgress = Mathf.Clamp01(progress);
            propertiesDirty = true;
        }

        private void OnValidate()
        {
            revealProgress = Mathf.Clamp01(revealProgress);
            transitionWidth = Mathf.Max(0.001f, transitionWidth);
            revealChaos = Mathf.Max(0f, revealChaos);
            scatterDistance = Mathf.Max(0f, scatterDistance);
            waveStrength = Mathf.Max(0f, waveStrength);
            propertiesDirty = true;
        }

        private void OnDidApplyAnimationProperties()
        {
            propertiesDirty = true;
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void Update()
        {
            if (previewRenderer != null)
            {
                Synchronize();
                if (Application.IsPlaying(gameObject))
                {
                    UpdateMotionTime();
                }
            }
        }

        private void UpdateMotionTime()
        {
            if (revealProgress <= 0f || revealProgress >= 1f)
            {
                return;
            }

            #if UNITY_EDITOR
            float motionTime = Application.IsPlaying(gameObject) ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
            #else
            float motionTime = Time.time;
            #endif
            propertyBlock.SetFloat("_MotionTime", motionTime);
            previewRenderer.SetPropertyBlock(propertyBlock);
        }

        #if UNITY_EDITOR
        private void AnimateEditorPreview()
        {
            if (Application.IsPlaying(gameObject) || previewRenderer == null || revealProgress <= 0f || revealProgress >= 1f)
            {
                return;
            }

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - lastEditorPreviewTick < 1.0 / 30.0)
            {
                return;
            }

            lastEditorPreviewTick = now;
            Synchronize();
            UpdateMotionTime();
            UnityEditor.SceneView.RepaintAll();
        }
        #endif

        private void Synchronize()
        {
            if (!TryPackBoundaries(out string error))
            {
                if (error != configurationError)
                {
                    Debug.LogWarning(error + " The last valid preview state is retained.", this);
                    configurationError = error;
                }

                return;
            }

            configurationError = null;
            bool rangeChanged = boundaryCount != boundaries.Count || appliedMatrix != transform.localToWorldMatrix;
            for (int index = 0; index < boundaries.Count; index++)
            {
                rangeChanged |= segments[index] != pendingSegments[index];
            }

            if (rangeChanged)
            {
                Array.Copy(pendingSegments, segments, MaximumBoundaries);
                boundaryCount = boundaries.Count;
                appliedMatrix = transform.localToWorldMatrix;
                maximumDistance = 0f;
                foreach (Vector3 center in fragmentCenters)
                {
                    Vector3 centerWS = appliedMatrix.MultiplyPoint3x4(center);
                    Vector2 centerXZ = new Vector2(centerWS.x, centerWS.z);
                    float minimumSqr = float.PositiveInfinity;
                    for (int index = 0; index < boundaryCount; index++)
                    {
                        Vector4 segment = segments[index];
                        Vector2 start = new Vector2(segment.x, segment.y);
                        Vector2 direction = new Vector2(segment.z - segment.x, segment.w - segment.y);
                        float projection = Mathf.Clamp01(Vector2.Dot(centerXZ - start, direction) / direction.sqrMagnitude);
                        minimumSqr = Mathf.Min(minimumSqr, (centerXZ - start - direction * projection).sqrMagnitude);
                    }

                    maximumDistance = Mathf.Max(maximumDistance, Mathf.Sqrt(minimumSqr));
                }
            }

            if (!rangeChanged && !propertiesDirty)
            {
                return;
            }

            Bounds animatedBounds = generatedMesh.bounds;
            animatedBounds.Expand(2f * (scatterDistance * 1.4f + waveStrength + 2f * maximumRadius));
            previewRenderer.localBounds = animatedBounds;
            propertyBlock.SetFloat("_RevealProgress", revealProgress);
            propertyBlock.SetFloat("_RevealMaxDistance", maximumDistance);
            propertyBlock.SetFloat("_TransitionWidth", transitionWidth);
            propertyBlock.SetFloat("_RevealChaos", revealChaos);
            propertyBlock.SetFloat("_ScatterDistance", scatterDistance);
            propertyBlock.SetFloat("_ScatterRotation", scatterRotation * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_WaveStrength", waveStrength);
            propertyBlock.SetInteger("_BoundaryCount", boundaryCount);
            propertyBlock.SetVectorArray("_Boundaries", segments);
            previewRenderer.SetPropertyBlock(propertyBlock);
            propertiesDirty = false;
        }

        private bool TryPackBoundaries(out string error)
        {
            if (boundaries.Count < 1 || boundaries.Count > MaximumBoundaries)
            {
                error = "Assign between 1 and 16 boundaries.";
                return false;
            }

            for (int index = 0; index < boundaries.Count; index++)
            {
                Boundary boundary = boundaries[index];
                if (boundary == null || boundary.start == null || boundary.end == null)
                {
                    error = "Boundary " + (index + 1) + " requires Start and End transforms.";
                    return false;
                }

                Vector3 start = boundary.start.position;
                Vector3 end = boundary.end.position;
                if (new Vector2(end.x - start.x, end.z - start.z).sqrMagnitude <= 0.00000001f)
                {
                    error = "Boundary " + (index + 1) + " must have distinct XZ endpoints.";
                    return false;
                }

                pendingSegments[index] = new Vector4(start.x, start.z, end.x, end.z);
            }

            error = null;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Color previous = Gizmos.color;
            Gizmos.color = Color.cyan;
            foreach (Boundary boundary in boundaries)
            {
                if (boundary != null && boundary.start != null && boundary.end != null)
                {
                    Vector3 start = boundary.start.position;
                    Vector3 end = boundary.end.position;
                    start.y = end.y = transform.position.y;
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawWireSphere(start, 0.08f);
                    Gizmos.DrawWireSphere(end, 0.08f);
                }
            }

            Gizmos.color = previous;
        }

        private static void DestroyTemporary(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
