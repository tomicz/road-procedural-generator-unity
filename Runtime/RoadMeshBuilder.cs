using UnityEngine;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    /// <summary>
    /// Builds a 2D plane mesh from a centerline path and road width.
    /// Non-MonoBehaviour; used by RoadGenerator to render the road as a mesh.
    /// </summary>
    public static class RoadMeshBuilder
    {
        private static readonly Vector3 Up = Vector3.up;

        /// <summary>
        /// Builds a mesh from the centerline path. Road is a strip of quads in the XZ plane with given width.
        /// </summary>
        /// <param name="path">Centerline positions (world space).</param>
        /// <param name="width">Road width (half-width offset each side).</param>
        /// <returns>Mesh with vertices, triangles, and UVs. Returns null if path has fewer than 2 points.</returns>
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

            var mesh = new Mesh();
            mesh.name = "RoadMesh";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 GetTangent(IList<Vector3> path, int index, int count)
        {
            if (count < 2) return Vector3.forward;
            if (index <= 0) return (path[1] - path[0]).normalized;
            if (index >= count - 1) return (path[count - 1] - path[count - 2]).normalized;
            return (path[index + 1] - path[index - 1]).normalized;
        }
    }
}
