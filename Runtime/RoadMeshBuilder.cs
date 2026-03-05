using UnityEngine;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    /// <summary>
    /// Builds a 2D plane mesh from a centerline path and road width.
    /// Uses mitered corners at bends to avoid clipping; adds caps only for closed-loop paths.
    /// </summary>
    public static class RoadMeshBuilder
    {
        private static readonly Vector3 Up = Vector3.up;
        private const float JunctionAngleThreshold = 0.95f;
        private const float ClosedPathDistanceThreshold = 2f;
        private const float MiterLimit = 8f;
        private const float Epsilon = 1e-6f;

        /// <summary>
        /// Builds a mesh from the centerline path. Road is a strip of quads in the XZ plane with given width.
        /// At interior points where the path bends sharply, adds a cap quad to close the gap.
        /// UVs are length-based so the texture tiles along the road (no stretching). uvScaleAlong = world units per full UV repeat.
        /// </summary>
        public static Mesh Build(IList<Vector3> path, float width, float uvScaleAlong = 2f)
        {
            if (path == null || path.Count < 2)
                return null;

            int n = path.Count;
            float halfWidth = width * 0.5f;
            if (uvScaleAlong <= 0f) uvScaleAlong = 1f;

            float[] lengthAt = new float[n];
            lengthAt[0] = 0f;
            for (int i = 1; i < n; i++)
                lengthAt[i] = lengthAt[i - 1] + Vector3.Distance(path[i - 1], path[i]);

            var vertices = new List<Vector3>(n * 2);
            var uvs = new List<Vector2>(n * 2);
            var triangles = new List<int>((n - 1) * 6);

            for (int i = 0; i < n; i++)
            {
                Vector3 leftPt, rightPt;
                GetMiteredOffset(path, n, i, halfWidth, out leftPt, out rightPt);

                vertices.Add(leftPt);
                vertices.Add(rightPt);

                float u = lengthAt[i] / uvScaleAlong;
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

            AddClosedLoopCapsIfNeeded(path, width, lengthAt, uvScaleAlong, vertices, uvs, triangles);

            var mesh = new Mesh();
            mesh.name = "RoadMesh";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// When the path forms a loop (first and last point close), add junction caps at both ends
        /// so the mesh closes without a gap.
        /// </summary>
        private static void AddClosedLoopCapsIfNeeded(IList<Vector3> path, float width, float[] lengthAt, float uvScaleAlong,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            int n = path.Count;
            if (n < 3 || lengthAt == null) return;
            float d = Vector3.Distance(path[0], path[n - 1]);
            if (d > width * ClosedPathDistanceThreshold)
                return;

            float halfWidth = width * 0.5f;
            float u0 = lengthAt[0] / uvScaleAlong;
            float uN = lengthAt[n - 1] / uvScaleAlong;

            Vector3 tangentIn0 = (path[0] - path[n - 1]).normalized;
            Vector3 tangentOut0 = (path[1] - path[0]).normalized;
            AddCapAtPoint(path[0], tangentIn0, tangentOut0, halfWidth, u0, vertices, uvs, triangles);

            Vector3 tangentInN = (path[n - 1] - path[n - 2]).normalized;
            Vector3 tangentOutN = (path[0] - path[n - 1]).normalized;
            AddCapAtPoint(path[n - 1], tangentInN, tangentOutN, halfWidth, uN, vertices, uvs, triangles);
        }

        private static void AddCapAtPoint(Vector3 center, Vector3 tangentIn, Vector3 tangentOut,
            float halfWidth, float u, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            if (Vector3.Dot(tangentIn, tangentOut) >= JunctionAngleThreshold)
                return;
            Vector3 rightIn = RightPerpXZ(tangentIn);
            Vector3 rightOut = RightPerpXZ(tangentOut);

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

        private static Vector3 RightPerpXZ(Vector3 tangent)
        {
            var right = Vector3.Cross(Up, tangent).normalized;
            return right.sqrMagnitude < 0.0001f ? Vector3.right : right;
        }

        /// <summary>
        /// Computes left/right strip vertices at path[i] using mitered corners so the mesh
        /// doesn't overlap or clip on the inside of bends. Endpoints use simple perpendicular offset.
        /// </summary>
        private static void GetMiteredOffset(IList<Vector3> path, int n, int i, float halfWidth,
            out Vector3 leftPt, out Vector3 rightPt)
        {
            Vector3 tangent = GetTangent(path, i, n);
            Vector3 right = RightPerpXZ(tangent);

            if (n < 3 || i <= 0 || i >= n - 1)
            {
                leftPt = path[i] - right * halfWidth;
                rightPt = path[i] + right * halfWidth;
                return;
            }

            Vector3 tangentIn = (path[i] - path[i - 1]).normalized;
            Vector3 tangentOut = (path[i + 1] - path[i]).normalized;
            Vector3 rightIn = RightPerpXZ(tangentIn);
            Vector3 rightOut = RightPerpXZ(tangentOut);

            Vector3 center = path[i];
            Vector3 pLeftIn = center - rightIn * halfWidth;
            Vector3 pLeftOut = center - rightOut * halfWidth;
            Vector3 pRightIn = center + rightIn * halfWidth;
            Vector3 pRightOut = center + rightOut * halfWidth;

            float limitSq = MiterLimit * MiterLimit * halfWidth * halfWidth;
            if (!LineLineIntersectXZ(pLeftIn, tangentIn, pLeftOut, tangentOut, out leftPt) ||
                (leftPt - center).sqrMagnitude > limitSq)
                leftPt = center - right * halfWidth;
            if (!LineLineIntersectXZ(pRightIn, tangentIn, pRightOut, tangentOut, out rightPt) ||
                (rightPt - center).sqrMagnitude > limitSq)
                rightPt = center + right * halfWidth;
        }

        /// <summary> 2D line-line intersection in XZ plane. Returns true if lines intersect. </summary>
        private static bool LineLineIntersectXZ(Vector3 p0, Vector3 d0, Vector3 p1, Vector3 d1, out Vector3 result)
        {
            result = Vector3.zero;
            float tx0 = d0.x, tz0 = d0.z;
            float tx1 = d1.x, tz1 = d1.z;
            float det = tx0 * tz1 - tz0 * tx1;
            if (Mathf.Abs(det) < Epsilon) return false;

            float dx = p1.x - p0.x, dz = p1.z - p0.z;
            float t = (dx * tz1 - dz * tx1) / det;
            float px = p0.x + t * tx0;
            float pz = p0.z + t * tz0;
            result = new Vector3(px, p0.y, pz);
            return true;
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

                Vector3 right = RightPerpXZ(tangent);
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
