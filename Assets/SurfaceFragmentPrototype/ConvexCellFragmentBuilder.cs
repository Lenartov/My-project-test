using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurfaceFragmentPrototype
{
    internal static class CellFragmentBuilder
    {
        internal sealed class Result
        {
            public Mesh mesh;
            public List<Vector3> centers;
            public float maximumRadius;
            public int thickenedTriangles;
            public int openCuts;
            public int ignoredPrimitives;
        }

        private struct Vertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 uv;
            public Color color;

            public static Vertex Interpolate(Vertex first, Vertex second, float t)
            {
                return new Vertex
                {
                    position = Vector3.LerpUnclamped(first.position, second.position, t),
                    normal = Vector3.LerpUnclamped(first.normal, second.normal, t),
                    uv = Vector2.LerpUnclamped(first.uv, second.uv, t),
                    color = Color.LerpUnclamped(first.color, second.color, t)
                };
            }
        }

        private sealed class Face
        {
            public readonly List<Vertex> vertices = new List<Vertex>();
            public int material;
            public bool interior;
        }

        private sealed class Piece
        {
            public List<Face> faces;
            public Vector3Int minimum;
            public Vector3Int maximum;
            public SurfaceSample sample;
        }

        private struct SurfaceSample
        {
            public Vector2 uv;
            public Color color;
            public int material;
        }

        private struct Segment
        {
            public Vector2 start;
            public Vector2 end;
        }

        private struct Crossing
        {
            public double middle;
            public double bottom;
            public double top;
            public int winding;
        }

        private sealed class Progress
        {
            private readonly Func<float, bool> callback;
            private readonly Stopwatch timer = Stopwatch.StartNew();
            public float value;

            public Progress(Func<float, bool> callback)
            {
                this.callback = callback;
            }

            public void Check()
            {
                if (callback != null && timer.ElapsedMilliseconds >= 40)
                {
                    timer.Restart();
                    if (callback(value)) throw new OperationCanceledException();
                }
            }
        }

        private sealed class Output
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<Vector3> vertexCenters = new List<Vector3>();
            public readonly List<Vector2> metadata = new List<Vector2>();
            public readonly List<Vector3> centers = new List<Vector3>();
            public readonly List<int>[] indices;
            private readonly Matrix4x4 matrix;
            private readonly Matrix4x4 normalMatrix;
            private readonly bool reverseWinding;
            public float maximumRadius;

            public Output(int materialCount, Matrix4x4 matrix)
            {
                this.matrix = matrix;
                normalMatrix = matrix.inverse.transpose;
                reverseWinding = matrix.determinant < 0f;
                indices = new List<int>[materialCount];
                for (int index = 0; index < indices.Length; index++) indices[index] = new List<int>();
            }

            public void Add(Piece piece, float epsilon, Progress progress)
            {
                Bounds bounds = GetBounds(piece.faces);
                Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
                float random = Mathf.Repeat(Mathf.Sin(Vector3.Dot(bounds.center,
                    new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f, 1f);
                int initialCount = vertices.Count;
                foreach (Face face in piece.faces)
                {
                    progress.Check();
                    int first = vertices.Count;
                    foreach (Vertex vertex in face.vertices)
                    {
                        Vector3 position = matrix.MultiplyPoint3x4(vertex.position);
                        vertices.Add(position);
                        normals.Add(normalMatrix.MultiplyVector(vertex.normal).normalized);
                        uv.Add(face.interior ? piece.sample.uv : vertex.uv);
                        colors.Add(face.interior ? piece.sample.color : vertex.color);
                        vertexCenters.Add(center);
                        metadata.Add(new Vector2(random, face.interior ? 1f : 0f));
                        maximumRadius = Mathf.Max(maximumRadius, Vector3.Distance(center, position));
                    }

                    for (int index = 1; index < face.vertices.Count - 1; index++)
                    {
                        Vector3 a = face.vertices[0].position;
                        Vector3 b = face.vertices[index].position;
                        Vector3 c = face.vertices[index + 1].position;
                        if (Vector3.Cross(b - a, c - a).sqrMagnitude <= epsilon * epsilon * epsilon * epsilon) continue;
                        int material = face.interior ? piece.sample.material : face.material;
                        indices[material].Add(first);
                        indices[material].Add(first + index + (reverseWinding ? 1 : 0));
                        indices[material].Add(first + index + (reverseWinding ? 0 : 1));
                    }
                }

                if (vertices.Count > initialCount) centers.Add(center);
            }
        }

        public static Result Build(Mesh source, float cellSize, Matrix4x4 targetFromSource,
            Func<float, bool> reportProgress)
        {
            if (!float.IsFinite(cellSize) || cellSize <= 0f)
                throw new ArgumentException("Cell Size must be finite and greater than zero.");

            Progress progress = new Progress(reportProgress);
            Result result = new Result();
            List<Face> faces = ReadFaces(source, progress, out result.ignoredPrimitives);
            if (faces.Count == 0)
                throw new ArgumentException("The source mesh contains no non-degenerate triangle or quad surfaces.");

            Bounds originalBounds = GetBounds(faces);
            float extent = Mathf.Max(originalBounds.size.x, originalBounds.size.y, originalBounds.size.z);
            float epsilon = Mathf.Min(cellSize * 0.00001f, extent * 0.000001f);
            result.thickenedTriangles = ThickenOpenSurfaces(faces, cellSize * 0.1f, extent * 0.000001f, progress);
            Bounds bounds = GetBounds(faces);
            Vector3Int grid = new Vector3Int(GridCount(bounds.size.x, cellSize),
                GridCount(bounds.size.y, cellSize), GridCount(bounds.size.z, cellSize));
            double totalCells = (double)grid.x * grid.y * grid.z;
            double completedCells = 0;
            Stack<Piece> pending = new Stack<Piece>();
            pending.Push(new Piece { faces = faces, minimum = Vector3Int.zero, maximum = grid });
            Output output = new Output(Mathf.Max(1, source.subMeshCount), targetFromSource);

            // Balanced grid splits avoid clipping the entire source against every candidate cell.
            while (pending.Count > 0)
            {
                progress.value = (float)(completedCells / totalCells);
                progress.Check();
                Piece piece = pending.Pop();
                piece.sample = SelectSurfaceSample(piece.faces, piece.sample);
                Vector3Int counts = piece.maximum - piece.minimum;
                int axis = counts.x >= counts.y && counts.x >= counts.z ? 0 : (counts.y >= counts.z ? 1 : 2);
                if (counts[axis] == 1)
                {
                    output.Add(piece, epsilon, progress);
                    completedCells += 1;
                    continue;
                }

                int middle = piece.minimum[axis] + counts[axis] / 2;
                float plane = (float)(bounds.min[axis] + (double)middle * cellSize);
                Split(piece.faces, axis, plane, epsilon, progress, out List<Face> lower,
                    out List<Face> upper, ref result.openCuts);
                Vector3Int lowerMaximum = piece.maximum;
                lowerMaximum[axis] = middle;
                Vector3Int upperMinimum = piece.minimum;
                upperMinimum[axis] = middle;
                if (lower.Count > 0)
                    pending.Push(new Piece { faces = lower, minimum = piece.minimum, maximum = lowerMaximum, sample = piece.sample });
                else
                    completedCells += CellCount(piece.minimum, lowerMaximum);
                if (upper.Count > 0)
                    pending.Push(new Piece { faces = upper, minimum = upperMinimum, maximum = piece.maximum, sample = piece.sample });
                else
                    completedCells += CellCount(upperMinimum, piece.maximum);
            }

            if (output.centers.Count == 0) throw new ArgumentException("No fragments were produced from the source geometry.");

            Mesh mesh = new Mesh
            {
                name = source.name + "_SurfaceFragments_Preview",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = output.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(output.vertices);
            mesh.SetNormals(output.normals);
            mesh.SetUVs(0, output.uv);
            mesh.SetColors(output.colors);
            mesh.SetUVs(1, output.vertexCenters);
            mesh.SetUVs(2, output.metadata);
            mesh.subMeshCount = output.indices.Length;
            for (int index = 0; index < output.indices.Length; index++)
                mesh.SetTriangles(output.indices[index], index, false);
            mesh.RecalculateBounds();
            result.mesh = mesh;
            result.centers = output.centers;
            result.maximumRadius = output.maximumRadius;
            return result;
        }

        private static int GridCount(float size, float cellSize)
        {
            double count = Math.Max(1, Math.Ceiling((double)size / cellSize));
            if (count > int.MaxValue)
                throw new ArgumentException("The requested grid exceeds 32-bit cell coordinates. Cell Size must be representable at this mesh scale.");
            return (int)count;
        }

        private static double CellCount(Vector3Int minimum, Vector3Int maximum)
        {
            Vector3Int size = maximum - minimum;
            return (double)size.x * size.y * size.z;
        }

        private static Bounds GetBounds(List<Face> faces)
        {
            Bounds bounds = new Bounds(faces[0].vertices[0].position, Vector3.zero);
            foreach (Face face in faces)
                foreach (Vertex vertex in face.vertices)
                    bounds.Encapsulate(vertex.position);
            return bounds;
        }

        private static SurfaceSample SelectSurfaceSample(List<Face> faces, SurfaceSample inherited)
        {
            Vector3 center = GetBounds(faces).center;
            float nearestDistance = float.PositiveInfinity;
            SurfaceSample sample = inherited;
            foreach (Face face in faces)
            {
                if (face.interior)
                {
                    continue;
                }

                Vector3 position = Vector3.zero;
                Vector2 uv = Vector2.zero;
                Color color = Color.clear;
                foreach (Vertex vertex in face.vertices)
                {
                    position += vertex.position / face.vertices.Count;
                    uv += vertex.uv / face.vertices.Count;
                    color += vertex.color / face.vertices.Count;
                }

                float distance = (position - center).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    sample = new SurfaceSample { uv = uv, color = color, material = face.material };
                }
            }

            // Fully internal cells inherit a nearby exterior sample from their parent region.
            return sample;
        }

        private static List<Face> ReadFaces(Mesh source, Progress progress, out int ignoredPrimitives)
        {
            List<Face> faces = new List<Face>();
            ignoredPrimitives = 0;
            #if UNITY_EDITOR
            using (Mesh.MeshDataArray snapshot = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(source))
            #else
            using (Mesh.MeshDataArray snapshot = Mesh.AcquireReadOnlyMeshData(source))
            #endif
            {
                Mesh.MeshData data = snapshot[0];
                using (NativeArray<Vector3> positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                using (NativeArray<Vector3> normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                using (NativeArray<Vector2> uv = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp))
                using (NativeArray<Color> colors = new NativeArray<Color>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(positions);
                    bool hasNormals = data.HasVertexAttribute(VertexAttribute.Normal);
                    bool hasUv = data.HasVertexAttribute(VertexAttribute.TexCoord0);
                    bool hasColors = data.HasVertexAttribute(VertexAttribute.Color);
                    if (hasNormals) data.GetNormals(normals);
                    if (hasUv) data.GetUVs(0, uv);
                    if (hasColors) data.GetColors(colors);
                    for (int material = 0; material < data.subMeshCount; material++)
                    {
                        SubMeshDescriptor descriptor = data.GetSubMesh(material);
                        int stride = descriptor.topology == MeshTopology.Quads ? 4 : 3;
                        if (descriptor.topology != MeshTopology.Triangles && descriptor.topology != MeshTopology.Quads)
                        {
                            ignoredPrimitives += descriptor.indexCount;
                            continue;
                        }

                        using (NativeArray<int> indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                        {
                            data.GetIndices(indices, material);
                            for (int index = 0; index < indices.Length; index += stride)
                            {
                                progress.Check();
                                for (int triangle = 0; triangle < stride - 2; triangle++)
                                {
                                    int a = indices[index];
                                    int b = indices[index + triangle + 1];
                                    int c = indices[index + triangle + 2];
                                    Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                                    if (normal.sqrMagnitude == 0f) continue;
                                    Face face = new Face { material = material };
                                    foreach (int vertexIndex in new[] { a, b, c })
                                    {
                                        Vector3 position = positions[vertexIndex];
                                        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                                            throw new ArgumentException("Source vertex positions must be finite.");
                                        face.vertices.Add(new Vertex
                                        {
                                            position = position,
                                            normal = hasNormals ? normals[vertexIndex] : normal.normalized,
                                            uv = hasUv ? uv[vertexIndex] : Vector2.zero,
                                            color = hasColors ? colors[vertexIndex] : Color.white
                                        });
                                    }

                                    faces.Add(face);
                                }
                            }
                        }
                    }
                }
            }

            return faces;
        }

        private static int ThickenOpenSurfaces(List<Face> faces, float thickness, float epsilon, Progress progress)
        {
            int originalCount = faces.Count;
            int[] parents = new int[originalCount];
            Dictionary<(Vector3Int, Vector3Int), List<int>> edges = new Dictionary<(Vector3Int, Vector3Int), List<int>>();
            Vector3 origin = GetBounds(faces).min;
            for (int index = 0; index < originalCount; index++)
            {
                progress.Check();
                parents[index] = index;
                Face face = faces[index];
                for (int edge = 0; edge < 3; edge++)
                {
                    Vector3Int a = Vector3Int.RoundToInt((face.vertices[edge].position - origin) / epsilon);
                    Vector3Int b = Vector3Int.RoundToInt((face.vertices[(edge + 1) % 3].position - origin) / epsilon);
                    if (Compare(a, b) > 0)
                    {
                        Vector3Int swap = a;
                        a = b;
                        b = swap;
                    }

                    if (!edges.TryGetValue((a, b), out List<int> adjacent))
                    {
                        adjacent = new List<int>();
                        edges.Add((a, b), adjacent);
                    }

                    adjacent.Add(index);
                }
            }

            foreach (List<int> adjacent in edges.Values)
                for (int index = 1; index < adjacent.Count; index++)
                    parents[FindRoot(parents, adjacent[index])] = FindRoot(parents, adjacent[0]);

            HashSet<int> openComponents = new HashSet<int>();
            foreach (List<int> adjacent in edges.Values)
                if (adjacent.Count != 2) openComponents.Add(FindRoot(parents, adjacent[0]));

            Dictionary<Vector3Int, Vector3> directions = new Dictionary<Vector3Int, Vector3>();
            int thickened = 0;
            for (int index = 0; index < originalCount; index++)
            {
                progress.Check();
                if (!openComponents.Contains(FindRoot(parents, index))) continue;
                Face face = faces[index];
                Vector3 normal = Vector3.Cross(face.vertices[1].position - face.vertices[0].position,
                    face.vertices[2].position - face.vertices[0].position);
                foreach (Vertex vertex in face.vertices)
                {
                    Vector3Int key = Vector3Int.RoundToInt((vertex.position - origin) / epsilon);
                    directions.TryGetValue(key, out Vector3 direction);
                    directions[key] = direction + normal;
                }
            }

            for (int index = 0; index < originalCount; index++)
            {
                progress.Check();
                if (!openComponents.Contains(FindRoot(parents, index))) continue;
                thickened++;
                Face face = faces[index];
                Face back = new Face { material = face.material };
                for (int vertexIndex = 2; vertexIndex >= 0; vertexIndex--)
                {
                    Vertex vertex = face.vertices[vertexIndex];
                    Vector3Int key = Vector3Int.RoundToInt((vertex.position - origin) / epsilon);
                    Vector3 direction = directions[key].normalized;
                    if (direction == Vector3.zero) direction = vertex.normal.normalized;
                    vertex.position -= direction * thickness;
                    vertex.normal = -vertex.normal;
                    back.vertices.Add(vertex);
                }

                faces.Add(back);
                for (int edge = 0; edge < 3; edge++)
                {
                    Vertex a = face.vertices[edge];
                    Vertex b = face.vertices[(edge + 1) % 3];
                    Vector3Int keyA = Vector3Int.RoundToInt((a.position - origin) / epsilon);
                    Vector3Int keyB = Vector3Int.RoundToInt((b.position - origin) / epsilon);
                    (Vector3Int, Vector3Int) key = Compare(keyA, keyB) > 0 ? (keyB, keyA) : (keyA, keyB);
                    if (edges[key].Count == 2) continue;
                    Vertex backA = back.vertices[2 - edge];
                    Vertex backB = back.vertices[2 - (edge + 1) % 3];
                    Face side = new Face { material = face.material, interior = true };
                    Vector3 sideNormal = Vector3.Cross(backA.position - a.position, b.position - a.position).normalized;
                    foreach (Vertex sourceVertex in new[] { a, backA, backB, b })
                    {
                        Vertex vertex = sourceVertex;
                        vertex.normal = sideNormal;
                        side.vertices.Add(vertex);
                    }

                    faces.Add(side);
                }
            }

            return thickened;
        }

        private static int FindRoot(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }

            return index;
        }

        private static int Compare(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        private static void Split(List<Face> faces, int axis, float plane, float epsilon, Progress progress,
            out List<Face> lower, out List<Face> upper, ref int openCuts)
        {
            lower = new List<Face>();
            upper = new List<Face>();
            List<Segment> segments = new List<Segment>();
            foreach (Face face in faces)
            {
                progress.Check();
                bool negative = false;
                bool positive = false;
                foreach (Vertex vertex in face.vertices)
                {
                    float distance = vertex.position[axis] - plane;
                    negative |= distance < -epsilon;
                    positive |= distance > epsilon;
                }

                Face lowerFace = null;
                if (!negative && !positive)
                {
                    Vector3 normal = Vector3.Cross(face.vertices[1].position - face.vertices[0].position,
                        face.vertices[2].position - face.vertices[0].position);
                    if (normal[axis] >= 0f)
                    {
                        lower.Add(face);
                        lowerFace = face;
                    }
                    else upper.Add(face);
                }
                else if (!positive)
                {
                    lower.Add(face);
                    lowerFace = face;
                }
                else if (!negative) upper.Add(face);
                else
                {
                    lowerFace = ClipFace(face, axis, plane, true, epsilon);
                    Face upperFace = ClipFace(face, axis, plane, false, epsilon);
                    if (lowerFace.vertices.Count >= 3) lower.Add(lowerFace);
                    if (upperFace.vertices.Count >= 3) upper.Add(upperFace);
                }

                if (lowerFace == null || lowerFace.vertices.Count < 3) continue;
                for (int edge = 0; edge < lowerFace.vertices.Count; edge++)
                {
                    Vector3 a = lowerFace.vertices[edge].position;
                    Vector3 b = lowerFace.vertices[(edge + 1) % lowerFace.vertices.Count].position;
                    if (Mathf.Abs(a[axis] - plane) <= epsilon && Mathf.Abs(b[axis] - plane) <= epsilon)
                        segments.Add(new Segment { start = Project(a, axis), end = Project(b, axis) });
                }
            }

            AddCaps(segments, axis, plane, epsilon, lower, upper, progress, ref openCuts);
        }

        private static Face ClipFace(Face source, int axis, float plane, bool keepLower, float epsilon)
        {
            Face face = new Face { material = source.material, interior = source.interior };
            Vertex previous = source.vertices[source.vertices.Count - 1];
            float previousDistance = previous.position[axis] - plane;
            if (Mathf.Abs(previousDistance) <= epsilon) previousDistance = 0f;
            foreach (Vertex item in source.vertices)
            {
                Vertex current = item;
                float currentDistance = current.position[axis] - plane;
                if (Mathf.Abs(currentDistance) <= epsilon)
                {
                    currentDistance = 0f;
                    current.position[axis] = plane;
                }

                bool previousInside = keepLower ? previousDistance <= 0f : previousDistance >= 0f;
                bool currentInside = keepLower ? currentDistance <= 0f : currentDistance >= 0f;
                if (previousInside != currentInside)
                {
                    Vertex intersection = Vertex.Interpolate(previous, current,
                        previousDistance / (previousDistance - currentDistance));
                    intersection.position[axis] = plane;
                    face.vertices.Add(intersection);
                }

                if (currentInside) face.vertices.Add(current);
                previous = current;
                previousDistance = currentDistance;
            }

            bool changed = true;
            while (changed && face.vertices.Count >= 3)
            {
                changed = false;
                for (int index = face.vertices.Count - 1; index >= 0; index--)
                {
                    Vector3 before = face.vertices[index].position - face.vertices[(index + face.vertices.Count - 1) % face.vertices.Count].position;
                    Vector3 after = face.vertices[(index + 1) % face.vertices.Count].position - face.vertices[index].position;
                    if (Vector3.Cross(before, after).sqrMagnitude <= epsilon * epsilon * (before.sqrMagnitude + after.sqrMagnitude))
                    {
                        face.vertices.RemoveAt(index);
                        changed = true;
                    }
                }
            }

            return face;
        }

        private static Vector2 Project(Vector3 position, int axis)
        {
            return new Vector2(position[(axis + 1) % 3], position[(axis + 2) % 3]);
        }

        private static Vector3 Unproject(double u, double v, int axis, float plane)
        {
            Vector3 position = Vector3.zero;
            position[axis] = plane;
            position[(axis + 1) % 3] = (float)u;
            position[(axis + 2) % 3] = (float)v;
            return position;
        }

        private static double Cross(Vector2 first, Vector2 second)
        {
            return (double)first.x * second.y - (double)first.y * second.x;
        }

        private static void AddCaps(List<Segment> segments, int axis, float plane, float epsilon,
            List<Face> lower, List<Face> upper, Progress progress, ref int openCuts)
        {
            if (segments.Count == 0) return;
            List<double> levels = new List<double>();
            for (int index = 0; index < segments.Count; index++)
            {
                progress.Check();
                Segment first = segments[index];
                levels.Add(first.start.y);
                levels.Add(first.end.y);
                // Intersections between overlapping shells also divide the sweep strips.
                for (int other = index + 1; other < segments.Count; other++)
                {
                    progress.Check();
                    Segment second = segments[other];
                    Vector2 a = first.end - first.start;
                    Vector2 b = second.end - second.start;
                    double denominator = Cross(a, b);
                    if (Math.Abs(denominator) <= (double)epsilon * epsilon) continue;
                    double t = Cross(second.start - first.start, b) / denominator;
                    double s = Cross(second.start - first.start, a) / denominator;
                    if (t > 0 && t < 1 && s > 0 && s < 1) levels.Add(first.start.y + t * a.y);
                }
            }

            levels.Sort();
            List<double> unique = new List<double>();
            foreach (double value in levels)
                if (unique.Count == 0 || value - unique[unique.Count - 1] > epsilon) unique.Add(value);

            bool incomplete = false;
            for (int level = 0; level < unique.Count - 1; level++)
            {
                progress.Check();
                double bottom = unique[level];
                double top = unique[level + 1];
                double middle = (bottom + top) * 0.5;
                List<Crossing> crossings = new List<Crossing>();
                foreach (Segment segment in segments)
                {
                    double dy = segment.end.y - segment.start.y;
                    if (middle <= Math.Min(segment.start.y, segment.end.y) || middle >= Math.Max(segment.start.y, segment.end.y)) continue;
                    double slope = (segment.end.x - segment.start.x) / dy;
                    crossings.Add(new Crossing
                    {
                        middle = segment.start.x + (middle - segment.start.y) * slope,
                        bottom = segment.start.x + (bottom - segment.start.y) * slope,
                        top = segment.start.x + (top - segment.start.y) * slope,
                        winding = dy > 0 ? 1 : -1
                    });
                }

                // Nonzero winding preserves holes and does not bridge disconnected contours.
                crossings.Sort((first, second) => first.middle.CompareTo(second.middle));
                int winding = 0;
                for (int index = 0; index < crossings.Count; index++)
                {
                    Crossing right = crossings[index];
                    if (winding != 0 && index > 0)
                    {
                        Crossing left = crossings[index - 1];
                        if (right.middle - left.middle > epsilon)
                        {
                            Vector3 a = Unproject(left.bottom, bottom, axis, plane);
                            Vector3 b = Unproject(right.bottom, bottom, axis, plane);
                            Vector3 c = Unproject(right.top, top, axis, plane);
                            Vector3 d = Unproject(left.top, top, axis, plane);
                            AddCapTriangle(a, b, c, axis, winding, epsilon, lower, upper);
                            AddCapTriangle(a, c, d, axis, winding, epsilon, lower, upper);
                        }
                    }

                    winding += right.winding;
                }

                incomplete |= winding != 0;
            }

            if (incomplete) openCuts++;
        }

        private static void AddCapTriangle(Vector3 a, Vector3 b, Vector3 c, int axis, int winding, float epsilon,
            List<Face> lower, List<Face> upper)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude <= epsilon * epsilon * epsilon * epsilon) return;
            Vector3 normal = Vector3.zero;
            normal[axis] = winding > 0 ? 1f : -1f;
            Vector3[] lowerPositions = winding > 0 ? new[] { a, b, c } : new[] { c, b, a };
            // Preserve signed boundary multiplicity: merging overlapping shells into one cap
            // changes their winding and can leave later, perpendicular cuts open.
            for (int layer = 0; layer < Math.Abs(winding); layer++)
            {
                Face lowerCap = new Face { interior = true };
                Face upperCap = new Face { interior = true };
                for (int index = 0; index < 3; index++)
                {
                    lowerCap.vertices.Add(new Vertex
                    {
                        position = lowerPositions[index], normal = normal, color = Color.white
                    });
                    upperCap.vertices.Add(new Vertex
                    {
                        position = lowerPositions[2 - index], normal = -normal, color = Color.white
                    });
                }

                lower.Add(lowerCap);
                upper.Add(upperCap);
            }
        }
    }
}
