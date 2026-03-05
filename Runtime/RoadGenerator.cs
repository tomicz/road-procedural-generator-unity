using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    [RequireComponent(typeof(LineRenderer))]
    public class RoadGenerator : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField, FormerlySerializedAs("_bezierSegmentsPerKnot")]
        private int _segmentsPerSpan = 16;

        private InputController _inputController;
        private List<Vector3> _roadKnots;
        private List<bool> _segmentSmooth;
        private List<Vector3> _committedPositions;
        private bool _useBezierCurve;
        private bool _strokeEnded;

        public void SetUseBezier(bool useBezier)
        {
            _useBezierCurve = useBezier;
            Debug.Log($"[Mode] {(useBezier ? "Smooth" : "Linear")}");
        }

        private void Awake()
        {
            _lineRenderer.positionCount = 0;
            _inputController = new InputController();
        }

        private void OnEnable()
        {
            _inputController.OnStartDrawing += OnStartDrawing;
            _inputController.OnEndDrawing += OnEndDrawing;
        }

        private void OnDisable()
        {
            _inputController.OnStartDrawing -= OnStartDrawing;
            _inputController.OnEndDrawing -= OnEndDrawing;
        }

        private void OnStartDrawing()
        {
            bool isNewStroke = _strokeEnded || _roadKnots == null || _roadKnots.Count == 0;
            if (isNewStroke)
            {
                Vector3 start = _inputController.StartPosition;
                _roadKnots = new List<Vector3> { start };
                _segmentSmooth = new List<bool>();
                _committedPositions = new List<Vector3> { start };
                _strokeEnded = false;
            }
        }

        private void OnEndDrawing()
        {
            _roadKnots ??= new List<Vector3>();
            _segmentSmooth ??= new List<bool>();
            _committedPositions ??= new List<Vector3>();

            _roadKnots.Add(_inputController.EndPosition);
            _segmentSmooth.Add(_useBezierCurve);

            // Freeze the new segment: compute its positions and append to committed
            int newSegIndex = _roadKnots.Count - 2;
            var segPositions = SampleSegment(_roadKnots, _segmentSmooth, newSegIndex);
            for (int i = 1; i < segPositions.Count; i++)
                _committedPositions.Add(segPositions[i]);

            bool isStrokeEnd = !_inputController.IsDrawing;

            // Render the frozen committed road
            SetLineRendererPositions(_committedPositions);

            if (isStrokeEnd)
            {
                _roadKnots = null;
                _segmentSmooth = null;
                _committedPositions = null;
                _strokeEnded = true;
            }
        }

        private void LateUpdate()
        {
            _inputController.HandleMouseDrag();
        }

        private void Update()
        {
            if (_roadKnots == null || _roadKnots.Count == 0 || !_inputController.IsDrawing)
                return;

            // Build a temporary knot list to compute only the preview segment
            var previewKnots = new List<Vector3>(_roadKnots);
            previewKnots.Add(_inputController.CurrentPosition);

            var previewSmooth = new List<bool>(_segmentSmooth);
            previewSmooth.Add(_useBezierCurve);

            int previewSegIndex = previewKnots.Count - 2;
            var previewPositions = SampleSegment(previewKnots, previewSmooth, previewSegIndex);

            // Display: frozen committed positions + live preview segment
            int committedCount = _committedPositions.Count;
            int total = committedCount + previewPositions.Count - 1;
            _lineRenderer.positionCount = total;

            for (int i = 0; i < committedCount; i++)
                _lineRenderer.SetPosition(i, _committedPositions[i]);
            for (int i = 1; i < previewPositions.Count; i++)
                _lineRenderer.SetPosition(committedCount + i - 1, previewPositions[i]);
        }

        private void SetLineRendererPositions(List<Vector3> positions)
        {
            _lineRenderer.positionCount = positions.Count;
            for (int i = 0; i < positions.Count; i++)
                _lineRenderer.SetPosition(i, positions[i]);
        }

        private List<Vector3> SampleSegment(List<Vector3> knots, List<bool> smooth, int segIndex)
        {
            Vector3 p1 = knots[segIndex];
            Vector3 p2 = knots[segIndex + 1];

            if (!smooth[segIndex])
                return new List<Vector3> { p1, p2 };

            // Catmull-Rom needs the points before and after the segment.
            // At boundaries, use phantom points (reflection across the endpoint).
            Vector3 p0 = segIndex > 0 ? knots[segIndex - 1] : 2f * p1 - p2;
            Vector3 p3 = segIndex + 2 < knots.Count ? knots[segIndex + 2] : 2f * p2 - p1;

            var result = new List<Vector3>();
            for (int s = 0; s <= _segmentsPerSpan; s++)
            {
                float t = s / (float)_segmentsPerSpan;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
            return result;
        }

        /// <summary>
        /// Standard Catmull-Rom interpolation between p1 and p2,
        /// using p0 and p3 as neighboring influence points.
        /// </summary>
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
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