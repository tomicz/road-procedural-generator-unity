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
        [SerializeField, Min(0.01f)] private float _roadWidth = 2f;
        [SerializeField] private MeshFilter _roadMeshFilter;
        [SerializeField] private MeshRenderer _roadMeshRenderer;

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
            EnsureRoadMeshExists();
        }

        private void EnsureRoadMeshExists()
        {
            if (_roadMeshFilter != null && _roadMeshRenderer != null)
                return;
            var go = new GameObject("RoadMesh");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            if (_roadMeshFilter == null) _roadMeshFilter = go.AddComponent<MeshFilter>();
            if (_roadMeshRenderer == null)
            {
                _roadMeshRenderer = go.AddComponent<MeshRenderer>();
                _roadMeshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 0.3f, 0.3f) };
            }
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
            bool isStrokeEnd = !_inputController.IsDrawing;

            if (isStrokeEnd)
            {
                RefreshRoadDisplay(_committedPositions ?? new List<Vector3>());
                _roadKnots = null;
                _segmentSmooth = null;
                _committedPositions = null;
                _strokeEnded = true;
                return;
            }

            _roadKnots ??= new List<Vector3>();
            _segmentSmooth ??= new List<bool>();
            _committedPositions ??= new List<Vector3>();

            _roadKnots.Add(_inputController.EndPosition);
            _segmentSmooth.Add(_useBezierCurve);

            int newSegIndex = _roadKnots.Count - 2;
            var segPositions = _spline.SampleSegment(_roadKnots, _segmentSmooth, newSegIndex);
            for (int i = 1; i < segPositions.Count; i++)
                _committedPositions.Add(segPositions[i]);

            RefreshRoadDisplay(_committedPositions);
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
            List<Vector3> fullPath = BuildCommittedPlusPreviewPath(previewSegmentPositions);
            RefreshRoadDisplay(fullPath);
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

        private List<Vector3> BuildCommittedPlusPreviewPath(List<Vector3> previewSegmentPositions)
        {
            int committedCount = _committedPositions.Count;
            int total = committedCount + previewSegmentPositions.Count - 1;
            var path = new List<Vector3>(total);
            for (int i = 0; i < committedCount; i++)
                path.Add(_committedPositions[i]);
            for (int i = 1; i < previewSegmentPositions.Count; i++)
                path.Add(previewSegmentPositions[i]);
            return path;
        }

        private void RefreshRoadDisplay(List<Vector3> path)
        {
            SetLineRendererPositions(path);
            UpdateRoadMesh(path);
        }

        private void SetLineRendererPositions(List<Vector3> positions)
        {
            _lineRenderer.positionCount = positions.Count;
            for (int i = 0; i < positions.Count; i++)
                _lineRenderer.SetPosition(i, positions[i]);
        }

        private void UpdateRoadMesh(List<Vector3> path)
        {
            if (_roadMeshFilter == null)
                return;
            Mesh mesh = RoadMeshBuilder.Build(path, _roadWidth);
            if (mesh != null)
                _roadMeshFilter.mesh = mesh;
        }

    }
}