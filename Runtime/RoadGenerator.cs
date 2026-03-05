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
        private RoadSpline _spline;
        private List<Vector3> _roadKnots;
        private List<bool> _segmentSmooth;
        private List<Vector3> _committedPositions;
        private bool _useBezierCurve;
        private bool _strokeEnded;

        public void SetUseBezier(bool useBezier)
        {
            _useBezierCurve = useBezier;
        }

        private void Awake()
        {
            _lineRenderer.positionCount = 0;
            _inputController = new InputController();
            _spline = new RoadSpline(_segmentsPerSpan);
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
            var segPositions = _spline.SampleSegment(_roadKnots, _segmentSmooth, newSegIndex);
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
            ProcessInput();
        }

        private void Update()
        {
            RefreshLineDisplay();
        }

        private void ProcessInput()
        {
            _inputController.HandleMouseDrag();
        }

        private void RefreshLineDisplay()
        {
            if (!HasActiveStroke())
                return;

            List<Vector3> previewSegmentPositions = BuildPreviewSegmentPositions();
            ApplyCommittedAndPreviewToLineRenderer(previewSegmentPositions);
        }

        private bool HasActiveStroke()
        {
            return _roadKnots != null && _roadKnots.Count > 0 && _inputController.IsDrawing;
        }

        private List<Vector3> BuildPreviewSegmentPositions()
        {
            var previewKnots = new List<Vector3>(_roadKnots) { _inputController.CurrentPosition };
            var previewSmooth = new List<bool>(_segmentSmooth) { _useBezierCurve };
            int previewSegIndex = previewKnots.Count - 2;
            return _spline.SampleSegment(previewKnots, previewSmooth, previewSegIndex);
        }

        private void ApplyCommittedAndPreviewToLineRenderer(List<Vector3> previewSegmentPositions)
        {
            int committedCount = _committedPositions.Count;
            int total = committedCount + previewSegmentPositions.Count - 1;
            _lineRenderer.positionCount = total;

            for (int i = 0; i < committedCount; i++)
                _lineRenderer.SetPosition(i, _committedPositions[i]);
            for (int i = 1; i < previewSegmentPositions.Count; i++)
                _lineRenderer.SetPosition(committedCount + i - 1, previewSegmentPositions[i]);
        }

        private void SetLineRendererPositions(List<Vector3> positions)
        {
            _lineRenderer.positionCount = positions.Count;
            for (int i = 0; i < positions.Count; i++)
                _lineRenderer.SetPosition(i, positions[i]);
        }

    }
}