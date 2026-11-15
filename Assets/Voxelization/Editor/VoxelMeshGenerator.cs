using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelization.Editor
{
    internal sealed class VoxelMeshGenerator
    {
        private const int VerticesPerVoxel = 24;
        private const int IndicesPerVoxel = 36;

        private static readonly Vector3[] CubeVertices =
        {
            new Vector3(-1f, -1f, -1f), new Vector3(-1f, -1f, 1f), new Vector3(-1f, 1f, 1f), new Vector3(-1f, 1f, -1f),
            new Vector3(1f, -1f, 1f), new Vector3(1f, -1f, -1f), new Vector3(1f, 1f, -1f), new Vector3(1f, 1f, 1f),
            new Vector3(-1f, -1f, 1f), new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f), new Vector3(1f, -1f, 1f),
            new Vector3(-1f, 1f, -1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, -1f),
            new Vector3(-1f, -1f, -1f), new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f), new Vector3(1f, -1f, -1f),
            new Vector3(1f, -1f, 1f), new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f), new Vector3(-1f, -1f, 1f)
        };

        private static readonly Vector3[] CubeNormals =
        {
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
        };

        private static readonly int[] FaceIndices = { 0, 1, 2, 0, 2, 3 };

        private readonly float voxelSize;
        private readonly float voxelGap;
        private readonly float surfaceAccuracy;
        private readonly Color fallbackColor;

        public VoxelMeshGenerator(float voxelSize, float voxelGap, float surfaceAccuracy, Color fallbackColor)
        {
            this.voxelSize = voxelSize;
            this.voxelGap = voxelGap;
            this.surfaceAccuracy = surfaceAccuracy;
            this.fallbackColor = fallbackColor;
        }

        public Mesh Generate(Mesh sourceMesh, Material[] materials)
        {
            List<VoxelRecord> voxels = Voxelize(sourceMesh, materials);
            if (voxels.Count == 0)
            {
                throw new InvalidOperationException("No intersected voxel cells were found. Increase Surface Accuracy or reduce Voxel Size.");
            }

            return BuildVoxelMesh(sourceMesh.name + "_VoxelMesh", voxels);
        }

        private List<VoxelRecord> Voxelize(Mesh sourceMesh, Material[] materials)
        {
            Bounds bounds = sourceMesh.bounds;
            Vector3Int gridSize = GetGridSize(bounds, voxelSize);
            Vector3[] vertices = sourceMesh.vertices;
            Vector2[] uv = sourceMesh.uv;
            bool hasUv = uv != null && uv.Length == vertices.Length;
            Dictionary<Vector3Int, VoxelCandidate> candidates = new Dictionary<Vector3Int, VoxelCandidate>();

            using (MaterialSamplerCache samplerCache = new MaterialSamplerCache(fallbackColor))
            {
                for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
                {
                    if (sourceMesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
                    {
                        continue;
                    }

                    Material material = subMeshIndex < materials.Length ? materials[subMeshIndex] : null;
                    MaterialSampler sampler = samplerCache.Get(material);
                    int[] triangles = sourceMesh.GetTriangles(subMeshIndex);

                    for (int triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3)
                    {
                        int indexA = triangles[triangleIndex];
                        int indexB = triangles[triangleIndex + 1];
                        int indexC = triangles[triangleIndex + 2];
                        Vector3 a = vertices[indexA];
                        Vector3 b = vertices[indexB];
                        Vector3 c = vertices[indexC];
                        Vector3 triangleMin = Vector3.Min(a, Vector3.Min(b, c));
                        Vector3 triangleMax = Vector3.Max(a, Vector3.Max(b, c));
                        Vector3Int minCell = GetClampedCell(triangleMin, bounds.min, gridSize);
                        Vector3Int maxCell = GetClampedCell(triangleMax, bounds.min, gridSize);

                        for (int z = minCell.z; z <= maxCell.z; z++)
                        {
                            for (int y = minCell.y; y <= maxCell.y; y++)
                            {
                                for (int x = minCell.x; x <= maxCell.x; x++)
                                {
                                    Vector3Int cell = new Vector3Int(x, y, z);
                                    Vector3 center = GetCellCenter(cell, bounds.min);
                                    if (!TriangleIntersectsBox(a, b, c, center, Vector3.one * (voxelSize * 0.5f)))
                                    {
                                        continue;
                                    }

                                    Vector3 closestPoint = ClosestPointOnTriangle(center, a, b, c);
                                    float distanceSqr = (center - closestPoint).sqrMagnitude;
                                    if (!IsWithinSurfaceAccuracy(distanceSqr))
                                    {
                                        continue;
                                    }

                                    VoxelCandidate existing;
                                    if (candidates.TryGetValue(cell, out existing) && existing.distanceSqr <= distanceSqr)
                                    {
                                        continue;
                                    }

                                    Vector3 barycentric = GetBarycentricCoordinates(closestPoint, a, b, c);
                                    Vector2 triangleUv = hasUv
                                        ? uv[indexA] * barycentric.x + uv[indexB] * barycentric.y + uv[indexC] * barycentric.z
                                        : Vector2.zero;
                                    candidates[cell] = new VoxelCandidate
                                    {
                                        center = center,
                                        distanceSqr = distanceSqr,
                                        color = sampler.Sample(triangleUv, hasUv)
                                    };

                                }
                            }
                        }
                    }
                }
            }

            List<VoxelRecord> voxels = new List<VoxelRecord>(candidates.Count);
            foreach (KeyValuePair<Vector3Int, VoxelCandidate> pair in candidates)
            {
                voxels.Add(new VoxelRecord
                {
                    center = pair.Value.center,
                    color = pair.Value.color,
                    random = GetDeterministicRandom(pair.Key)
                });
            }

            return voxels;
        }

        private Mesh BuildVoxelMesh(string meshName, List<VoxelRecord> voxels)
        {
            int vertexCount = voxels.Count * VerticesPerVoxel;
            int indexCount = voxels.Count * IndicesPerVoxel;
            List<Vector3> vertices = new List<Vector3>(vertexCount);
            List<Vector3> normals = new List<Vector3>(vertexCount);
            List<Color> colors = new List<Color>(vertexCount);
            List<Vector2> uv0 = new List<Vector2>(vertexCount);
            List<Vector3> voxelCenters = new List<Vector3>(vertexCount);
            List<Vector2> randomValues = new List<Vector2>(vertexCount);
            List<int> indices = new List<int>(indexCount);
            float cubeHalfSize = (voxelSize - voxelGap) * 0.5f;

            for (int voxelIndex = 0; voxelIndex < voxels.Count; voxelIndex++)
            {
                VoxelRecord voxel = voxels[voxelIndex];
                int firstVertex = vertices.Count;
                for (int vertexIndex = 0; vertexIndex < CubeVertices.Length; vertexIndex++)
                {
                    vertices.Add(voxel.center + CubeVertices[vertexIndex] * cubeHalfSize);
                    normals.Add(CubeNormals[vertexIndex]);
                    colors.Add(voxel.color);
                    uv0.Add(Vector2.zero);
                    voxelCenters.Add(voxel.center);
                    randomValues.Add(new Vector2(voxel.random, 0f));
                }

                for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                {
                    int faceFirstVertex = firstVertex + faceIndex * 4;
                    for (int index = 0; index < FaceIndices.Length; index++)
                    {
                        indices.Add(faceFirstVertex + FaceIndices[index]);
                    }
                }
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, voxelCenters);
            mesh.SetUVs(2, randomValues);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3Int GetGridSize(Bounds bounds, float size)
        {
            return new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / size)),
                Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / size)),
                Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / size)));
        }

        private Vector3Int GetClampedCell(Vector3 position, Vector3 gridMin, Vector3Int gridSize)
        {
            return new Vector3Int(
                Mathf.Clamp(Mathf.FloorToInt((position.x - gridMin.x) / voxelSize), 0, gridSize.x - 1),
                Mathf.Clamp(Mathf.FloorToInt((position.y - gridMin.y) / voxelSize), 0, gridSize.y - 1),
                Mathf.Clamp(Mathf.FloorToInt((position.z - gridMin.z) / voxelSize), 0, gridSize.z - 1));
        }

        private Vector3 GetCellCenter(Vector3Int cell, Vector3 gridMin)
        {
            float halfVoxelSize = voxelSize * 0.5f;
            return gridMin + new Vector3(
                cell.x * voxelSize + halfVoxelSize,
                cell.y * voxelSize + halfVoxelSize,
                cell.z * voxelSize + halfVoxelSize);
        }

        private bool IsWithinSurfaceAccuracy(float distanceSqr)
        {
            float maximumDistance = voxelSize * 0.8660254f * surfaceAccuracy;
            return distanceSqr <= maximumDistance * maximumDistance + 0.000001f;
        }

        private static bool TriangleIntersectsBox(Vector3 a, Vector3 b, Vector3 c, Vector3 boxCenter, Vector3 boxHalfSize)
        {
            Vector3 p0 = a - boxCenter;
            Vector3 p1 = b - boxCenter;
            Vector3 p2 = c - boxCenter;
            Vector3 edge0 = p1 - p0;
            Vector3 edge1 = p2 - p1;
            Vector3 edge2 = p0 - p2;

            if (!OverlapsBoxAxis(p0.x, p1.x, p2.x, boxHalfSize.x) ||
                !OverlapsBoxAxis(p0.y, p1.y, p2.y, boxHalfSize.y) ||
                !OverlapsBoxAxis(p0.z, p1.z, p2.z, boxHalfSize.z))
            {
                return false;
            }

            Vector3 triangleNormal = Vector3.Cross(edge0, edge1);
            if (!OverlapsTriangleAxis(triangleNormal, p0, p1, p2, boxHalfSize))
            {
                return false;
            }

            Vector3[] edges = { edge0, edge1, edge2 };
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                Vector3 edge = edges[edgeIndex];
                if (!OverlapsTriangleAxis(Vector3.Cross(edge, Vector3.right), p0, p1, p2, boxHalfSize) ||
                    !OverlapsTriangleAxis(Vector3.Cross(edge, Vector3.up), p0, p1, p2, boxHalfSize) ||
                    !OverlapsTriangleAxis(Vector3.Cross(edge, Vector3.forward), p0, p1, p2, boxHalfSize))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OverlapsBoxAxis(float a, float b, float c, float halfSize)
        {
            return !(Mathf.Min(a, Mathf.Min(b, c)) > halfSize || Mathf.Max(a, Mathf.Max(b, c)) < -halfSize);
        }

        private static bool OverlapsTriangleAxis(Vector3 axis, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 boxHalfSize)
        {
            if (axis.sqrMagnitude < 0.0000001f)
            {
                return true;
            }

            float a = Vector3.Dot(p0, axis);
            float b = Vector3.Dot(p1, axis);
            float c = Vector3.Dot(p2, axis);
            float triangleMin = Mathf.Min(a, Mathf.Min(b, c));
            float triangleMax = Mathf.Max(a, Mathf.Max(b, c));
            float boxRadius = boxHalfSize.x * Mathf.Abs(axis.x) + boxHalfSize.y * Mathf.Abs(axis.y) + boxHalfSize.z * Mathf.Abs(axis.z);
            return !(triangleMin > boxRadius || triangleMax < -boxRadius);
        }

        private static Vector3 ClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return a + v * ab;
            }

            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return a + w * ac;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                Vector3 bc = c - b;
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + w * bc;
            }

            float denominator = 1f / (va + vb + vc);
            float barycentricB = vb * denominator;
            float barycentricC = vc * denominator;
            return a + ab * barycentricB + ac * barycentricC;
        }

        private static Vector3 GetBarycentricCoordinates(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;
            float dotABAB = Vector3.Dot(ab, ab);
            float dotABAC = Vector3.Dot(ab, ac);
            float dotACAC = Vector3.Dot(ac, ac);
            float dotAPAB = Vector3.Dot(ap, ab);
            float dotAPAC = Vector3.Dot(ap, ac);
            float denominator = dotABAB * dotACAC - dotABAC * dotABAC;
            if (Mathf.Abs(denominator) < 0.0000001f)
            {
                return new Vector3(1f, 0f, 0f);
            }

            float barycentricB = (dotACAC * dotAPAB - dotABAC * dotAPAC) / denominator;
            float barycentricC = (dotABAB * dotAPAC - dotABAC * dotAPAB) / denominator;
            return new Vector3(1f - barycentricB - barycentricC, barycentricB, barycentricC);
        }

        private static float GetDeterministicRandom(Vector3Int cell)
        {
            unchecked
            {
                uint value = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663) ^ (uint)(cell.z * 83492791);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (value & 0x00ffffff) / 16777215f;
            }
        }

        private struct VoxelCandidate
        {
            public Vector3 center;
            public float distanceSqr;
            public Color color;
        }

        private struct VoxelRecord
        {
            public Vector3 center;
            public Color color;
            public float random;
        }

        private sealed class MaterialSamplerCache : IDisposable
        {
            private readonly Color fallbackColor;
            private readonly List<MaterialSampler> samplers = new List<MaterialSampler>();
            private readonly TextureSamplerCache textureCache = new TextureSamplerCache();

            public MaterialSamplerCache(Color fallbackColor)
            {
                this.fallbackColor = fallbackColor;
            }

            public MaterialSampler Get(Material material)
            {
                for (int index = 0; index < samplers.Count; index++)
                {
                    if (samplers[index].Material == material)
                    {
                        return samplers[index];
                    }
                }

                MaterialSampler sampler = new MaterialSampler(material, fallbackColor, textureCache);
                samplers.Add(sampler);
                return sampler;
            }

            public void Dispose()
            {
                textureCache.Dispose();
            }
        }

        private sealed class MaterialSampler
        {
            private readonly Color baseColor;
            private readonly Texture2D texture;
            private readonly Vector2 textureScale;
            private readonly Vector2 textureOffset;

            public Material Material { get; private set; }

            public MaterialSampler(Material material, Color fallbackColor, TextureSamplerCache textureCache)
            {
                Material = material;
                baseColor = fallbackColor;
                if (material == null)
                {
                    return;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    baseColor = material.GetColor("_BaseColor");
                }
                else if (material.HasProperty("_Color"))
                {
                    baseColor = material.GetColor("_Color");
                }

                Texture sourceTexture = null;
                string textureProperty = null;
                if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                {
                    sourceTexture = material.GetTexture("_BaseMap");
                    textureProperty = "_BaseMap";
                }
                else if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
                {
                    sourceTexture = material.GetTexture("_MainTex");
                    textureProperty = "_MainTex";
                }

                Texture2D sourceTexture2D = sourceTexture as Texture2D;
                if (sourceTexture2D == null)
                {
                    return;
                }

                texture = textureCache.Get(sourceTexture2D);
                textureScale = material.GetTextureScale(textureProperty);
                textureOffset = material.GetTextureOffset(textureProperty);
            }

            public Color Sample(Vector2 uv, bool hasUv)
            {
                if (!hasUv || texture == null)
                {
                    return baseColor;
                }

                Vector2 transformedUv = Vector2.Scale(uv, textureScale) + textureOffset;
                transformedUv.x = WrapUv(transformedUv.x, texture.wrapModeU);
                transformedUv.y = WrapUv(transformedUv.y, texture.wrapModeV);
                return baseColor * texture.GetPixelBilinear(transformedUv.x, transformedUv.y);
            }

            private static float WrapUv(float value, TextureWrapMode mode)
            {
                if (mode == TextureWrapMode.Clamp || mode == TextureWrapMode.MirrorOnce)
                {
                    return Mathf.Clamp01(value);
                }

                if (mode == TextureWrapMode.Mirror)
                {
                    return Mathf.PingPong(value, 1f);
                }

                return Mathf.Repeat(value, 1f);
            }
        }

        private sealed class TextureSamplerCache : IDisposable
        {
            private readonly Dictionary<Texture2D, Texture2D> readableTextures = new Dictionary<Texture2D, Texture2D>();
            private readonly List<Texture2D> temporaryCopies = new List<Texture2D>();

            public Texture2D Get(Texture2D texture)
            {
                Texture2D readableTexture;
                if (readableTextures.TryGetValue(texture, out readableTexture))
                {
                    return readableTexture;
                }

                readableTexture = texture.isReadable ? texture : CreateReadableCopy(texture);
                readableTextures.Add(texture, readableTexture);
                if (readableTexture != texture)
                {
                    temporaryCopies.Add(readableTexture);
                }

                return readableTexture;
            }

            public void Dispose()
            {
                for (int index = 0; index < temporaryCopies.Count; index++)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryCopies[index]);
                }

                temporaryCopies.Clear();
                readableTextures.Clear();
            }

            private static Texture2D CreateReadableCopy(Texture2D source)
            {
                RenderTexture temporaryRenderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previousRenderTexture = RenderTexture.active;
                Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
                {
                    name = source.name + "_VoxelSamplingCopy",
                    hideFlags = HideFlags.HideAndDontSave
                };

                try
                {
                    Graphics.Blit(source, temporaryRenderTexture);
                    RenderTexture.active = temporaryRenderTexture;
                    copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                    copy.Apply(false, false);
                    return copy;
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                    throw;
                }
                finally
                {
                    RenderTexture.active = previousRenderTexture;
                    RenderTexture.ReleaseTemporary(temporaryRenderTexture);
                }
            }
        }
    }
}
