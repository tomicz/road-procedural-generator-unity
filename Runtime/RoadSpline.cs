using UnityEngine;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    /// <summary>
    /// Samples road segments as either linear (two points) or smooth Catmull-Rom spline.
    /// Non-MonoBehaviour; inject into RoadGenerator or use standalone.
    /// </summary>
    public class RoadSpline
    {
        private readonly int _segmentsPerSpan;

        public RoadSpline(int segmentsPerSpan = 16)
        {
            _segmentsPerSpan = Mathf.Max(1, segmentsPerSpan);
        }

        /// <summary>
        /// Samples a single segment between knots[segIndex] and knots[segIndex + 1].
        /// If smooth[segIndex] is true, uses Catmull-Rom; otherwise returns just the two endpoints.
        /// </summary>
        public List<Vector3> SampleSegment(List<Vector3> knots, List<bool> smooth, int segIndex)
        {
            Vector3 p1 = knots[segIndex];
            Vector3 p2 = knots[segIndex + 1];

            if (!smooth[segIndex])
                return new List<Vector3> { p1, p2 };

            Vector3 p0 = segIndex > 0 ? knots[segIndex - 1] : 2f * p1 - p2;
            Vector3 p3 = segIndex + 2 < knots.Count ? knots[segIndex + 2] : 2f * p2 - p1;

            var result = new List<Vector3>(_segmentsPerSpan + 1);
            for (int s = 0; s <= _segmentsPerSpan; s++)
            {
                float t = s / (float)_segmentsPerSpan;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
            return result;
        }

        /// <summary>
        /// Standard Catmull-Rom interpolation between p1 and p2,
        /// using p0 and p3 as neighboring influence points. t in [0, 1].
        /// </summary>
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}
