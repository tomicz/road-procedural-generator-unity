using UnityEngine;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    /// <summary>
    /// Builds a 2D plane mesh from a centerline path and road width.
    /// Adds junction cap geometry at sharp turns / merge points to fill gaps.
    /// </summary>
    public static class RoadMeshBuilder
    {
        private static readonly Vector3 Up = Vector3.up;
        private const float JunctionAngleThreshold = 0.95f; // cos(angle): below this we add a cap (stricter = more caps)
        private const float ClosedPathDistanceThreshold = 2f; // path treated as closed when first-last distance below this (multiplied by width)

        /// <summary>
        /// Builds a mesh from the centerline path. Road is a strip of quads in the XZ plane with given width.
        /// At interior points where the path bends sharply, adds a cap quad to close the gap.
        /// </summary>
        public static Mesh Build(IList<Vector3> path, float width)
        {
            if (path == null || path.Count < 2)
                return null;

            int n = path.Count;
            float halfWidth = width * 0.5f;

            var vertices = new List<Vector3>(n * 2);
            var uvs = new List<Vector2>(n * 2);
            var triangles = new List<int>((n - 1) * 6);

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = GetTangent(path, i, n);
                Vector3 right = Vector3.Cross(Up, tangent).normalized;
                if (right.sqrMagnitude < 0.0001f)
                    right = Vector3.right;

                Vector3 left = path[i] - right * halfWidth;
                Vector3 rightPt = path[i] + right * halfWidth;
                vertices.Add(left);
                vertices.Add(rightPt);

                float u = i / (float)(n - 1);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                int d = (i + 1) * 2 + 1;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }

            AddJunctionCaps(path, width, vertices, uvs, triangles);
            AddClosedLoopCapsIfNeeded(path, width, vertices, uvs, triangles);

            var mesh = new Mesh();
            mesh.name = "RoadMesh";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddJunctionCaps(IList<Vector3> path, float width,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            int n = path.Count;
            if (n < 3) return;
            float halfWidth = width * 0.5f;

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 tangentIn = (path[i] - path[i - 1]).normalized;
                Vector3 tangentOut = (path[i + 1] - path[i]).normalized;
                float cosAngle = Vector3.Dot(tangentIn, tangentOut);
                if (cosAngle >= JunctionAngleThreshold)
                    continue;

                Vector3 rightIn = Vector3.Cross(Up, tangentIn).normalized;
                Vector3 rightOut = Vector3.Cross(Up, tangentOut).normalized;
                if (rightIn.sqrMagnitude < 0.0001f) rightIn = Vector3.right;
                if (rightOut.sqrMagnitude < 0.0001f) rightOut = Vector3.right;

                Vector3 center = path[i];
                Vector3 leftIn = center - rightIn * halfWidth;
                Vector3 rightInPt = center + rightIn * halfWidth;
                Vector3 leftOut = center - rightOut * halfWidth;
                Vector3 rightOutPt = center + rightOut * halfWidth;

                int o = vertices.Count;
                vertices.Add(leftIn);
                vertices.Add(rightInPt);
                vertices.Add(rightOutPt);
                vertices.Add(leftOut);
                float u = i / (float)(n - 1);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                uvs.Add(new Vector2(u, 1f));
                uvs.Add(new Vector2(u, 0f));

                triangles.Add(o);
                triangles.Add(o + 1);
                triangles.Add(o + 2);
                triangles.Add(o);
                triangles.Add(o + 2);
                triangles.Add(o + 3);
            }
        }

        /// <summary>
        /// When the path forms a loop (first and last point close), add junction caps at both ends
        /// so the mesh closes without a gap.
        /// </summary>
        private static void AddClosedLoopCapsIfNeeded(IList<Vector3> path, float width,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            int n = path.Count;
            if (n < 3) return;
            float d = Vector3.Distance(path[0], path[n - 1]);
            if (d > width * ClosedPathDistanceThreshold)
                return;

            float halfWidth = width * 0.5f;

            // Cap at path[0]: tangentIn from last to first, tangentOut from first to second
            Vector3 tangentIn0 = (path[0] - path[n - 1]).normalized;
            Vector3 tangentOut0 = (path[1] - path[0]).normalized;
            AddCapAtPoint(path[0], tangentIn0, tangentOut0, halfWidth, 0f, vertices, uvs, triangles);

            // Cap at path[n-1]: tangentIn from second-last to last, tangentOut from last to first
            Vector3 tangentInN = (path[n - 1] - path[n - 2]).normalized;
            Vector3 tangentOutN = (path[0] - path[n - 1]).normalized;
            AddCapAtPoint(path[n - 1], tangentInN, tangentOutN, halfWidth, (n - 1) / (float)(n - 1), vertices, uvs, triangles);
        }

        private static void AddCapAtPoint(Vector3 center, Vector3 tangentIn, Vector3 tangentOut,
            float halfWidth, float u, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            float cosAngle = Vector3.Dot(tangentIn, tangentOut);
            if (cosAngle >= JunctionAngleThreshold)
                return;

            Vector3 rightIn = Vector3.Cross(Up, tangentIn).normalized;
            Vector3 rightOut = Vector3.Cross(Up, tangentOut).normalized;
            if (rightIn.sqrMagnitude < 0.0001f) rightIn = Vector3.right;
            if (rightOut.sqrMagnitude < 0.0001f) rightOut = Vector3.right;

            Vector3 leftIn = center - rightIn * halfWidth;
            Vector3 rightInPt = center + rightIn * halfWidth;
            Vector3 leftOut = center - rightOut * halfWidth;
            Vector3 rightOutPt = center + rightOut * halfWidth;

            int o = vertices.Count;
            vertices.Add(leftIn);
            vertices.Add(rightInPt);
            vertices.Add(rightOutPt);
            vertices.Add(leftOut);
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));
            uvs.Add(new Vector2(u, 1f));
            uvs.Add(new Vector2(u, 0f));

            triangles.Add(o);
            triangles.Add(o + 1);
            triangles.Add(o + 2);
            triangles.Add(o);
            triangles.Add(o + 2);
            triangles.Add(o + 3);
        }

        private static Vector3 GetTangent(IList<Vector3> path, int index, int count)
        {
            if (count < 2) return Vector3.forward;
            if (index <= 0) return (path[1] - path[0]).normalized;
            if (index >= count - 1) return (path[count - 1] - path[count - 2]).normalized;
            return (path[index + 1] - path[index - 1]).normalized;
        }

        /// <summary>
        /// Builds a short curved mesh strip (Hermite interpolation) that bridges
        /// two road endpoints. The tangent directions ensure the bridge aligns
        /// smoothly with each road's end direction.
        /// </summary>
        public static Mesh BuildBridge(Vector3 endA, Vector3 tangentA,
                                        Vector3 endB, Vector3 tangentB,
                                        float width, int segments = 8)
        {
            if (segments < 1) segments = 1;
            float halfWidth = width * 0.5f;
            int n = segments + 1;

            var vertices = new List<Vector3>(n * 2);
            var uvs = new List<Vector2>(n * 2);
            var triangles = new List<int>(segments * 6);

            // Scale tangents by distance so the curve has sensible shape
            float dist = Vector3.Distance(endA, endB);
            Vector3 m0 = tangentA * dist;
            Vector3 m1 = tangentB * (-dist); // negate because tangentB points away from B

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 pos = HermiteInterpolate(endA, m0, endB, m1, t);
                Vector3 tangent = HermiteTangent(endA, m0, endB, m1, t).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;

                Vector3 right = Vector3.Cross(Up, tangent).normalized;
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

                vertices.Add(pos - right * halfWidth);
                vertices.Add(pos + right * halfWidth);

                float u = t;
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                int d = (i + 1) * 2 + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            var mesh = new Mesh { name = "BridgeMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary> Cubic Hermite interpolation: p(t) given endpoints and tangents. </summary>
        private static Vector3 HermiteInterpolate(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0
                 + (t3 - 2f * t2 + t) * m0
                 + (-2f * t3 + 3f * t2) * p1
                 + (t3 - t2) * m1;
        }

        /// <summary> First derivative of the Hermite curve. </summary>
        private static Vector3 HermiteTangent(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t;
            return (6f * t2 - 6f * t) * p0
                 + (3f * t2 - 4f * t + 1f) * m0
                 + (-6f * t2 + 6f * t) * p1
                 + (3f * t2 - 2f * t) * m1;
        }
    }
}
