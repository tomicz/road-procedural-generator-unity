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
        [SerializeField, Min(0.01f)] private float _mergeDistance = 1f;
        [SerializeField, Min(0.01f)] private float _snapDistance = 1.5f;
        [SerializeField] private MeshFilter _roadMeshFilter;
        [SerializeField] private MeshRenderer _roadMeshRenderer;

        private InputController _inputController;
        private RoadSpline _spline;
        private List<Vector3> _roadKnots;
        private List<bool> _segmentSmooth;
        private List<Vector3> _committedPositions;
        private List<Vector3> _persistedPath;
        private bool _useBezierCurve;
        private bool _strokeEnded;
        private readonly List<MeshFilter> _bridgeMeshFilters = new List<MeshFilter>();
        private Vector3 _snapIndicatorPos;
        private bool _showSnapIndicator;

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
            RoadNetwork.Unregister(this);
        }

        private void OnStartDrawing()
        {
            bool isNewStroke = _strokeEnded || _roadKnots == null || _roadKnots.Count == 0;
            if (isNewStroke)
            {
                Vector3 start = _inputController.StartPosition;

                // Snap to a nearby endpoint on another road
                if (RoadNetwork.FindNearestEndpoint(start, _snapDistance, GetInstanceID(), out var snap))
                    start = snap.EndpointPosition;

                if (TryMergeWithPersistedPath(start, out List<Vector3> mergedCommitted, out Vector3 firstKnot))
                {
                    _committedPositions = mergedCommitted;
                    _roadKnots = new List<Vector3> { firstKnot };
                }
                else
                {
                    _roadKnots = new List<Vector3> { start };
                    _committedPositions = new List<Vector3> { start };
                }
                _segmentSmooth = new List<bool>();
                _strokeEnded = false;
            }
        }

        private bool TryMergeWithPersistedPath(Vector3 start, out List<Vector3> committed, out Vector3 firstKnot)
        {
            committed = null;
            firstKnot = start;
            if (_persistedPath == null || _persistedPath.Count < 2)
                return false;

            Vector3 pathStart = _persistedPath[0];
            Vector3 pathEnd = _persistedPath[_persistedPath.Count - 1];
            float dStart = Vector3.Distance(start, pathStart);
            float dEnd = Vector3.Distance(start, pathEnd);

            if (dEnd <= _mergeDistance)
            {
                committed = new List<Vector3>(_persistedPath);
                firstKnot = pathEnd;
                return true;
            }
            if (dStart <= _mergeDistance)
            {
                committed = new List<Vector3>(_persistedPath);
                committed.Reverse();
                firstKnot = pathStart;
                return true;
            }
            return false;
        }

        private void OnEndDrawing()
        {
            bool isStrokeEnd = !_inputController.IsDrawing;

            if (isStrokeEnd)
            {
                List<Vector3> path = _committedPositions ?? new List<Vector3>();

                // Snap the last point to a nearby endpoint on another road
                if (path.Count >= 2 &&
                    RoadNetwork.FindNearestEndpoint(path[path.Count - 1], _snapDistance, GetInstanceID(), out var snapEnd))
                {
                    path[path.Count - 1] = snapEnd.EndpointPosition;
                    TryBuildBridge(path, snapEnd);
                }

                // Also check start for bridge (the stroke start may have snapped)
                if (path.Count >= 2 &&
                    RoadNetwork.FindNearestEndpoint(path[0], _snapDistance, GetInstanceID(), out var snapStart))
                {
                    path[0] = snapStart.EndpointPosition;
                    TryBuildBridge(path, snapStart);
                }

                RefreshRoadDisplay(path);
                _persistedPath = path.Count >= 2 ? new List<Vector3>(path) : _persistedPath;

                // Register in the network so other roads can snap to this one
                if (_persistedPath != null && _persistedPath.Count >= 2)
                    RoadNetwork.Register(this, _persistedPath, _roadWidth);

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
            UpdateSnapIndicator();
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

        /// <summary>
        /// Creates a bridge mesh between this road's endpoint and the snapped road's endpoint.
        /// </summary>
        private void TryBuildBridge(List<Vector3> path, RoadNetwork.SnapResult snap)
        {
            // Determine tangent at our road's endpoint closest to the snap
            Vector3 ourEnd;
            Vector3 ourTangent;
            float dStart = Vector3.Distance(path[0], snap.EndpointPosition);
            float dEnd = Vector3.Distance(path[path.Count - 1], snap.EndpointPosition);
            if (dEnd <= dStart)
            {
                int last = path.Count - 1;
                ourEnd = path[last];
                ourTangent = (last > 0 ? (path[last] - path[last - 1]).normalized : Vector3.forward);
            }
            else
            {
                ourEnd = path[0];
                ourTangent = (path.Count > 1 ? (path[0] - path[1]).normalized : Vector3.forward);
            }

            // Don't build a bridge if our endpoint IS the snap point (already merged)
            if (Vector3.Distance(ourEnd, snap.EndpointPosition) < 0.01f)
                return;

            Mesh bridgeMesh = RoadMeshBuilder.BuildBridge(
                ourEnd, ourTangent,
                snap.EndpointPosition, snap.Tangent,
                _roadWidth);

            if (bridgeMesh == null) return;

            var go = new GameObject("BridgeMesh");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = bridgeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _roadMeshRenderer != null
                ? _roadMeshRenderer.sharedMaterial
                : new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 0.3f, 0.3f) };
            _bridgeMeshFilters.Add(mf);
        }

        /// <summary>
        /// Shows a snap indicator gizmo when the cursor is near another road's endpoint.
        /// </summary>
        private void UpdateSnapIndicator()
        {
            _showSnapIndicator = false;
            if (!_inputController.IsDrawing) return;

            Vector3 cursor = _inputController.CurrentPosition;
            if (RoadNetwork.FindNearestEndpoint(cursor, _snapDistance, GetInstanceID(), out var snap))
            {
                _snapIndicatorPos = snap.EndpointPosition;
                _showSnapIndicator = true;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showSnapIndicator) return;
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(_snapIndicatorPos, _roadWidth * 0.6f);
            Gizmos.DrawSphere(_snapIndicatorPos, _roadWidth * 0.2f);
        }
#endif
    }
}